using System.ComponentModel.DataAnnotations;
using SalesApp.Attributes;
using Xunit;

namespace SalesApp.Tests.Attributes
{
    public class ValidUserNameAttributeTests
    {
        private ValidUserNameAttribute _attribute;

        public ValidUserNameAttributeTests()
        {
            _attribute = new ValidUserNameAttribute();
        }

        [Theory]
        [InlineData("John Doe")]
        [InlineData("Mary-Jane")]
        [InlineData("O'Brien")]
        [InlineData("José García")]
        [InlineData("François Müller")]
        [InlineData("Anne-Marie")]
        [InlineData("D'Angelo")]
        public void IsValid_ValidName_ReturnsSuccess(string name)
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "Name" };

            // Act
            var result = _attribute.GetValidationResult(name, validationContext);

            // Assert
            Assert.Equal(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData("John123")]
        [InlineData("User@Domain")]
        [InlineData("Name<script>")]
        [InlineData("Test_User")]
        [InlineData("Name.Surname")]
        [InlineData("User#1")]
        public void IsValid_InvalidCharacters_ReturnsError(string name)
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "Name" };

            // Act
            var result = _attribute.GetValidationResult(name, validationContext);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(ValidationResult.Success, result);
            Assert.Contains("letters, spaces, hyphens, and apostrophes", result.ErrorMessage);
        }

        [Fact]
        public void IsValid_NullValue_ReturnsSuccess()
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "Name" };

            // Act
            var result = _attribute.GetValidationResult(null, validationContext);

            // Assert
            Assert.Equal(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void IsValid_EmptyOrWhitespace_ReturnsSuccess(string name)
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "Name" };

            // Act
            var result = _attribute.GetValidationResult(name, validationContext);

            // Assert
            Assert.Equal(ValidationResult.Success, result);
        }
    }
}
