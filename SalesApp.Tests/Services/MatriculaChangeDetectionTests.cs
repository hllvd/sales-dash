using FluentAssertions;
using SalesApp.Services;
using Xunit;

namespace SalesApp.Tests.Services
{
    /// <summary>
    /// Unit tests for ImportExecutionService.IsMatriculaChanged.
    /// Pure function — no mocks, no IO, zero infrastructure.
    /// </summary>
    public class MatriculaChangeDetectionTests
    {
        // ── Happy path ──────────────────────────────────────────────────────────

        [Fact]
        public void IsMatriculaChanged_WhenBothIdsAreTheSame_ShouldReturnFalse()
        {
            var result = ImportExecutionService.IsMatriculaChanged(
                existingMatriculaId: 5,
                incomingMatriculaId: 5);

            result.Should().BeFalse();
        }

        [Fact]
        public void IsMatriculaChanged_WhenIdsDiffer_ShouldReturnTrue()
        {
            var result = ImportExecutionService.IsMatriculaChanged(
                existingMatriculaId: 5,
                incomingMatriculaId: 6);

            result.Should().BeTrue();
        }

        // ── Null-safety: neither side null = no change should fire ──────────────

        [Fact]
        public void IsMatriculaChanged_WhenExistingIsNull_ShouldReturnFalse()
        {
            // Existing null means contract had no matricula before → not a "change",
            // it's an initial assignment handled elsewhere.
            var result = ImportExecutionService.IsMatriculaChanged(
                existingMatriculaId: null,
                incomingMatriculaId: 6);

            result.Should().BeFalse();
        }

        [Fact]
        public void IsMatriculaChanged_WhenIncomingIsNull_ShouldReturnFalse()
        {
            // CSV has no matricula column or blank value → cannot determine change.
            var result = ImportExecutionService.IsMatriculaChanged(
                existingMatriculaId: 5,
                incomingMatriculaId: null);

            result.Should().BeFalse();
        }

        [Fact]
        public void IsMatriculaChanged_WhenBothAreNull_ShouldReturnFalse()
        {
            var result = ImportExecutionService.IsMatriculaChanged(
                existingMatriculaId: null,
                incomingMatriculaId: null);

            result.Should().BeFalse();
        }

        // ── Theory: exhaustive inline coverage ─────────────────────────────────

        [Theory]
        [InlineData(1,    1,    false)]   // same
        [InlineData(1,    2,    true)]    // different
        [InlineData(999,  1000, true)]    // large ids, different
        [InlineData(null, 5,    false)]   // existing null
        [InlineData(5,    null, false)]   // incoming null
        [InlineData(null, null, false)]   // both null
        public void IsMatriculaChanged_Theory(int? existing, int? incoming, bool expected)
        {
            ImportExecutionService.IsMatriculaChanged(existing, incoming)
                .Should().Be(expected);
        }
    }
}
