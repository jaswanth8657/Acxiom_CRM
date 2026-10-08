using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels.Leads;

namespace AcxiomCRM.Controllers;

[Authorize]
public class LeadsController : Controller
{
    private readonly ILeadService _leadService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<LeadsController> _logger;

    public LeadsController(
        ILeadService leadService,
        UserManager<ApplicationUser> userManager,
        ILogger<LeadsController> logger)
    {
        _leadService = leadService;
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
    public async Task<IActionResult> Index([FromQuery] LeadFilterViewModel filter)
    {
        var (userId, _, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var viewModel = await _leadService.GetPagedLeadsAsync(filter, userId, roles);
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

        var details = await _leadService.GetLeadDetailsAsync(id, userId, roles);
        if (details == null)
        {
            var canAccess = await _leadService.CanUserAccessLeadAsync(id, userId, roles);
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
        var (userId, _, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);
        var model = new LeadCreateViewModel
        {
            LeadCode = await _leadService.GenerateLeadCodeAsync(),
            AssignedToUserId = userId,
            Source = LeadSources.Website,
            Status = LeadStatus.New
        };

        if (isPrivileged)
        {
            model.AvailableUsers = await _leadService.GetAvailableAssigneesAsync();
        }

        model.AvailableSources = LeadSources.All
            .Select(s => new SelectListItem { Value = s, Text = s, Selected = s == model.Source })
            .ToList();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LeadCreateViewModel model)
    {
        var (userId, fullName, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        // Server-Side Validations
        if (string.IsNullOrWhiteSpace(model.LeadName))
        {
            ModelState.AddModelError(nameof(model.LeadName), "Lead Name is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Enter a valid email address.");
        }
        else if (!await _leadService.IsEmailUniqueAsync(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Email already exists.");
        }

        if (!string.IsNullOrWhiteSpace(model.LeadCode) && !await _leadService.IsLeadCodeUniqueAsync(model.LeadCode))
        {
            ModelState.AddModelError(nameof(model.LeadCode), "Lead Code already exists.");
        }

        if (model.ExpectedValue.HasValue && model.ExpectedValue.Value < 0)
        {
            ModelState.AddModelError(nameof(model.ExpectedValue), "Expected Value cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(model.Source))
        {
            ModelState.AddModelError(nameof(model.Source), "Source is required.");
        }

        if (!isPrivileged)
        {
            // Non-privileged users can only assign leads to themselves
            model.AssignedToUserId = userId;
        }

        if (!ModelState.IsValid)
        {
            if (isPrivileged)
            {
                model.AvailableUsers = await _leadService.GetAvailableAssigneesAsync();
            }
            model.AvailableSources = LeadSources.All
                .Select(s => new SelectListItem { Value = s, Text = s, Selected = s == model.Source })
                .ToList();
            return View(model);
        }

        var ipAddress = GetClientIpAddress();
        var (success, error, leadId) = await _leadService.CreateLeadAsync(model, userId, fullName, ipAddress);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Failed to create lead.");
            if (isPrivileged)
            {
                model.AvailableUsers = await _leadService.GetAvailableAssigneesAsync();
            }
            model.AvailableSources = LeadSources.All
                .Select(s => new SelectListItem { Value = s, Text = s, Selected = s == model.Source })
                .ToList();
            return View(model);
        }

        TempData["SuccessMessage"] = $"Lead '{model.LeadName}' created successfully.";
        return RedirectToAction(nameof(Details), new { id = leadId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var (userId, _, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _leadService.CanUserAccessLeadAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var lead = await _leadService.GetLeadByIdAsync(id);
        if (lead == null)
        {
            return NotFound();
        }

        if (lead.Status == LeadStatus.Converted)
        {
            TempData["WarningMessage"] = "Converted leads cannot be modified.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var model = new LeadEditViewModel
        {
            Id = lead.Id,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            Title = lead.Title,
            Source = lead.Source,
            CurrentStatus = lead.Status,
            Status = lead.Status,
            ExpectedValue = lead.ExpectedValue,
            AssignedToUserId = lead.AssignedToUserId,
            CanReassign = isPrivileged
        };

        if (isPrivileged)
        {
            model.AvailableUsers = await _leadService.GetAvailableAssigneesAsync();
        }

        model.AvailableSources = LeadSources.All
            .Select(s => new SelectListItem { Value = s, Text = s, Selected = s == lead.Source })
            .ToList();

        var allowedStatuses = _leadService.GetAllowedNextStatuses(lead.Status);
        model.AllowedStatuses = allowedStatuses
            .Select(s => new SelectListItem { Value = ((int)s).ToString(), Text = s.ToString(), Selected = s == lead.Status })
            .ToList();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, LeadEditViewModel model)
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

        var canAccess = await _leadService.CanUserAccessLeadAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        // Server-Side Validations
        if (string.IsNullOrWhiteSpace(model.LeadName))
        {
            ModelState.AddModelError(nameof(model.LeadName), "Lead Name is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Enter a valid email address.");
        }
        else if (!await _leadService.IsEmailUniqueAsync(model.Email, id))
        {
            ModelState.AddModelError(nameof(model.Email), "Email already exists.");
        }

        if (model.ExpectedValue.HasValue && model.ExpectedValue.Value < 0)
        {
            ModelState.AddModelError(nameof(model.ExpectedValue), "Expected Value cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(model.Source))
        {
            ModelState.AddModelError(nameof(model.Source), "Source is required.");
        }

        if (!isPrivileged)
        {
            // Non-privileged users cannot reassign
            var existingLead = await _leadService.GetLeadByIdAsync(id);
            if (existingLead != null)
            {
                model.AssignedToUserId = existingLead.AssignedToUserId;
            }
        }

        if (!ModelState.IsValid)
        {
            if (isPrivileged)
            {
                model.AvailableUsers = await _leadService.GetAvailableAssigneesAsync();
            }
            model.AvailableSources = LeadSources.All
                .Select(s => new SelectListItem { Value = s, Text = s, Selected = s == model.Source })
                .ToList();

            var allowedStatuses = _leadService.GetAllowedNextStatuses(model.CurrentStatus);
            model.AllowedStatuses = allowedStatuses
                .Select(s => new SelectListItem { Value = ((int)s).ToString(), Text = s.ToString(), Selected = s == model.Status })
                .ToList();

            return View(model);
        }

        var ipAddress = GetClientIpAddress();
        var (success, error) = await _leadService.UpdateLeadAsync(model, userId, fullName, roles, ipAddress);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Failed to update lead.");
            if (isPrivileged)
            {
                model.AvailableUsers = await _leadService.GetAvailableAssigneesAsync();
            }
            model.AvailableSources = LeadSources.All
                .Select(s => new SelectListItem { Value = s, Text = s, Selected = s == model.Source })
                .ToList();
            var allowedStatuses = _leadService.GetAllowedNextStatuses(model.CurrentStatus);
            model.AllowedStatuses = allowedStatuses
                .Select(s => new SelectListItem { Value = ((int)s).ToString(), Text = s.ToString(), Selected = s == model.Status })
                .ToList();
            return View(model);
        }

        TempData["SuccessMessage"] = $"Lead '{model.LeadName}' updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var (userId, _, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _leadService.CanUserAccessLeadAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var lead = await _leadService.GetLeadByIdAsync(id);
        if (lead == null)
        {
            return NotFound();
        }

        return View(lead);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var (userId, _, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _leadService.CanUserAccessLeadAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var ipAddress = GetClientIpAddress();
        var (success, error) = await _leadService.DeleteLeadAsync(id, userId, roles, ipAddress);

        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Failed to delete lead.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["SuccessMessage"] = "Lead deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Convert(int id)
    {
        var (userId, _, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _leadService.CanUserAccessLeadAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var lead = await _leadService.GetLeadByIdAsync(id);
        if (lead == null)
        {
            return NotFound();
        }

        if (lead.Status == LeadStatus.Converted)
        {
            TempData["WarningMessage"] = "This lead has already been converted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (lead.Status != LeadStatus.Qualified)
        {
            TempData["WarningMessage"] = "Only Qualified leads can be converted. Please change the lead status to Qualified first.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Check if an existing customer matches email or phone to show duplicate notification
        var normalizedEmail = lead.Email.Trim().ToLower();
        var phone = !string.IsNullOrWhiteSpace(lead.Phone) ? lead.Phone.Trim() : null;

        var existingCustomer = await _leadService.GetLeadDetailsAsync(id, userId, roles); // using DB context via service

        var model = new LeadConvertViewModel
        {
            LeadId = lead.Id,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            ExpectedValue = lead.ExpectedValue,
            AssignedToUserId = lead.AssignedToUserId,
            AssignedToUserName = lead.AssignedToUser?.FullName ?? "Unassigned",
            OpportunityTitle = !string.IsNullOrWhiteSpace(lead.CompanyName)
                ? $"{lead.CompanyName} - Expansion Deal"
                : $"{lead.LeadName} - Deal",
            OpportunityAmount = lead.ExpectedValue ?? 10000m,
            OpportunityStage = OpportunityStage.Prospecting,
            ExpectedCloseDate = DateTime.Today.AddDays(30)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(int id, LeadConvertViewModel model)
    {
        if (id != model.LeadId)
        {
            return BadRequest();
        }

        var (userId, fullName, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _leadService.CanUserAccessLeadAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        // Server-Side Validations
        if (string.IsNullOrWhiteSpace(model.OpportunityTitle))
        {
            ModelState.AddModelError(nameof(model.OpportunityTitle), "Opportunity Title is required.");
        }

        if (!model.OpportunityAmount.HasValue || model.OpportunityAmount.Value < 0)
        {
            ModelState.AddModelError(nameof(model.OpportunityAmount), "Valid opportunity amount is required.");
        }

        if (!model.ExpectedCloseDate.HasValue)
        {
            ModelState.AddModelError(nameof(model.ExpectedCloseDate), "Expected Close Date is required.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var ipAddress = GetClientIpAddress();
        var (success, error, customerId, opportunityId) = await _leadService.ConvertLeadAsync(
            model, userId, fullName, roles, ipAddress);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Failed to convert lead.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Lead successfully converted! Customer #{customerId} and Opportunity #{opportunityId} created.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(LeadStatusChangeViewModel model)
    {
        var (userId, fullName, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _leadService.CanUserAccessLeadAsync(model.LeadId, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var ipAddress = GetClientIpAddress();
        var (success, error) = await _leadService.ChangeStatusAsync(
            model.LeadId, model.NewStatus, model.Note, userId, fullName, roles, ipAddress);

        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Failed to update lead status.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Lead status updated to {model.NewStatus}.";
        }

        return RedirectToAction(nameof(Details), new { id = model.LeadId });
    }
}
