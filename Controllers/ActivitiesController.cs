using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels.Activities;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ActivitiesController : Controller
{
    private readonly IActivityService _activityService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ActivitiesController> _logger;

    public ActivitiesController(
        IActivityService activityService,
        UserManager<ApplicationUser> userManager,
        ILogger<ActivitiesController> logger)
    {
        _activityService = activityService;
        _userManager = userManager;
        _logger = logger;
    }

    private async Task<(string UserId, string FullName, IList<string> Roles)> GetCurrentUserContextAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return (string.Empty, string.Empty, new List<string>());
        }
        var roles = await _userManager.GetRolesAsync(user);
        return (user.Id, user.FullName, roles);
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ActivityFilterViewModel filter)
    {
        var (userId, _, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var viewModel = await _activityService.GetPagedActivitiesAsync(filter, userId, roles);
        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var (userId, _, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var viewModel = await _activityService.GetActivityDetailsAsync(id, userId, roles);
        if (viewModel == null)
        {
            var canAccess = await _activityService.CanUserAccessActivityAsync(id, userId, roles);
            if (!canAccess)
            {
                _logger.LogWarning("User {UserId} forbidden from viewing Activity {Id}", userId, id);
                return Forbid();
            }
            return NotFound();
        }

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId, int? leadId, int? opportunityId)
    {
        var (userId, _, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var viewModel = await _activityService.GetActivityCreateViewModelAsync(userId, roles, customerId, leadId, opportunityId);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ActivityCreateViewModel model)
    {
        var (userId, fullName, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            var reloadedVm = await _activityService.GetActivityCreateViewModelAsync(userId, roles, model.CustomerId, model.LeadId, model.OpportunityId);
            model.AvailableCustomers = reloadedVm.AvailableCustomers;
            model.AvailableLeads = reloadedVm.AvailableLeads;
            model.AvailableOpportunities = reloadedVm.AvailableOpportunities;
            model.AvailableUsers = reloadedVm.AvailableUsers;
            model.CanAssignAny = reloadedVm.CanAssignAny;
            return View(model);
        }

        var ip = GetClientIpAddress();
        var (success, errorMessage, activityId) = await _activityService.CreateActivityAsync(model, userId, fullName, roles, ip);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, errorMessage ?? "Failed to create activity.");
            var reloadedVm = await _activityService.GetActivityCreateViewModelAsync(userId, roles, model.CustomerId, model.LeadId, model.OpportunityId);
            model.AvailableCustomers = reloadedVm.AvailableCustomers;
            model.AvailableLeads = reloadedVm.AvailableLeads;
            model.AvailableOpportunities = reloadedVm.AvailableOpportunities;
            model.AvailableUsers = reloadedVm.AvailableUsers;
            model.CanAssignAny = reloadedVm.CanAssignAny;
            return View(model);
        }

        TempData["SuccessMessage"] = $"Activity '{model.Subject}' logged successfully.";
        return RedirectToAction(nameof(Details), new { id = activityId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var (userId, _, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _activityService.CanUserAccessActivityAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var viewModel = await _activityService.GetActivityForEditAsync(id, userId, roles);
        if (viewModel == null)
        {
            return NotFound();
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ActivityEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var (userId, fullName, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _activityService.CanUserAccessActivityAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            var reloadedVm = await _activityService.GetActivityForEditAsync(id, userId, roles);
            if (reloadedVm != null)
            {
                model.AvailableCustomers = reloadedVm.AvailableCustomers;
                model.AvailableLeads = reloadedVm.AvailableLeads;
                model.AvailableOpportunities = reloadedVm.AvailableOpportunities;
                model.AvailableUsers = reloadedVm.AvailableUsers;
                model.CanAssignAny = reloadedVm.CanAssignAny;
            }
            return View(model);
        }

        var ip = GetClientIpAddress();
        var (success, errorMessage) = await _activityService.UpdateActivityAsync(model, userId, fullName, roles, ip);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, errorMessage ?? "Failed to update activity.");
            var reloadedVm = await _activityService.GetActivityForEditAsync(id, userId, roles);
            if (reloadedVm != null)
            {
                model.AvailableCustomers = reloadedVm.AvailableCustomers;
                model.AvailableLeads = reloadedVm.AvailableLeads;
                model.AvailableOpportunities = reloadedVm.AvailableOpportunities;
                model.AvailableUsers = reloadedVm.AvailableUsers;
                model.CanAssignAny = reloadedVm.CanAssignAny;
            }
            return View(model);
        }

        TempData["SuccessMessage"] = $"Activity '{model.Subject}' updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var (userId, fullName, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _activityService.CanUserAccessActivityAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var ip = GetClientIpAddress();
        var (success, errorMessage) = await _activityService.CompleteActivityAsync(id, userId, fullName, roles, ip);

        if (!success)
        {
            TempData["ErrorMessage"] = errorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Activity marked as completed.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var (userId, fullName, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _activityService.CanUserAccessActivityAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var ip = GetClientIpAddress();
        var (success, errorMessage) = await _activityService.CancelActivityAsync(id, userId, fullName, roles, ip);

        if (!success)
        {
            TempData["ErrorMessage"] = errorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Activity cancelled.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (userId, fullName, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _activityService.CanUserAccessActivityAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var ip = GetClientIpAddress();
        var (success, errorMessage) = await _activityService.DeleteActivityAsync(id, userId, fullName, roles, ip);

        if (!success)
        {
            TempData["ErrorMessage"] = errorMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["SuccessMessage"] = "Activity deleted successfully.";
        return RedirectToAction(nameof(Index));
    }
}
