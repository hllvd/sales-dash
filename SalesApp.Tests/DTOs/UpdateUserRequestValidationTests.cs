using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SalesApp.DTOs;
using Xunit;

namespace SalesApp.Tests.DTOs
{
    public class UpdateUserRequestValidationTests
    {
        private List<ValidationResult> ValidateModel(object model)
        {
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(model, null, null);
            Validator.TryValidateObject(model, validationContext, validationResults, true);
            return validationResults;
        }

        #region Name Validation Tests

        [Theory]
        [InlineData("John Doe")]
        [InlineData("José García")]
        [InlineData("O'Brien")]
        [InlineData(null)]
        public void Name_ValidName_PassesValidation(string? name)
        {
            // Arrange
            var request = new UpdateUserRequest
            {
                Name = name
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.Empty(results);
        }

        [Theory]
        [InlineData("<script>alert(1)</script>")]
        [InlineData("Name123")]
        [InlineData("User@Domain")]
        public void Name_InvalidCharacters_FailsValidation(string name)
        {
            // Arrange
            var request = new UpdateUserRequest
            {
                Name = name
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.MemberNames.Contains("Name"));
        }

        [Fact]
        public void Name_TooShort_FailsValidation()
        {
            // Arrange
            var request = new UpdateUserRequest
            {
                Name = "A"
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.MemberNames.Contains("Name"));
        }

        [Fact]
        public void Name_TooLong_FailsValidation()
        {
            // Arrange
            var request = new UpdateUserRequest
            {
                Name = new string('A', 151)
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.MemberNames.Contains("Name"));
        }

        #endregion

        #region Email Validation Tests

        [Theory]
        [InlineData("test@example.com")]
        [InlineData("user.name@domain.co.uk")]
        [InlineData(null)]
        public void Email_ValidEmail_PassesValidation(string? email)
        {
            // Arrange
            var request = new UpdateUserRequest
            {
                Email = email
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.Empty(results);
        }

        [Theory]
        [InlineData("notanemail")]
        [InlineData("@example.com")]
        [InlineData("user@")]
        public void Email_InvalidEmail_FailsValidation(string email)
        {
            // Arrange
            var request = new UpdateUserRequest
            {
                Email = email
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.MemberNames.Contains("Email"));
        }

        #endregion

        #region Password Validation Tests

        [Theory]
        [InlineData("ValidPass123!")]
        [InlineData("Str0ng#Password")]
        [InlineData(null)]
        public void Password_ValidPassword_PassesValidation(string? password)
        {
            // Arrange
            var request = new UpdateUserRequest
            {
                Password = password
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.Empty(results);
        }

        [Theory]
        [InlineData("short")]
        [InlineData("12345")]
        public void Password_TooShort_FailsValidation(string password)
        {
            // Arrange
            var request = new UpdateUserRequest
            {
                Password = password
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.MemberNames.Contains("Password"));
        }

        [Fact]
        public void Password_TooLong_FailsValidation()
        {
            // Arrange
            var request = new UpdateUserRequest
            {
                Password = new string('A', 151)
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.MemberNames.Contains("Password"));
        }

        #endregion

        #region Role Validation Tests

        [Theory]
        [InlineData("user")]
        [InlineData("admin")]
        [InlineData("superadmin")]
        [InlineData(null)]
        public void Role_ValidRole_PassesValidation(string? role)
        {
            // Arrange
            var request = new UpdateUserRequest
            {
                Role = role
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.Empty(results);
        }

        [Theory]
        [InlineData("hacker")]
        [InlineData("superuser")]
        [InlineData("root")]
        public void Role_InvalidRole_FailsValidation(string role)
        {
            // Arrange
            var request = new UpdateUserRequest
            {
                Role = role
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.MemberNames.Contains("Role"));
        }

        #endregion

        #region Partial Update Tests

        [Fact]
        public void PartialUpdate_OnlyName_PassesValidation()
        {
            // Arrange
            var request = new UpdateUserRequest
            {
                Name = "John Doe"
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void PartialUpdate_OnlyEmail_PassesValidation()
        {
            // Arrange
            var request = new UpdateUserRequest
            {
                Email = "test@example.com"
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void PartialUpdate_OnlyPassword_PassesValidation()
        {
            // Arrange
            var request = new UpdateUserRequest
            {
                Password = "ValidPass123!"
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void PartialUpdate_MultipleFields_PassesValidation()
        {
            // Arrange
            var request = new UpdateUserRequest
            {
                Name = "John Doe",
                Email = "test@example.com",
                IsActive = false
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.Empty(results);
        }

        #endregion
    }
}
