using System.ComponentModel.DataAnnotations;
using SalesApp.Attributes;
using Xunit;

namespace SalesApp.Tests.Attributes
{
    public class ValidContractStatusAttributeTests
    {
        private ValidContractStatusAttribute _attribute;

        public ValidContractStatusAttributeTests()
        {
            _attribute = new ValidContractStatusAttribute();
        }

        [Theory]
        [InlineData("Active")]
        [InlineData("Late1")]
        [InlineData("Late2")]
        [InlineData("Late3")]
        [InlineData("Defaulted")]
        public void IsValid_ValidStatus_ReturnsSuccess(string status)
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "Status" };

            // Act
            var result = _attribute.GetValidationResult(status, validationContext);

            // Assert
            Assert.Equal(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData("ACTIVE")]
        [InlineData("active")]
        [InlineData("LaTe1")]
        [InlineData("DEFAULTED")]
        public void IsValid_ValidStatusDifferentCasing_ReturnsSuccess(string status)
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "Status" };

            // Act
            var result = _attribute.GetValidationResult(status, validationContext);

            // Assert
            Assert.Equal(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData("Pending")]
        [InlineData("Cancelled")]
        [InlineData("Completed")]
        [InlineData("Invalid")]
        [InlineData("Late4")]
        public void IsValid_InvalidStatus_ReturnsError(string status)
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "Status" };

            // Act
            var result = _attribute.GetValidationResult(status, validationContext);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(ValidationResult.Success, result);
            Assert.Contains("Status must be one of:", result.ErrorMessage);
        }

        [Fact]
        public void IsValid_NullValue_ReturnsSuccess()
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "Status" };

            // Act
            var result = _attribute.GetValidationResult(null, validationContext);

            // Assert
            Assert.Equal(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void IsValid_EmptyOrWhitespace_ReturnsSuccess(string status)
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "Status" };

            // Act
            var result = _attribute.GetValidationResult(status, validationContext);

            // Assert
            Assert.Equal(ValidationResult.Success, result);
        }

        [Fact]
        public void IsValid_ErrorMessage_ContainsAllValidStatuses()
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "Status" };

            // Act
            var result = _attribute.GetValidationResult("Invalid", validationContext);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Active", result.ErrorMessage);
            Assert.Contains("Late1", result.ErrorMessage);
            Assert.Contains("Late2", result.ErrorMessage);
            Assert.Contains("Late3", result.ErrorMessage);
            Assert.Contains("Defaulted", result.ErrorMessage);
        }
    }
}
