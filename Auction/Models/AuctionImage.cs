namespace Auction.Models
{
    public class AuctionImage
    {
        public int Id { get; set; }

        public string ImagePath { get; set; }

        public int AuctionItemId { get; set; }

        public AuctionItem AuctionItem { get; set; }
    }
}
