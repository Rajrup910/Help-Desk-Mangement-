using System.Diagnostics;
using HelpDesk.Mvc.Models;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Mvc.Controllers;

public class HomeController : Controller
{
    // The dashboard is the landing page; there is no separate marketing home.
    public IActionResult Index() => RedirectToAction("Dashboard", "Ticket");

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
    });
}
