using Microsoft.EntityFrameworkCore;
using SalesApp.Models;

namespace SalesApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        
        public DbSet<User> Users { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<Contract> Contracts { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // User entity configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Email).IsRequired();
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.Role).HasDefaultValue(UserRole.User);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                
                // Self-referencing relationship
                entity.HasOne(e => e.ParentUser)
                    .WithMany(e => e.ChildUsers)
                    .HasForeignKey(e => e.ParentUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            
            // Group entity configuration
            modelBuilder.Entity<Group>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Commission).HasColumnType("decimal(5,2)");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });
            
            // Contract entity configuration (maps to Sales table)
            modelBuilder.Entity<Contract>(entity =>
            {
                entity.ToTable("Sales");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Status).HasDefaultValue("active");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                    
                entity.HasOne(e => e.Group)
                    .WithMany()
                    .HasForeignKey(e => e.GroupId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}