using Microsoft.EntityFrameworkCore;

namespace Auction.Models
{
    public class AuctionDbContext : DbContext
    {
        public AuctionDbContext(DbContextOptions<AuctionDbContext> options)
            : base(options)
        {
        }

        // Tables
        public DbSet<User> Users { get; set; }
        public DbSet<AuctionItem> AuctionItems { get; set; }
        public DbSet<Bid> Bids { get; set; }
        public DbSet<AuctionImage> AuctionImages { get; set; } // optional

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User → AuctionItem (creator)
            modelBuilder.Entity<AuctionItem>()
                .HasOne(a => a.CreatedByUser)
                .WithMany()
                .HasForeignKey(a => a.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // AuctionItem → Bids
            modelBuilder.Entity<Bid>()
                .HasOne(b => b.AuctionItem)
                .WithMany(a => a.Bids)
                .HasForeignKey(b => b.AuctionItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // User → Bids
            modelBuilder.Entity<Bid>()
                .HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // AuctionItem → Images (optional)
            modelBuilder.Entity<AuctionImage>()
                .HasOne(i => i.AuctionItem)
                .WithMany(a => a.Images)
                .HasForeignKey(i => i.AuctionItemId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
