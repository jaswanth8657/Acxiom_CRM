using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;

namespace AcxiomCRM.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }
        return RedirectToAction("Login", "Account");
    }

    [Authorize]
    public IActionResult Placeholder(string module)
    {
        ViewBag.ModuleName = module ?? "Module";
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode = null)
    {
        var model = new ErrorViewModel
        {
            RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = statusCode ?? HttpContext.Response.StatusCode
        };
        return View(model);
    }
}
