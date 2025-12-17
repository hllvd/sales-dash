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
        public DbSet<Role> Roles { get; set; }
        public DbSet<ImportTemplate> ImportTemplates { get; set; }
        public DbSet<ImportSession> ImportSessions { get; set; }
        public DbSet<ImportColumnMapping> ImportColumnMappings { get; set; }
        public DbSet<ImportUserMapping> ImportUserMappings { get; set; }
        public DbSet<PV> PVs { get; set; }
        public DbSet<UserMatricula> UserMatriculas { get; set; }
        
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
                entity.Property(e => e.RoleId).HasDefaultValue(3);
                
                entity.HasOne(e => e.Role)
                    .WithMany(r => r.Users)
                    .HasForeignKey(e => e.RoleId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                
                // Self-referencing relationship
                entity.HasOne(e => e.ParentUser)
                    .WithMany(e => e.ChildUsers)
                    .HasForeignKey(e => e.ParentUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(e => e.Matricula).IsRequired(false);
                entity.Property(e => e.IsMatriculaOwner).HasDefaultValue(false);
            });
            
            // Group entity configuration
            modelBuilder.Entity<Group>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Commission).HasColumnType("decimal(5,2)");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });
            
            // Role entity configuration
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });
            
            // Contract entity configuration
            modelBuilder.Entity<Contract>(entity =>
            {
                entity.ToTable("Contracts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.HasIndex(e => e.ContractNumber).IsUnique();
                entity.Property(e => e.ContractNumber).IsRequired();
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
                    
                entity.HasOne(e => e.PV)
                    .WithMany(p => p.Contracts)
                    .HasForeignKey(e => e.PvId)
                    .OnDelete(DeleteBehavior.Restrict);
                    
                entity.HasOne(e => e.UserMatricula)
                    .WithMany(m => m.Contracts)
                    .HasForeignKey(e => e.MatriculaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            
            // PV entity configuration
            modelBuilder.Entity<PV>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever(); // NOT auto-increment
                entity.Property(e => e.Name).IsRequired().HasColumnType("text");
            });
            
            // ImportTemplate entity configuration
            modelBuilder.Entity<ImportTemplate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.EntityType).IsRequired();
                entity.Property(e => e.RequiredFields).IsRequired();
                entity.Property(e => e.OptionalFields).IsRequired();
                entity.Property(e => e.DefaultMappings).IsRequired();
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                
                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            
            // ImportSession entity configuration
            modelBuilder.Entity<ImportSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.HasIndex(e => e.UploadId).IsUnique();
                entity.Property(e => e.UploadId).IsRequired();
                entity.Property(e => e.FileName).IsRequired();
                entity.Property(e => e.FileType).IsRequired();
                entity.Property(e => e.Status).HasDefaultValue("preview");
                
                entity.HasOne(e => e.Template)
                    .WithMany()
                    .HasForeignKey(e => e.TemplateId)
                    .OnDelete(DeleteBehavior.Restrict);
                    
                entity.HasOne(e => e.UploadedBy)
                    .WithMany()
                    .HasForeignKey(e => e.UploadedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            
            // ImportColumnMapping entity configuration
            modelBuilder.Entity<ImportColumnMapping>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.MappingName).IsRequired();
                entity.Property(e => e.FileType).IsRequired();
                entity.Property(e => e.SourceColumn).IsRequired();
                entity.Property(e => e.TargetField).IsRequired();
                
                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            
            // ImportUserMapping entity configuration
            modelBuilder.Entity<ImportUserMapping>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.SourceName).IsRequired();
                entity.Property(e => e.SourceSurname).IsRequired();
                entity.Property(e => e.Action).HasDefaultValue("pending");
                
                entity.HasOne(e => e.ImportSession)
                    .WithMany()
                    .HasForeignKey(e => e.ImportSessionId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasOne(e => e.ResolvedUser)
                    .WithMany()
                    .HasForeignKey(e => e.ResolvedUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            
            // UserMatricula entity configuration
            modelBuilder.Entity<UserMatricula>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.MatriculaNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.StartDate).IsRequired();
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                
                // Index for faster lookups
                entity.HasIndex(e => e.MatriculaNumber);
                entity.HasIndex(e => new { e.UserId, e.MatriculaNumber });
                
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}