using System;
using System.Collections.Generic;
using System.Linq;
using SalesApp.Models;

namespace SalesApp.CareerPlan
{
    public static class CareerLevelResolver
    {
        /// <summary>
        /// Pure function to resolve the target classification level for a user.
        /// If the user has no active level, returns the entry root level of the progression chain (e.g. Alef).
        /// If the user has an active level, returns the next level in the chain (NextLevel).
        /// </summary>
        public static ClassificationLevel? ResolveTargetLevel(
            ClassificationLevel? currentLevel,
            IReadOnlyList<ClassificationLevel> allLevels)
        {
            if (allLevels == null || allLevels.Count == 0) return null;

            if (currentLevel == null)
            {
                // Find root levels: levels that are not pointed to by any other level's NextLevelId
                var targetLevelIds = allLevels
                    .Where(l => l.NextLevelId.HasValue)
                    .Select(l => l.NextLevelId!.Value)
                    .ToHashSet();

                var rootLevels = allLevels
                    .Where(l => !targetLevelIds.Contains(l.Id))
                    .OrderBy(l => l.SalesGoal ?? decimal.MaxValue)
                    .ThenBy(l => l.Name)
                    .ToList();

                return rootLevels.FirstOrDefault() ?? allLevels.OrderBy(l => l.SalesGoal ?? decimal.MaxValue).First();
            }

            if (currentLevel.NextLevel != null)
            {
                return currentLevel.NextLevel;
            }

            if (currentLevel.NextLevelId.HasValue)
            {
                return allLevels.FirstOrDefault(l => l.Id == currentLevel.NextLevelId.Value);
            }

            return null;
        }
    }
}
