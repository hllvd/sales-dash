namespace SalesApp.Services
{
    public class WizardHeaderValidator
    {
        private static readonly Dictionary<string, List<string>> _headerAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Contrato"] = new() { "Contrato", "Contract" },
            ["Código PV"] = new() { "Código PV", "Codigo PV", "PV Code", "Cod PV" },
            ["PV"] = new() { "PV", "Ponto de Venda", "Nome PV" },
            ["Matrícula"] = new() { "Matrícula", "Matricula", "Enrollment", "Mat", "ID" },
            ["Comissionado"] = new() { "Comissionado", "Comissionada", "Consultor", "Consultar", "Vendedor", "Seller", "Vendedor(a)", "Consultor(a)", "Usuário", "Usuario" },
            ["Grupo"] = new() { "Grupo", "Group", "Equipe" },
            ["Cota"] = new() { "Cota", "Quota" },
            ["Data da Venda"] = new() { "Data da Venda", "Data Venda", "Sale Date" },
            ["Valor"] = new() { "Valor", "Value", "Amount" },
            ["Nome do Cliente"] = new() { "Nome do Cliente", "Nome Cliente", "Customer Name", "Cliente" },
            ["Tipo"] = new() { "Tipo", "Type" },
            ["Status"] = new() { "Status", "State","Estado" }
        };

        public static HeaderValidationResult Validate(IEnumerable<string> columns)
        {
            var detectedColumns = columns.Select(c => c.Trim()).ToList();
            var missingHeaders = new List<string>();

            foreach (var expected in _headerAliases.Keys)
            {
                var aliases = _headerAliases[expected];
                bool found = aliases.Any(alias => 
                    detectedColumns.Any(col => string.Equals(col, alias, StringComparison.OrdinalIgnoreCase)));

                if (!found)
                {
                    missingHeaders.Add(expected);
                }
            }

            return new HeaderValidationResult
            {
                IsValid = !missingHeaders.Any(),
                MissingHeaders = missingHeaders,
                ExpectedHeaders = _headerAliases.Keys.ToList()
            };
        }
    }

    public class HeaderValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> MissingHeaders { get; set; } = new();
        public List<string> ExpectedHeaders { get; set; } = new();
    }
}
