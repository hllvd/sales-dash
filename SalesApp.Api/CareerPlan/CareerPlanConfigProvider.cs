using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SalesApp.CareerPlan
{
    public interface ICareerPlanConfigProvider
    {
        CareerPlanConfig GetConfig();
        IReadOnlyList<string> ValidateAgainstDbLevels(IEnumerable<string> dbLevelNames);
    }

    public class CareerPlanConfigProvider : ICareerPlanConfigProvider
    {
        private readonly CareerPlanConfig _config;
        private readonly ILogger<CareerPlanConfigProvider> _logger;

        public CareerPlanConfigProvider(IHostEnvironment hostEnvironment, ILogger<CareerPlanConfigProvider> logger)
        {
            _logger = logger;

            string primaryPath = Path.Combine(AppContext.BaseDirectory, "CareerPlan", "career-plan.json");
            string fallbackPath = Path.Combine(hostEnvironment.ContentRootPath, "CareerPlan", "career-plan.json");

            string filePath = File.Exists(primaryPath) ? primaryPath : fallbackPath;

            if (!File.Exists(filePath))
            {
                var msg = $"Career plan configuration file not found at '{primaryPath}' or '{fallbackPath}'.";
                _logger.LogCritical(msg);
                throw new FileNotFoundException(msg, filePath);
            }

            _logger.LogInformation("Loading career plan configuration from: {FilePath}", filePath);
            string json = File.ReadAllText(filePath);

            var parseResult = CareerPlanConfigParser.Parse(json);
            if (!parseResult.IsSuccess || parseResult.Config == null)
            {
                var errDetails = string.Join("; ", parseResult.Errors);
                var msg = $"Invalid career plan configuration in '{filePath}': {errDetails}";
                _logger.LogCritical(msg);
                throw new InvalidOperationException(msg);
            }

            // Semantic validation (without DB levels yet)
            var errors = CareerPlanConfigValidator.Validate(parseResult.Config, existingDbLevelNames: null);
            if (errors.Count > 0)
            {
                var errDetails = string.Join("; ", errors);
                var msg = $"Career plan validation failed: {errDetails}";
                _logger.LogCritical(msg);
                throw new InvalidOperationException(msg);
            }

            _config = parseResult.Config;
            _logger.LogInformation("Career plan configuration loaded successfully with {LevelCount} levels (Closing day: {ClosingDay}).",
                _config.Levels.Count, _config.ClosingDay);
        }

        public CareerPlanConfig GetConfig() => _config;

        public IReadOnlyList<string> ValidateAgainstDbLevels(IEnumerable<string> dbLevelNames)
        {
            var errors = CareerPlanConfigValidator.Validate(_config, dbLevelNames);
            if (errors.Count > 0)
            {
                var errDetails = string.Join("; ", errors);
                _logger.LogWarning("Career plan database level validation warnings: {Errors}", errDetails);
            }
            else
            {
                _logger.LogInformation("Career plan levels validated against DB levels successfully.");
            }
            return errors;
        }
    }
}
