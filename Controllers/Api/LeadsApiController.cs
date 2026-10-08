using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels.Leads;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/leads")]
[Authorize]
public class LeadsApiController : ControllerBase
{
    private readonly ILeadService _leadService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<LeadsApiController> _logger;

    public LeadsApiController(
        ILeadService leadService,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<LeadsApiController> logger)
    {
        _leadService = leadService;
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<LeadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] LeadQueryDto query)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var filter = new LeadFilterViewModel
        {
            SearchTerm = query.SearchTerm,
            Status = query.Status,
            Source = query.Source,
            AssignedToUserId = isPrivileged ? query.AssignedToUserId : currentUser.Id,
            Page = query.Page < 1 ? 1 : query.Page,
            PageSize = query.PageSize < 1 ? 10 : query.PageSize
        };

        var pagedResult = await _leadService.GetPagedLeadsAsync(filter, currentUser.Id, roles);

        var dtos = pagedResult.Leads.Select(l => new LeadDto
        {
            Id = l.Id,
            LeadCode = l.LeadCode,
            LeadName = l.LeadName,
            Email = l.Email,
            Phone = l.Phone,
            CompanyName = l.CompanyName,
            Source = l.Source,
            Status = l.Status.ToString(),
            ExpectedValue = l.ExpectedValue,
            CreatedDate = l.CreatedDate,
            AssignedToUserId = l.AssignedToUserId,
            AssignedToUserName = l.AssignedToUserName
        }).ToList();

        return Ok(new PagedResultDto<LeadDto>
        {
            Items = dtos,
            TotalItems = pagedResult.TotalItems,
            Page = pagedResult.CurrentPage,
            PageSize = pagedResult.PageSize
        });
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var lead = await _context.Leads
            .Include(l => l.AssignedToUser)
            .Include(l => l.ConvertedCustomer)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lead == null)
        {
            return NotFound(new { message = $"Lead with ID {id} not found." });
        }

        if (!isPrivileged && lead.AssignedToUserId != currentUser.Id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to view this lead." });
        }

        return Ok(MapToDto(lead));
    }

    [HttpPost]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateLeadDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        if (!await _leadService.IsEmailUniqueAsync(dto.Email))
        {
            return Conflict(new { message = "A lead with this email address already exists." });
        }

        if (!string.IsNullOrEmpty(dto.LeadCode) && !await _leadService.IsLeadCodeUniqueAsync(dto.LeadCode))
        {
            return Conflict(new { message = "Lead code is already in use." });
        }

        var model = new LeadCreateViewModel
        {
            LeadName = dto.LeadName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Title = dto.Title,
            Source = dto.Source,
            ExpectedValue = dto.ExpectedValue,
            LeadCode = dto.LeadCode,
            AssignedToUserId = isPrivileged ? (dto.AssignedToUserId ?? currentUser.Id) : currentUser.Id
        };

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (success, error, leadId) = await _leadService.CreateLeadAsync(model, currentUser.Id, currentUser.FullName, ip);

        if (!success || !leadId.HasValue)
        {
            return BadRequest(new { message = error ?? "Failed to create lead." });
        }

        var created = await _context.Leads
            .Include(l => l.AssignedToUser)
            .Include(l => l.ConvertedCustomer)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == leadId.Value);

        var resultDto = MapToDto(created!);
        return CreatedAtAction(nameof(GetById), new { id = resultDto.Id }, resultDto);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateLeadDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var existing = await _context.Leads.FindAsync(id);
        if (existing == null)
        {
            return NotFound(new { message = $"Lead with ID {id} not found." });
        }

        if (!isPrivileged && existing.AssignedToUserId != currentUser.Id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to modify this lead." });
        }

        // Validate status transition rule if status is changing
        if (existing.Status != dto.Status && !_leadService.IsValidStatusTransition(existing.Status, dto.Status))
        {
            return BadRequest(new { message = $"Invalid status transition from {existing.Status} to {dto.Status}." });
        }

        if (!await _leadService.IsEmailUniqueAsync(dto.Email, id))
        {
            return Conflict(new { message = "A lead with this email address already exists." });
        }

        var model = new LeadEditViewModel
        {
            Id = id,
            LeadCode = existing.LeadCode,
            LeadName = dto.LeadName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Title = dto.Title,
            Source = dto.Source,
            Status = dto.Status,
            ExpectedValue = dto.ExpectedValue,
            AssignedToUserId = isPrivileged ? (dto.AssignedToUserId ?? existing.AssignedToUserId) : existing.AssignedToUserId
        };

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (success, error) = await _leadService.UpdateLeadAsync(model, currentUser.Id, currentUser.FullName, roles, ip);

        if (!success)
        {
            return BadRequest(new { message = error ?? "Failed to update lead." });
        }

        var updated = await _context.Leads
            .Include(l => l.AssignedToUser)
            .Include(l => l.ConvertedCustomer)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id);

        return Ok(MapToDto(updated!));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var existing = await _context.Leads.FindAsync(id);
        if (existing == null)
        {
            return NotFound(new { message = $"Lead with ID {id} not found." });
        }

        if (!isPrivileged && existing.AssignedToUserId != currentUser.Id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to delete this lead." });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (success, error) = await _leadService.DeleteLeadAsync(id, currentUser.Id, roles, ip);

        if (!success)
        {
            return BadRequest(new { message = error ?? "Failed to delete lead." });
        }

        return Ok(new { message = $"Lead #{id} deleted successfully." });
    }

    [HttpPost("{id:int}/convert")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Convert(int id, [FromBody] ConvertLeadDto dto)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var lead = await _context.Leads.FindAsync(id);
        if (lead == null)
        {
            return NotFound(new { message = $"Lead with ID {id} not found." });
        }

        if (!isPrivileged && lead.AssignedToUserId != currentUser.Id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to convert this lead." });
        }

        if (lead.Status != LeadStatus.Qualified)
        {
            return BadRequest(new { message = $"Only leads with 'Qualified' status can be converted. Current status: {lead.Status}." });
        }

        var model = new LeadConvertViewModel
        {
            LeadId = id,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = dto.CompanyName ?? lead.CompanyName,
            ExpectedValue = lead.ExpectedValue,
            AssignedToUserId = lead.AssignedToUserId,
            OpportunityTitle = dto.OpportunityName ?? (!string.IsNullOrWhiteSpace(dto.CompanyName ?? lead.CompanyName) ? $"{dto.CompanyName ?? lead.CompanyName} - Expansion Deal" : $"{lead.LeadName} - Deal"),
            OpportunityAmount = dto.OpportunityAmount ?? (lead.ExpectedValue ?? 1000m),
            ExpectedCloseDate = dto.ExpectedCloseDate ?? DateTime.Today.AddDays(30),
            OpportunityStage = OpportunityStage.Prospecting
        };

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (success, error, customerId, opportunityId) = await _leadService.ConvertLeadAsync(
            model,
            currentUser.Id,
            currentUser.FullName,
            roles,
            ip);

        if (!success)
        {
            return BadRequest(new { message = error ?? "Failed to convert lead." });
        }

        return Ok(new
        {
            message = "Lead successfully converted.",
            customerId,
            opportunityId
        });
    }

    private static LeadDto MapToDto(Lead l) => new()
    {
        Id = l.Id,
        LeadCode = l.LeadCode,
        LeadName = l.LeadName,
        Email = l.Email,
        Phone = l.Phone,
        CompanyName = l.CompanyName,
        Title = l.Title,
        Source = l.Source,
        Status = l.Status.ToString(),
        ExpectedValue = l.ExpectedValue,
        CreatedDate = l.CreatedDate,
        CreatedBy = l.CreatedBy,
        AssignedToUserId = l.AssignedToUserId,
        AssignedToUserName = l.AssignedToUser?.FullName,
        ConvertedCustomerId = l.ConvertedCustomerId,
        ConvertedCustomerName = l.ConvertedCustomer?.CustomerName,
        ConvertedOpportunityId = l.ConvertedOpportunityId,
        ConvertedDate = l.ConvertedDate
    };
}
