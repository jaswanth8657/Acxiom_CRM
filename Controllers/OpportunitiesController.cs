using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels.Opportunities;

namespace AcxiomCRM.Controllers;

[Authorize]
public class OpportunitiesController : Controller
{
    private readonly IOpportunityService _opportunityService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<OpportunitiesController> _logger;

    public OpportunitiesController(
        IOpportunityService opportunityService,
        UserManager<ApplicationUser> userManager,
        ILogger<OpportunitiesController> logger)
    {
        _opportunityService = opportunityService;
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
    public async Task<IActionResult> Index([FromQuery] OpportunityFilterViewModel filter)
    {
        var (userId, _, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var viewModel = await _opportunityService.GetPagedOpportunitiesAsync(filter, userId, roles);
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

        var details = await _opportunityService.GetOpportunityDetailsAsync(id, userId, roles);
        if (details == null)
        {
            var canAccess = await _opportunityService.CanUserAccessOpportunityAsync(id, userId, roles);
            if (!canAccess)
            {
                return Forbid();
            }
            return NotFound();
        }

        return View(details);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
    {
        var (userId, _, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var model = new OpportunityCreateViewModel
        {
            CustomerId = customerId ?? 0,
            LeadId = leadId,
            Stage = OpportunityStage.Qualification,
            Probability = 25,
            ExpectedCloseDate = DateTime.Today.AddDays(30),
            AssignedToUserId = userId
        };

        model.AvailableCustomers = await _opportunityService.GetAvailableCustomersAsync(userId, roles);
        model.AvailableLeads = await _opportunityService.GetAvailableLeadsAsync(userId, roles);
        if (isPrivileged)
        {
            model.AvailableUsers = await _opportunityService.GetAvailableAssigneesAsync();
        }

        model.AvailableStages = new[]
        {
            OpportunityStage.Qualification,
            OpportunityStage.Proposal,
            OpportunityStage.Negotiation,
            OpportunityStage.Won,
            OpportunityStage.Lost
        }
        .Select(s => new SelectListItem { Value = ((int)s).ToString(), Text = s.ToString(), Selected = s == model.Stage })
        .ToList();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OpportunityCreateViewModel model)
    {
        var (userId, fullName, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        // Server-Side Validations
        if (string.IsNullOrWhiteSpace(model.OpportunityName))
        {
            ModelState.AddModelError(nameof(model.OpportunityName), "Opportunity Name is required.");
        }

        if (model.CustomerId <= 0)
        {
            ModelState.AddModelError(nameof(model.CustomerId), "Customer is required.");
        }

        if (model.Amount <= 0)
        {
            ModelState.AddModelError(nameof(model.Amount), "Amount must be greater than 0.");
        }

        if (model.Probability < 0 || model.Probability > 100)
        {
            ModelState.AddModelError(nameof(model.Probability), "Probability must be between 0 and 100 inclusive.");
        }

        if (model.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
        {
            ModelState.AddModelError(nameof(model.ExpectedCloseDate), "Expected Close Date cannot be in the past for an active opportunity.");
        }

        if (!isPrivileged)
        {
            model.AssignedToUserId = userId;
        }

        if (!ModelState.IsValid)
        {
            model.AvailableCustomers = await _opportunityService.GetAvailableCustomersAsync(userId, roles);
            model.AvailableLeads = await _opportunityService.GetAvailableLeadsAsync(userId, roles);
            if (isPrivileged)
            {
                model.AvailableUsers = await _opportunityService.GetAvailableAssigneesAsync();
            }
            model.AvailableStages = new[]
            {
                OpportunityStage.Qualification,
                OpportunityStage.Proposal,
                OpportunityStage.Negotiation,
                OpportunityStage.Won,
                OpportunityStage.Lost
            }
            .Select(s => new SelectListItem { Value = ((int)s).ToString(), Text = s.ToString(), Selected = s == model.Stage })
            .ToList();

            return View(model);
        }

        var ipAddress = GetClientIpAddress();
        var (success, error, opportunityId) = await _opportunityService.CreateOpportunityAsync(
            model, userId, fullName, roles, ipAddress);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Failed to create opportunity.");
            model.AvailableCustomers = await _opportunityService.GetAvailableCustomersAsync(userId, roles);
            model.AvailableLeads = await _opportunityService.GetAvailableLeadsAsync(userId, roles);
            if (isPrivileged)
            {
                model.AvailableUsers = await _opportunityService.GetAvailableAssigneesAsync();
            }
            model.AvailableStages = new[]
            {
                OpportunityStage.Qualification,
                OpportunityStage.Proposal,
                OpportunityStage.Negotiation,
                OpportunityStage.Won,
                OpportunityStage.Lost
            }
            .Select(s => new SelectListItem { Value = ((int)s).ToString(), Text = s.ToString(), Selected = s == model.Stage })
            .ToList();

            return View(model);
        }

        TempData["SuccessMessage"] = $"Opportunity '{model.OpportunityName}' created successfully.";
        return RedirectToAction(nameof(Details), new { id = opportunityId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var (userId, _, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _opportunityService.CanUserAccessOpportunityAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var opp = await _opportunityService.GetOpportunityByIdAsync(id);
        if (opp == null)
        {
            return NotFound();
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        var model = new OpportunityEditViewModel
        {
            Id = opp.Id,
            OpportunityName = opp.OpportunityName,
            CustomerId = opp.CustomerId,
            CustomerName = opp.Customer?.CustomerName,
            LeadId = opp.LeadId,
            LeadName = opp.Lead?.LeadName,
            Amount = opp.Amount,
            CurrentStage = opp.Stage,
            Stage = opp.Stage,
            Probability = opp.Probability,
            ExpectedCloseDate = opp.ExpectedCloseDate,
            Status = opp.Status,
            AssignedToUserId = opp.AssignedToUserId,
            Notes = opp.Notes,
            CanReassign = isPrivileged
        };

        model.AvailableCustomers = await _opportunityService.GetAvailableCustomersAsync(userId, roles);
        model.AvailableLeads = await _opportunityService.GetAvailableLeadsAsync(userId, roles);
        if (isPrivileged)
        {
            model.AvailableUsers = await _opportunityService.GetAvailableAssigneesAsync();
        }

        var allowedStages = _opportunityService.GetAllowedNextStages(opp.Stage);
        model.AllowedStages = allowedStages
            .Select(s => new SelectListItem { Value = ((int)s).ToString(), Text = s.ToString(), Selected = s == opp.Stage })
            .ToList();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, OpportunityEditViewModel model)
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

        var canAccess = await _opportunityService.CanUserAccessOpportunityAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var isPrivileged = roles.Contains(DbInitializer.RoleAdmin) || roles.Contains(DbInitializer.RoleManager);

        // Server-Side Validations
        if (string.IsNullOrWhiteSpace(model.OpportunityName))
        {
            ModelState.AddModelError(nameof(model.OpportunityName), "Opportunity Name is required.");
        }

        if (model.CustomerId <= 0)
        {
            ModelState.AddModelError(nameof(model.CustomerId), "Customer is required.");
        }

        if (model.Status == OpportunityStatus.Active && model.Amount <= 0)
        {
            ModelState.AddModelError(nameof(model.Amount), "Amount must be greater than 0.");
        }

        if (model.Probability < 0 || model.Probability > 100)
        {
            ModelState.AddModelError(nameof(model.Probability), "Probability must be between 0 and 100 inclusive.");
        }

        if (model.Status == OpportunityStatus.Active && model.Stage != OpportunityStage.Won && model.Stage != OpportunityStage.Lost && model.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
        {
            ModelState.AddModelError(nameof(model.ExpectedCloseDate), "Expected Close Date cannot be in the past for an active opportunity.");
        }

        if (!isPrivileged)
        {
            var existingOpp = await _opportunityService.GetOpportunityByIdAsync(id);
            if (existingOpp != null)
            {
                model.AssignedToUserId = existingOpp.AssignedToUserId;
            }
        }

        if (!ModelState.IsValid)
        {
            model.AvailableCustomers = await _opportunityService.GetAvailableCustomersAsync(userId, roles);
            model.AvailableLeads = await _opportunityService.GetAvailableLeadsAsync(userId, roles);
            if (isPrivileged)
            {
                model.AvailableUsers = await _opportunityService.GetAvailableAssigneesAsync();
            }
            var allowedStages = _opportunityService.GetAllowedNextStages(model.CurrentStage);
            model.AllowedStages = allowedStages
                .Select(s => new SelectListItem { Value = ((int)s).ToString(), Text = s.ToString(), Selected = s == model.Stage })
                .ToList();

            return View(model);
        }

        var ipAddress = GetClientIpAddress();
        var (success, error) = await _opportunityService.UpdateOpportunityAsync(
            model, userId, fullName, roles, ipAddress);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Failed to update opportunity.");
            model.AvailableCustomers = await _opportunityService.GetAvailableCustomersAsync(userId, roles);
            model.AvailableLeads = await _opportunityService.GetAvailableLeadsAsync(userId, roles);
            if (isPrivileged)
            {
                model.AvailableUsers = await _opportunityService.GetAvailableAssigneesAsync();
            }
            var allowedStages = _opportunityService.GetAllowedNextStages(model.CurrentStage);
            model.AllowedStages = allowedStages
                .Select(s => new SelectListItem { Value = ((int)s).ToString(), Text = s.ToString(), Selected = s == model.Stage })
                .ToList();

            return View(model);
        }

        TempData["SuccessMessage"] = $"Opportunity '{model.OpportunityName}' updated successfully.";
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

        var canAccess = await _opportunityService.CanUserAccessOpportunityAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var opp = await _opportunityService.GetOpportunityByIdAsync(id);
        if (opp == null)
        {
            return NotFound();
        }

        return View(opp);
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

        var canAccess = await _opportunityService.CanUserAccessOpportunityAsync(id, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var ipAddress = GetClientIpAddress();
        var (success, error) = await _opportunityService.DeleteOpportunityAsync(id, userId, roles, ipAddress);

        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Failed to delete/deactivate opportunity.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["SuccessMessage"] = "Opportunity deactivated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStage(OpportunityStageChangeViewModel model)
    {
        var (userId, fullName, roles) = await GetCurrentUserContextAsync();
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var canAccess = await _opportunityService.CanUserAccessOpportunityAsync(model.OpportunityId, userId, roles);
        if (!canAccess)
        {
            return Forbid();
        }

        var ipAddress = GetClientIpAddress();
        var (success, error) = await _opportunityService.ChangeStageAsync(
            model.OpportunityId, model.NewStage, model.Note, userId, fullName, roles, ipAddress);

        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Failed to update opportunity stage.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Opportunity stage advanced to {model.NewStage}.";
        }

        return RedirectToAction(nameof(Details), new { id = model.OpportunityId });
    }
}
