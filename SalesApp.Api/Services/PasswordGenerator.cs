namespace SalesApp.Services
{
    /// <summary>
    /// Utility class for generating secure random passwords
    /// </summary>
    public static class PasswordGenerator
    {
        private static readonly Random _random = new Random();

        /// <summary>
        /// Generates an 8-character password based on user's full name
        /// Pattern: First letter of first name + First letter of last name + 6 random digits
        /// For single names: First two letters + 6 random digits
        /// </summary>
        /// <param name="fullName">User's full name (e.g., "John Doe" or "Madonna")</param>
        /// <returns>8-character password</returns>
        public static string GeneratePassword(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                throw new ArgumentException("Full name cannot be empty", nameof(fullName));
            }

            // Clean and split the name
            var nameParts = fullName.Trim()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToArray();

            string prefix;

            if (nameParts.Length == 0)
            {
                throw new ArgumentException("Full name must contain at least one valid name part", nameof(fullName));
            }
            else if (nameParts.Length == 1)
            {
                // Single name: use first two letters
                var name = nameParts[0];
                if (name.Length >= 2)
                {
                    prefix = name.Substring(0, 2);
                }
                else if (name.Length == 1)
                {
                    // Single letter name: repeat the letter
                    prefix = name + name;
                }
                else
                {
                    throw new ArgumentException("Name part is too short", nameof(fullName));
                }
            }
            else
            {
                // Multiple names: use first letter of first name + first letter of last name
                var firstName = nameParts[0];
                var lastName = nameParts[nameParts.Length - 1];
                prefix = firstName.Substring(0, 1) + lastName.Substring(0, 1);
            }

            // Capitalize the prefix
            prefix = char.ToUpper(prefix[0]) + (prefix.Length > 1 ? char.ToUpper(prefix[1]).ToString() : "");

            // Generate 6 random digits
            var digits = GenerateRandomDigits(6);

            return prefix + digits;
        }

        /// <summary>
        /// Generates a string of random digits
        /// </summary>
        private static string GenerateRandomDigits(int length)
        {
            var digits = new char[length];
            for (int i = 0; i < length; i++)
            {
                digits[i] = (char)('0' + _random.Next(0, 10));
            }
            return new string(digits);
        }
    }
}
