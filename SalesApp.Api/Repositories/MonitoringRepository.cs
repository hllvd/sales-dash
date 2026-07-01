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
