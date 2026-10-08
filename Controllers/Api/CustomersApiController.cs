using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels.Customers;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/customers")]
[Authorize]
public class CustomersApiController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CustomersApiController> _logger;

    public CustomersApiController(
        ICustomerService customerService,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<CustomersApiController> logger)
    {
        _customerService = customerService;
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] CustomerQueryDto query)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var filter = new CustomerFilterViewModel
        {
            SearchTerm = query.SearchTerm,
            Status = query.Status,
            AssignedToUserId = isPrivileged ? query.AssignedToUserId : currentUser.Id,
            Page = query.Page < 1 ? 1 : query.Page,
            PageSize = query.PageSize < 1 ? 10 : query.PageSize
        };

        var pagedResult = await _customerService.GetPagedCustomersAsync(filter, currentUser.Id, roles);

        var dtos = pagedResult.Customers.Select(c => new CustomerDto
        {
            Id = c.Id,
            CustomerCode = c.CustomerCode,
            CustomerName = c.CustomerName,
            Email = c.Email,
            Phone = c.Phone,
            CompanyName = c.CompanyName,
            Status = c.Status.ToString(),
            CreatedDate = c.CreatedDate,
            AssignedToUserId = c.AssignedToUserId,
            AssignedToUserName = c.AssignedToUserName
        }).ToList();

        return Ok(new PagedResultDto<CustomerDto>
        {
            Items = dtos,
            TotalItems = pagedResult.TotalItems,
            Page = pagedResult.CurrentPage,
            PageSize = pagedResult.PageSize
        });
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var customer = await _context.Customers
            .Include(c => c.AssignedToUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (customer == null)
        {
            return NotFound(new { message = $"Customer with ID {id} not found." });
        }

        if (!isPrivileged && customer.AssignedToUserId != currentUser.Id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to view this customer." });
        }

        return Ok(MapToDto(customer));
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        // Uniqueness checks
        if (!await _customerService.IsEmailUniqueAsync(dto.Email))
        {
            return Conflict(new { message = "A customer with this email address already exists." });
        }

        if (!await _customerService.IsPhoneUniqueAsync(dto.Phone))
        {
            return Conflict(new { message = "A customer with this phone number already exists." });
        }

        if (!string.IsNullOrEmpty(dto.CustomerCode) && !await _customerService.IsCustomerCodeUniqueAsync(dto.CustomerCode))
        {
            return Conflict(new { message = "Customer code is already in use." });
        }

        var model = new CustomerCreateViewModel
        {
            CustomerName = dto.CustomerName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            CustomerCode = dto.CustomerCode,
            AssignedToUserId = isPrivileged ? (dto.AssignedToUserId ?? currentUser.Id) : currentUser.Id
        };

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (success, errorMessage, createdCustomer) = await _customerService.CreateCustomerAsync(model, currentUser.Id, roles, ip);

        if (!success || createdCustomer == null)
        {
            return BadRequest(new { message = errorMessage ?? "Failed to create customer." });
        }

        // Reload with navigation properties
        var customer = await _context.Customers
            .Include(c => c.AssignedToUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == createdCustomer.Id);

        var resultDto = MapToDto(customer ?? createdCustomer);
        return CreatedAtAction(nameof(GetById), new { id = resultDto.Id }, resultDto);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var existing = await _context.Customers.FindAsync(id);
        if (existing == null)
        {
            return NotFound(new { message = $"Customer with ID {id} not found." });
        }

        if (!isPrivileged && existing.AssignedToUserId != currentUser.Id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to modify this customer." });
        }

        if (!await _customerService.IsEmailUniqueAsync(dto.Email, id))
        {
            return Conflict(new { message = "A customer with this email address already exists." });
        }

        if (!await _customerService.IsPhoneUniqueAsync(dto.Phone, id))
        {
            return Conflict(new { message = "A customer with this phone number already exists." });
        }

        var model = new CustomerEditViewModel
        {
            Id = id,
            CustomerCode = existing.CustomerCode,
            CustomerName = dto.CustomerName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Status = dto.Status,
            AssignedToUserId = isPrivileged ? (dto.AssignedToUserId ?? existing.AssignedToUserId) : existing.AssignedToUserId
        };

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (success, errorMessage) = await _customerService.UpdateCustomerAsync(model, currentUser.Id, roles, ip);

        if (!success)
        {
            return BadRequest(new { message = errorMessage ?? "Failed to update customer." });
        }

        var updated = await _context.Customers
            .Include(c => c.AssignedToUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

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

        var existing = await _context.Customers.FindAsync(id);
        if (existing == null)
        {
            return NotFound(new { message = $"Customer with ID {id} not found." });
        }

        if (!isPrivileged && existing.AssignedToUserId != currentUser.Id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to deactivate this customer." });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (success, errorMessage) = await _customerService.DeactivateCustomerAsync(id, currentUser.Id, roles, ip);

        if (!success)
        {
            return BadRequest(new { message = errorMessage ?? "Failed to deactivate customer." });
        }

        return Ok(new { message = $"Customer #{id} successfully deactivated." });
    }

    private static CustomerDto MapToDto(Customer c) => new()
    {
        Id = c.Id,
        CustomerCode = c.CustomerCode,
        CustomerName = c.CustomerName,
        Email = c.Email,
        Phone = c.Phone,
        CompanyName = c.CompanyName,
        Address = c.Address,
        City = c.City,
        State = c.State,
        Status = c.Status.ToString(),
        CreatedDate = c.CreatedDate,
        CreatedBy = c.CreatedBy,
        AssignedToUserId = c.AssignedToUserId,
        AssignedToUserName = c.AssignedToUser?.FullName
    };
}
