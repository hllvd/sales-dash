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
                ContractType = source.ContractType,
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
                Group = source.Group
            };
        }

        public static IReadOnlyList<Contract> ApplyOverrides(
            IEnumerable<Contract> contracts,
            IEnumerable<ContractRtOverrideDto>? overrides,
            ContractStatusEntity activeStatus,
            ContractStatusEntity late1Status)
        {
            if (contracts == null) return Array.Empty<Contract>();

            var overrideMap = (overrides ?? Enumerable.Empty<ContractRtOverrideDto>())
                .Where(o => !string.IsNullOrWhiteSpace(o.ContractNumber) && !string.IsNullOrWhiteSpace(o.State))
                .GroupBy(o => o.ContractNumber.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last().State.Trim(), StringComparer.OrdinalIgnoreCase);

            var result = new List<Contract>();
            foreach (var contract in contracts)
            {
                var clone = Clone(contract);
                if (overrideMap.TryGetValue(contract.ContractNumber, out var state))
                {
                    if (string.Equals(state, "upToDate", StringComparison.OrdinalIgnoreCase))
                    {
                        clone.ContractStatus = activeStatus;
                        clone.ContractStatusId = activeStatus.Id;
                    }
                    else if (string.Equals(state, "overdue", StringComparison.OrdinalIgnoreCase))
                    {
                        clone.ContractStatus = late1Status;
                        clone.ContractStatusId = late1Status.Id;
                    }
                }
                result.Add(clone);
            }

            return result;
        }
    }
}
