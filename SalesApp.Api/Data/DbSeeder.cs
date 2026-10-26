using SalesApp.Models;
using Microsoft.EntityFrameworkCore;

namespace SalesApp.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            await context.Database.MigrateAsync();

            // Self-healing migration guard: ensure index on Contracts.SaleStartDate exists for high-performance reports
            try
            {
                await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Contracts_SaleStartDate\" ON \"Contracts\" (\"SaleStartDate\");");
            }
            catch
            {
                // Index already exists or table handles it
            }
            
            // Check if admin user exists by email
            var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@salesapp.com");
            if (adminUser == null)
            {
                adminUser = new User
                {
                    Id = Guid.Parse("32af666a-413e-4745-bd26-bb4c6fef4a72"),
                    Name = "Admin User",
                    Email = "admin@salesapp.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    RoleId = 2, // Admin role
                    IsActive = true
                };
                
                context.Users.Add(adminUser);
            }
            else if (adminUser.RoleId != 2)
            {
                // Update existing admin user to have admin role
                adminUser.RoleId = 2;
                context.Users.Update(adminUser);
            }
            
            // Add test admin user
            var testAdminUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "user@example.com");
            if (testAdminUser == null)
            {
                testAdminUser = new User
                {
                    Id = Guid.Parse("2e23a8b2-4ead-4138-879f-85f2e1c74422"),
                    Name = "Test Admin",
                    Email = "user@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("string"),
                    RoleId = 2, // Admin role
                    IsActive = true
                };
                
                context.Users.Add(testAdminUser);
            }
            
            // Add superadmin user
            var superAdminUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "superadmin@salesapp.com");
            if (superAdminUser == null)
            {
                superAdminUser = new User
                {
                    Id = Guid.Parse("080a0aea-4cbd-490f-9d6c-bc001391b005"),
                    Name = "Super Admin",
                    Email = "superadmin@salesapp.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("string"),
                    RoleId = (int)Models.RoleId.SuperAdmin,
                    IsActive = true
                };
                
                context.Users.Add(superAdminUser);
            }
            
            // Flush users to DB so InternalId gets assigned before templates reference it
            await context.SaveChangesAsync();

            // Resolve the seeder InternalId to use for templates
            var seederInternalId = superAdminUser?.InternalId > 0 ? superAdminUser.InternalId
                : adminUser?.InternalId > 0 ? adminUser.InternalId
                : 1; // fallback: first user

            // Seed ImportTemplates
            var usersTemplate = await context.ImportTemplates.FirstOrDefaultAsync(t => t.Name == "Users");
            if (usersTemplate == null)
            {
                context.ImportTemplates.Add(new ImportTemplate
                {
                    Id = 1,
                    Name = "Users",
                    EntityType = "User",
                    Description = "Template for importing users",
                    RequiredFields = System.Text.Json.JsonSerializer.Serialize(new List<string> { "Name", "Email" }),
                    OptionalFields = System.Text.Json.JsonSerializer.Serialize(new List<string> { "Surname", "Role", "ParentEmail", "SendEmail", "Matricula", "IsMatriculaOwner", "Password" }),
                    DefaultMappings = "{}",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserInternalId = seederInternalId
                 });
            }
            
            var contractsTemplate = await context.ImportTemplates.FirstOrDefaultAsync(t => t.Name == "Contracts");
            if (contractsTemplate == null)
            {
                context.ImportTemplates.Add(new ImportTemplate
                {
                    Id = 2,
                    Name = "Contracts",
                    EntityType = "Contract",
                    Description = "Template for importing contracts",
                    RequiredFields = System.Text.Json.JsonSerializer.Serialize(new List<string> { "ContractNumber", "UserEmail", "TotalAmount", "MatriculaNumber" }),
                    OptionalFields = System.Text.Json.JsonSerializer.Serialize(new List<string> { "GroupId", "Status", "SaleStartDate", "SaleEndDate", "ContractType", "Quota", "PvId", "PvName", "CustomerName", "Version" }),
                    DefaultMappings = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string> { { "Matrícula", "MatriculaNumber" } }),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserInternalId = seederInternalId
                });
            }
            else
            {
                // Ensure existing template is updated to the new required/optional fields and mappings
                contractsTemplate.RequiredFields = System.Text.Json.JsonSerializer.Serialize(new List<string> { "ContractNumber", "UserEmail", "TotalAmount", "MatriculaNumber" });
                contractsTemplate.OptionalFields = System.Text.Json.JsonSerializer.Serialize(new List<string> { "GroupId", "Status", "SaleStartDate", "SaleEndDate", "ContractType", "Quota", "PvId", "PvName", "CustomerName", "Version" });
                contractsTemplate.DefaultMappings = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string> { { "Matrícula", "MatriculaNumber" } });
                context.ImportTemplates.Update(contractsTemplate);
            }
            
            var dashboardTemplate = await context.ImportTemplates.FirstOrDefaultAsync(t => t.Name == "contractDashboard");
            if (dashboardTemplate == null)
            {
                context.ImportTemplates.Add(new ImportTemplate
                {
                    Id = 3,
                    Name = "contractDashboard",
                    EntityType = "Contract",
                    Description = "Template for contract dashboard import from Power BI",
                    RequiredFields = System.Text.Json.JsonSerializer.Serialize(new List<string> { "ContractNumber", "TotalAmount", "SaleStartDate", "GroupId", "Quota", "CustomerName", "MatriculaNumber" }),
                    OptionalFields = System.Text.Json.JsonSerializer.Serialize(new List<string> { "Status", "PvId", "PvName", "Version", "Category", "PlanoVenda", "UserEmail", "HasPayment" }),
                    DefaultMappings = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string> {
                        { "cota.group", "GroupId" },
                        { "cota.cota", "Quota" },
                        { "cota.customer", "CustomerName" },
                        { "cota.contract", "ContractNumber" },
                        { "DtVenda", "SaleStartDate" },
                        { "Dt Venda", "SaleStartDate" },
                        { "Situação Cobrança", "Status" },
                        { "SituacaoCobranca", "Status" },
                        { "CodPV", "PvId" },
                        { "Cód. PV", "PvId" },
                        { "PV", "PvName" },
                        { "Versao", "Version" },
                        { "Matricula", "MatriculaNumber" },
                        { "Categoria", "Category" },
                        { "PlanoVenda", "PlanoVenda" },
                        { "Email", "UserEmail" },
                        { "Tem Pagamento?", "HasPayment" },
                        { "Tem Pagamento", "HasPayment" },
                        { "TemPagamento", "HasPayment" }
                    }),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserInternalId = seederInternalId
                });
            }
            else
            {
                // Ensure existing dashboard template is updated
                dashboardTemplate.RequiredFields = System.Text.Json.JsonSerializer.Serialize(new List<string> { "ContractNumber", "TotalAmount", "SaleStartDate", "GroupId", "Quota", "CustomerName", "MatriculaNumber" });
                dashboardTemplate.OptionalFields = System.Text.Json.JsonSerializer.Serialize(new List<string> { "Status", "PvId", "PvName", "Version", "Category", "PlanoVenda", "UserEmail", "HasPayment" });
                dashboardTemplate.DefaultMappings = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string> {
                    { "cota.group", "GroupId" },
                    { "cota.cota", "Quota" },
                    { "cota.customer", "CustomerName" },
                    { "cota.contract", "ContractNumber" },
                    { "DtVenda", "SaleStartDate" },
                    { "Dt Venda", "SaleStartDate" },
                    { "Situação Cobrança", "Status" },
                    { "SituacaoCobranca", "Status" },
                    { "CodPV", "PvId" },
                    { "Cód. PV", "PvId" },
                    { "PV", "PvName" },
                    { "Versao", "Version" },
                    { "Matricula", "MatriculaNumber" },
                    { "Categoria", "Category" },
                    { "PlanoVenda", "PlanoVenda" },
                    { "Email", "UserEmail" },
                    { "Tem Pagamento?", "HasPayment" },
                    { "Tem Pagamento", "HasPayment" },
                    { "TemPagamento", "HasPayment" }
                });
                context.ImportTemplates.Update(dashboardTemplate);
            }
            
            await SeedPermissions(context);
            await SeedMetadataFields(context);
            await SeedClassificationLevels(context);
            
            await context.SaveChangesAsync();
        }

        private static async Task SeedClassificationLevels(AppDbContext context)
        {
            // Proteção: apenas seeda se a tabela estiver completamente vazia para não tocar em dados de Produção
            if (await context.ClassificationLevels.AnyAsync())
            {
                return;
            }

            // Remove legacy levels (Bronze, Prata, Ouro) if present
            var legacyNames = new[] { "bronze", "prata", "ouro" };
            var legacyLevels = await context.ClassificationLevels
                .Where(l => legacyNames.Contains(l.Name.ToLower()))
                .ToListAsync();

            if (legacyLevels.Count > 0)
            {
                var legacyIds = legacyLevels.Select(l => l.Id).ToHashSet();
                var referencingLevels = await context.ClassificationLevels
                    .Where(l => l.NextLevelId.HasValue && legacyIds.Contains(l.NextLevelId.Value))
                    .ToListAsync();
                foreach (var refL in referencingLevels)
                {
                    refL.NextLevelId = null;
                }

                foreach (var leg in legacyLevels)
                {
                    leg.NextLevelId = null;
                }
                await context.SaveChangesAsync();

                var legacyAssignments = await context.UserClassifications
                    .Where(uc => legacyIds.Contains(uc.LevelId))
                    .ToListAsync();
                if (legacyAssignments.Count > 0)
                {
                    context.UserClassifications.RemoveRange(legacyAssignments);
                }

                context.ClassificationLevels.RemoveRange(legacyLevels);
                await context.SaveChangesAsync();
            }

            var baseLevels = new List<(string Name, string Description, string? Prize, decimal? SalesGoal, int? Retention, string? NextLevelName)>
            {
                ("Alef", "Nível Alef", "ALMOÇO BATEL GRILL", 2000000m, null, "Beth"),
                ("Beth", "Nível Beth", "VALE ZARA", 4000000m, null, "Guimel"),
                ("Guimel", "Nível Guimel", "CAMISA", 10000000m, 79, "Daleth"),
                ("Daleth", "Nível Daleth", "RELÓGIO", 40000000m, null, "Vav"),
                ("Vav", "Nível Vav", "CANETA MONT BLANC", 40000000m, null, "Hé"),
                ("Hé", "Nível Hé", "TERNO OU BOLSA", 80000000m, null, "Zayin"),
                ("Zayin", "Nível Zayin", "CRUZEIRO", 140000000m, null, "Heth"),
                ("Heth", "Nível Heth", "PUNTA CANA", 240000000m, null, "Teth"),
                ("Teth", "Nível Teth", "VIAGEM PARA DISNEY", 300000000m, null, "Iod"),
                ("Iod", "Nível Iod", "VIAGEM GRÉCIA E SANTORINI", 500000000m, null, "Kaf"),
                ("Kaf", "Nível Kaf", "VIAGEM DUBAI E MALDIVAS", 700000000m, null, "Lamed"),
                ("Lamed", "Nível Lamed", "VIAGEM VOLTA AO MUNDO", 1000000000m, null, null)
            };

            foreach (var item in baseLevels)
            {
                var existing = await context.ClassificationLevels.FirstOrDefaultAsync(l => l.Name.ToLower() == item.Name.ToLower());
                if (existing == null)
                {
                    context.ClassificationLevels.Add(new ClassificationLevel
                    {
                        Name = item.Name,
                        Description = item.Description,
                        Prize = item.Prize,
                        SalesGoal = item.SalesGoal,
                        Retention = item.Retention,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.Description = item.Description;
                    existing.Prize = item.Prize;
                    existing.SalesGoal = item.SalesGoal;
                    existing.Retention = item.Retention;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }
            await context.SaveChangesAsync();

            // Link NextLevelId if not already set
            var allDbLevels = await context.ClassificationLevels.ToListAsync();
            var nameToLevel = allDbLevels.ToDictionary(l => l.Name.ToLower(), l => l);

            bool updatedAny = false;
            foreach (var item in baseLevels)
            {
                if (item.NextLevelName != null &&
                    nameToLevel.TryGetValue(item.Name.ToLower(), out var curr) &&
                    nameToLevel.TryGetValue(item.NextLevelName.ToLower(), out var next))
                {
                    if (curr.NextLevelId != next.Id)
                    {
                        curr.NextLevelId = next.Id;
                        updatedAny = true;
                    }
                }
                else if (item.NextLevelName == null &&
                         nameToLevel.TryGetValue(item.Name.ToLower(), out var lastLevel) &&
                         lastLevel.NextLevelId.HasValue)
                {
                    lastLevel.NextLevelId = null;
                    updatedAny = true;
                }
            }

            if (updatedAny)
            {
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedMetadataFields(AppDbContext context)
        {
            var fields = new List<UserMetadataField>
            {
                new() { Key = "secretary_name", Label = "Nome da Secretária", GroupLabel = "Secretária", FieldType = "text", IsRequired = false, IsActive = true, CreatedAt = DateTime.UtcNow },
                new() { Key = "secretary_email", Label = "E-mail da Secretária", GroupLabel = "Secretária", FieldType = "text", IsRequired = false, IsActive = true, CreatedAt = DateTime.UtcNow },
                new() { Key = "secretary_whatsapp", Label = "WhatsApp da Secretária", GroupLabel = "Secretária", FieldType = "text", IsRequired = false, IsActive = true, CreatedAt = DateTime.UtcNow }
            };

            foreach (var field in fields)
            {
                if (!await context.UserMetadataFields.AnyAsync(f => f.Key == field.Key))
                {
                    context.UserMetadataFields.Add(field);
                }
            }
            await context.SaveChangesAsync();
        }

        private static async Task SeedPermissions(AppDbContext context)
        {
            var permissions = new List<string>
            {
                "users:read", "users:create", "users:update", "users:delete", "users:profile-update", "users:reset-password",
                "contracts:read", "contracts:create", "contracts:update", "contracts:delete",
                "pvs:read", "pvs:create", "pvs:update", "pvs:delete",
                "imports:execute", "imports:history", "imports:rollback",
                "groups:read", "groups:write",
                "matriculas:read", "matriculas:write",
                "roles:read", "roles:create", "roles:update", "roles:delete",
                "teams:manage",
                "requests:read",
                "system:admin", "system:superadmin"
            };

            foreach (var permName in permissions)
            {
                if (!await context.Permissions.AnyAsync(p => p.Name == permName))
                {
                    context.Permissions.Add(new Permission { Name = permName, Description = $"Permission to {permName}" });
                }
            }
            await context.SaveChangesAsync();

            // Assign to SuperAdmin (All)
            var allPerms = await context.Permissions.ToListAsync();
            await AssignPermissionsToRole(context, (int)Models.RoleId.SuperAdmin, allPerms);

            // Assign to Admin (Most permissions except high-risk ones)
            var adminPerms = allPerms.Where(p => 
                p.Name != "imports:rollback" && 
                p.Name != "system:superadmin" &&
                p.Name != "roles:delete" &&
                !p.Name.StartsWith("pvs:")
            ).ToList();
            await AssignPermissionsToRole(context, (int)Models.RoleId.Admin, adminPerms);

            // Assign to User (Basic)
            var userPerms = allPerms.Where(p => 
                p.Name == "contracts:read" || 
                p.Name == "users:profile-update" || 
                p.Name == "users:reset-password" ||
                p.Name == "requests:read"
            ).ToList();
            await AssignPermissionsToRole(context, (int)Models.RoleId.User, userPerms);
        }

        private static async Task AssignPermissionsToRole(AppDbContext context, int roleId, List<Permission> permissions)
        {
            foreach (var perm in permissions)
            {
                if (!await context.RolePermissions.AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == perm.Id))
                {
                    context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = perm.Id });
                }
            }
            await context.SaveChangesAsync();
        }
    }
}