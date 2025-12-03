namespace SalesApp.Services
{
    public class AutoMappingService : IAutoMappingService
    {
        // Define mapping rules for different entity types
        private readonly Dictionary<string, Dictionary<string, string[]>> _mappingRules = new()
        {
            ["Contract"] = new Dictionary<string, string[]>
            {
                ["ContractNumber"] = new[] { "contract number", "contract_number", "contractnumber", "number", "contract #", "contract#", "contrato" },
                ["UserEmail"] = new[] { "user email", "useremail", "user_email", "email", "client email", "customer email", "e-mail" },
                ["TotalAmount"] = new[] { "total amount", "totalamount", "total_amount", "amount", "value", "price", "valor" },
                ["GroupId"] = new[] { "group id", "groupid", "group_id", "group", "team id", "teamid" },
                ["Status"] = new[] { "status", "state", "contract status", "contract_status" },
                ["SaleStartDate"] = new[] { "start date", "startdate", "start_date", "sale start", "contract start", "begin date", "data da venda", "data venda" },
                ["SaleEndDate"] = new[] { "end date", "enddate", "end_date", "sale end", "contract end", "finish date" },
                ["PvId"] = new[] { "pv id", "pvid", "pv_id", "pv", "point of sale", "codigo pv", "código pv" },
                ["Quota"] = new[] { "quota", "cota" },
                ["ContractType"] = new[] { "contract type", "contracttype", "contract_type", "type", "tipo" },
                ["CustomerName"] = new[] { "customer name", "customername", "customer_name", "client name", "clientname", "nome do cliente", "nome cliente", "cliente" }
            },
            ["User"] = new Dictionary<string, string[]>
            {
                ["Name"] = new[] { "name", "first name", "firstname", "user name", "username" },
                ["Email"] = new[] { "email", "e-mail", "email address", "mail" },
                ["RoleId"] = new[] { "role id", "roleid", "role_id", "role" }
            }
        };

        public Dictionary<string, string> SuggestMappings(List<string> sourceColumns, string entityType, List<string>? templateFields = null)
        {
            var mappings = new Dictionary<string, string>();
            
            // First priority: Exact case-insensitive matching with template fields
            if (templateFields != null && templateFields.Any())
            {
                foreach (var sourceColumn in sourceColumns)
                {
                    var exactMatch = templateFields.FirstOrDefault(tf => 
                        string.Equals(tf, sourceColumn, StringComparison.OrdinalIgnoreCase));
                    
                    if (exactMatch != null)
                    {
                        mappings[sourceColumn] = exactMatch;
                    }
                }
            }
            
            // Second priority: Pattern matching for unmapped columns
            if (!_mappingRules.ContainsKey(entityType))
            {
                return mappings;
            }

            var rules = _mappingRules[entityType];
            
            foreach (var sourceColumn in sourceColumns)
            {
                // Skip if already mapped by exact match
                if (mappings.ContainsKey(sourceColumn))
                {
                    continue;
                }
                
                var normalizedSource = NormalizeColumnName(sourceColumn);
                
                foreach (var (targetField, patterns) in rules)
                {
                    if (patterns.Any(pattern => normalizedSource.Contains(NormalizeColumnName(pattern))))
                    {
                        mappings[sourceColumn] = targetField;
                        break;
                    }
                }
            }

            return mappings;
        }

        public Dictionary<string, string> ApplyTemplateMappings(Dictionary<string, string> templateMappings, List<string> sourceColumns)
        {
            var mappings = new Dictionary<string, string>();
            
            foreach (var sourceColumn in sourceColumns)
            {
                // Try exact match first
                if (templateMappings.ContainsKey(sourceColumn))
                {
                    mappings[sourceColumn] = templateMappings[sourceColumn];
                    continue;
                }
                
                // Try case-insensitive match
                var matchingKey = templateMappings.Keys.FirstOrDefault(k => 
                    string.Equals(k, sourceColumn, StringComparison.OrdinalIgnoreCase));
                    
                if (matchingKey != null)
                {
                    mappings[sourceColumn] = templateMappings[matchingKey];
                }
            }

            return mappings;
        }

        private string NormalizeColumnName(string columnName)
        {
            return columnName
                .ToLowerInvariant()
                .Replace("_", " ")
                .Replace("-", " ")
                .Trim();
        }
    }
}
