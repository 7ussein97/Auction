using System.ComponentModel.DataAnnotations;

namespace Auction.Models
{
    public class AuctionItem
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Item name is required.")]
        public string? Name { get; set; }

        public string? Description { get; set; }

        // Optional: main image
        public string? ImagePath { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Invalid minimum price.")]
        public decimal? MinimumPrice { get; set; }

        [Required]
        public int CreatedByUserId { get; set; }
        public User? CreatedByUser { get; set; }

        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime EndTime { get; set; }
        public bool IsActive { get; set; } = true;

        // **Add this line for multiple images**
        public List<AuctionImage> Images { get; set; } = new List<AuctionImage>();

        // List of bids
        public List<Bid> Bids { get; set; } = new List<Bid>();
    }
}
