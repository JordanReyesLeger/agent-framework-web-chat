using Microsoft.AspNetCore.Mvc;

namespace AFWebChat.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();

    public IActionResult Chat() => View();

    public IActionResult Documents() => View();

    public IActionResult Notifications() => View();

    public IActionResult AgentChat() => View();
}
