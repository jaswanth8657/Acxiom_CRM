using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels.Dashboard;

namespace AcxiomCRM.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(
        IDashboardService dashboardService,
        UserManager<ApplicationUser> userManager)
    {
        _dashboardService = dashboardService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] DashboardFilterViewModel filter)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        var roles = await _userManager.GetRolesAsync(currentUser);

        ViewBag.FullName = currentUser.FullName ?? User.Identity?.Name ?? "User";
        ViewBag.Roles = roles;
        ViewBag.UserEmail = currentUser.Email ?? "";
        ViewBag.PrimaryRole = roles.FirstOrDefault() ?? "SalesExecutive";

        var viewModel = await _dashboardService.GetDashboardMetricsAsync(filter, currentUser.Id, roles);
        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> GetLeadStatusChartData(DateTime? startDate, DateTime? endDate)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var data = await _dashboardService.GetLeadStatusChartDataAsync(currentUser.Id, roles, startDate, endDate);
        return Json(data);
    }

    [HttpGet]
    public async Task<IActionResult> GetOpportunityPipelineChartData(DateTime? startDate, DateTime? endDate)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var data = await _dashboardService.GetOpportunityPipelineChartDataAsync(currentUser.Id, roles, startDate, endDate);
        return Json(data);
    }

    [HttpGet]
    public async Task<IActionResult> GetMonthlySalesChartData()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var data = await _dashboardService.GetMonthlySalesChartDataAsync(currentUser.Id, roles);
        return Json(data);
    }
}
