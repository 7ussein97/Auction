using System.ComponentModel.DataAnnotations;

namespace Auction.Models
{
    public class Bid
    {
        public int Id { get; set; }

        [Required]
        public int AuctionItemId { get; set; }

        public AuctionItem AuctionItem { get; set; }

        // User who placed the bid
        [Required]
        public int UserId { get; set; }

        public User User { get; set; }

        // Price the user bid
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Bid must be greater than zero.")]
        public decimal Amount { get; set; }

        // Highest bid wins at the end
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
