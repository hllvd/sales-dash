using Microsoft.EntityFrameworkCore;
using SalesApp.Models;
using System.Security.Claims;
using System.Text.Json;

namespace SalesApp.Data
{
    public class AppDbContext : DbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor httpContextAccessor) 
            : base(options) 
        { 
            _httpContextAccessor = httpContextAccessor;
        }
        
        public DbSet<User> Users { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<Contract> Contracts { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<ImportTemplate> ImportTemplates { get; set; }
        public DbSet<ImportSession> ImportSessions { get; set; }
        public DbSet<ImportColumnMapping> ImportColumnMappings { get; set; }
        public DbSet<PV> PVs { get; set; }
        public DbSet<UserMatricula> UserMatriculas { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<ContractMetadata> ContractMetadata { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        
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
                
                entity.HasOne(e => e.ParentUser)
                    .WithMany(e => e.ChildUsers)
                    .HasForeignKey(e => e.ParentUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ImportSession)
                    .WithMany()
                    .HasForeignKey(e => e.ImportSessionId)
                    .OnDelete(DeleteBehavior.Restrict);
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

                entity.HasOne(e => e.ImportSession)
                    .WithMany()
                    .HasForeignKey(e => e.ImportSessionId)
                    .OnDelete(DeleteBehavior.Restrict);
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
                    
                entity.HasOne(e => e.PlanoVendaMetadata)
                    .WithMany(m => m.ContractsWithPlanoVenda)
                    .HasForeignKey(e => e.PlanoVendaMetadataId)
                    .OnDelete(DeleteBehavior.Restrict);
                    
                entity.HasOne(e => e.CategoryMetadata)
                    .WithMany(m => m.ContractsWithCategory)
                    .HasForeignKey(e => e.CategoryMetadataId)
                    .OnDelete(DeleteBehavior.Restrict);
                    
                entity.HasOne(e => e.UserMatricula)
                    .WithMany()
                    .HasForeignKey(e => e.UserMatriculaId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ImportSession)
                    .WithMany()
                    .HasForeignKey(e => e.ImportSessionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            
            // PV entity configuration
            modelBuilder.Entity<PV>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever(); // NOT auto-increment
                entity.Property(e => e.Name).IsRequired().HasColumnType("text");

                entity.HasOne(e => e.ImportSession)
                    .WithMany()
                    .HasForeignKey(e => e.ImportSessionId)
                    .OnDelete(DeleteBehavior.Restrict);
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
            
            
            // UserMatricula entity configuration
            modelBuilder.Entity<UserMatricula>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.MatriculaNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.StartDate).IsRequired();
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("active");
                
                // Index for faster lookups - Unique per user
                entity.HasIndex(e => e.MatriculaNumber);
                entity.HasIndex(e => new { e.UserId, e.MatriculaNumber }).IsUnique();
                
                entity.HasOne(e => e.User)
                    .WithMany(u => u.UserMatriculas)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ImportSession)
                    .WithMany()
                    .HasForeignKey(e => e.ImportSessionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            
            // RefreshToken entity configuration
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
                entity.Property(e => e.ExpiresAt).IsRequired();
                entity.Property(e => e.IsRevoked).HasDefaultValue(false);
                
                // Index for faster token lookups
                entity.HasIndex(e => e.Token);
                entity.HasIndex(e => new { e.UserId, e.IsRevoked });
                
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            
            // ContractMetadata entity configuration
            modelBuilder.Entity<ContractMetadata>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Value).IsRequired().HasMaxLength(100);
            });
                
            // Permission entity configuration
            modelBuilder.Entity<Permission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            });

            // RolePermission (Many-to-Many join) configuration
            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });

                entity.HasOne(rp => rp.Role)
                    .WithMany(r => r.RolePermissions)
                    .HasForeignKey(rp => rp.RoleId);

                entity.HasOne(rp => rp.Permission)
                    .WithMany(p => p.RolePermissions)
                    .HasForeignKey(rp => rp.PermissionId);
            });

            // AuditLog entity configuration
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Action).IsRequired().HasMaxLength(20);
                entity.Property(e => e.EntityName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.EntityId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.UserId).IsRequired();
                
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var auditEntries = OnBeforeSaveChanges();
            var result = await base.SaveChangesAsync(cancellationToken);
            await OnAfterSaveChanges(auditEntries);
            return result;
        }

        private List<AuditEntry> OnBeforeSaveChanges()
        {
            ChangeTracker.DetectChanges();
            var auditEntries = new List<AuditEntry>();

            // Get current User ID from claims
            var userIdString = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid userId = Guid.Empty;
            if (Guid.TryParse(userIdString, out var parsedGuid))
            {
                userId = parsedGuid;
            }

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var entityName = entry.Entity.GetType().Name;
                if (entityName.EndsWith("Proxy")) entityName = entry.Entity.GetType().BaseType!.Name;

                var auditableEntities = new[] { "User", "Contract", "UserMatricula", "Group", "PV" };
                if (!auditableEntities.Contains(entityName))
                    continue;

                var auditEntry = new AuditEntry(entry)
                {
                    EntityName = entityName,
                    UserId = userId,
                };
                auditEntries.Add(auditEntry);

                foreach (var property in entry.Properties)
                {
                    string propertyName = property.Metadata.Name;
                    var skipFields = new[] { "PasswordHash", "CreatedAt", "UpdatedAt", "Id", "UserId" };
                    if (skipFields.Contains(propertyName)) continue;

                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[propertyName] = property.CurrentValue!;
                        continue;
                    }

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.AuditType = "Create";
                            if (property.CurrentValue != null) auditEntry.NewValues[propertyName] = property.CurrentValue;
                            if (property.IsTemporary) auditEntry.TemporaryProperties.Add(property);
                            break;

                        case EntityState.Deleted:
                            auditEntry.AuditType = "Delete";
                            if (property.OriginalValue != null) auditEntry.OldValues[propertyName] = property.OriginalValue;
                            break;

                        case EntityState.Modified:
                            if (property.IsModified)
                            {
                                if (!Equals(property.OriginalValue, property.CurrentValue))
                                {
                                    auditEntry.AuditType = "Update";
                                    if (property.OriginalValue != null) auditEntry.OldValues[propertyName] = property.OriginalValue;
                                    if (property.CurrentValue != null) auditEntry.NewValues[propertyName] = property.CurrentValue;
                                }
                            }
                            break;
                    }
                }
            }

            foreach (var auditEntry in auditEntries.Where(_ => !_.HasTemporaryProperties))
            {
                AuditLogs.Add(auditEntry.ToAudit());
            }

            return auditEntries.Where(_ => _.HasTemporaryProperties).ToList();
        }

        private async Task OnAfterSaveChanges(List<AuditEntry> auditEntries)
        {
            if (auditEntries == null || auditEntries.Count == 0)
                return;

            foreach (var auditEntry in auditEntries)
            {
                foreach (var prop in auditEntry.TemporaryProperties)
                {
                    if (prop.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue!;
                    }
                    else
                    {
                        auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue!;
                    }
                }
                AuditLogs.Add(auditEntry.ToAudit());
            }

            await base.SaveChangesAsync();
        }

        private class AuditEntry
        {
            public AuditEntry(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry) { Entry = entry; }
            public Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Entry { get; }
            public Guid UserId { get; set; }
            public string EntityName { get; set; } = string.Empty;
            public string AuditType { get; set; } = string.Empty;
            public Dictionary<string, object> KeyValues { get; } = new();
            public Dictionary<string, object> OldValues { get; } = new();
            public Dictionary<string, object> NewValues { get; } = new();
            public List<Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry> TemporaryProperties { get; } = new();
            public bool HasTemporaryProperties => TemporaryProperties.Any();

            public AuditLog ToAudit()
            {
                var audit = new AuditLog
                {
                    UserId = UserId,
                    Action = AuditType,
                    EntityName = EntityName,
                    Timestamp = DateTime.UtcNow,
                    EntityId = JsonSerializer.Serialize(KeyValues)
                };

                var changes = new Dictionary<string, object[]>();
                foreach (var key in OldValues.Keys)
                {
                    changes[key] = new[] { OldValues[key], NewValues.ContainsKey(key) ? NewValues[key] : null! };
                }
                foreach (var key in NewValues.Keys.Where(k => !OldValues.ContainsKey(k)))
                {
                    changes[key] = new[] { null!, NewValues[key] };
                }

                if (changes.Count > 0)
                {
                    audit.Changes = JsonSerializer.Serialize(changes);
                }

                return audit;
            }
        }
    }
}