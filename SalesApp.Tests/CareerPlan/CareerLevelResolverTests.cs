using System;
using System.Collections.Generic;
using FluentAssertions;
using SalesApp.CareerPlan;
using SalesApp.Models;
using Xunit;

namespace SalesApp.Tests.CareerPlan
{
    public class CareerLevelResolverTests
    {
        private readonly List<ClassificationLevel> _levels;

        public CareerLevelResolverTests()
        {
            var lamed = new ClassificationLevel { Id = 12, Name = "Lamed", NextLevelId = null, SalesGoal = 1_000_000_000m };
            var kaf = new ClassificationLevel { Id = 11, Name = "Kaf", NextLevelId = 12, SalesGoal = 700_000_000m };
            var iod = new ClassificationLevel { Id = 10, Name = "Iod", NextLevelId = 11, SalesGoal = 500_000_000m };
            var teth = new ClassificationLevel { Id = 9, Name = "Teth", NextLevelId = 10, SalesGoal = 300_000_000m };
            var heth = new ClassificationLevel { Id = 8, Name = "Heth", NextLevelId = 9, SalesGoal = 240_000_000m };
            var zayin = new ClassificationLevel { Id = 7, Name = "Zayin", NextLevelId = 8, SalesGoal = 140_000_000m };
            var he = new ClassificationLevel { Id = 6, Name = "Hé", NextLevelId = 7, SalesGoal = 80_000_000m };
            var vav = new ClassificationLevel { Id = 5, Name = "Vav", NextLevelId = 6, SalesGoal = 40_000_000m };
            var daleth = new ClassificationLevel { Id = 4, Name = "Daleth", NextLevelId = 5, SalesGoal = 40_000_000m };
            var guimel = new ClassificationLevel { Id = 3, Name = "Guimel", NextLevelId = 4, SalesGoal = 10_000_000m };
            var beth = new ClassificationLevel { Id = 2, Name = "Beth", NextLevelId = 3, SalesGoal = 4_000_000m };
            var alef = new ClassificationLevel { Id = 1, Name = "Alef", NextLevelId = 2, SalesGoal = 2_000_000m };

            _levels = new List<ClassificationLevel>
            {
                alef, beth, guimel, daleth, vav, he, zayin, heth, teth, iod, kaf, lamed
            };
        }

        [Fact]
        public void ResolveTargetLevel_WhenUserHasNoActiveLevel_ReturnsRootLevelAlef()
        {
            var target = CareerLevelResolver.ResolveTargetLevel(null, _levels);
            target.Should().NotBeNull();
            target!.Name.Should().Be("Alef");
            target.Id.Should().Be(1);
        }

        [Fact]
        public void ResolveTargetLevel_WhenUserIsAlef_ReturnsBeth()
        {
            var current = _levels[0]; // Alef
            var target = CareerLevelResolver.ResolveTargetLevel(current, _levels);
            target.Should().NotBeNull();
            target!.Name.Should().Be("Beth");
        }

        [Fact]
        public void ResolveTargetLevel_WhenUserIsBeth_ReturnsGuimel()
        {
            var current = _levels[1]; // Beth
            var target = CareerLevelResolver.ResolveTargetLevel(current, _levels);
            target.Should().NotBeNull();
            target!.Name.Should().Be("Guimel");
        }

        [Fact]
        public void ResolveTargetLevel_WhenUserIsAtHighestLevelLamed_ReturnsNull()
        {
            var current = _levels[11]; // Lamed (NextLevelId == null)
            var target = CareerLevelResolver.ResolveTargetLevel(current, _levels);
            target.Should().BeNull();
        }
    }
}
