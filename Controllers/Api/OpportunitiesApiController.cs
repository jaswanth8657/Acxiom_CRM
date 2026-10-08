using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels.Opportunities;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/opportunities")]
[Authorize]
public class OpportunitiesApiController : ControllerBase
{
    private readonly IOpportunityService _opportunityService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<OpportunitiesApiController> _logger;

    public OpportunitiesApiController(
        IOpportunityService opportunityService,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<OpportunitiesApiController> logger)
    {
        _opportunityService = opportunityService;
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<OpportunityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] OpportunityQueryDto query)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var filter = new OpportunityFilterViewModel
        {
            SearchTerm = query.SearchTerm,
            Stage = query.Stage,
            Status = query.Status,
            AssignedToUserId = isPrivileged ? query.AssignedToUserId : currentUser.Id,
            Page = query.Page < 1 ? 1 : query.Page,
            PageSize = query.PageSize < 1 ? 10 : query.PageSize
        };

        var pagedResult = await _opportunityService.GetPagedOpportunitiesAsync(filter, currentUser.Id, roles);

        var dtos = pagedResult.Opportunities.Select(o => new OpportunityDto
        {
            Id = o.Id,
            OpportunityName = o.OpportunityName,
            CustomerId = o.CustomerId,
            CustomerName = o.CustomerName,
            LeadId = o.LeadId,
            LeadName = o.LeadName,
            Amount = o.Amount,
            Stage = o.Stage.ToString(),
            Probability = o.Probability,
            WeightedAmount = o.WeightedAmount,
            ExpectedCloseDate = o.ExpectedCloseDate,
            Status = o.Status.ToString(),
            CreatedDate = o.CreatedDate,
            AssignedToUserId = o.AssignedToUserId,
            AssignedToUserName = o.AssignedToUserName
        }).ToList();

        return Ok(new PagedResultDto<OpportunityDto>
        {
            Items = dtos,
            TotalItems = pagedResult.TotalItems,
            Page = pagedResult.CurrentPage,
            PageSize = pagedResult.PageSize
        });
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var opp = await _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedToUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id);

        if (opp == null)
        {
            return NotFound(new { message = $"Opportunity with ID {id} not found." });
        }

        if (!isPrivileged && opp.AssignedToUserId != currentUser.Id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to view this opportunity." });
        }

        return Ok(MapToDto(opp));
    }

    [HttpPost]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateOpportunityDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        // Validate customer existence and access
        var customer = await _context.Customers.FindAsync(dto.CustomerId);
        if (customer == null)
        {
            return BadRequest(new { message = $"Specified customer #{dto.CustomerId} does not exist." });
        }

        if (!isPrivileged && customer.AssignedToUserId != currentUser.Id)
        {
            return BadRequest(new { message = "You can only create opportunities for customers assigned to you." });
        }

        // Validate business rules
        if (dto.Amount <= 0)
        {
            return BadRequest(new { message = "Opportunity amount must be greater than 0." });
        }

        if (dto.Probability < 0 || dto.Probability > 100)
        {
            return BadRequest(new { message = "Probability must be between 0 and 100." });
        }

        if (dto.ExpectedCloseDate.Date < DateTime.Today)
        {
            return BadRequest(new { message = "Expected Close Date cannot be in the past." });
        }

        var model = new OpportunityCreateViewModel
        {
            OpportunityName = dto.OpportunityName,
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            Amount = dto.Amount,
            Stage = dto.Stage,
            Probability = dto.Probability,
            ExpectedCloseDate = dto.ExpectedCloseDate,
            Notes = dto.Notes,
            AssignedToUserId = isPrivileged ? (dto.AssignedToUserId ?? currentUser.Id) : currentUser.Id
        };

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (success, error, oppId) = await _opportunityService.CreateOpportunityAsync(
            model,
            currentUser.Id,
            currentUser.FullName,
            roles,
            ip);

        if (!success || !oppId.HasValue)
        {
            return BadRequest(new { message = error ?? "Failed to create opportunity." });
        }

        var created = await _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedToUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == oppId.Value);

        var resultDto = MapToDto(created!);
        return CreatedAtAction(nameof(GetById), new { id = resultDto.Id }, resultDto);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOpportunityDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var existing = await _context.Opportunities.FindAsync(id);
        if (existing == null)
        {
            return NotFound(new { message = $"Opportunity with ID {id} not found." });
        }

        if (!isPrivileged && existing.AssignedToUserId != currentUser.Id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to modify this opportunity." });
        }

        // Terminal stage check
        if (existing.Stage == OpportunityStage.Won || existing.Stage == OpportunityStage.Lost)
        {
            if (existing.Stage != dto.Stage)
            {
                return BadRequest(new { message = $"Cannot change stage of a closed deal ({existing.Stage}). Closed deals are in terminal state." });
            }
        }
        else if (existing.Stage != dto.Stage && !_opportunityService.IsValidStageTransition(existing.Stage, dto.Stage))
        {
            return BadRequest(new { message = $"Invalid stage transition from {existing.Stage} to {dto.Stage}." });
        }

        if (dto.Amount <= 0)
        {
            return BadRequest(new { message = "Opportunity amount must be greater than 0." });
        }

        if (dto.Probability < 0 || dto.Probability > 100)
        {
            return BadRequest(new { message = "Probability must be between 0 and 100." });
        }

        var model = new OpportunityEditViewModel
        {
            Id = id,
            OpportunityName = dto.OpportunityName,
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            Amount = dto.Amount,
            Stage = dto.Stage,
            Probability = dto.Probability,
            ExpectedCloseDate = dto.ExpectedCloseDate,
            Status = dto.Status,
            Notes = dto.Notes,
            AssignedToUserId = isPrivileged ? (dto.AssignedToUserId ?? existing.AssignedToUserId) : existing.AssignedToUserId
        };

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (success, error) = await _opportunityService.UpdateOpportunityAsync(
            model,
            currentUser.Id,
            currentUser.FullName,
            roles,
            ip);

        if (!success)
        {
            return BadRequest(new { message = error ?? "Failed to update opportunity." });
        }

        var updated = await _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedToUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id);

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

        var existing = await _context.Opportunities.FindAsync(id);
        if (existing == null)
        {
            return NotFound(new { message = $"Opportunity with ID {id} not found." });
        }

        if (!isPrivileged && existing.AssignedToUserId != currentUser.Id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to delete this opportunity." });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (success, error) = await _opportunityService.DeleteOpportunityAsync(id, currentUser.Id, roles, ip);

        if (!success)
        {
            return BadRequest(new { message = error ?? "Failed to deactivate opportunity." });
        }

        return Ok(new { message = $"Opportunity #{id} successfully deactivated." });
    }

    private static OpportunityDto MapToDto(Opportunity o) => new()
    {
        Id = o.Id,
        OpportunityName = o.OpportunityName,
        CustomerId = o.CustomerId,
        CustomerName = o.Customer?.CustomerName ?? "Unknown",
        LeadId = o.LeadId,
        LeadName = o.Lead?.LeadName,
        Amount = o.Amount,
        Stage = o.Stage.ToString(),
        Probability = o.Probability,
        WeightedAmount = o.WeightedAmount,
        ExpectedCloseDate = o.ExpectedCloseDate,
        Status = o.Status.ToString(),
        CreatedDate = o.CreatedDate,
        AssignedToUserId = o.AssignedToUserId,
        AssignedToUserName = o.AssignedToUser?.FullName,
        Notes = o.Notes
    };
}
