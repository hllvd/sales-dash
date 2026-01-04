using System.ComponentModel.DataAnnotations;
using SalesApp.Attributes;
using Xunit;

namespace SalesApp.Tests.Attributes
{
    public class ValidateSQLInjectionAttributeTests
    {
        private ValidateSQLInjectionAttribute _attribute;

        public ValidateSQLInjectionAttributeTests()
        {
            _attribute = new ValidateSQLInjectionAttribute();
        }

        [Theory]
        [InlineData("Normal text")]
        [InlineData("Text with numbers 123")]
        [InlineData("Contract-123")]
        [InlineData("User Name")]
        [InlineData("SELECT a product")] // "SELECT" as normal word, not SQL
        [InlineData("Update your profile")] // "UPDATE" as normal word
        public void IsValid_SafeText_ReturnsSuccess(string input)
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "TestField" };

            // Act
            var result = _attribute.GetValidationResult(input, validationContext);

            // Assert
            Assert.Equal(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData("' OR '1'='1")]
        [InlineData("admin'--")]
        [InlineData("'; DROP TABLE users--")]
        [InlineData("1' OR 1=1--")]
        [InlineData("UNION SELECT * FROM users")]
        [InlineData("DELETE FROM contracts")]
        [InlineData("INSERT INTO users VALUES")]
        [InlineData("UPDATE users SET password")]
        [InlineData("EXEC(malicious)")]
        [InlineData("EXECUTE(sp_executesql)")]
        [InlineData("/* comment */")]
        public void IsValid_SQLInjectionAttempt_ReturnsError(string input)
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "TestField" };

            // Act
            var result = _attribute.GetValidationResult(input, validationContext);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(ValidationResult.Success, result);
            Assert.Contains("SQL patterns", result.ErrorMessage);
        }

        [Fact]
        public void IsValid_NullValue_ReturnsSuccess()
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "TestField" };

            // Act
            var result = _attribute.GetValidationResult(null, validationContext);

            // Assert
            Assert.Equal(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void IsValid_EmptyOrWhitespace_ReturnsSuccess(string input)
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "TestField" };

            // Act
            var result = _attribute.GetValidationResult(input, validationContext);

            // Assert
            Assert.Equal(ValidationResult.Success, result);
        }
    }
}
