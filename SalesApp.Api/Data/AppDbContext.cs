using Microsoft.EntityFrameworkCore;
using SalesApp.Models;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;

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
        public DbSet<ImportRow> ImportRows { get; set; }
        public DbSet<PV> PVs { get; set; }
        public DbSet<UserMatricula> UserMatriculas { get; set; }
        public DbSet<Matricula> Matriculas { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<ContractMetadata> ContractMetadata { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<ScrapeConfig> ScrapeConfigs { get; set; }
        public DbSet<PendingContractClaim> PendingContractClaims { get; set; }
        public DbSet<ContractStatusEntity> ContractStatuses { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<UserTeam> UserTeams { get; set; }
        public DbSet<ClassificationLevel> ClassificationLevels { get; set; }
        public DbSet<UserClassification> UserClassifications { get; set; }
        public DbSet<UserMetadataField> UserMetadataFields { get; set; }
        public DbSet<UserMetadataValue> UserMetadataValues { get; set; }
        public DbSet<ApprovalRequest> ApprovalRequests { get; set; }
        public DbSet<Store> Stores { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // User entity configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.InternalId).ValueGeneratedNever();
                entity.HasAlternateKey(e => e.InternalId);
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
                entity.HasIndex(e => new { e.IsActive, e.SaleStartDate }).HasDatabaseName("IX_Contracts_IsActive_SaleStartDate");
                entity.HasIndex(e => e.UserInternalId).HasDatabaseName("IX_Contracts_UserInternalId");
                entity.HasIndex(e => e.ContractStatusId).HasDatabaseName("IX_Contracts_ContractStatusId");
                entity.HasIndex(e => e.MatriculaId).HasDatabaseName("IX_Contracts_MatriculaId");
                entity.HasIndex(e => e.TempMatricula).HasDatabaseName("IX_Contracts_TempMatricula");
                entity.Property(e => e.ContractNumber).IsRequired();
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserInternalId)
                    .HasPrincipalKey(u => u.InternalId)
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
                    
                entity.HasOne(e => e.Matricula)
                    .WithMany(m => m.Contracts)
                    .HasForeignKey(e => e.MatriculaId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ImportSession)
                    .WithMany()
                    .HasForeignKey(e => e.ImportSessionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ContractStatus)
                    .WithMany()                         // no inverse nav needed for now
                    .HasForeignKey(e => e.ContractStatusId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(true);                 // now required
            });

            
            // PV entity configuration
            modelBuilder.Entity<PV>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
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
                    .HasForeignKey(e => e.CreatedByUserInternalId)
                    .HasPrincipalKey(u => u.InternalId)
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
                    .HasForeignKey(e => e.UploadedByUserInternalId)
                    .HasPrincipalKey(u => u.InternalId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ImportRow entity configuration
            modelBuilder.Entity<ImportRow>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.RowData).IsRequired();
                entity.Property(e => e.RowIndex).IsRequired();

                entity.HasOne(e => e.ImportSession)
                    .WithMany()
                    .HasForeignKey(e => e.ImportSessionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.ImportSessionId);
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
                    .HasForeignKey(e => e.CreatedByUserInternalId)
                    .HasPrincipalKey(u => u.InternalId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            
            
            // Matricula entity configuration
            modelBuilder.Entity<Matricula>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.MatriculaNumber).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.MatriculaNumber).IsUnique();
                
                entity.HasOne(e => e.ImportSession)
                    .WithMany()
                    .HasForeignKey(e => e.ImportSessionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            
            // UserMatricula entity configuration
            modelBuilder.Entity<UserMatricula>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                
                // Uniqueness: A user can only be linked to the same matricula once
                entity.HasIndex(e => new { e.UserInternalId, e.MatriculaId }).IsUnique();
                
                // Ownership Constraint: A matricula can have only one owner
                entity.HasIndex(e => e.MatriculaId)
                    .IsUnique()
                    .HasFilter("[IsOwner] = 1");
                
                entity.HasOne(e => e.User)
                    .WithMany(u => u.UserMatriculas)
                    .HasForeignKey(e => e.UserInternalId)
                    .HasPrincipalKey(u => u.InternalId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Matricula)
                    .WithMany(m => m.UserMatriculas)
                    .HasForeignKey(e => e.MatriculaId)
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
                entity.HasIndex(e => new { e.UserInternalId, e.IsRevoked });
                
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserInternalId)
                    .HasPrincipalKey(u => u.InternalId)
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
                
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserInternalId)
                    .HasPrincipalKey(u => u.InternalId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ScrapeConfig entity configuration
            modelBuilder.Entity<ScrapeConfig>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Ignore(e => e.UserId);
                entity.Property(e => e.Store).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Matricula).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PowerBiPassword).HasMaxLength(500);
                entity.Property(e => e.CredentialStatus).HasMaxLength(50);
                
                entity.HasOne(e => e.User)
                    .WithMany(u => u.ScrapeConfigs)
                    .HasForeignKey(e => e.UserInternalId)
                    .HasPrincipalKey(u => u.InternalId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // PendingContractClaim entity configuration
            modelBuilder.Entity<PendingContractClaim>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.HasIndex(e => new { e.ContractNumber }).IsUnique(); // Unique by contract number so only first user gets it
                entity.HasIndex(e => e.IsResolved);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserInternalId)
                    .HasPrincipalKey(u => u.InternalId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Matricula)
                    .WithMany()
                    .HasForeignKey(e => e.MatriculaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ContractStatusEntity (lookup table)
            modelBuilder.Entity<ContractStatusEntity>(entity =>
            {
                entity.ToTable("ContractStatuses");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();   // AUTOINCREMENT
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50).HasColumnType("TEXT");
                entity.HasIndex(e => e.Name).IsUnique();
            });

            // Store entity configuration
            modelBuilder.Entity<Store>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.State).IsRequired().HasMaxLength(2);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });

            // Team entity configuration
            modelBuilder.Entity<Team>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);

                entity.HasOne(e => e.Owner)
                    .WithMany()
                    .HasForeignKey(e => e.OwnerUserInternalId)
                    .HasPrincipalKey(u => u.InternalId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Store)
                    .WithMany(s => s.Teams)
                    .HasForeignKey(e => e.StoreId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // UserTeam entity configuration
            modelBuilder.Entity<UserTeam>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.StartDate).IsRequired();

                entity.HasOne(e => e.User)
                    .WithMany(u => u.UserTeams)
                    .HasForeignKey(e => e.UserInternalId)
                    .HasPrincipalKey(u => u.InternalId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Team)
                    .WithMany(t => t.UserTeams)
                    .HasForeignKey(e => e.TeamId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.UserInternalId, e.EndDate });
            });

            // ClassificationLevel entity configuration
            modelBuilder.Entity<ClassificationLevel>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Prize).HasMaxLength(200);
                entity.Property(e => e.SalesGoal).HasColumnType("decimal(18,2)");

                // Self-referencing FK: next level in the progression chain
                entity.HasOne(e => e.NextLevel)
                    .WithMany()
                    .HasForeignKey(e => e.NextLevelId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);

                // Self-referencing FK: minimum direct rule #1
                entity.HasOne(e => e.MinimumDirect1Level)
                    .WithMany()
                    .HasForeignKey(e => e.MinimumDirect1LevelId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);

                // Self-referencing FK: minimum direct rule #2
                entity.HasOne(e => e.MinimumDirect2Level)
                    .WithMany()
                    .HasForeignKey(e => e.MinimumDirect2LevelId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);
            });


            // UserClassification entity configuration
            modelBuilder.Entity<UserClassification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.StartDate).IsRequired();

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserInternalId)
                    .HasPrincipalKey(u => u.InternalId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Level)
                    .WithMany(l => l.UserClassifications)
                    .HasForeignKey(e => e.LevelId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.UserInternalId, e.EndDate });
            });

            // UserMetadataField entity configuration
            modelBuilder.Entity<UserMetadataField>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.HasIndex(e => e.Key).IsUnique();
                entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Label).IsRequired().HasMaxLength(150);
                entity.Property(e => e.FieldType).IsRequired().HasMaxLength(50).HasDefaultValue("text");
            });

            // UserMetadataValue entity configuration
            modelBuilder.Entity<UserMetadataValue>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.HasIndex(e => new { e.UserInternalId, e.UserMetadataFieldId }).IsUnique();

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserInternalId)
                    .HasPrincipalKey(u => u.InternalId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Field)
                    .WithMany(f => f.Values)
                    .HasForeignKey(e => e.UserMetadataFieldId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ApprovalRequest entity configuration
            modelBuilder.Entity<ApprovalRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RequestType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");
                entity.Property(e => e.PayloadJson).IsRequired();
                entity.Property(e => e.ApproverComment).HasMaxLength(500);

                entity.HasOne(e => e.Requester)
                    .WithMany()
                    .HasForeignKey(e => e.RequesterId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Approver)
                    .WithMany()
                    .HasForeignKey(e => e.ApproverId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.RequesterId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => new { e.RequestType, e.Status });
            });
        }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            
            // Suppress warning about pending model changes in development/E2E environments
            optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await SyncUserInternalIdsAsync(cancellationToken);
            await SyncEntityUserInternalIdsAsync(cancellationToken);
            var auditEntries = OnBeforeSaveChanges();
            var result = await base.SaveChangesAsync(cancellationToken);
            await OnAfterSaveChanges(auditEntries);
            return result;
        }

        private async Task SyncUserInternalIdsAsync(CancellationToken cancellationToken)
        {
            var newUserEntries = ChangeTracker.Entries<User>()
                .Where(e => e.State == EntityState.Added || e.Entity.InternalId <= 0)
                .ToList();

            if (!newUserEntries.Any())
                return;

            var currentMax = await Users.AnyAsync(cancellationToken)
                ? await Users.MaxAsync(u => u.InternalId, cancellationToken)
                : 0;

            if (currentMax < 0)
            {
                currentMax = 0;
            }

            var trackedUsers = ChangeTracker.Entries<User>()
                .Where(e => e.State == EntityState.Added && e.Entity.InternalId > 0)
                .Select(e => e.Entity.InternalId)
                .ToList();

            if (trackedUsers.Any())
            {
                currentMax = Math.Max(currentMax, trackedUsers.Max());
            }

            foreach (var entry in newUserEntries)
            {
                var user = entry.Entity;
                if (user.InternalId <= 0)
                {
                    currentMax++;
                    user.InternalId = currentMax;
                }
            }
        }


        private async Task SyncEntityUserInternalIdsAsync(CancellationToken cancellationToken)
        {
            // 1. Sync UserMatricula — UserInternalId is set directly by callers; no GUID fallback needed.
            // 2. Sync ImportSession — UploadedByUserInternalId is set directly by callers; no GUID fallback needed.
            // 3. Sync AuditLog — UserInternalId is set directly by callers; no GUID fallback needed.
            // 4. Sync PendingContractClaim — UserInternalId is set directly by callers; no GUID fallback needed.

            // 5. Sync ScrapeConfig — UserId (GUID) is retained for JWT auth; derive InternalId from it.
            var scrapeEntries = ChangeTracker.Entries<ScrapeConfig>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                .ToList();
            if (scrapeEntries.Any())
            {
                foreach (var entry in scrapeEntries)
                {
                    var entity = entry.Entity;
                    if (entity.UserId == null)
                    {
                        entity.UserInternalId = null;
                        continue;
                    }
                    var trackedUser = ChangeTracker.Entries<User>()
                        .FirstOrDefault(u => u.Entity.Id == entity.UserId)?.Entity;
                    if (trackedUser != null)
                    {
                        entity.UserInternalId = trackedUser.InternalId;
                    }
                    else
                    {
                        var internalId = await Users
                            .Where(u => u.Id == entity.UserId)
                            .Select(u => u.InternalId)
                            .FirstOrDefaultAsync(cancellationToken);
                        if (internalId > 0)
                        {
                            entity.UserInternalId = internalId;
                        }
                    }
                }
            }

            // 6. RefreshToken — UserInternalId is set directly by callers; no GUID fallback needed.
            // 7. ImportTemplate — CreatedByUserInternalId is set directly by callers; no GUID fallback needed.
            // 8. ImportColumnMapping — CreatedByUserInternalId is set directly by callers; no GUID fallback needed.
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

            // Skip auditing if there's no authenticated user (e.g., during seeding or unauthenticated requests)
            if (userId == Guid.Empty)
            {
                return auditEntries;
            }

            // Resolve the current user's internal ID synchronously
            var userInternalId = Users.Local.FirstOrDefault(u => u.Id == userId)?.InternalId 
                ?? Users.Where(u => u.Id == userId).Select(u => u.InternalId).FirstOrDefault();

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var entityName = entry.Entity.GetType().Name;
                if (entityName.EndsWith("Proxy")) entityName = entry.Entity.GetType().BaseType!.Name;

                var auditableEntities = new[] { "User", "Contract", "UserMatricula", "Matricula", "Group", "PV" };
                if (!auditableEntities.Contains(entityName))
                    continue;

                var auditEntry = new AuditEntry(entry)
                {
                    EntityName = entityName,
                    UserId = userId,
                    UserInternalId = userInternalId,
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
            public int UserInternalId { get; set; }
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
                    UserInternalId = UserInternalId,
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