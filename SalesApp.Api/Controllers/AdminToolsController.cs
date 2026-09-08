using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;

namespace SalesApp.Controllers
{
    [ApiController]
    [Route("api/admin-tools")]
    [Authorize]
    public class AdminToolsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminToolsController(AppDbContext context)
        {
            _context = context;
        }

        private bool IsSuperAdmin()
        {
            var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;
            return emailClaim == "superadmin@salesapp.com" ||
                   emailClaim == "superadmin@test.com" ||
                   User.HasClaim("perm", "system:superadmin");
        }

        [HttpGet("users/search")]
        public async Task<ActionResult<ApiResponse<List<AdminUserSearchItem>>>> SearchUsers([FromQuery] string? query)
        {
            if (!IsSuperAdmin())
            {
                return Forbid();
            }

            var usersQuery = _context.Users
                .AsNoTracking()
                .Include(u => u.UserMatriculas)
                    .ThenInclude(um => um.Matricula)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim().ToLower();
                usersQuery = usersQuery.Where(u => u.Email.ToLower().Contains(q) || u.Name.ToLower().Contains(q));
            }

            var users = await usersQuery
                .OrderBy(u => u.Name)
                .Take(20)
                .Select(u => new AdminUserSearchItem
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsActive = u.IsActive,
                    Matriculas = u.UserMatriculas
                        .Where(um => um.IsActive && um.Matricula != null)
                        .Select(um => um.Matricula.MatriculaNumber)
                        .ToList()
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<AdminUserSearchItem>>
            {
                Success = true,
                Data = users,
                Message = "Usuários consultados com sucesso."
            });
        }

        [HttpPost("migrate-contracts")]
        public async Task<ActionResult<ApiResponse<AdminMigrateContractsResult>>> MigrateContracts([FromBody] AdminMigrateContractsRequest request)
        {
            if (!IsSuperAdmin())
            {
                return Forbid();
            }

            if (request == null || string.IsNullOrWhiteSpace(request.FromEmail) || string.IsNullOrWhiteSpace(request.ToEmail))
            {
                return BadRequest(new ApiResponse<AdminMigrateContractsResult>
                {
                    Success = false,
                    Message = "Os e-mails de origem e destino são obrigatórios."
                });
            }

            var fromEmail = request.FromEmail.Trim().ToLower();
            var toEmail = request.ToEmail.Trim().ToLower();

            if (fromEmail == toEmail)
            {
                return BadRequest(new ApiResponse<AdminMigrateContractsResult>
                {
                    Success = false,
                    Message = "O e-mail de origem e destino devem ser diferentes."
                });
            }

            var fromUser = await _context.Users
                .Include(u => u.UserMatriculas)
                    .ThenInclude(um => um.Matricula)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == fromEmail);

            if (fromUser == null)
            {
                return NotFound(new ApiResponse<AdminMigrateContractsResult>
                {
                    Success = false,
                    Message = $"Usuário de origem não encontrado: {request.FromEmail}"
                });
            }

            var toUser = await _context.Users
                .Include(u => u.UserMatriculas)
                    .ThenInclude(um => um.Matricula)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == toEmail);

            if (toUser == null)
            {
                return NotFound(new ApiResponse<AdminMigrateContractsResult>
                {
                    Success = false,
                    Message = $"Usuário de destino não encontrado: {request.ToEmail}"
                });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Fetch all contracts for the 'from' user
                var contracts = await _context.Contracts
                    .Where(c => c.UserInternalId == fromUser.InternalId)
                    .ToListAsync();

                int matriculasMigrated = 0;

                if (request.MigrateMatricula)
                {
                    var fromMatriculas = await _context.UserMatriculas
                        .Where(um => um.UserInternalId == fromUser.InternalId)
                        .ToListAsync();

                    var toMatriculas = await _context.UserMatriculas
                        .Where(um => um.UserInternalId == toUser.InternalId)
                        .ToListAsync();

                    matriculasMigrated = fromMatriculas.Count;

                    foreach (var um in fromMatriculas)
                    {
                        var existingToUm = toMatriculas.FirstOrDefault(m => m.MatriculaId == um.MatriculaId);
                        if (existingToUm != null)
                        {
                            if (um.IsOwner)
                            {
                                existingToUm.IsOwner = true;
                                existingToUm.UpdatedAt = DateTime.UtcNow;
                            }
                            _context.UserMatriculas.Remove(um);
                        }
                        else
                        {
                            um.UserInternalId = toUser.InternalId;
                            um.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }

                foreach (var contract in contracts)
                {
                    contract.UserInternalId = toUser.InternalId;
                    contract.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new ApiResponse<AdminMigrateContractsResult>
                {
                    Success = true,
                    Data = new AdminMigrateContractsResult
                    {
                        ContractsMigrated = contracts.Count,
                        MatriculasMigrated = matriculasMigrated,
                        FromUser = $"{fromUser.Name} ({fromUser.Email})",
                        ToUser = $"{toUser.Name} ({toUser.Email})"
                    },
                    Message = $"{contracts.Count} contratos migrados com sucesso para {toUser.Name}."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ApiResponse<AdminMigrateContractsResult>
                {
                    Success = false,
                    Message = $"Erro ao realizar migração de contratos: {ex.Message}"
                });
            }
        }
    }
}
