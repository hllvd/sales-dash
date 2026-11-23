using System;
using System.Linq;
using SalesApp.DTOs;
using SalesApp.Models;

namespace SalesApp.Utils
{
    public static class ContractMappingHelper
    {
        public static ContractResponse MapToContractResponse(Contract contract, bool treatUnpaidAsAwaiting = false)
        {
            var matriculaNumber = contract.Matricula?.MatriculaNumber 
                ?? contract.TempMatricula
                ?? contract.User?.UserMatriculas?.FirstOrDefault(um => um.IsActive && um.IsOwner)?.Matricula?.MatriculaNumber
                ?? contract.User?.UserMatriculas?.FirstOrDefault(um => um.IsActive)?.Matricula?.MatriculaNumber;

            matriculaNumber = NormalizationUtils.NormalizeNumber(matriculaNumber);
            if (string.IsNullOrWhiteSpace(matriculaNumber))
            {
                matriculaNumber = null;
            }

            var statusName = contract.ContractStatus?.Name ?? "";
            var isAwaitingPayment = statusName.Equals("Active", StringComparison.OrdinalIgnoreCase) && contract.HasPayment == false;
            var isRemapped = treatUnpaidAsAwaiting && isAwaitingPayment;
            var finalStatus = isRemapped ? "AwaitingPayment" : statusName;

            var contractUser = contract.User 
                ?? contract.Matricula?.UserMatriculas?.FirstOrDefault(um => um.IsActive && um.IsOwner)?.User 
                ?? contract.Matricula?.UserMatriculas?.FirstOrDefault(um => um.IsActive)?.User;

            var contractDate = contract.SaleStartDate.Date;
            var teamName = contractUser?.UserTeams?
                .Where(ut => contractDate >= ut.StartDate.Date && (ut.EndDate == null || contractDate <= ut.EndDate.Value.Date))
                .OrderByDescending(ut => ut.StartDate)
                .Select(ut => ut.Team?.Name)
                .FirstOrDefault(t => !string.IsNullOrEmpty(t));

            return new ContractResponse
            {
                Id = contract.Id,
                ContractNumber = contract.ContractNumber,
                UserId = contract.User?.Id,
                UserName = contract.User?.Name ?? "",
                TotalAmount = contract.TotalAmount,
                GroupId = contract.GroupId,
                GroupName = contract.Group?.Name ?? "",
                Status = finalStatus,
                ContractStartDate = contract.SaleStartDate,
                IsActive = contract.IsActive,
                CreatedAt = contract.CreatedAt,
                UpdatedAt = contract.UpdatedAt,
                Segment = contract.Group?.Segment,
                SegmentName = SegmentExtensions.ToDisplayName(contract.Group?.Segment),
                Quota = contract.Quota,
                PvId = contract.PvId,
                CustomerName = contract.CustomerName,
                MatriculaId = contract.MatriculaId,
                MatriculaNumber = matriculaNumber,
                RawStatus = contract.RawStatus,
                HasPayment = contract.HasPayment,
                IsAwaitingPayment = isAwaitingPayment,
                IsRemappedToAwaitingPayment = isRemapped,
                TeamName = teamName
            };
        }
    }
}
