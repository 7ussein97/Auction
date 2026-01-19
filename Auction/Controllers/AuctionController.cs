using Auction.Models;
using Auction.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Auction.Controllers
{
    public class AuctionController : Controller
    {
        private readonly AuctionDbContext _context;
        private readonly ILogger<AuctionController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IHubContext<AuctionHub> _hubContext;

        public AuctionController(AuctionDbContext context, ILogger<AuctionController> logger, IWebHostEnvironment environment, IHubContext<AuctionHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
            _hubContext = hubContext;
        }

        // GET: Auction
        public async Task<IActionResult> Index()
        {
            var auctions = await _context.AuctionItems
                .Include(a => a.Bids)
                .Include(a => a.Images)
                .Include(a => a.CreatedByUser)
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();

            return View(auctions);
        }

        // GET: Auction/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var auctionItem = await _context.AuctionItems.FindAsync(id);
            if (auctionItem == null)
            {
                return NotFound();
            }

            return View(auctionItem);
        }

        // POST: Auction/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, AuctionItem auctionItem, IFormFile? mainImage, List<IFormFile>? additionalImages)
        {
            if (id != auctionItem.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingAuction = await _context.AuctionItems
                        .Include(a => a.Images)
                        .FirstOrDefaultAsync(a => a.Id == id);

                    if (existingAuction == null)
                    {
                        return NotFound();
                    }

                    // Update properties
                    existingAuction.Name = auctionItem.Name;
                    existingAuction.Description = auctionItem.Description;
                    existingAuction.MinimumPrice = auctionItem.MinimumPrice;
                    existingAuction.StartTime = auctionItem.StartTime;
                    existingAuction.EndTime = auctionItem.EndTime;
                    existingAuction.IsActive = auctionItem.IsActive;

                    // Handle main image upload
                    if (mainImage != null && mainImage.Length > 0)
                    {
                        var imagePath = await SaveImage(mainImage, "auctions");
                        existingAuction.ImagePath = imagePath;
                    }

                    // Handle additional images
                    if (additionalImages != null && additionalImages.Count > 0)
                    {
                        foreach (var image in additionalImages)
                        {
                            if (image.Length > 0)
                            {
                                var imagePath = await SaveImage(image, "auctions");
                                var auctionImage = new AuctionImage
                                {
                                    AuctionItemId = existingAuction.Id,
                                    ImagePath = imagePath
                                };
                                _context.AuctionImages.Add(auctionImage);
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Auction updated successfully!";
                    return RedirectToAction("Details", new { id = existingAuction.Id });
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error updating auction: {ex.Message}");
                    TempData["ExceptionMessage"] = "An error occurred while updating the auction.";
                }
            }

            return View(auctionItem);
        }

        // POST: Auction/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var auctionItem = await _context.AuctionItems
                    .Include(a => a.Bids)
                    .Include(a => a.Images)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (auctionItem == null)
                {
                    TempData["ExceptionMessage"] = "Auction not found.";
                    return RedirectToAction("Index");
                }

                // Delete related bids
                _context.Bids.RemoveRange(auctionItem.Bids);
                
                // Delete related images
                _context.AuctionImages.RemoveRange(auctionItem.Images);
                
                // Delete the auction
                _context.AuctionItems.Remove(auctionItem);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Auction deleted successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting auction: {ex.Message}");
                TempData["ExceptionMessage"] = "An error occurred while deleting the auction.";
                return RedirectToAction("Details", new { id });
            }
        }

        // GET: Auction/Create
        [Authorize(Roles = "Admin,AuctionCreator")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Auction/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,AuctionCreator")]
        public async Task<IActionResult> Create(AuctionItem auctionItem, IFormFile? mainImage, List<IFormFile>? additionalImages)
        {
            // Validate required fields manually
            if (string.IsNullOrWhiteSpace(auctionItem.Name))
            {
                ModelState.AddModelError(nameof(auctionItem.Name), "Item name is required.");
            }

            if (auctionItem.EndTime <= auctionItem.StartTime)
            {
                ModelState.AddModelError(nameof(auctionItem.EndTime), "End time must be after start time.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                    if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                    {
                        TempData["ExceptionMessage"] = "User not authenticated.";
                        return RedirectToAction("Index", "Home");
                    }

                    // Ensure Name is not null
                    if (string.IsNullOrWhiteSpace(auctionItem.Name))
                    {
                        ModelState.AddModelError(nameof(auctionItem.Name), "Item name is required.");
                        return View(auctionItem);
                    }

                    // Verify user exists
                    var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                    if (!userExists)
                    {
                        TempData["ExceptionMessage"] = "User not found.";
                        return RedirectToAction("Index", "Home");
                    }

                    // Create a new AuctionItem instance to avoid navigation property issues
                    var newAuctionItem = new AuctionItem
                    {
                        Name = auctionItem.Name,
                        Description = auctionItem.Description,
                        MinimumPrice = auctionItem.MinimumPrice,
                        StartTime = auctionItem.StartTime,
                        EndTime = auctionItem.EndTime,
                        CreatedByUserId = userId, // Set foreign key only
                        IsActive = true
                    };

                    // Handle main image upload
                    if (mainImage != null && mainImage.Length > 0)
                    {
                        var imagePath = await SaveImage(mainImage, "auctions");
                        newAuctionItem.ImagePath = imagePath;
                    }

                    _context.AuctionItems.Add(newAuctionItem);
                    await _context.SaveChangesAsync();

                    // Handle additional images
                    if (additionalImages != null && additionalImages.Count > 0)
                    {
                        foreach (var image in additionalImages)
                        {
                            if (image.Length > 0)
                            {
                                var imagePath = await SaveImage(image, "auctions");
                                var auctionImage = new AuctionImage
                                {
                                    AuctionItemId = newAuctionItem.Id,
                                    ImagePath = imagePath
                                };
                                _context.AuctionImages.Add(auctionImage);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    TempData["SuccessMessage"] = "Auction created successfully!";
                    return RedirectToAction("Details", new { id = newAuctionItem.Id });
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError($"Database error creating auction: {dbEx.Message}");
                    TempData["ExceptionMessage"] = "An error occurred while saving the auction. Please check your input.";
                    ModelState.AddModelError("", "Database error: " + dbEx.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error creating auction: {ex.Message}");
                    _logger.LogError($"Stack trace: {ex.StackTrace}");
                    TempData["ExceptionMessage"] = $"An error occurred while creating the auction: {ex.Message}";
                    ModelState.AddModelError("", "Error: " + ex.Message);
                }
            }
            else
            {
                // Log validation errors
                foreach (var error in ModelState)
                {
                    foreach (var errorMessage in error.Value.Errors)
                    {
                        _logger.LogWarning($"Validation error for {error.Key}: {errorMessage.ErrorMessage}");
                    }
                }
            }

            return View(auctionItem);
        }

        // GET: Auction/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var auctionItem = await _context.AuctionItems
                .Include(a => a.Bids)
                    .ThenInclude(b => b.User)
                .Include(a => a.Images)
                .Include(a => a.CreatedByUser)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (auctionItem == null)
            {
                return NotFound();
            }

            return View(auctionItem);
        }

        // POST: Auction/PlaceBid (AJAX endpoint)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Bidder,Admin")]
        public async Task<IActionResult> PlaceBid(int auctionItemId, decimal bidAmount)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Json(new { success = false, message = "User not authenticated." });
                }

                var auctionItem = await _context.AuctionItems
                    .Include(a => a.Bids)
                    .FirstOrDefaultAsync(a => a.Id == auctionItemId);

                if (auctionItem == null)
                {
                    return Json(new { success = false, message = "Auction not found." });
                }

                // Validate auction is active
                if (!auctionItem.IsActive || DateTime.Now < auctionItem.StartTime || DateTime.Now > auctionItem.EndTime)
                {
                    return Json(new { success = false, message = "This auction is not currently active." });
                }

                // Get current highest bid or minimum price
                var highestBid = auctionItem.Bids?.OrderByDescending(b => b.Amount).FirstOrDefault();
                var minimumBid = highestBid?.Amount ?? auctionItem.MinimumPrice ?? 0;

                // Validate bid amount
                if (bidAmount <= minimumBid)
                {
                    return Json(new { success = false, message = $"Your bid must be higher than {minimumBid:N2} OMR." });
                }

                // Create new bid
                var bid = new Bid
                {
                    AuctionItemId = auctionItemId,
                    UserId = userId,
                    Amount = bidAmount,
                    CreatedAt = DateTime.Now
                };

                _context.Bids.Add(bid);
                await _context.SaveChangesAsync();

                // Reload auction with updated bids for response
                var updatedAuction = await _context.AuctionItems
                    .Include(a => a.Bids)
                        .ThenInclude(b => b.User)
                    .FirstOrDefaultAsync(a => a.Id == auctionItemId);

                var newHighestBid = updatedAuction?.Bids?.OrderByDescending(b => b.Amount).FirstOrDefault();
                var newCurrentBid = newHighestBid?.Amount ?? updatedAuction?.MinimumPrice ?? 0;

                // Return updated bid data
                var bidsList = updatedAuction?.Bids?.OrderByDescending(b => b.Amount).ToList() ?? new List<Bid>();
                var bidsData = bidsList.Select(b => new
                {
                    id = b.Id,
                    bidderName = b.User?.Name ?? "Unknown",
                    amount = b.Amount.ToString("N2"),
                    time = b.CreatedAt.ToString("MMM dd, yyyy HH:mm"),
                    isHighest = b.Id == newHighestBid?.Id
                }).ToList();

                // Broadcast the bid update to all clients viewing this auction
                await _hubContext.Clients.Group($"Auction_{auctionItemId}").SendAsync("BidPlaced", new
                {
                    auctionId = auctionItemId,
                    currentBid = newCurrentBid.ToString("N2"),
                    bids = bidsData,
                    minimumNextBid = (newCurrentBid + 0.01m).ToString("N2"),
                    bidderName = updatedAuction?.Bids?.FirstOrDefault(b => b.Id == bid.Id)?.User?.Name ?? "Unknown",
                    bidAmount = bidAmount.ToString("N2")
                });

                return Json(new
                {
                    success = true,
                    message = "Your bid has been placed successfully!",
                    currentBid = newCurrentBid.ToString("N2"),
                    bids = bidsData,
                    minimumNextBid = (newCurrentBid + 0.01m).ToString("N2")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error placing bid: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while placing your bid." });
            }
        }

        // POST: Auction/DeleteBid
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,AuctionCreator")]
        public async Task<IActionResult> DeleteBid(int bidId, int auctionItemId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    TempData["ExceptionMessage"] = "User not authenticated.";
                    return RedirectToAction("Details", new { id = auctionItemId });
                }

                var bid = await _context.Bids
                    .Include(b => b.AuctionItem)
                    .FirstOrDefaultAsync(b => b.Id == bidId);

                if (bid == null)
                {
                    TempData["ExceptionMessage"] = "Bid not found.";
                    return RedirectToAction("Details", new { id = auctionItemId });
                }

                // Check permissions: Admin can delete any bid, Creator can only delete bids from their own auctions
                var isAdmin = User.IsInRole("Admin");
                var isCreator = User.IsInRole("AuctionCreator") && bid.AuctionItem.CreatedByUserId == userId;

                if (!isAdmin && !isCreator)
                {
                    TempData["ExceptionMessage"] = "You don't have permission to delete this bid.";
                    return RedirectToAction("Details", new { id = auctionItemId });
                }

                _context.Bids.Remove(bid);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Bid deleted successfully.";
                return RedirectToAction("Details", new { id = auctionItemId });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting bid: {ex.Message}");
                TempData["ExceptionMessage"] = "An error occurred while deleting the bid.";
                return RedirectToAction("Details", new { id = auctionItemId });
            }
        }

        // POST: Auction/EndAuction
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,AuctionCreator")]
        public async Task<IActionResult> EndAuction(int auctionItemId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    TempData["ExceptionMessage"] = "User not authenticated.";
                    return RedirectToAction("Details", new { id = auctionItemId });
                }

                var auctionItem = await _context.AuctionItems
                    .FirstOrDefaultAsync(a => a.Id == auctionItemId);

                if (auctionItem == null)
                {
                    TempData["ExceptionMessage"] = "Auction not found.";
                    return RedirectToAction("Index");
                }

                // Check permissions: Admin can end any auction, Creator can only end their own auctions
                var isAdmin = User.IsInRole("Admin");
                var isCreator = User.IsInRole("AuctionCreator") && auctionItem.CreatedByUserId == userId;

                if (!isAdmin && !isCreator)
                {
                    TempData["ExceptionMessage"] = "You don't have permission to end this auction.";
                    return RedirectToAction("Details", new { id = auctionItemId });
                }

                // End the auction by setting EndTime to now and IsActive to false
                auctionItem.EndTime = DateTime.Now;
                auctionItem.IsActive = false;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Auction ended successfully.";
                return RedirectToAction("Details", new { id = auctionItemId });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error ending auction: {ex.Message}");
                TempData["ExceptionMessage"] = "An error occurred while ending the auction.";
                return RedirectToAction("Details", new { id = auctionItemId });
            }
        }

        // GET: Auction/Winner/{id}
        [Authorize(Roles = "Admin,AuctionCreator")]
        public async Task<IActionResult> Winner(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                TempData["ExceptionMessage"] = "User not authenticated.";
                return RedirectToAction("Index");
            }

            var auctionItem = await _context.AuctionItems
                .Include(a => a.Bids)
                    .ThenInclude(b => b.User)
                .Include(a => a.CreatedByUser)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (auctionItem == null)
            {
                return NotFound();
            }

            // Check permissions: Admin can view any winner, Creator can only view winners of their own auctions
            var isAdmin = User.IsInRole("Admin");
            var isCreator = User.IsInRole("AuctionCreator") && auctionItem.CreatedByUserId == userId;

            if (!isAdmin && !isCreator)
            {
                TempData["ExceptionMessage"] = "You don't have permission to view this auction's winner.";
                return RedirectToAction("Index");
            }

            var winnerBid = auctionItem.Bids?.OrderByDescending(b => b.Amount).FirstOrDefault();

            ViewBag.WinnerBid = winnerBid;
            ViewBag.AuctionItem = auctionItem;

            return View(auctionItem);
        }

        // POST: Auction/CloseCase
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,AuctionCreator")]
        public async Task<IActionResult> CloseCase(int auctionItemId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    TempData["ExceptionMessage"] = "User not authenticated.";
                    return RedirectToAction("Details", new { id = auctionItemId });
                }

                var auctionItem = await _context.AuctionItems
                    .FirstOrDefaultAsync(a => a.Id == auctionItemId);

                if (auctionItem == null)
                {
                    TempData["ExceptionMessage"] = "Auction not found.";
                    return RedirectToAction("Index");
                }

                // Check permissions: Admin can close any case, Creator can only close their own cases
                var isAdmin = User.IsInRole("Admin");
                var isCreator = User.IsInRole("AuctionCreator") && auctionItem.CreatedByUserId == userId;

                if (!isAdmin && !isCreator)
                {
                    TempData["ExceptionMessage"] = "You don't have permission to close this case.";
                    return RedirectToAction("Details", new { id = auctionItemId });
                }

                // Close the case by ensuring auction is ended and inactive
                auctionItem.EndTime = DateTime.Now;
                auctionItem.IsActive = false;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Case closed successfully.";
                return RedirectToAction("Winner", new { id = auctionItemId });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error closing case: {ex.Message}");
                TempData["ExceptionMessage"] = "An error occurred while closing the case.";
                return RedirectToAction("Details", new { id = auctionItemId });
            }
        }

        private async Task<string> SaveImage(IFormFile file, string folder)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", folder);
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/images/{folder}/{uniqueFileName}";
        }
    }
}

