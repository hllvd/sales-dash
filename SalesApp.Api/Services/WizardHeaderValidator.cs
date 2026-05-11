using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SalesApp.Services
{
    public class WizardHeaderValidator : IWizardHeaderValidator
    {
        private readonly Dictionary<string, List<string>> _headerAliases;

        public WizardHeaderValidator(IConfiguration configuration)
        {
            _headerAliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            
            // Try to load from configuration
            var section = configuration.GetSection("WizardHeaderAliases");
            if (section.Exists())
            {
                foreach (var child in section.GetChildren())
                {
                    var aliases = child.Get<List<string>>() ?? new List<string>();
                    if (!aliases.Contains(child.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        aliases.Add(child.Key);
                    }
                    _headerAliases[child.Key] = aliases;
                }
            }
            
            // Fallback to defaults if configuration is missing or incomplete
            EnsureDefaultAlias("Contrato", "Contract");
            EnsureDefaultAlias("Código PV", "Codigo PV", "PV Code", "Cod PV");
            EnsureDefaultAlias("PV", "Ponto de Venda", "Nome PV");
            EnsureDefaultAlias("Matrícula", "Matricula", "Enrollment", "Mat", "ID");
            EnsureDefaultAlias("Comissionado", "Comissionada", "Consultor", "Consultar", "Vendedor", "Seller", "Vendedor(a)", "Consultor(a)", "Usuário", "Usuario");
            EnsureDefaultAlias("Grupo", "Group", "Equipe");
            EnsureDefaultAlias("Cota", "Quota");
            EnsureDefaultAlias("Data da Venda", "Data Venda", "Sale Date");
            EnsureDefaultAlias("Valor", "Value", "Amount");
            EnsureDefaultAlias("Nome do Cliente", "Nome Cliente", "Customer Name", "Cliente");
            EnsureDefaultAlias("Tipo", "Type");
            EnsureDefaultAlias("Status", "State", "Estado");
        }

        private void EnsureDefaultAlias(string key, params string[] defaults)
        {
            if (!_headerAliases.ContainsKey(key))
            {
                var list = new List<string> { key };
                list.AddRange(defaults);
                _headerAliases[key] = list;
            }
        }

        public HeaderValidationResult Validate(IEnumerable<string> columns)
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
}
