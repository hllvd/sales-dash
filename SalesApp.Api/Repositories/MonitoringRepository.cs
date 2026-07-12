using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;

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

            var healthData = await _context.Contracts
                .AsNoTracking()
                .Where(c => c.IsActive && c.ContractStatus.Name.ToLower() != "desistente")
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

            // Fetch users (including Role)
            var users = await _context.Users
                .Include(u => u.Role)
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
    }
}
