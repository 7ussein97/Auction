using Microsoft.AspNetCore.Mvc;

namespace Auction.Controllers
{
    public class PageController : Controller
    {
        public IActionResult Forbidden()
        {
            return View();
        }
        public IActionResult Notfound()
        {
            return View();
        }
        public IActionResult LayoutNotFound()
        {
            return View();
        }
    }
}
