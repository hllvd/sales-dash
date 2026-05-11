using SalesApp.ReportFilters.DTOs;

namespace SalesApp.ReportFilters.Validators
{
    /// <summary>
    /// Structured validation error to be included in 400 responses.
    /// </summary>
    public record ValidationError(string Field, string Message);

    /// <summary>
    /// Shared validation logic for report filter request DTOs.
    /// All methods are pure functions — no side effects, no DI required.
    /// </summary>
    public static class ReportFilterValidationRules
    {
        private static readonly HashSet<string> ValidScopes = new(StringComparer.OrdinalIgnoreCase)
        {
            "private", "shared"
        };

        private static readonly HashSet<string> ValidSources = new(StringComparer.OrdinalIgnoreCase)
        {
            "Contracts", "Users_Contract", "Users_Matricula", "Status", "PV", "Group"
        };

        public static List<ValidationError> ValidateName(string? name)
        {
            var errors = new List<ValidationError>();
            if (string.IsNullOrWhiteSpace(name))
                errors.Add(new("name", "Name is required."));
            else if (name.Length > 100)
                errors.Add(new("name", "Name must be 100 characters or fewer."));
            return errors;
        }

        public static List<ValidationError> ValidateDescription(string? description)
        {
            var errors = new List<ValidationError>();
            if (description != null && description.Length > 500)
                errors.Add(new("description", "Description must be 500 characters or fewer."));
            return errors;
        }

        public static List<ValidationError> ValidateScope(string? scope)
        {
            var errors = new List<ValidationError>();
            if (string.IsNullOrWhiteSpace(scope))
                errors.Add(new("scope", "Scope is required."));
            else if (!ValidScopes.Contains(scope))
                errors.Add(new("scope", "Scope must be 'private' or 'shared'."));
            return errors;
        }

        public static List<ValidationError> ValidateFilterConfig(FilterConfigRequest? config)
        {
            var errors = new List<ValidationError>();

            if (config == null)
            {
                errors.Add(new("filterConfig", "filterConfig is required and must be a non-empty object."));
                return errors;
            }

            var hasAtLeastOneFilter =
                (config.Matriculas?.Count > 0) ||
                config.StartDate.HasValue ||
                config.EndDate.HasValue ||
                config.CurrentUserAsParent.HasValue ||
                (config.Emails?.Count > 0) ||
                (config.Groups?.Count > 0) ||
                (config.Pvs?.Count > 0);

            if (!hasAtLeastOneFilter)
                errors.Add(new("filterConfig", "filterConfig must contain at least one filter field."));

            if (config.StartDate.HasValue && config.EndDate.HasValue &&
                config.StartDate.Value > config.EndDate.Value)
                errors.Add(new("filterConfig.startDate", "startDate must be before endDate."));

            if (config.Matriculas != null && config.Matriculas.Count == 0)
                errors.Add(new("filterConfig.matriculas", "matriculas must be a non-empty array if provided."));

            if (config.Emails != null && config.Emails.Count == 0)
                errors.Add(new("filterConfig.emails", "emails must be a non-empty array if provided."));

            if (config.Groups != null && config.Groups.Count == 0)
                errors.Add(new("filterConfig.groups", "groups must be a non-empty array if provided."));

            if (config.Pvs != null && config.Pvs.Count == 0)
                errors.Add(new("filterConfig.pvs", "pvs must be a non-empty array if provided."));

            return errors;
        }

        public static List<ValidationError> ValidateOutputColumns(List<OutputColumnRequest>? columns)
        {
            var errors = new List<ValidationError>();

            if (columns == null || columns.Count == 0)
            {
                errors.Add(new("outputColumns", "outputColumns must contain at least one entry."));
                return errors;
            }

            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                var prefix = $"outputColumns[{i}]";

                if (string.IsNullOrWhiteSpace(col.Source))
                    errors.Add(new($"{prefix}.source", "source is required."));
                else if (!ValidSources.Contains(col.Source))
                    errors.Add(new($"{prefix}.source", $"source must be one of: {string.Join(", ", ValidSources)}."));

                if (string.IsNullOrWhiteSpace(col.Field))
                    errors.Add(new($"{prefix}.field", "field is required."));

                if (string.IsNullOrWhiteSpace(col.Label))
                    errors.Add(new($"{prefix}.label", "label is required."));
            }

            // Validate order values are unique and sequential starting at 1
            var orders = columns.Select(c => c.Order).OrderBy(o => o).ToList();
            var expectedOrders = Enumerable.Range(1, columns.Count).ToList();
            if (!orders.SequenceEqual(expectedOrders))
                errors.Add(new("outputColumns", "order values must be unique and sequential starting at 1."));

            return errors;
        }

        /// <summary>
        /// Validates all fields of a CreateReportFilterRequest.
        /// Returns an empty list if valid.
        /// </summary>
        public static List<ValidationError> Validate(CreateReportFilterRequest request)
        {
            var errors = new List<ValidationError>();
            errors.AddRange(ValidateName(request.Name));
            errors.AddRange(ValidateDescription(request.Description));
            errors.AddRange(ValidateScope(request.Scope));
            errors.AddRange(ValidateFilterConfig(request.FilterConfig));
            errors.AddRange(ValidateOutputColumns(request.OutputColumns));
            return errors;
        }

        /// <summary>
        /// Validates all fields of an UpdateReportFilterRequest.
        /// Returns an empty list if valid.
        /// </summary>
        public static List<ValidationError> Validate(UpdateReportFilterRequest request)
        {
            var errors = new List<ValidationError>();
            errors.AddRange(ValidateName(request.Name));
            errors.AddRange(ValidateDescription(request.Description));
            errors.AddRange(ValidateScope(request.Scope));
            errors.AddRange(ValidateFilterConfig(request.FilterConfig));
            errors.AddRange(ValidateOutputColumns(request.OutputColumns));
            return errors;
        }
    }
}
