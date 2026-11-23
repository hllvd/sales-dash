using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Utils;

namespace SalesApp.Repositories
{
    public class MonitoringRepository : IMonitoringRepository
    {
        private readonly AppDbContext _context;

        public MonitoringRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<MatriculaHealthResponse>> GetMatriculaHealthAsync()
        {
            var now = DateTime.UtcNow;
            var cutoffDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-14);

            var healthData = await _context.Contracts
                .AsNoTracking()
                .Where(c => c.IsActive && c.ContractStatus.Name.ToLower() != "desistente" && c.SaleStartDate >= cutoffDate)
                .Select(c => new
                {
                    Matricula = c.Matricula != null ? c.Matricula.MatriculaNumber : c.TempMatricula,
                    c.UpdatedAt
                })
                .Where(x => !string.IsNullOrEmpty(x.Matricula))
                .GroupBy(x => x.Matricula)
                .Select(g => new
                {
                    Matricula = g.Key!,
                    LastUpdate = g.Max(x => x.UpdatedAt),
                    Count = g.Count()
                })
                .OrderBy(h => h.LastUpdate)
                .ToListAsync();

            return healthData.Select(h => new MatriculaHealthResponse
            {
                Matricula = h.Matricula,
                LastUpdate = DateTime.SpecifyKind(h.LastUpdate, DateTimeKind.Utc),
                ContractCount = h.Count,
                Status = (now - h.LastUpdate).TotalHours switch
                {
                    > 168 => "Danger",
                    > 72 => "OutOfDate",
                    > 36 => "Warning",
                    _ => "Healthy"
                }
            }).ToList();
        }

        public async Task<List<TeamMatriculaHealthResponse>> GetEquipesHealthAsync()
        {
            var now = DateTime.UtcNow;

            // 1. Load entities flat to avoid SQL APPLY operator not supported by SQLite
            var teams = await _context.Teams
                .AsNoTracking()
                .Select(t => new { t.Id, t.Name })
                .ToListAsync();

            var userTeams = await _context.UserTeams
                .AsNoTracking()
                .Where(ut => ut.StartDate <= now && (ut.EndDate == null || ut.EndDate > now))
                .Select(ut => new { ut.TeamId, ut.UserInternalId })
                .ToListAsync();

            var userMatriculas = await _context.UserMatriculas
                .AsNoTracking()
                .Where(um => um.IsActive && (um.EndDate == null || um.EndDate > now))
                .Select(um => new { um.UserInternalId, MatriculaNumber = um.Matricula != null ? um.Matricula.MatriculaNumber : "" })
                .Where(um => !string.IsNullOrEmpty(um.MatriculaNumber))
                .ToListAsync();

            // 2. Get health for all matriculas
            var allHealth = await GetMatriculaHealthAsync();
            var healthMap = allHealth.ToDictionary(h => h.Matricula, h => h);

            var result = new List<TeamMatriculaHealthResponse>();

            foreach (var team in teams)
            {
                var memberIds = userTeams
                    .Where(ut => ut.TeamId == team.Id)
                    .Select(ut => ut.UserInternalId)
                    .ToList();

                var teamMatriculaNumbers = userMatriculas
                    .Where(um => memberIds.Contains(um.UserInternalId))
                    .Select(um => um.MatriculaNumber)
                    .Distinct()
                    .ToList();

                var teamHealthList = new List<MatriculaHealthResponse>();
                foreach (var matriculaNum in teamMatriculaNumbers)
                {
                    if (healthMap.TryGetValue(matriculaNum, out var health))
                    {
                        teamHealthList.Add(health);
                    }
                }

                // If a team has no matching matriculas with health records, we skip it (hidden)
                if (teamHealthList.Count == 0)
                {
                    continue;
                }

                // Determine Worst Status: Danger > OutOfDate > Warning > Healthy
                string worstStatus = "Healthy";
                if (teamHealthList.Any(h => h.Status == "Danger"))
                {
                    worstStatus = "Danger";
                }
                else if (teamHealthList.Any(h => h.Status == "OutOfDate"))
                {
                    worstStatus = "OutOfDate";
                }
                else if (teamHealthList.Any(h => h.Status == "Warning"))
                {
                    worstStatus = "Warning";
                }

                result.Add(new TeamMatriculaHealthResponse
                {
                    TeamId = team.Id,
                    TeamName = team.Name,
                    TotalMatriculas = teamHealthList.Count,
                    WorstStatus = worstStatus,
                    Matriculas = teamHealthList.OrderBy(m => m.LastUpdate).ToList()
                });
            }

            // Sort teams: worst status first, then by team name
            return result
                .OrderBy(t => GetStatusSeverity(t.WorstStatus))
                .ThenBy(t => t.TeamName)
                .ToList();
        }

        public async Task<List<AdminImportStatsResponse>> GetAdminImportStatsAsync()
        {
            var admins = await _context.Users
                .AsNoTracking()
                .Where(u => u.RoleId == 2 || (u.Role != null && u.Role.Name.ToLower() == "admin"))
                .Select(u => new
                {
                    u.Id,
                    u.InternalId,
                    u.Name,
                    u.Email,
                    LastImportAt = _context.ImportSessions
                        .Where(s => s.UploadedByUserInternalId == u.InternalId && s.TemplateId == 3 && s.Status == "completed")
                        .Max(s => (DateTime?)s.CompletedAt),
                    TotalImports = _context.ImportSessions
                        .Count(s => s.UploadedByUserInternalId == u.InternalId && s.TemplateId == 3 && s.Status == "completed")
                })
                .ToListAsync();

            return admins.Select(a => new AdminImportStatsResponse
            {
                UserId = a.Id,
                UserInternalId = a.InternalId,
                UserName = a.Name,
                UserEmail = a.Email,
                LastImportAt = a.LastImportAt.HasValue ? DateTime.SpecifyKind(a.LastImportAt.Value, DateTimeKind.Utc) : null,
                TotalImports = a.TotalImports
            })
            .OrderByDescending(a => a.LastImportAt.HasValue) // Has value first
            .ThenByDescending(a => a.LastImportAt)
            .ThenBy(a => a.UserName)
            .ToList();
        }

        public async Task<LicensingReportResponse> GetLicensingReportAsync(
            int year,
            int month,
            int minimumActiveDays,
            List<string> excludedEmails,
            List<SalesApp.Models.Configuration.PriceTier> priceTiers)
        {
            var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);
            var daysInMonth = DateTime.DaysInMonth(year, month);

            // Fetch users (including Role and ParentUser)
            var users = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.ParentUser)
                .AsNoTracking()
                .ToListAsync();

            var excludedSet = new HashSet<string>(excludedEmails ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var targetUsers = users
                .Where(u => !excludedSet.Contains(u.Email))
                .OrderBy(u => u.Name)
                .ToList();

            // Fetch active teams for users
            var now = DateTime.UtcNow;
            var userActiveTeams = await _context.UserTeams
                .Include(ut => ut.Team)
                .AsNoTracking()
                .Where(ut => ut.StartDate <= now && (ut.EndDate == null || ut.EndDate > now))
                .Select(ut => new { ut.UserInternalId, TeamName = ut.Team.Name })
                .ToListAsync();

            var userTeamsMap = userActiveTeams
                .GroupBy(ut => ut.UserInternalId)
                .ToDictionary(g => g.Key, g => g.First().TeamName);

            // Fetch audit logs for Users
            var auditLogs = await _context.AuditLogs
                .Where(a => a.EntityName == "User")
                .AsNoTracking()
                .OrderBy(a => a.Timestamp)
                .ToListAsync();

            // Group logs by UserId
            var logsByUser = auditLogs
                .Select(a => new { Log = a, UserId = ExtractGuid(a.EntityId) })
                .Where(x => x.UserId.HasValue)
                .GroupBy(x => x.UserId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Log).ToList());

            var userDetails = new List<UserLicenseDetailDto>();

            foreach (var u in targetUsers)
            {
                string teamName = "Sem equipe";
                if (userTeamsMap.TryGetValue(u.InternalId, out var team))
                {
                    teamName = team;
                }

                string parentUserDisplay = "-";
                if (u.ParentUser != null)
                {
                    string parentTeamName = "Sem Equipe";
                    if (userTeamsMap.TryGetValue(u.ParentUser.InternalId, out var pTeam) && !string.IsNullOrWhiteSpace(pTeam))
                    {
                        parentTeamName = pTeam;
                    }
                    parentUserDisplay = $"{u.ParentUser.Email} ({parentTeamName})";
                }

                // If user was created after the month ended, they had 0 active days
                if (u.CreatedAt >= monthEnd)
                {
                    userDetails.Add(new UserLicenseDetailDto
                    {
                        UserId = u.Id,
                        Name = u.Name,
                        Email = u.Email,
                        Role = u.Role?.Name ?? "user",
                        TeamName = teamName,
                        ParentUser = parentUserDisplay,
                        ActiveDaysInMonth = 0,
                        IsLicensed = false
                    });
                    continue;
                }

                List<AuditLog> userLogs;
                if (!logsByUser.TryGetValue(u.Id, out userLogs))
                {
                    userLogs = new List<AuditLog>();
                }

                var logsBefore = userLogs.Where(l => l.Timestamp < monthStart).ToList();
                var logsDuring = userLogs.Where(l => l.Timestamp >= monthStart && l.Timestamp < monthEnd).ToList();

                // Determine starting IsActive status
                bool startingIsActive = true;
                var lastBefore = logsBefore.LastOrDefault(l => GetIsActiveValue(l, true).HasValue);
                if (lastBefore != null)
                {
                    startingIsActive = GetIsActiveValue(lastBefore, true)!.Value;
                }
                else
                {
                    if (u.CreatedAt < monthStart)
                    {
                        // No logs before the month started, and created before.
                        // If there are also no logs during the month, they kept u.IsActive status.
                        startingIsActive = u.IsActive;
                    }
                    else
                    {
                        // Created during the month; they started as inactive (non-existent) before creation
                        startingIsActive = false;
                    }
                }

                double totalActiveDays = 0;
                bool currentStatus = startingIsActive;
                DateTime periodStart = monthStart > u.CreatedAt ? monthStart : u.CreatedAt;

                foreach (var l in logsDuring)
                {
                    var newIsActive = GetIsActiveValue(l, true);
                    if (newIsActive.HasValue)
                    {
                        if (currentStatus)
                        {
                            totalActiveDays += (l.Timestamp - periodStart).TotalDays;
                        }
                        currentStatus = newIsActive.Value;
                        periodStart = l.Timestamp;
                    }
                }

                if (currentStatus)
                {
                    totalActiveDays += (monthEnd - periodStart).TotalDays;
                }

                int activeDaysCount = (int)Math.Round(totalActiveDays);
                if (activeDaysCount > daysInMonth) activeDaysCount = daysInMonth;
                if (activeDaysCount < 0) activeDaysCount = 0;

                userDetails.Add(new UserLicenseDetailDto
                {
                    UserId = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    Role = u.Role?.Name ?? "user",
                    TeamName = teamName,
                    ParentUser = parentUserDisplay,
                    ActiveDaysInMonth = activeDaysCount,
                    IsLicensed = activeDaysCount >= minimumActiveDays
                });
            }

            int totalLicensed = userDetails.Count(ud => ud.IsLicensed);

            // Find matching tier
            var matchedTier = priceTiers
                .OrderBy(t => t.From)
                .FirstOrDefault(t => totalLicensed >= t.From && (t.To == null || totalLicensed <= t.To));

            decimal pricePerUser = matchedTier?.PricePerUser ?? 0;
            decimal totalCost = totalLicensed * pricePerUser;

            var priceTierDtos = priceTiers
                .Select(t => new PriceTierDto
                {
                    From = t.From,
                    To = t.To,
                    PricePerUser = t.PricePerUser,
                    IsCurrentTier = matchedTier != null && matchedTier.From == t.From && matchedTier.To == t.To
                })
                .ToList();

            return new LicensingReportResponse
            {
                Year = year,
                Month = month,
                MinimumActiveDays = minimumActiveDays,
                TotalLicensedUsers = totalLicensed,
                TotalUsersConsidered = targetUsers.Count,
                PricePerUser = pricePerUser,
                TotalCost = totalCost,
                PriceTiers = priceTierDtos,
                Users = userDetails
            };
        }

        private static Guid? ExtractGuid(string entityId)
        {
            if (string.IsNullOrEmpty(entityId)) return null;
            try
            {
                using (var doc = System.Text.Json.JsonDocument.Parse(entityId))
                {
                    if (doc.RootElement.TryGetProperty("Id", out var idProp) || doc.RootElement.TryGetProperty("id", out idProp))
                    {
                        if (Guid.TryParse(idProp.GetString(), out var guid))
                            return guid;
                    }
                }
            }
            catch
            {
                var match = System.Text.RegularExpressions.Regex.Match(entityId, @"[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}");
                if (match.Success && Guid.TryParse(match.Value, out var guid))
                    return guid;
            }
            return null;
        }

        private static bool? GetIsActiveValue(AuditLog log, bool getNewValue)
        {
            if (string.IsNullOrEmpty(log.Changes)) return null;
            try
            {
                using (var doc = System.Text.Json.JsonDocument.Parse(log.Changes))
                {
                    if (doc.RootElement.TryGetProperty("IsActive", out var prop) || doc.RootElement.TryGetProperty("isActive", out prop))
                    {
                        if (prop.ValueKind == System.Text.Json.JsonValueKind.Array && prop.GetArrayLength() == 2)
                        {
                            var valElement = prop[getNewValue ? 1 : 0];
                            if (valElement.ValueKind == System.Text.Json.JsonValueKind.True) return true;
                            if (valElement.ValueKind == System.Text.Json.JsonValueKind.False) return false;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static int GetStatusSeverity(string status)
        {
            return status switch
            {
                "Danger" => 1,
                "OutOfDate" => 2,
                "Warning" => 3,
                "Healthy" => 4,
                _ => 5
            };
        }

        private IQueryable<Contract> BuildMonitoringContractsBaseQuery(
            List<int>? teamIds,
            List<Guid>? userIds,
            List<string>? matriculas,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var query = _context.Contracts
                .AsNoTracking()
                .Where(c => c.IsActive);

            if (startDate.HasValue)
            {
                var start = startDate.Value.Date;
                query = query.Where(c => c.SaleStartDate >= start);
            }

            if (endDate.HasValue)
            {
                var end = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(c => c.SaleStartDate <= end);
            }

            if (matriculas != null && matriculas.Count > 0)
            {
                var normalizedMatriculas = matriculas
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => m.ToLower().Trim())
                    .ToList();

                if (normalizedMatriculas.Count > 0)
                {
                    query = query.Where(c =>
                        (c.Matricula != null && normalizedMatriculas.Any(n => c.Matricula.MatriculaNumber.ToLower().Contains(n))) ||
                        (!string.IsNullOrEmpty(c.TempMatricula) && normalizedMatriculas.Any(n => c.TempMatricula.ToLower().Contains(n)))
                    );
                }
            }

            if (teamIds != null && teamIds.Count > 0)
            {
                query = query.Where(c => c.UserInternalId != null && _context.UserTeams.Any(ut =>
                    teamIds.Contains(ut.TeamId) &&
                    ut.UserInternalId == c.UserInternalId.Value &&
                    c.SaleStartDate.Date >= ut.StartDate.Date &&
                    (ut.EndDate == null || c.SaleStartDate.Date <= ut.EndDate.Value.Date)
                ));
            }

            if (userIds != null && userIds.Count > 0)
            {
                query = query.Where(c => c.User != null && userIds.Contains(c.User.Id));
            }

            return query;
        }

        public async Task<ContractUpdateSummaryResponse> GetContractUpdateSummaryAsync(
            List<int>? teamIds = null,
            List<Guid>? userIds = null,
            List<string>? matriculas = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var baseQuery = BuildMonitoringContractsBaseQuery(teamIds, userIds, matriculas, startDate, endDate);

            var now = DateTime.UtcNow;
            var t1h = now.AddHours(-1);
            var t3h = now.AddHours(-3);
            var t6h = now.AddHours(-6);
            var t1d = now.AddDays(-1);
            var t3d = now.AddDays(-3);
            var t1w = now.AddDays(-7);
            var t1m = now.AddMonths(-1);

            var counts = await baseQuery
                .GroupBy(_ => 1)
                .Select(g => new ContractUpdateSummaryResponse
                {
                    Total = g.Count(),
                    MoreThan1Hour = g.Count(c => c.UpdatedAt < t1h),
                    MoreThan3Hours = g.Count(c => c.UpdatedAt < t3h),
                    MoreThan6Hours = g.Count(c => c.UpdatedAt < t6h),
                    MoreThan1Day = g.Count(c => c.UpdatedAt < t1d),
                    MoreThan3Days = g.Count(c => c.UpdatedAt < t3d),
                    MoreThan1Week = g.Count(c => c.UpdatedAt < t1w),
                    MoreThan1Month = g.Count(c => c.UpdatedAt < t1m)
                })
                .FirstOrDefaultAsync();

            return counts ?? new ContractUpdateSummaryResponse();
        }

        public async Task<(List<ContractResponse> Items, int TotalCount)> GetContractsByUpdateAsync(
            string? updateFilter,
            List<int>? teamIds,
            List<Guid>? userIds,
            List<string>? matriculas,
            DateTime? startDate,
            DateTime? endDate,
            int page,
            int pageSize)
        {
            var query = BuildMonitoringContractsBaseQuery(teamIds, userIds, matriculas, startDate, endDate);

            var now = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(updateFilter) && !updateFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                switch (updateFilter.ToLowerInvariant())
                {
                    case "morethan1hour":
                    case "olderthan1hour":
                    case "1h":
                    case ">1h":
                    case ">1hora":
                    case "1hora":
                        var t1h = now.AddHours(-1);
                        query = query.Where(c => c.UpdatedAt < t1h);
                        break;
                    case "morethan3hours":
                    case "olderthan3hours":
                    case "3h":
                    case ">3h":
                    case ">3horas":
                    case "3horas":
                        var t3h = now.AddHours(-3);
                        query = query.Where(c => c.UpdatedAt < t3h);
                        break;
                    case "morethan6hours":
                    case "olderthan6hours":
                    case "6h":
                    case ">6h":
                    case ">6horas":
                    case "6horas":
                        var t6h = now.AddHours(-6);
                        query = query.Where(c => c.UpdatedAt < t6h);
                        break;
                    case "morethan1day":
                    case "olderthan1day":
                    case "1d":
                    case ">1d":
                    case "24h":
                    case ">1dia":
                    case "1dia":
                        var t1d = now.AddDays(-1);
                        query = query.Where(c => c.UpdatedAt < t1d);
                        break;
                    case "morethan3days":
                    case "olderthan3days":
                    case "3d":
                    case ">3d":
                    case ">3dias":
                    case "3dias":
                        var t3d = now.AddDays(-3);
                        query = query.Where(c => c.UpdatedAt < t3d);
                        break;
                    case "morethan1week":
                    case "olderthan1week":
                    case "1w":
                    case ">1w":
                    case "7d":
                    case ">7d":
                    case ">1semana":
                    case "1semana":
                        var t1w = now.AddDays(-7);
                        query = query.Where(c => c.UpdatedAt < t1w);
                        break;
                    case "morethan1month":
                    case "olderthan1month":
                    case "1m":
                    case ">1m":
                    case ">1mes":
                    case ">1mês":
                    case "1mes":
                    case "1mês":
                        var t1m = now.AddMonths(-1);
                        query = query.Where(c => c.UpdatedAt < t1m);
                        break;
                }
            }

            int totalCount = await query.CountAsync();

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 100;

            var contracts = await query
                .OrderByDescending(c => c.UpdatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(c => c.User!).ThenInclude(u => u.UserMatriculas)
                .Include(c => c.User!).ThenInclude(u => u.UserTeams).ThenInclude(ut => ut.Team)
                .Include(c => c.Matricula!).ThenInclude(m => m.UserMatriculas).ThenInclude(um => um.User).ThenInclude(u => u.UserTeams).ThenInclude(ut => ut.Team)
                .Include(c => c.Group)
                .Include(c => c.PV)
                .Include(c => c.ContractStatus)
                .ToListAsync();

            var mappedItems = contracts.Select(c => ContractMappingHelper.MapToContractResponse(c, false)).ToList();

            return (mappedItems, totalCount);
        }
    }
}
