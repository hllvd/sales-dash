using Xunit;
using FluentAssertions;
using SalesApp.Services;

namespace SalesApp.Tests
{
    public class PasswordGeneratorTests
    {
        [Fact]
        public void GeneratePassword_MultipleNames_ReturnsFirstAndLastInitials()
        {
            // Arrange
            var fullName = "John Doe";

            // Act
            var password = PasswordGenerator.GeneratePassword(fullName);

            // Assert
            password.Should().HaveLength(8);
            password.Should().StartWith("JD");
            password.Substring(2).Should().MatchRegex(@"^\d{6}$", "last 6 characters should be digits");
        }

        [Fact]
        public void GeneratePassword_ThreeNames_ReturnsFirstAndLastInitials()
        {
            // Arrange
            var fullName = "John Michael Doe";

            // Act
            var password = PasswordGenerator.GeneratePassword(fullName);

            // Assert
            password.Should().HaveLength(8);
            password.Should().StartWith("JD");
            password.Substring(2).Should().MatchRegex(@"^\d{6}$");
        }

        [Fact]
        public void GeneratePassword_SingleName_ReturnsFirstTwoLetters()
        {
            // Arrange
            var fullName = "Madonna";

            // Act
            var password = PasswordGenerator.GeneratePassword(fullName);

            // Assert
            password.Should().HaveLength(8);
            password.Should().StartWith("MA");
            password.Substring(2).Should().MatchRegex(@"^\d{6}$");
        }

        [Fact]
        public void GeneratePassword_ShortSingleName_ReturnsFirstTwoLetters()
        {
            // Arrange
            var fullName = "Li";

            // Act
            var password = PasswordGenerator.GeneratePassword(fullName);

            // Assert
            password.Should().HaveLength(8);
            password.Should().StartWith("LI");
            password.Substring(2).Should().MatchRegex(@"^\d{6}$");
        }

        [Fact]
        public void GeneratePassword_SingleLetterName_RepeatsLetter()
        {
            // Arrange
            var fullName = "X";

            // Act
            var password = PasswordGenerator.GeneratePassword(fullName);

            // Assert
            password.Should().HaveLength(8);
            password.Should().StartWith("XX");
            password.Substring(2).Should().MatchRegex(@"^\d{6}$");
        }

        [Fact]
        public void GeneratePassword_NameWithExtraSpaces_HandlesCorrectly()
        {
            // Arrange
            var fullName = "  John   Doe  ";

            // Act
            var password = PasswordGenerator.GeneratePassword(fullName);

            // Assert
            password.Should().HaveLength(8);
            password.Should().StartWith("JD");
            password.Substring(2).Should().MatchRegex(@"^\d{6}$");
        }

        [Fact]
        public void GeneratePassword_LowercaseName_CapitalizesPrefix()
        {
            // Arrange
            var fullName = "john doe";

            // Act
            var password = PasswordGenerator.GeneratePassword(fullName);

            // Assert
            password.Should().HaveLength(8);
            password.Should().StartWith("JD");
            password.Substring(2).Should().MatchRegex(@"^\d{6}$");
        }

        [Fact]
        public void GeneratePassword_EmptyString_ThrowsArgumentException()
        {
            // Arrange
            var fullName = "";

            // Act
            Action act = () => PasswordGenerator.GeneratePassword(fullName);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("Full name cannot be empty*");
        }

        [Fact]
        public void GeneratePassword_WhitespaceOnly_ThrowsArgumentException()
        {
            // Arrange
            var fullName = "   ";

            // Act
            Action act = () => PasswordGenerator.GeneratePassword(fullName);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("Full name cannot be empty*");
        }

        [Fact]
        public void GeneratePassword_AlwaysReturns8Characters()
        {
            // Arrange
            var testNames = new[] { "John Doe", "Madonna", "X", "John Michael Smith", "Li" };

            // Act & Assert
            foreach (var name in testNames)
            {
                var password = PasswordGenerator.GeneratePassword(name);
                password.Should().HaveLength(8, $"password for '{name}' should be 8 characters");
            }
        }

        [Fact]
        public void GeneratePassword_Last6CharactersAreAlwaysDigits()
        {
            // Arrange
            var testNames = new[] { "John Doe", "Madonna", "X", "John Michael Smith" };

            // Act & Assert
            foreach (var name in testNames)
            {
                var password = PasswordGenerator.GeneratePassword(name);
                password.Substring(2).Should().MatchRegex(@"^\d{6}$", 
                    $"last 6 characters of password for '{name}' should be digits");
            }
        }

        [Fact]
        public void GeneratePassword_GeneratesDifferentPasswords()
        {
            // Arrange
            var fullName = "John Doe";
            var passwords = new HashSet<string>();

            // Act - Generate multiple passwords
            for (int i = 0; i < 100; i++)
            {
                var password = PasswordGenerator.GeneratePassword(fullName);
                passwords.Add(password);
            }

            // Assert - Should have many different passwords (randomness check)
            passwords.Should().HaveCountGreaterThan(90, "passwords should be random");
        }
    }
}
