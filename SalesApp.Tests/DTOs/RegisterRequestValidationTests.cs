using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SalesApp.DTOs;
using Xunit;

namespace SalesApp.Tests.DTOs
{
    public class RegisterRequestValidationTests
    {
        private List<ValidationResult> ValidateModel(object model)
        {
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(model, null, null);
            Validator.TryValidateObject(model, validationContext, validationResults, true);
            return validationResults;
        }

        #region Name Validation Tests

        [Fact]
        public void Name_ValidName_PassesValidation()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "John Doe",
                Email = "test@example.com",
                Password = "ValidPass123!",
                Role = "user"
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.Empty(results);
        }

        [Theory]
        [InlineData("José García")]
        [InlineData("François Müller")]
        [InlineData("O'Brien")]
        [InlineData("Mary-Jane")]
        public void Name_ValidNameWithAccentsAndSpecialChars_PassesValidation(string name)
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = name,
                Email = "test@example.com",
                Password = "ValidPass123!",
                Role = "user"
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.Empty(results);
        }

        [Theory]
        [InlineData("<script>alert(1)</script>")]
        [InlineData("John<img src=x>")]
        [InlineData("Test&lt;script&gt;")]
        [InlineData("Name123")]
        [InlineData("User@Domain")]
        public void Name_InvalidCharacters_FailsValidation(string name)
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = name,
                Email = "test@example.com",
                Password = "ValidPass123!",
                Role = "user"
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.MemberNames.Contains("Name"));
        }

        [Theory]
        [InlineData("A")]
        [InlineData("")]
        public void Name_TooShort_FailsValidation(string name)
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = name,
                Email = "test@example.com",
                Password = "ValidPass123!",
                Role = "user"
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.NotEmpty(results);
        }

        [Fact]
        public void Name_TooLong_FailsValidation()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = new string('A', 151),
                Email = "test@example.com",
                Password = "ValidPass123!",
                Role = "user"
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
        [InlineData("first+last@example.org")]
        public void Email_ValidEmail_PassesValidation(string email)
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "John Doe",
                Email = email,
                Password = "ValidPass123!",
                Role = "user"
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
        [InlineData("user @example.com")]
        [InlineData("user\n@example.com")]
        public void Email_InvalidEmail_FailsValidation(string email)
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "John Doe",
                Email = email,
                Password = "ValidPass123!",
                Role = "user"
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.MemberNames.Contains("Email"));
        }

        [Fact]
        public void Email_TooLong_FailsValidation()
        {
            // Arrange
            var longEmail = new string('a', 250) + "@example.com";
            var request = new RegisterRequest
            {
                Name = "John Doe",
                Email = longEmail,
                Password = "ValidPass123!",
                Role = "user"
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
        [InlineData("MyP@ssw0rd123")]
        public void Password_ValidPassword_PassesValidation(string password)
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "John Doe",
                Email = "test@example.com",
                Password = password,
                Role = "user"
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.Empty(results);
        }

        [Theory]
        [InlineData("short")]
        [InlineData("12345")]
        [InlineData("onlyletters")]
        public void Password_TooShort_FailsValidation(string password)
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "John Doe",
                Email = "test@example.com",
                Password = password,
                Role = "user"
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
            var request = new RegisterRequest
            {
                Name = "John Doe",
                Email = "test@example.com",
                Password = new string('A', 151),
                Role = "user"
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
        public void Role_ValidRole_PassesValidation(string role)
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "John Doe",
                Email = "test@example.com",
                Password = "ValidPass123!",
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
        [InlineData("moderator")]
        public void Role_InvalidRole_FailsValidation(string role)
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "John Doe",
                Email = "test@example.com",
                Password = "ValidPass123!",
                Role = role
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.MemberNames.Contains("Role"));
        }

        #endregion

        #region MatriculaNumber Validation Tests

        [Theory]
        [InlineData("ABC123")]
        [InlineData("12345")]
        [InlineData("MAT001")]
        [InlineData(null)]
        public void MatriculaNumber_ValidMatricula_PassesValidation(string? matriculaNumber)
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "John Doe",
                Email = "test@example.com",
                Password = "ValidPass123!",
                Role = "user",
                MatriculaNumber = matriculaNumber
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.Empty(results);
        }

        [Theory]
        [InlineData("ABC-123")]
        [InlineData("MAT@001")]
        [InlineData("<script>")]
        [InlineData("MAT 001")]
        public void MatriculaNumber_InvalidCharacters_FailsValidation(string matriculaNumber)
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "John Doe",
                Email = "test@example.com",
                Password = "ValidPass123!",
                Role = "user",
                MatriculaNumber = matriculaNumber
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.MemberNames.Contains("MatriculaNumber"));
        }

        [Fact]
        public void MatriculaNumber_TooLong_FailsValidation()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "John Doe",
                Email = "test@example.com",
                Password = "ValidPass123!",
                Role = "user",
                MatriculaNumber = new string('A', 51)
            };

            // Act
            var results = ValidateModel(request);

            // Assert
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.MemberNames.Contains("MatriculaNumber"));
        }

        #endregion
    }
}
