using System.ComponentModel.DataAnnotations;
using SalesApp.Attributes;
using Xunit;

namespace SalesApp.Tests.Attributes
{
    public class ValidateXSSAttributeTests
    {
        private ValidateXSSAttribute _attribute;

        public ValidateXSSAttributeTests()
        {
            _attribute = new ValidateXSSAttribute();
        }

        [Theory]
        [InlineData("Normal text")]
        [InlineData("Text with numbers 123")]
        [InlineData("Text-with-hyphens")]
        [InlineData("Text_with_underscores")]
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
        [InlineData("<script>alert(1)</script>")]
        [InlineData("<img src=x onerror=alert(1)>")]
        [InlineData("Text with <b>HTML</b> tags")]
        [InlineData("javascript:alert(1)")]
        [InlineData("onclick=alert(1)")]
        [InlineData("onload=malicious()")]
        [InlineData("&lt;script&gt;")]
        [InlineData("&#60;script&#62;")]
        public void IsValid_XSSAttempt_ReturnsError(string input)
        {
            // Arrange
            var validationContext = new ValidationContext(new object()) { MemberName = "TestField" };

            // Act
            var result = _attribute.GetValidationResult(input, validationContext);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(ValidationResult.Success, result);
            Assert.Contains("dangerous content", result.ErrorMessage);
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
