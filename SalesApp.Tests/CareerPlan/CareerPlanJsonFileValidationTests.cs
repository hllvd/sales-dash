using System;
using System.IO;
using FluentAssertions;
using SalesApp.CareerPlan;
using Xunit;

namespace SalesApp.Tests.CareerPlan
{
    public class CareerPlanJsonFileValidationTests
    {
        [Fact]
        public void ActualCareerPlanJson_IsValidAndParsesSuccessfully()
        {
            // Find career-plan.json
            var basePath = AppContext.BaseDirectory;
            var path = Path.Combine(basePath, "CareerPlan", "career-plan.json");

            if (!File.Exists(path))
            {
                // Fallback for running test runner without build copy
                path = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", "..", "SalesApp.Api", "CareerPlan", "career-plan.json"));
            }

            File.Exists(path).Should().BeTrue($"career-plan.json must exist at {path}");

            var json = File.ReadAllText(path);
            var parseResult = CareerPlanConfigParser.Parse(json);

            parseResult.IsSuccess.Should().BeTrue($"Errors: {string.Join("; ", parseResult.Errors)}");
            var config = parseResult.Config!;
            config.ClosingDay.Should().Be(15);
            config.Levels.Should().ContainKey("Alef");
            config.Levels.Should().ContainKey("Beth");
            config.Levels.Should().ContainKey("Guimel");

            var knownDbLevels = new[]
            {
                "Alef", "Beth", "Guimel", "Daleth", "Vav", "Hé", "Zayin", "Heth", "Teth", "Iod", "Kaf", "Lamed"
            };

            var validationErrors = CareerPlanConfigValidator.Validate(config, knownDbLevels);
            validationErrors.Should().BeEmpty();
        }
    }
}
