using Cylo_Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Cylo_Backend.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Notification> Notifications { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Profile> Profiles { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Follow> Follows { get; set; }
        public DbSet<Comments> Comments { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Like> Likes { get; set; }
        public DbSet<Bookmark> Bookmarks { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<CommentLike> CommentLikes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 🛡️ GLOBAL QUERY FILTERS (Safely null-checked for new listing creation)
            modelBuilder.Entity<Post>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<Comments>().HasQueryFilter(c => c.Post == null || !c.Post.IsDeleted);
            modelBuilder.Entity<Like>().HasQueryFilter(l => l.Post == null || !l.Post.IsDeleted);
            modelBuilder.Entity<Bookmark>().HasQueryFilter(b => b.Post == null || !b.Post.IsDeleted);

            // 🏷️ Seed Categories
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Electronics" },
                new Category { Id = 2, Name = "Clothing" },
                new Category { Id = 3, Name = "Home" }
            );

            modelBuilder.Entity<Follow>()
                .HasOne(f => f.Follower)
                .WithMany(u => u.Following)
                .HasForeignKey(f => f.FollowerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Follow>()
                .HasOne(f => f.Following)
                .WithMany(u => u.Followers)
                .HasForeignKey(f => f.FollowingId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Like>()
                .HasOne(l => l.User)
                .WithMany(u => u.Likes)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Order>()
                .Property(o => o.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Buyer)
                .WithMany()
                .HasForeignKey(o => o.BuyerId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Seller)
                .WithMany()
                .HasForeignKey(o => o.SellerId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Post)
                .WithMany()
                .HasForeignKey(o => o.PostId);

            modelBuilder.Entity<Post>()
                .HasOne(p => p.User)
                .WithMany(u => u.Posts)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Post>()
                .HasMany(p => p.Tags)
                .WithMany(t => t.Posts)
                .UsingEntity(j => j.ToTable("PostTags"));

            modelBuilder.Entity<Comments>()
                 .HasOne(c => c.Post)
                 .WithMany(p => p.Comments)
                 .HasForeignKey(c => c.PostId)
                 .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Comments>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Bookmark>()
                .HasIndex(b => new { b.UserId, b.PostId })
                .IsUnique();
        }
    }
}