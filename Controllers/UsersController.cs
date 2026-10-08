using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Users;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = DbInitializer.RoleAdmin)]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext context,
        ILogger<UsersController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var currentUserId = _userManager.GetUserId(User);
        var users = await _userManager.Users
            .OrderByDescending(u => u.CreatedDate)
            .ToListAsync();

        var viewModels = new List<UserListItemViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var primaryRole = roles.FirstOrDefault() ?? "None";

            viewModels.Add(new UserListItemViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Role = primaryRole,
                IsActive = user.IsActive,
                CreatedDate = user.CreatedDate,
                IsCurrentUser = user.Id == currentUserId
            });
        }

        return View(viewModels);
    }

    [HttpGet]
    public IActionResult Create()
    {
        var model = new UserCreateViewModel
        {
            AvailableRoles = GetAvailableRolesList()
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableRoles = GetAvailableRolesList();
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError(nameof(model.Email), "A user with this email address already exists.");
            model.AvailableRoles = GetAvailableRolesList();
            return View(model);
        }

        if (!await _roleManager.RoleExistsAsync(model.SelectedRole))
        {
            ModelState.AddModelError(nameof(model.SelectedRole), "Specified role does not exist.");
            model.AvailableRoles = GetAvailableRolesList();
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            EmailConfirmed = true,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            model.AvailableRoles = GetAvailableRolesList();
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, model.SelectedRole);

        var currentUserId = _userManager.GetUserId(User) ?? string.Empty;
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "CreateUser",
            EntityName = "ApplicationUser",
            EntityId = user.Id,
            Details = $"Created user '{user.FullName}' ({user.Email}) with role '{model.SelectedRole}'.",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"User '{user.FullName}' created successfully with role '{model.SelectedRole}'.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> EditRole(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var primaryRole = currentRoles.FirstOrDefault() ?? DbInitializer.RoleSalesExecutive;

        var model = new UserEditRoleViewModel
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            CurrentRole = primaryRole,
            SelectedRole = primaryRole,
            AvailableRoles = GetAvailableRolesList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRole(UserEditRoleViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            return NotFound();
        }

        var currentUserId = _userManager.GetUserId(User);
        if (user.Id == currentUserId && model.SelectedRole != DbInitializer.RoleAdmin)
        {
            ModelState.AddModelError(string.Empty, "Administrators cannot remove their own Admin role.");
            model.FullName = user.FullName;
            model.Email = user.Email ?? string.Empty;
            model.AvailableRoles = GetAvailableRolesList();
            return View(model);
        }

        if (!await _roleManager.RoleExistsAsync(model.SelectedRole))
        {
            ModelState.AddModelError(nameof(model.SelectedRole), "Specified role does not exist.");
            model.FullName = user.FullName;
            model.Email = user.Email ?? string.Empty;
            model.AvailableRoles = GetAvailableRolesList();
            return View(model);
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var oldRole = currentRoles.FirstOrDefault() ?? "None";

        // Remove all previous roles to enforce exactly one primary role
        if (currentRoles.Any())
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }

        await _userManager.AddToRoleAsync(user, model.SelectedRole);

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId ?? string.Empty,
            Action = "ChangeUserRole",
            EntityName = "ApplicationUser",
            EntityId = user.Id,
            Details = $"Changed role for user '{user.FullName}' ({user.Email}) from '{oldRole}' to '{model.SelectedRole}'.",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Role for '{user.FullName}' successfully updated to '{model.SelectedRole}'.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var currentUserId = _userManager.GetUserId(User);
        if (user.Id == currentUserId)
        {
            TempData["ErrorMessage"] = "You cannot deactivate your own account.";
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);

        var action = user.IsActive ? "ActivateUser" : "DeactivateUser";
        var statusText = user.IsActive ? "activated" : "deactivated";

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId ?? string.Empty,
            Action = action,
            EntityName = "ApplicationUser",
            EntityId = user.Id,
            Details = $"User '{user.FullName}' ({user.Email}) was {statusText}.",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"User '{user.FullName}' has been {statusText}.";
        return RedirectToAction(nameof(Index));
    }

    private static List<SelectListItem> GetAvailableRolesList()
    {
        return new List<SelectListItem>
        {
            new() { Value = DbInitializer.RoleAdmin, Text = "Admin (Full Access)" },
            new() { Value = DbInitializer.RoleManager, Text = "Manager (Team & Pipeline Access)" },
            new() { Value = DbInitializer.RoleSalesExecutive, Text = "SalesExecutive (Assigned Records Only)" }
        };
    }
}
