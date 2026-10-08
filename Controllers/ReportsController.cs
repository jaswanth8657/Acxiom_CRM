using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels.Reports;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly IReportService _reportService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportsController(
        IReportService reportService,
        UserManager<ApplicationUser> userManager)
    {
        _reportService = reportService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        ViewBag.IsAdmin = roles.Contains(DbInitializer.RoleAdmin);
        ViewBag.IsManager = roles.Contains(DbInitializer.RoleManager);
        ViewBag.Roles = roles;

        return View();
    }

    #region 1. Customer Report
    [HttpGet]
    public async Task<IActionResult> Customers([FromQuery] CustomerReportViewModel filter)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var report = await _reportService.GetCustomerReportAsync(filter, currentUser.Id, roles);
        return View(report);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCustomers([FromQuery] CustomerReportViewModel filter)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var bytes = await _reportService.ExportCustomerReportCsvAsync(filter, currentUser.Id, roles);
        return File(bytes, "text/csv", $"Customer_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }
    #endregion

    #region 2. Lead Report
    [HttpGet]
    public async Task<IActionResult> Leads([FromQuery] LeadReportViewModel filter)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var report = await _reportService.GetLeadReportAsync(filter, currentUser.Id, roles);
        return View(report);
    }

    [HttpGet]
    public async Task<IActionResult> ExportLeads([FromQuery] LeadReportViewModel filter)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var bytes = await _reportService.ExportLeadReportCsvAsync(filter, currentUser.Id, roles);
        return File(bytes, "text/csv", $"Lead_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }
    #endregion

    #region 3. Follow-Up Report
    [HttpGet]
    public async Task<IActionResult> FollowUps([FromQuery] FollowUpReportViewModel filter)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var report = await _reportService.GetFollowUpReportAsync(filter, currentUser.Id, roles);
        return View(report);
    }

    [HttpGet]
    public async Task<IActionResult> ExportFollowUps([FromQuery] FollowUpReportViewModel filter)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var bytes = await _reportService.ExportFollowUpReportCsvAsync(filter, currentUser.Id, roles);
        return File(bytes, "text/csv", $"FollowUp_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }
    #endregion

    #region 4. Opportunity Report
    [HttpGet]
    public async Task<IActionResult> Opportunities([FromQuery] OpportunityReportViewModel filter)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var report = await _reportService.GetOpportunityReportAsync(filter, currentUser.Id, roles);
        return View(report);
    }

    [HttpGet]
    public async Task<IActionResult> ExportOpportunities([FromQuery] OpportunityReportViewModel filter)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var bytes = await _reportService.ExportOpportunityReportCsvAsync(filter, currentUser.Id, roles);
        return File(bytes, "text/csv", $"Opportunity_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }
    #endregion

    #region 5. Pipeline Report
    [HttpGet]
    public async Task<IActionResult> Pipeline()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var report = await _reportService.GetPipelineReportAsync(currentUser.Id, roles);
        return View(report);
    }

    [HttpGet]
    public async Task<IActionResult> ExportPipeline()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var bytes = await _reportService.ExportPipelineReportCsvAsync(currentUser.Id, roles);
        return File(bytes, "text/csv", $"Pipeline_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }
    #endregion

    #region 6. Conversion Report
    [HttpGet]
    public async Task<IActionResult> Conversion()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var report = await _reportService.GetConversionReportAsync(currentUser.Id, roles);
        return View(report);
    }

    [HttpGet]
    public async Task<IActionResult> ExportConversion()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var bytes = await _reportService.ExportConversionReportCsvAsync(currentUser.Id, roles);
        return File(bytes, "text/csv", $"Conversion_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }
    #endregion

    #region 7. User Activity Report
    [HttpGet]
    public async Task<IActionResult> UserActivity([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var report = await _reportService.GetUserActivityReportAsync(startDate, endDate, currentUser.Id, roles);
        return View(report);
    }

    [HttpGet]
    public async Task<IActionResult> ExportUserActivity([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var bytes = await _reportService.ExportUserActivityReportCsvAsync(startDate, endDate, currentUser.Id, roles);
        return File(bytes, "text/csv", $"UserActivity_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }
    #endregion

    #region 8. Audit Report (Admin Only)
    [HttpGet]
    [Authorize(Roles = DbInitializer.RoleAdmin)]
    public async Task<IActionResult> Audit([FromQuery] AuditReportViewModel filter)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var report = await _reportService.GetAuditReportAsync(filter, currentUser.Id, roles);
        return View(report);
    }

    [HttpGet]
    [Authorize(Roles = DbInitializer.RoleAdmin)]
    public async Task<IActionResult> ExportAudit([FromQuery] AuditReportViewModel filter)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var bytes = await _reportService.ExportAuditReportCsvAsync(filter, currentUser.Id, roles);
        return File(bytes, "text/csv", $"Audit_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }
    #endregion
}
