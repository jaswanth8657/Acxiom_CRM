using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels.Customers;

namespace AcxiomCRM.Controllers;

[Authorize]
public class CustomersController : Controller
{
    private readonly ICustomerService _customerService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        ICustomerService customerService,
        UserManager<ApplicationUser> userManager,
        ILogger<CustomersController> logger)
    {
        _customerService = customerService;
        _userManager = userManager;
        _logger = logger;
    }

    private async Task<(string UserId, IList<string> Roles)> GetCurrentUserContextAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return (string.Empty, new List<string>());
        }
        var roles = await _userManager.GetRolesAsync(user);
        return (user.Id, roles);
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] CustomerFilterViewModel filter)
    {
        var (userId, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var viewModel = await _customerService.GetPagedCustomersAsync(filter, userId, roles);
        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var (userId, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var details = await _customerService.GetCustomerDetailsAsync(id, userId, roles);
        if (details == null)
        {
            // Verify if record exists to distinguish between NotFound and AccessDenied
            var canAccess = await _customerService.CanUserAccessCustomerAsync(id, userId, roles);
            if (!canAccess)
            {
                return Forbid();
            }
            return NotFound();
        }

        return View(details);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var (userId, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        var model = new CustomerCreateViewModel
        {
            CustomerCode = await _customerService.GenerateCustomerCodeAsync(),
            AssignedToUserId = userId
        };

        if (isPrivileged)
        {
            model.AvailableUsers = await _userManager.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = $"{u.FullName} ({u.Email})",
                    Selected = u.Id == userId
                })
                .ToListAsync();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerCreateViewModel model)
    {
        var (userId, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        // Server-Side Validations
        if (string.IsNullOrWhiteSpace(model.CustomerName))
        {
            ModelState.AddModelError(nameof(model.CustomerName), "Customer Name is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Enter a valid email address.");
        }
        else if (!await _customerService.IsEmailUniqueAsync(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Email already exists.");
        }

        if (string.IsNullOrWhiteSpace(model.Phone))
        {
            ModelState.AddModelError(nameof(model.Phone), "Enter a valid phone number.");
        }
        else if (!await _customerService.IsPhoneUniqueAsync(model.Phone))
        {
            ModelState.AddModelError(nameof(model.Phone), "Phone number already exists.");
        }

        if (!string.IsNullOrWhiteSpace(model.CustomerCode) && !await _customerService.IsCustomerCodeUniqueAsync(model.CustomerCode))
        {
            ModelState.AddModelError(nameof(model.CustomerCode), "Customer code already exists.");
        }

        if (!ModelState.IsValid)
        {
            if (isPrivileged)
            {
                model.AvailableUsers = await _userManager.Users
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.FullName)
                    .Select(u => new SelectListItem
                    {
                        Value = u.Id,
                        Text = $"{u.FullName} ({u.Email})",
                        Selected = u.Id == model.AssignedToUserId
                    })
                    .ToListAsync();
            }
            return View(model);
        }

        var (success, errorMessage, createdCustomer) = await _customerService.CreateCustomerAsync(
            model, userId, roles, GetClientIpAddress());

        if (!success)
        {
            ModelState.AddModelError(string.Empty, errorMessage ?? "Failed to create customer.");
            if (isPrivileged)
            {
                model.AvailableUsers = await _userManager.Users
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.FullName)
                    .Select(u => new SelectListItem
                    {
                        Value = u.Id,
                        Text = $"{u.FullName} ({u.Email})",
                        Selected = u.Id == model.AssignedToUserId
                    })
                    .ToListAsync();
            }
            return View(model);
        }

        TempData["SuccessMessage"] = $"Customer '{createdCustomer?.CustomerName}' was created successfully.";
        return RedirectToAction(nameof(Details), new { id = createdCustomer?.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var (userId, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var model = await _customerService.GetCustomerForEditAsync(id, userId, roles);
        if (model == null)
        {
            var canAccess = await _customerService.CanUserAccessCustomerAsync(id, userId, roles);
            if (!canAccess)
            {
                return Forbid();
            }
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CustomerEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var (userId, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        // Authorization gate check
        var canAccess = await _customerService.CanUserAccessCustomerAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        // Server-Side Validations
        if (string.IsNullOrWhiteSpace(model.CustomerName))
        {
            ModelState.AddModelError(nameof(model.CustomerName), "Customer Name is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Enter a valid email address.");
        }
        else if (!await _customerService.IsEmailUniqueAsync(model.Email, id))
        {
            ModelState.AddModelError(nameof(model.Email), "Email already exists.");
        }

        if (string.IsNullOrWhiteSpace(model.Phone))
        {
            ModelState.AddModelError(nameof(model.Phone), "Enter a valid phone number.");
        }
        else if (!await _customerService.IsPhoneUniqueAsync(model.Phone, id))
        {
            ModelState.AddModelError(nameof(model.Phone), "Phone number already exists.");
        }

        if (!await _customerService.IsCustomerCodeUniqueAsync(model.CustomerCode, id))
        {
            ModelState.AddModelError(nameof(model.CustomerCode), "Customer code already exists.");
        }

        if (!ModelState.IsValid)
        {
            if (isPrivileged)
            {
                model.AvailableUsers = await _userManager.Users
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.FullName)
                    .Select(u => new SelectListItem
                    {
                        Value = u.Id,
                        Text = $"{u.FullName} ({u.Email})",
                        Selected = u.Id == model.AssignedToUserId
                    })
                    .ToListAsync();
            }
            return View(model);
        }

        var (success, errorMessage) = await _customerService.UpdateCustomerAsync(
            model, userId, roles, GetClientIpAddress());

        if (!success)
        {
            if (errorMessage != null && errorMessage.Contains("not authorized"))
            {
                return Forbid();
            }
            ModelState.AddModelError(string.Empty, errorMessage ?? "Failed to update customer.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Customer '{model.CustomerName}' was updated successfully.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var (userId, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var model = await _customerService.GetCustomerForDeleteAsync(id, userId, roles);
        if (model == null)
        {
            var canAccess = await _customerService.CanUserAccessCustomerAsync(id, userId, roles);
            if (!canAccess)
            {
                return Forbid();
            }
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var (userId, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _customerService.CanUserAccessCustomerAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var (success, errorMessage) = await _customerService.DeactivateCustomerAsync(
            id, userId, roles, GetClientIpAddress());

        if (!success)
        {
            if (errorMessage != null && errorMessage.Contains("not authorized"))
            {
                return Forbid();
            }
            TempData["ErrorMessage"] = errorMessage ?? "Failed to deactivate customer.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = "Customer was successfully deactivated (Status set to Inactive).";
        return RedirectToAction(nameof(Index));
    }
}
