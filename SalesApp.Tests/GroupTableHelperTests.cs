using FluentAssertions;
using OfficeOpenXml;
using SalesApp.Models;
using SalesApp.Utils;
using Xunit;

namespace SalesApp.Tests.Utils
{
    public class GroupTableHelperTests
    {
        [Theory]
        [InlineData(null, true, GroupStatusFilter.Active)]
        [InlineData("", true, GroupStatusFilter.Active)]
        [InlineData("active", true, GroupStatusFilter.Active)]
        [InlineData("ACTIVE", true, GroupStatusFilter.Active)]
        [InlineData("inactive", true, GroupStatusFilter.Inactive)]
        [InlineData("all", true, GroupStatusFilter.All)]
        [InlineData("invalid", false, GroupStatusFilter.Active)]
        public void TryParseStatusFilter_ShouldValidateInput(string? raw, bool expectedSuccess, GroupStatusFilter expectedFilter)
        {
            var success = GroupTableHelper.TryParseStatusFilter(raw, out var filter);
            success.Should().Be(expectedSuccess);
            filter.Should().Be(expectedFilter);
        }

        [Fact]
        public void FilterAndSortGroups_ShouldFilterAndSortMatchingUiTableOrder()
        {
            var groups = new List<Group>
            {
                new() { Id = 1, Name = "10", Description = "Desc 10", Segment = 2, Commission = 5m, IsActive = true },
                new() { Id = 2, Name = "2", Description = "Desc 2", Segment = 2, Commission = 10m, IsActive = true },
                new() { Id = 3, Name = "Sem Seg B", Description = "", Segment = null, Commission = 0m, IsActive = true },
                new() { Id = 4, Name = "Sem Seg A", Description = "Pendente", Segment = null, Commission = 12.5m, IsActive = true },
                new() { Id = 5, Name = "Grupo Lar", Description = "Casa", Segment = 1, Commission = 8m, IsActive = true },
                new() { Id = 6, Name = "Grupo Inativo", Description = "Antigo", Segment = 1, Commission = 3m, IsActive = false },
            };

            var activeSorted = GroupTableHelper.FilterAndSortGroups(groups, GroupStatusFilter.Active, null);
            activeSorted.Select(g => g.Name).Should().Equal(
                "Sem Seg A",
                "Sem Seg B",
                "Grupo Lar",
                "2",
                "10"
            );

            var inactiveSorted = GroupTableHelper.FilterAndSortGroups(groups, GroupStatusFilter.Inactive, null);
            inactiveSorted.Select(g => g.Name).Should().Equal("Grupo Inativo");

            var allWithSearch = GroupTableHelper.FilterAndSortGroups(groups, GroupStatusFilter.All, "Motores");
            allWithSearch.Select(g => g.Name).Should().Equal("2", "10");
        }

        [Fact]
        public void BuildGroupsXlsx_ShouldProduceExpectedColumnsOrderAndCellValues()
        {
            var groups = new List<Group>
            {
                new() { Id = 10, Name = "Sem Seg", Description = "", Segment = null, Commission = 0m, IsActive = true },
                new() { Id = 20, Name = "101", Description = "Grupo residencial", Segment = 1, Commission = 12.50m, IsActive = false },
            };

            var bytes = GroupTableHelper.BuildGroupsXlsx(groups);

            ExcelPackage.License.SetNonCommercialOrganization("SalesApp");
            using var stream = new MemoryStream(bytes);
            using var package = new ExcelPackage(stream);
            var ws = package.Workbook.Worksheets["Grupos"];

            ws.Should().NotBeNull();
            ws!.Cells[1, 1].Text.Should().Be("ID");
            ws.Cells[1, 2].Text.Should().Be("Nome");
            ws.Cells[1, 3].Text.Should().Be("Descrição");
            ws.Cells[1, 4].Text.Should().Be("Segmento");
            ws.Cells[1, 5].Text.Should().Be("Comissão");
            ws.Cells[1, 6].Text.Should().Be("Status");

            ws.Cells[2, 1].Value.Should().Be(10);
            ws.Cells[2, 2].Text.Should().Be("Sem Seg");
            ws.Cells[2, 3].Text.Should().Be("-");
            ws.Cells[2, 4].Text.Should().Be("Não Definido");
            ws.Cells[2, 5].Text.Should().Be("0%");
            ws.Cells[2, 6].Text.Should().Be("Ativo");

            ws.Cells[3, 1].Value.Should().Be(20);
            ws.Cells[3, 2].Text.Should().Be("101");
            ws.Cells[3, 3].Text.Should().Be("Grupo residencial");
            ws.Cells[3, 4].Text.Should().Be("Lar");
            ws.Cells[3, 5].Text.Should().Be("12.5%");
            ws.Cells[3, 6].Text.Should().Be("Inativo");
        }
    }
}
