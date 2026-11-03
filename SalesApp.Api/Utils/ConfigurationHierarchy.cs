using System.Collections.ObjectModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace SalesApp.Utils
{
    public static class ConfigurationHierarchy
    {
        /// <summary>
        /// Pure function: Parses raw .env file content into key-value pairs.
        /// Handles comments (#), empty lines, whitespace, and quoted values.
        /// </summary>
        public static IReadOnlyDictionary<string, string> ParseDotEnv(string? content)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(content))
            {
                return new ReadOnlyDictionary<string, string>(result);
            }

            using var reader = new StringReader(content);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                {
                    continue;
                }

                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = trimmed[..separatorIndex].Trim();
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                var rawValue = trimmed[(separatorIndex + 1)..].Trim();
                var cleanValue = StripQuotes(rawValue);

                result[key] = cleanValue;
            }

            return new ReadOnlyDictionary<string, string>(result);
        }

        private static string StripQuotes(string value)
        {
            if (value.Length >= 2)
            {
                if ((value.StartsWith('"') && value.EndsWith('"')) ||
                    (value.StartsWith('\'') && value.EndsWith('\'')))
                {
                    return value[1..^1];
                }
            }

            // If not quoted, strip trailing inline comments (e.g. KEY=val # comment)
            var commentIndex = value.IndexOf(" #", StringComparison.Ordinal);
            if (commentIndex >= 0)
            {
                return value[..commentIndex].Trim();
            }

            return value;
        }

        /// <summary>
        /// Resolves existing .env candidate file paths in order of precedence (lowest to highest).
        /// Checks base directory and parent directory.
        /// </summary>
        public static IReadOnlyList<string> ResolveDotEnvFilePaths(string baseDirectory, string environmentName)
        {
            var searchDirs = new List<string> { baseDirectory };
            var parentDir = Directory.GetParent(baseDirectory)?.FullName;
            if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
            {
                searchDirs.Add(parentDir);
            }

            var envLower = environmentName.ToLowerInvariant();
            var candidateFileNames = new List<string> { ".env" };

            if (envLower == "e2e" || envLower == "development")
            {
                candidateFileNames.Add(".env.development");
            }

            if (envLower == "e2e")
            {
                candidateFileNames.Add(".env.e2e");
            }
            else if (envLower == "production")
            {
                candidateFileNames.Add(".env.production");
            }

            candidateFileNames.Add(".env.local");

            var resolved = new List<string>();
            foreach (var fileName in candidateFileNames)
            {
                foreach (var dir in searchDirs)
                {
                    var fullPath = Path.Combine(dir, fileName);
                    if (File.Exists(fullPath) && !resolved.Contains(fullPath))
                    {
                        resolved.Add(fullPath);
                        break; // Found file for this candidate name
                    }
                }
            }

            return resolved.AsReadOnly();
        }

        /// <summary>
        /// Boundary side-effect: Reads candidate .env files and loads missing keys
        /// into process environment variables (without overriding existing environment variables).
        /// </summary>
        public static void LoadDotEnvIntoEnvironment(string baseDirectory, string environmentName)
        {
            try
            {
                var files = ResolveDotEnvFilePaths(baseDirectory, environmentName);
                foreach (var file in files)
                {
                    var content = File.ReadAllText(file);
                    var pairs = ParseDotEnv(content);
                    foreach (var (key, value) in pairs)
                    {
                        var current = Environment.GetEnvironmentVariable(key);
                        if (string.IsNullOrEmpty(current))
                        {
                            Environment.SetEnvironmentVariable(key, value);
                        }
                    }
                }
            }
            catch
            {
                // Non-blocking fallback if filesystem is restricted
            }
        }

        /// <summary>
        /// Configures the 4-level configuration hierarchy:
        /// Level 1: appsettings.json (Base / Production)
        /// Level 2: appsettings.Development.json (Loaded for Development AND E2E)
        /// Level 3: appsettings.{Environment}.json (e.g. appsettings.E2E.json)
        /// Level 4: .env files + System Environment variables + Command-line args
        /// </summary>
        public static void ConfigureAppConfigurationHierarchy(
            IConfigurationBuilder builder,
            IHostEnvironment env,
            string[]? args = null)
        {
            builder.Sources.Clear();

            // Level 1: Base / Production
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            // Level 2: Development configuration (Active in Development AND E2E)
            if (env.IsDevelopment() || env.IsEnvironment("E2E"))
            {
                builder.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
            }

            // Level 3: Specific environment (e.g. E2E or Staging)
            if (!env.IsDevelopment())
            {
                builder.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
            }

            // Level 4: .env files as InMemoryCollection
            try
            {
                var files = ResolveDotEnvFilePaths(env.ContentRootPath, env.EnvironmentName);
                var dotEnvPairs = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

                foreach (var file in files)
                {
                    var content = File.ReadAllText(file);
                    var parsed = ParseDotEnv(content);
                    foreach (var (key, value) in parsed)
                    {
                        dotEnvPairs[key] = value;
                        if (key.Contains("__"))
                        {
                            dotEnvPairs[key.Replace("__", ":")] = value;
                        }
                    }
                }

                if (dotEnvPairs.Count > 0)
                {
                    builder.AddInMemoryCollection(dotEnvPairs);
                }
            }
            catch
            {
                // Non-blocking fallback
            }

            // Level 4 (Highest Precedence): System Environment Variables & Command Line
            builder.AddEnvironmentVariables();

            if (args != null && args.Length > 0)
            {
                builder.AddCommandLine(args);
            }
        }
    }
}
