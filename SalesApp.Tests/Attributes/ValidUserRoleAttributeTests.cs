using System.ComponentModel.DataAnnotations;
using SalesApp.Attributes;
using SalesApp.Models;
using Xunit;

namespace SalesApp.Tests.Attributes
{
    public class ValidUserRoleAttributeTests
    {
        private ValidUserRoleAttribute _attribute;

        public ValidUserRoleAttributeTests()
        {
            _attribute = new ValidUserRoleAttribute();
        }

        [Theory]
        [InlineData("user")]
        [InlineData("admin")]
        [InlineData("superadmin")]
        public void IsValid_ValidRole_ReturnsSuccess(string role)
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "Role" };

            // Act
            var result = _attribute.GetValidationResult(role, validationContext);

            // Assert
            Assert.Equal(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData("USER")]
        [InlineData("Admin")]
        [InlineData("SUPERADMIN")]
        [InlineData("SuPeRaDmIn")]
        public void IsValid_ValidRoleDifferentCasing_ReturnsSuccess(string role)
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "Role" };

            // Act
            var result = _attribute.GetValidationResult(role, validationContext);

            // Assert
            Assert.Equal(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData("hacker")]
        [InlineData("superuser")]
        [InlineData("root")]
        [InlineData("moderator")]
        [InlineData("guest")]
        [InlineData("invalid")]
        public void IsValid_InvalidRole_ReturnsError(string role)
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "Role" };

            // Act
            var result = _attribute.GetValidationResult(role, validationContext);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(ValidationResult.Success, result);
            Assert.Contains("Role must be one of:", result.ErrorMessage);
        }

        [Fact]
        public void IsValid_NullValue_ReturnsSuccess()
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "Role" };

            // Act
            var result = _attribute.GetValidationResult(null, validationContext);

            // Assert
            Assert.Equal(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void IsValid_EmptyOrWhitespace_ReturnsSuccess(string role)
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "Role" };

            // Act
            var result = _attribute.GetValidationResult(role, validationContext);

            // Assert
            Assert.Equal(ValidationResult.Success, result);
        }

        [Fact]
        public void IsValid_ErrorMessage_ContainsAllValidRoles()
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "Role" };

            // Act
            var result = _attribute.GetValidationResult("invalid", validationContext);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("user", result.ErrorMessage);
            Assert.Contains("admin", result.ErrorMessage);
            Assert.Contains("superadmin", result.ErrorMessage);
        }
    }
}
