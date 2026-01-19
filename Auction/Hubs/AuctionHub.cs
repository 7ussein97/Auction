using Microsoft.AspNetCore.SignalR;

namespace Auction.Hubs
{
    public class AuctionHub : Hub
    {
        public async Task JoinAuctionGroup(int auctionId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Auction_{auctionId}");
        }

        public async Task LeaveAuctionGroup(int auctionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Auction_{auctionId}");
        }
    }
}


