using System;
using System.Collections.Generic;
using System.Linq;
using SalesApp.Models;

namespace SalesApp.Services
{
    /// <summary>
    /// Pure, deterministic service for evaluating and deriving contract status
    /// from Consultor report raw columns (Definição.Situação, Quant.Parcelas em Atraso,
    /// Quant.Parcelas Pagas, Últ.Pagto Parcela, Data.Venda).
    /// </summary>
    public static class ConsultorStatusEvaluator
    {
        /// <summary>
        /// Checks whether the given row belongs to a Consultor report.
        /// </summary>
        public static bool IsConsultorRow(Dictionary<string, string>? row)
        {
            if (row == null || row.Count == 0) return false;

            return row.Keys.Any(k =>
                k.Contains("2 Rel Carteira.Definição.Situação", StringComparison.OrdinalIgnoreCase) ||
                k.Contains("Definição.Situação", StringComparison.OrdinalIgnoreCase) ||
                k.Contains("Quant.Parcelas em Atraso", StringComparison.OrdinalIgnoreCase) ||
                k.Contains("Quant.Parcelas Pagas", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Derives the canonical contract status for a Consultor report row.
        /// </summary>
        public static string DeriveStatus(
            string? rawStatus,
            int? overdueInstallments,
            int? paidInstallments,
            bool? hasPayment,
            DateTime? saleStartDate,
            DateTime referenceDate,
            Func<string?, string>? defaultStatusMapper = null)
        {
            if (string.IsNullOrWhiteSpace(rawStatus))
            {
                return defaultStatusMapper != null 
                    ? defaultStatusMapper(rawStatus) 
                    : ContractStatus.NaoDefinido.ToApiString();
            }

            var trimmedStatus = rawStatus.Trim();

            // 1. Excluído / Cancelado status handling
            if (IsExcluidoStatus(trimmedStatus))
            {
                // Desistente: Definição.Situação = Excluido E Quant.Parcelas Pagas = 0
                if (paidInstallments.HasValue && paidInstallments.Value == 0)
                {
                    return ContractStatus.Desistente.ToApiString();
                }

                // Defaulted: Definição.Situação = Excluido E Quant.Parcelas Pagas > 0 (or unspecified)
                return ContractStatus.Defaulted.ToApiString();
            }

            // 2. Normal / Active status handling
            if (IsNormalStatus(trimmedStatus))
            {
                // When there is no payment (Últ.Pagto Parcela is null, empty, "0" or hasPayment == false)
                if (hasPayment == false)
                {
                    // If purchase date is <= 30 days from reference date: AwaitingPayment
                    if (saleStartDate.HasValue && (referenceDate.Date - saleStartDate.Value.Date).TotalDays <= 30)
                    {
                        return ContractStatus.AwaitingPayment.ToApiString();
                    }

                    // If purchase date is > 30 days (or not provided), check overdue installments
                    if (overdueInstallments.HasValue)
                    {
                        if (overdueInstallments.Value >= 3) return ContractStatus.Late3.ToApiString();
                        if (overdueInstallments.Value == 2) return ContractStatus.Late2.ToApiString();
                        if (overdueInstallments.Value == 1) return ContractStatus.Late1.ToApiString();
                    }

                    return ContractStatus.AwaitingPayment.ToApiString();
                }

                // When there is payment (hasPayment == true or unknown/null)
                if (overdueInstallments.HasValue)
                {
                    if (overdueInstallments.Value >= 3) return ContractStatus.Late3.ToApiString();
                    if (overdueInstallments.Value == 2) return ContractStatus.Late2.ToApiString();
                    if (overdueInstallments.Value == 1) return ContractStatus.Late1.ToApiString();
                }

                return ContractStatus.Active.ToApiString();
            }

            // Fallback to default mapper if provided, or return rawStatus
            return defaultStatusMapper != null ? defaultStatusMapper(rawStatus) : rawStatus;
        }

        private static bool IsExcluidoStatus(string status)
        {
            return status.Equals("Excluido", StringComparison.OrdinalIgnoreCase) ||
                   status.Equals("Excluído", StringComparison.OrdinalIgnoreCase) ||
                   status.Equals("EXCLUIDO", StringComparison.OrdinalIgnoreCase) ||
                   status.Equals("EXCLUÍDO", StringComparison.OrdinalIgnoreCase) ||
                   status.Equals("Cancelado", StringComparison.OrdinalIgnoreCase) ||
                   status.Equals("Defaulted", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNormalStatus(string status)
        {
            return status.Equals("Normal", StringComparison.OrdinalIgnoreCase) ||
                   status.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
                   status.Equals("Ativo", StringComparison.OrdinalIgnoreCase) ||
                   status.Equals("Ativa", StringComparison.OrdinalIgnoreCase);
        }
    }
}
