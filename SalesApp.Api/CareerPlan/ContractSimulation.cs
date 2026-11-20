using System;
using System.Collections.Generic;
using System.Linq;
using SalesApp.DTOs;
using SalesApp.Models;

namespace SalesApp.CareerPlan
{
    public static class ContractSimulation
    {
        public static Contract Clone(Contract source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return new Contract
            {
                Id = source.Id,
                ContractNumber = source.ContractNumber,
                UserInternalId = source.UserInternalId,
                TotalAmount = source.TotalAmount,
                GroupId = source.GroupId,
                ContractStatusId = source.ContractStatusId,
                ContractStatus = source.ContractStatus,
                SaleStartDate = source.SaleStartDate,
                IsActive = source.IsActive,
                CreatedAt = source.CreatedAt,
                UpdatedAt = source.UpdatedAt,
                UploadId = source.UploadId,
                ImportSessionId = source.ImportSessionId,
                PvId = source.PvId,
                CustomerName = source.CustomerName,
                Quota = source.Quota,
                Version = source.Version,
                HasPayment = source.HasPayment,
                RawStatus = source.RawStatus,
                TempMatricula = source.TempMatricula,
                PlanoVendaMetadataId = source.PlanoVendaMetadataId,
                CategoryMetadataId = source.CategoryMetadataId,
                MatriculaId = source.MatriculaId,
                User = source.User,
                Matricula = source.Matricula,
                Group = source.Group,
                SimulatedPaidMonths = source.SimulatedPaidMonths
            };
        }

        public static IReadOnlyList<Contract> ApplyOverrides(
            IEnumerable<Contract> contracts,
            IEnumerable<ContractRtOverrideDto>? overrides,
            ContractStatusEntity activeStatus,
            ContractStatusEntity late1Status)
        {
            var dict = new Dictionary<string, ContractStatusEntity>(StringComparer.OrdinalIgnoreCase)
            {
                ["Active"] = activeStatus,
                ["Late1"] = late1Status
            };
            return ApplyOverrides(contracts, overrides, dict);
        }

        public static IReadOnlyList<Contract> ApplyOverrides(
            IEnumerable<Contract> contracts,
            IEnumerable<ContractRtOverrideDto>? overrides,
            IReadOnlyDictionary<string, ContractStatusEntity>? statusDict)
        {
            if (contracts == null) return Array.Empty<Contract>();

            var safeDict = statusDict ?? new Dictionary<string, ContractStatusEntity>(StringComparer.OrdinalIgnoreCase);

            var activeStatus = GetStatus(safeDict, "Active", ContractStatus.Active);
            var late1Status = GetStatus(safeDict, "Late1", ContractStatus.Late1);
            var late2Status = GetStatus(safeDict, "Late2", ContractStatus.Late2);
            var late3Status = GetStatus(safeDict, "Late3", ContractStatus.Late3);
            var defaultedStatus = GetStatus(safeDict, "Defaulted", ContractStatus.Defaulted);

            var overrideMap = (overrides ?? Enumerable.Empty<ContractRtOverrideDto>())
                .Where(o => !string.IsNullOrWhiteSpace(o.ContractNumber))
                .GroupBy(o => o.ContractNumber.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

            var result = new List<Contract>();
            foreach (var contract in contracts)
            {
                var clone = Clone(contract);
                if (overrideMap.TryGetValue(contract.ContractNumber, out var dto))
                {
                    var targetStatus = ResolveTargetStatus(
                        dto,
                        activeStatus,
                        late1Status,
                        late2Status,
                        late3Status,
                        defaultedStatus);

                    if (targetStatus != null)
                    {
                        clone.ContractStatus = targetStatus;
                        clone.ContractStatusId = targetStatus.Id;
                    }

                    if (dto.PaidMonths.HasValue)
                    {
                        clone.SimulatedPaidMonths = dto.PaidMonths.Value;
                    }
                }
                result.Add(clone);
            }

            return result;
        }

        private static ContractStatusEntity ResolveTargetStatus(
            ContractRtOverrideDto dto,
            ContractStatusEntity active,
            ContractStatusEntity late1,
            ContractStatusEntity late2,
            ContractStatusEntity late3,
            ContractStatusEntity defaulted)
        {
            if (!string.IsNullOrWhiteSpace(dto.SimulatedStatus))
            {
                var s = dto.SimulatedStatus.Trim().ToLowerInvariant();
                return s switch
                {
                    "active" => active,
                    "late1" => late1,
                    "late2" => late2,
                    "late3" => late3,
                    "defaulted" or "cancelado" => defaulted,
                    _ => active
                };
            }

            if (dto.OverdueMonths.HasValue)
            {
                return dto.OverdueMonths.Value switch
                {
                    <= 0 => active,
                    1 => late1,
                    2 => late2,
                    3 => late3,
                    _ => defaulted
                };
            }

            if (dto.SimulatedRts != null && dto.SimulatedRts.Count > 0)
            {
                int overdueCount = dto.SimulatedRts.Count(r => string.Equals(r, "unpaid", StringComparison.OrdinalIgnoreCase));
                return overdueCount switch
                {
                    0 => active,
                    1 => late1,
                    2 => late2,
                    3 => late3,
                    _ => defaulted
                };
            }

            if (!string.IsNullOrWhiteSpace(dto.State))
            {
                if (string.Equals(dto.State, "upToDate", StringComparison.OrdinalIgnoreCase))
                    return active;
                if (string.Equals(dto.State, "overdue", StringComparison.OrdinalIgnoreCase))
                    return late1;
            }

            return active;
        }

        private static ContractStatusEntity GetStatus(
            IReadOnlyDictionary<string, ContractStatusEntity> dict,
            string name,
            ContractStatus fallback)
        {
            if (dict.TryGetValue(name, out var entity) && entity != null)
            {
                return entity;
            }

            return new ContractStatusEntity
            {
                Id = (int)fallback,
                Name = name
            };
        }
    }
}
