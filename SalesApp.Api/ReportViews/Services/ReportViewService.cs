using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Models.Configuration;
using SalesApp.Repositories;
using SalesApp.ReportViews.DTOs;
using SalesApp.ReportViews.Models;
using SalesApp.ReportViews.Repositories;

namespace SalesApp.ReportViews.Services
{
    public class ReportViewService : IReportViewService
    {
        private readonly IReportViewRepository _repository;
        private readonly IUserRepository _userRepository;
        private readonly AdminInfoOptions _adminInfo;

        public ReportViewService(
            IReportViewRepository repository,
            IUserRepository userRepository,
            IOptions<AdminInfoOptions>? adminInfoOptions = null)
        {
            _repository = repository;
            _userRepository = userRepository;
            _adminInfo = adminInfoOptions?.Value ?? new AdminInfoOptions();
        }

        private bool IsMasterSuperAdminEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var masterEmail = _adminInfo.MasterSuperAdminEmail ?? "superadmin@salesapp.com";
            return string.Equals(email.Trim(), masterEmail.Trim(), StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(email.Trim(), "superadmin@test.com", StringComparison.OrdinalIgnoreCase);
        }

        // ── List ──────────────────────────────────────────────────────────────

        public async Task<ServiceResult<List<ReportViewResponse>>> ListAsync(string callerId)
        {
            // Load caller details to check visibility restrictions and master superadmin status
            User? user = null;
            if (Guid.TryParse(callerId, out var callerGuid))
            {
                user = await _userRepository.GetByIdAsync(callerGuid);
            }

            if (user == null)
                return new ServiceResult<List<ReportViewResponse>>(true, new List<ReportViewResponse>());

            bool isMasterSuperAdmin = IsMasterSuperAdminEmail(user.Email);
            bool isSuperAdmin = isMasterSuperAdmin || user.Role?.Name == "superadmin";

            List<ReportView> views;
            if (isMasterSuperAdmin)
            {
                views = await _repository.ListAllAsync();
                return new ServiceResult<List<ReportViewResponse>>(true, views.Select(MapToResponse).ToList());
            }

            views = await _repository.ListForUserAsync(callerId);
            var allowedViews = new List<ReportView>();

            foreach (var v in views)
            {
                // Owner and Superadmin are always allowed
                if (v.UserId == callerId || isSuperAdmin)
                {
                    allowedViews.Add(v);
                    continue;
                }

                // Private views not owned -> skip
                if (v.Scope == "private")
                    continue;

                // Shared view restrictions
                if (v.AllowedRoles?.Count > 0 || v.AllowedTeamIds?.Count > 0)
                {
                    bool roleMatch = v.AllowedRoles?.Count > 0 && 
                                     user.Role?.Name != null && 
                                     v.AllowedRoles.Contains(user.Role.Name);

                    bool teamMatch = false;
                    if (v.AllowedTeamIds?.Count > 0)
                    {
                        var activeTeamIds = user.UserTeams
                            .Where(ut => ut.StartDate <= DateTime.UtcNow && (ut.EndDate == null || ut.EndDate > DateTime.UtcNow))
                            .Select(ut => ut.TeamId)
                            .ToList();

                        teamMatch = activeTeamIds.Any(tid => v.AllowedTeamIds.Contains(tid));
                    }

                    if (roleMatch || teamMatch)
                    {
                        allowedViews.Add(v);
                    }
                }
                else
                {
                    // No restrictions -> allowed
                    allowedViews.Add(v);
                }
            }

            var response = allowedViews.Select(MapToResponse).ToList();
            return new ServiceResult<List<ReportViewResponse>>(true, response);
        }

        // ── Get single ────────────────────────────────────────────────────────

        public async Task<ServiceResult<ReportViewResponse>> GetAsync(string callerId, string viewId)
        {
            User? user = null;
            if (Guid.TryParse(callerId, out var callerGuid))
            {
                user = await _userRepository.GetByIdAsync(callerGuid);
            }

            bool isMasterSuperAdmin = user != null && IsMasterSuperAdminEmail(user.Email);
            bool isSuperAdmin = isMasterSuperAdmin || (user?.Role?.Name == "superadmin");

            // Strategy: try caller's own key first
            var view = await _repository.GetByIdAsync(callerId, viewId);

            if (view == null && isMasterSuperAdmin)
            {
                view = await _repository.GetAnyByIdAsync(viewId);
            }

            if (view == null)
            {
                // Caller doesn't own it — check shared list GSI results
                var all = await _repository.ListForUserAsync(callerId);
                view = all.FirstOrDefault(v => v.ViewId == viewId && v.Scope == "shared");
            }

            if (view == null)
                return new ServiceResult<ReportViewResponse>(false, null, null, 404);

            // Owner is always allowed
            if (view.UserId == callerId)
                return new ServiceResult<ReportViewResponse>(true, MapToResponse(view));

            if (user == null)
                return new ServiceResult<ReportViewResponse>(false, null, null, 404);

            // Superadmin (and Master Superadmin) is always allowed
            if (isSuperAdmin)
                return new ServiceResult<ReportViewResponse>(true, MapToResponse(view));

            // Private views not owned -> 404
            if (view.Scope == "private")
                return new ServiceResult<ReportViewResponse>(false, null, null, 404);

            // Check shared view restrictions
            if (view.AllowedRoles?.Count > 0 || view.AllowedTeamIds?.Count > 0)
            {
                bool roleMatch = view.AllowedRoles?.Count > 0 && 
                                 user.Role?.Name != null && 
                                 view.AllowedRoles.Contains(user.Role.Name);

                bool teamMatch = false;
                if (view.AllowedTeamIds?.Count > 0)
                {
                    var activeTeamIds = user.UserTeams
                        .Where(ut => ut.StartDate <= DateTime.UtcNow && (ut.EndDate == null || ut.EndDate > DateTime.UtcNow))
                        .Select(ut => ut.TeamId)
                        .ToList();

                    teamMatch = activeTeamIds.Any(tid => view.AllowedTeamIds.Contains(tid));
                }

                if (!roleMatch && !teamMatch)
                {
                    return new ServiceResult<ReportViewResponse>(false, null, null, 404);
                }
            }

            return new ServiceResult<ReportViewResponse>(true, MapToResponse(view));
        }

        // ── Create ────────────────────────────────────────────────────────────

        public async Task<ServiceResult<ReportViewResponse>> CreateAsync(
            string callerId,
            CreateReportViewRequest request)
        {
            var errors = ValidateRequest(request.Name, request.Description, request.Scope);
            if (errors.Count > 0)
                return new ServiceResult<ReportViewResponse>(false, null, errors, 400);

            var now = DateTime.UtcNow;
            var viewId = GenerateViewId();

            var view = new ReportView
            {
                PK             = "VIEW#",
                SK             = BuildSk(callerId, viewId),
                UserId         = callerId,
                ViewId         = viewId,
                Name           = request.Name.Trim(),
                Description    = request.Description?.Trim(),
                Scope          = request.Scope.ToLower(),
                Rows           = request.Rows ?? new List<ViewRow>(),
                AllowedTeamIds = request.AllowedTeamIds,
                AllowedRoles   = request.AllowedRoles,
                CreatedAt      = now,
                UpdatedAt      = now
            };

            await _repository.CreateAsync(view);
            return new ServiceResult<ReportViewResponse>(true, MapToResponse(view), null, 201);
        }

        // ── Update ────────────────────────────────────────────────────────────

        public async Task<ServiceResult<ReportViewResponse>> UpdateAsync(
            string callerId,
            string viewId,
            UpdateReportViewRequest request)
        {
            var errors = ValidateRequest(request.Name, request.Description, request.Scope);
            if (errors.Count > 0)
                return new ServiceResult<ReportViewResponse>(false, null, errors, 400);

            User? user = null;
            if (Guid.TryParse(callerId, out var callerGuid))
            {
                user = await _userRepository.GetByIdAsync(callerGuid);
            }

            bool isMasterSuperAdmin = user != null && IsMasterSuperAdminEmail(user.Email);

            var view = await _repository.GetByIdAsync(callerId, viewId);
            if (view == null && isMasterSuperAdmin)
            {
                view = await _repository.GetAnyByIdAsync(viewId);
            }

            if (view == null)
                return new ServiceResult<ReportViewResponse>(false, null, null, 404);

            // Ownership check — only owner or Master SuperAdmin can edit
            if (view.UserId != callerId && !isMasterSuperAdmin)
                return new ServiceResult<ReportViewResponse>(false, null, null, 403);

            view.Name           = request.Name.Trim();
            view.Description    = request.Description?.Trim();
            view.Scope          = request.Scope.ToLower();
            view.Rows           = request.Rows ?? new List<ViewRow>();
            view.AllowedTeamIds = request.AllowedTeamIds;
            view.AllowedRoles   = request.AllowedRoles;
            view.UpdatedAt      = DateTime.UtcNow;

            await _repository.UpdateAsync(view);
            return new ServiceResult<ReportViewResponse>(true, MapToResponse(view));
        }

        // ── Delete ────────────────────────────────────────────────────────────

        public async Task<ServiceResult<bool>> DeleteAsync(string callerId, string viewId)
        {
            User? user = null;
            if (Guid.TryParse(callerId, out var callerGuid))
            {
                user = await _userRepository.GetByIdAsync(callerGuid);
            }

            bool isMasterSuperAdmin = user != null && IsMasterSuperAdminEmail(user.Email);

            var view = await _repository.GetByIdAsync(callerId, viewId);
            if (view == null && isMasterSuperAdmin)
            {
                view = await _repository.GetAnyByIdAsync(viewId);
            }

            if (view == null)
                return new ServiceResult<bool>(false, false, null, 404);

            // Ownership check — only owner or Master SuperAdmin can delete
            if (view.UserId != callerId && !isMasterSuperAdmin)
                return new ServiceResult<bool>(false, false, null, 403);

            // Always delete using the view's actual UserId (where the SK was created)
            await _repository.DeleteAsync(view.UserId, viewId);
            return new ServiceResult<bool>(true, true);
        }

        // ── Validation & Mapping Helpers ─────────────────────────────────────

        private static string BuildSk(string userId, string viewId) => $"#U-{userId}#VIEW-{viewId}";

        private static List<string> ValidateRequest(string? name, string? description, string? scope)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(name))
                errors.Add("Name is required.");
            else if (name.Length > 100)
                errors.Add("Name must be 100 characters or fewer.");

            if (description != null && description.Length > 500)
                errors.Add("Description must be 500 characters or fewer.");

            if (string.IsNullOrWhiteSpace(scope))
                errors.Add("Scope is required.");
            else
            {
                var cleanScope = scope.ToLower();
                if (cleanScope != "private" && cleanScope != "shared")
                    errors.Add("Scope must be 'private' or 'shared'.");
            }

            return errors;
        }

        private static ReportViewResponse MapToResponse(ReportView v) =>
            new()
            {
                ViewId         = v.ViewId,
                UserId         = v.UserId,
                Name           = v.Name,
                Description    = v.Description,
                Scope          = v.Scope,
                Rows           = v.Rows,
                AllowedTeamIds = v.AllowedTeamIds,
                AllowedRoles   = v.AllowedRoles,
                CreatedAt      = v.CreatedAt,
                UpdatedAt      = v.UpdatedAt
            };

        private static string GenerateViewId()
        {
            var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            var suffix = Guid.NewGuid().ToString("N")[..12];
            return $"{ts}{suffix}";
        }
    }
}
