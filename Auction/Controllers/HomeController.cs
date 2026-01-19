using Auction.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Auction.Controllers
{
    public class HomeController : Controller
    {
        private readonly AuctionDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(AuctionDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var activeAuctions = await _context.AuctionItems
                .Include(a => a.Bids)
                .Include(a => a.Images)
                .Include(a => a.CreatedByUser)
                .Where(a => a.IsActive && a.StartTime <= DateTime.Now && a.EndTime >= DateTime.Now)
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();

            return View(activeAuctions);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
