using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels.FollowUps;

namespace AcxiomCRM.Controllers;

[Authorize]
public class FollowUpsController : Controller
{
    private readonly IFollowUpService _followUpService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<FollowUpsController> _logger;

    public FollowUpsController(
        IFollowUpService followUpService,
        UserManager<ApplicationUser> userManager,
        ILogger<FollowUpsController> logger)
    {
        _followUpService = followUpService;
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
    public async Task<IActionResult> Index([FromQuery] FollowUpFilterViewModel filter)
    {
        var (userId, _, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var viewModel = await _followUpService.GetPagedFollowUpsAsync(filter, userId, roles);
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

        var viewModel = await _followUpService.GetFollowUpDetailsAsync(id, userId, roles);
        if (viewModel == null)
        {
            // Check if record exists to distinguish between 404 and 403
            var canAccess = await _followUpService.CanUserAccessFollowUpAsync(id, userId, roles);
            if (!canAccess)
            {
                _logger.LogWarning("User {UserId} attempted unauthorized access to FollowUp {Id}", userId, id);
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

        var viewModel = await _followUpService.GetFollowUpCreateViewModelAsync(userId, roles, customerId, leadId, opportunityId);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUpCreateViewModel model)
    {
        var (userId, fullName, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
            model.CanAssignAny = isPrivileged;
            var reloadedVm = await _followUpService.GetFollowUpCreateViewModelAsync(userId, roles, model.CustomerId, model.LeadId, model.OpportunityId);
            model.AvailableCustomers = reloadedVm.AvailableCustomers;
            model.AvailableLeads = reloadedVm.AvailableLeads;
            model.AvailableOpportunities = reloadedVm.AvailableOpportunities;
            model.AvailableUsers = reloadedVm.AvailableUsers;
            return View(model);
        }

        var ip = GetClientIpAddress();
        var (success, errorMessage, followUpId) = await _followUpService.CreateFollowUpAsync(model, userId, fullName, roles, ip);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, errorMessage ?? "An error occurred while creating the follow-up.");
            var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
            model.CanAssignAny = isPrivileged;
            var reloadedVm = await _followUpService.GetFollowUpCreateViewModelAsync(userId, roles, model.CustomerId, model.LeadId, model.OpportunityId);
            model.AvailableCustomers = reloadedVm.AvailableCustomers;
            model.AvailableLeads = reloadedVm.AvailableLeads;
            model.AvailableOpportunities = reloadedVm.AvailableOpportunities;
            model.AvailableUsers = reloadedVm.AvailableUsers;
            return View(model);
        }

        TempData["SuccessMessage"] = $"Follow-up '{model.Subject}' scheduled successfully.";
        return RedirectToAction(nameof(Details), new { id = followUpId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var (userId, _, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _followUpService.CanUserAccessFollowUpAsync(id, userId, roles);
        if (!canAccess)
        {
            _logger.LogWarning("User {UserId} forbidden from editing FollowUp {Id}", userId, id);
            return Forbid();
        }

        var viewModel = await _followUpService.GetFollowUpForEditAsync(id, userId, roles);
        if (viewModel == null)
        {
            return NotFound();
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FollowUpEditViewModel model)
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

        var canAccess = await _followUpService.CanUserAccessFollowUpAsync(id, userId, roles);
        if (!canAccess)
        {
            _logger.LogWarning("User {UserId} forbidden from updating FollowUp {Id}", userId, id);
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            var reloadedVm = await _followUpService.GetFollowUpForEditAsync(id, userId, roles);
            if (reloadedVm != null)
            {
                model.AvailableCustomers = reloadedVm.AvailableCustomers;
                model.AvailableLeads = reloadedVm.AvailableLeads;
                model.AvailableOpportunities = reloadedVm.AvailableOpportunities;
                model.AvailableUsers = reloadedVm.AvailableUsers;
            }
            return View(model);
        }

        var ip = GetClientIpAddress();
        var (success, errorMessage) = await _followUpService.UpdateFollowUpAsync(model, userId, fullName, roles, ip);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, errorMessage ?? "Failed to update follow-up.");
            var reloadedVm = await _followUpService.GetFollowUpForEditAsync(id, userId, roles);
            if (reloadedVm != null)
            {
                model.AvailableCustomers = reloadedVm.AvailableCustomers;
                model.AvailableLeads = reloadedVm.AvailableLeads;
                model.AvailableOpportunities = reloadedVm.AvailableOpportunities;
                model.AvailableUsers = reloadedVm.AvailableUsers;
            }
            return View(model);
        }

        TempData["SuccessMessage"] = $"Follow-up '{model.Subject}' updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, string? notes)
    {
        var (userId, fullName, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _followUpService.CanUserAccessFollowUpAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var ip = GetClientIpAddress();
        var (success, errorMessage) = await _followUpService.CompleteFollowUpAsync(id, notes, userId, fullName, roles, ip);

        if (!success)
        {
            TempData["ErrorMessage"] = errorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Follow-up marked as completed.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkMissed(int id, string? reason)
    {
        var (userId, fullName, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _followUpService.CanUserAccessFollowUpAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var ip = GetClientIpAddress();
        var (success, errorMessage) = await _followUpService.MarkMissedFollowUpAsync(id, reason, userId, fullName, roles, ip);

        if (!success)
        {
            TempData["ErrorMessage"] = errorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Follow-up marked as missed.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        var (userId, fullName, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _followUpService.CanUserAccessFollowUpAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var ip = GetClientIpAddress();
        var (success, errorMessage) = await _followUpService.CancelFollowUpAsync(id, reason, userId, fullName, roles, ip);

        if (!success)
        {
            TempData["ErrorMessage"] = errorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Follow-up cancelled.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(int id, DateTime newDate, string? reason)
    {
        var (userId, fullName, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _followUpService.CanUserAccessFollowUpAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var ip = GetClientIpAddress();
        var (success, errorMessage) = await _followUpService.RescheduleFollowUpAsync(id, newDate, reason, userId, fullName, roles, ip);

        if (!success)
        {
            TempData["ErrorMessage"] = errorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Follow-up rescheduled successfully.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
