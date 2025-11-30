using SalesApp.Services;
using Xunit;
using FluentAssertions;

namespace SalesApp.Tests.Services
{
    public class AutoMappingServiceTests
    {
        private readonly AutoMappingService _service;

        public AutoMappingServiceTests()
        {
            _service = new AutoMappingService();
        }

        #region Exact Matching Tests

        [Fact]
        public void SuggestMappings_ExactMatch_CaseInsensitive_ShouldMap()
        {
            // Arrange
            var sourceColumns = new List<string> { "Name", "EMAIL", "surname" };
            var templateFields = new List<string> { "Name", "Email", "Surname" };

            // Act
            var result = _service.SuggestMappings(sourceColumns, "User", templateFields);

            // Assert
            result.Should().ContainKey("Name");
            result["Name"].Should().Be("Name");
            result.Should().ContainKey("EMAIL");
            result["EMAIL"].Should().Be("Email");
            result.Should().ContainKey("surname");
            result["surname"].Should().Be("Surname");
        }

        [Fact]
        public void SuggestMappings_ExactMatch_WithTemplateFields_ShouldPrioritizeExactMatch()
        {
            // Arrange
            var sourceColumns = new List<string> { "Matricula", "IsMatriculaOwner" };
            var templateFields = new List<string> { "Matricula", "IsMatriculaOwner" };

            // Act
            var result = _service.SuggestMappings(sourceColumns, "User", templateFields);

            // Assert
            result.Should().ContainKey("Matricula");
            result["Matricula"].Should().Be("Matricula");
            result.Should().ContainKey("IsMatriculaOwner");
            result["IsMatriculaOwner"].Should().Be("IsMatriculaOwner");
        }

        #endregion

        #region Contract Pattern Matching Tests

        [Fact]
        public void SuggestMappings_ContractNumber_VariousPatterns_ShouldMap()
        {
            // Arrange
            var testCases = new[]
            {
                "contract number",
                "contract_number",
                "contractnumber",
                "Contract #",
                "contract#",
                "number"
            };

            foreach (var columnName in testCases)
            {
                var sourceColumns = new List<string> { columnName };

                // Act
                var result = _service.SuggestMappings(sourceColumns, "Contract");

                // Assert
                result.Should().ContainKey(columnName, $"because '{columnName}' should map to ContractNumber");
                result[columnName].Should().Be("ContractNumber");
            }
        }

        [Fact]
        public void SuggestMappings_UserEmail_VariousPatterns_ShouldMap()
        {
            // Arrange
            var testCases = new[]
            {
                "user email",
                "useremail",
                "user_email",
                "email",
                "client email",
                "customer email",
                "e-mail"
            };

            foreach (var columnName in testCases)
            {
                var sourceColumns = new List<string> { columnName };

                // Act
                var result = _service.SuggestMappings(sourceColumns, "Contract");

                // Assert
                result.Should().ContainKey(columnName, $"because '{columnName}' should map to UserEmail");
                result[columnName].Should().Be("UserEmail");
            }
        }

        [Fact]
        public void SuggestMappings_TotalAmount_VariousPatterns_ShouldMap()
        {
            // Arrange
            var testCases = new[]
            {
                "total amount",
                "totalamount",
                "total_amount",
                "amount",
                "total",
                "value",
                "price"
            };

            foreach (var columnName in testCases)
            {
                var sourceColumns = new List<string> { columnName };

                // Act
                var result = _service.SuggestMappings(sourceColumns, "Contract");

                // Assert
                result.Should().ContainKey(columnName, $"because '{columnName}' should map to TotalAmount");
                result[columnName].Should().Be("TotalAmount");
            }
        }

        [Fact]
        public void SuggestMappings_GroupId_VariousPatterns_ShouldMap()
        {
            // Arrange
            var testCases = new[]
            {
                "group id",
                "groupid",
                "group_id",
                "group",
                "team id",
                "teamid"
            };

            foreach (var columnName in testCases)
            {
                var sourceColumns = new List<string> { columnName };

                // Act
                var result = _service.SuggestMappings(sourceColumns, "Contract");

                // Assert
                result.Should().ContainKey(columnName, $"because '{columnName}' should map to GroupId");
                result[columnName].Should().Be("GroupId");
            }
        }

        [Fact]
        public void SuggestMappings_Dates_VariousPatterns_ShouldMap()
        {
            // Arrange - Start Date
            var startDateColumns = new[] { "start date", "startdate", "start_date", "sale start", "contract start", "begin date" };
            foreach (var columnName in startDateColumns)
            {
                var sourceColumns = new List<string> { columnName };

                // Act
                var result = _service.SuggestMappings(sourceColumns, "Contract");

                // Assert
                result.Should().ContainKey(columnName);
                result[columnName].Should().Be("SaleStartDate");
            }

            // Arrange - End Date
            var endDateColumns = new[] { "end date", "enddate", "end_date", "sale end", "contract end", "finish date" };
            foreach (var columnName in endDateColumns)
            {
                var sourceColumns = new List<string> { columnName };

                // Act
                var result = _service.SuggestMappings(sourceColumns, "Contract");

                // Assert
                result.Should().ContainKey(columnName);
                result[columnName].Should().Be("SaleEndDate");
            }
        }

        [Fact]
        public void SuggestMappings_AllContractFields_ShouldMapCorrectly()
        {
            // Arrange
            var sourceColumns = new List<string>
            {
                "contract_number",
                "client email",
                "total",
                "group",
                "status",
                "sale start",
                "sale end"
            };

            // Act
            var result = _service.SuggestMappings(sourceColumns, "Contract");

            // Assert
            result.Should().HaveCount(7);
            result["contract_number"].Should().Be("ContractNumber");
            result["client email"].Should().Be("UserEmail");
            result["total"].Should().Be("TotalAmount");
            result["group"].Should().Be("GroupId");
            result["status"].Should().Be("Status");
            result["sale start"].Should().Be("SaleStartDate");
            result["sale end"].Should().Be("SaleEndDate");
        }

        #endregion

        #region User Pattern Matching Tests

        [Fact]
        public void SuggestMappings_UserFields_ShouldMapCorrectly()
        {
            // Arrange
            var sourceColumns = new List<string> { "first name", "email address", "role" };

            // Act
            var result = _service.SuggestMappings(sourceColumns, "User");

            // Assert
            result.Should().HaveCount(3);
            result["first name"].Should().Be("Name");
            result["email address"].Should().Be("Email");
            result["role"].Should().Be("RoleId");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void SuggestMappings_UnknownEntityType_ShouldReturnOnlyExactMatches()
        {
            // Arrange
            var sourceColumns = new List<string> { "Name", "Unknown Column" };
            var templateFields = new List<string> { "Name" };

            // Act
            var result = _service.SuggestMappings(sourceColumns, "UnknownEntity", templateFields);

            // Assert
            result.Should().HaveCount(1);
            result.Should().ContainKey("Name");
            result["Name"].Should().Be("Name");
        }

        [Fact]
        public void SuggestMappings_NoMatches_ShouldReturnEmpty()
        {
            // Arrange
            var sourceColumns = new List<string> { "xyz", "abc", "def" };

            // Act
            var result = _service.SuggestMappings(sourceColumns, "Contract");

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void SuggestMappings_EmptyColumns_ShouldReturnEmpty()
        {
            // Arrange
            var sourceColumns = new List<string>();

            // Act
            var result = _service.SuggestMappings(sourceColumns, "Contract");

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void SuggestMappings_ExactMatchTakesPriority_OverPatternMatch()
        {
            // Arrange
            var sourceColumns = new List<string> { "Name" };
            var templateFields = new List<string> { "Name", "UserName" };

            // Act
            var result = _service.SuggestMappings(sourceColumns, "Contract", templateFields);

            // Assert
            result.Should().HaveCount(1);
            result["Name"].Should().Be("Name", "exact match should take priority over pattern match");
        }

        #endregion

        #region ApplyTemplateMappings Tests

        [Fact]
        public void ApplyTemplateMappings_ExactMatch_ShouldApply()
        {
            // Arrange
            var templateMappings = new Dictionary<string, string>
            {
                { "col1", "Field1" },
                { "col2", "Field2" }
            };
            var sourceColumns = new List<string> { "col1", "col2" };

            // Act
            var result = _service.ApplyTemplateMappings(templateMappings, sourceColumns);

            // Assert
            result.Should().HaveCount(2);
            result["col1"].Should().Be("Field1");
            result["col2"].Should().Be("Field2");
        }

        [Fact]
        public void ApplyTemplateMappings_CaseInsensitive_ShouldApply()
        {
            // Arrange
            var templateMappings = new Dictionary<string, string>
            {
                { "Name", "TargetName" },
                { "Email", "TargetEmail" }
            };
            var sourceColumns = new List<string> { "name", "EMAIL" };

            // Act
            var result = _service.ApplyTemplateMappings(templateMappings, sourceColumns);

            // Assert
            result.Should().HaveCount(2);
            result["name"].Should().Be("TargetName");
            result["EMAIL"].Should().Be("TargetEmail");
        }

        [Fact]
        public void ApplyTemplateMappings_NoMatch_ShouldNotMap()
        {
            // Arrange
            var templateMappings = new Dictionary<string, string>
            {
                { "col1", "Field1" }
            };
            var sourceColumns = new List<string> { "col2", "col3" };

            // Act
            var result = _service.ApplyTemplateMappings(templateMappings, sourceColumns);

            // Assert
            result.Should().BeEmpty();
        }

        #endregion
    }
}
