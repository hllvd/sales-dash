using System.Collections.Generic;
using SalesApp.CareerPlan;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.Tests.CareerPlan
{
    public class ContractSimulationTests
    {
        [Fact]
        public void ApplyOverrides_WithSimulatedStatus_SetsMatchingStatus()
        {
            var contract = new Contract
            {
                Id = 1,
                ContractNumber = "C-100",
                ContractStatus = new ContractStatusEntity { Id = (int)ContractStatus.Active, Name = "Active" }
            };

            var overrides = new List<ContractRtOverrideDto>
            {
                new()
                {
                    ContractNumber = "C-100",
                    SimulatedStatus = "Late2"
                }
            };

            var simulated = ContractSimulation.ApplyOverrides(new[] { contract }, overrides, null);

            Assert.Single(simulated);
            Assert.Equal("Late2", simulated[0].ContractStatus?.Name);
            Assert.Equal((int)ContractStatus.Late2, simulated[0].ContractStatusId);
        }

        [Fact]
        public void ApplyOverrides_WithOverdueMonths_MapsToCorrectStatus()
        {
            var contract1 = new Contract { Id = 1, ContractNumber = "C-1", ContractStatus = new ContractStatusEntity { Id = 1, Name = "Active" } };
            var contract2 = new Contract { Id = 2, ContractNumber = "C-2", ContractStatus = new ContractStatusEntity { Id = 1, Name = "Active" } };
            var contract3 = new Contract { Id = 3, ContractNumber = "C-3", ContractStatus = new ContractStatusEntity { Id = 1, Name = "Active" } };
            var contract4 = new Contract { Id = 4, ContractNumber = "C-4", ContractStatus = new ContractStatusEntity { Id = 1, Name = "Active" } };

            var overrides = new List<ContractRtOverrideDto>
            {
                new() { ContractNumber = "C-1", OverdueMonths = 0 },
                new() { ContractNumber = "C-2", OverdueMonths = 1 },
                new() { ContractNumber = "C-3", OverdueMonths = 3 },
                new() { ContractNumber = "C-4", OverdueMonths = 5 }
            };

            var simulated = ContractSimulation.ApplyOverrides(new[] { contract1, contract2, contract3, contract4 }, overrides, null);

            Assert.Equal("Active", simulated[0].ContractStatus?.Name);
            Assert.Equal("Late1", simulated[1].ContractStatus?.Name);
            Assert.Equal("Late3", simulated[2].ContractStatus?.Name);
            Assert.Equal("Defaulted", simulated[3].ContractStatus?.Name);
        }

        [Fact]
        public void ApplyOverrides_WithSimulatedRts_CountsUnpaidAndMapsStatus()
        {
            var contract = new Contract
            {
                Id = 1,
                ContractNumber = "C-999",
                ContractStatus = new ContractStatusEntity { Id = 1, Name = "Active" }
            };

            var overrides = new List<ContractRtOverrideDto>
            {
                new()
                {
                    ContractNumber = "C-999",
                    SimulatedRts = new List<string> { "paid", "paid", "unpaid", "unpaid", "blank", "blank" } // 2 unpaid
                }
            };

            var simulated = ContractSimulation.ApplyOverrides(new[] { contract }, overrides, null);

            Assert.Single(simulated);
            Assert.Equal("Late2", simulated[0].ContractStatus?.Name);
            Assert.Equal((int)ContractStatus.Late2, simulated[0].ContractStatusId);
        }

        [Fact]
        public void ApplyOverrides_LegacyStateOverload_WorksConsistently()
        {
            var contract = new Contract
            {
                Id = 1,
                ContractNumber = "C-LEGACY",
                ContractStatus = new ContractStatusEntity { Id = 2, Name = "Late1" }
            };

            var active = new ContractStatusEntity { Id = 10, Name = "Active" };
            var late1 = new ContractStatusEntity { Id = 20, Name = "Late1" };

            var overrides = new List<ContractRtOverrideDto>
            {
                new() { ContractNumber = "C-LEGACY", State = "upToDate" }
            };

            var simulated = ContractSimulation.ApplyOverrides(new[] { contract }, overrides, active, late1);

            Assert.Single(simulated);
            Assert.Equal("Active", simulated[0].ContractStatus?.Name);
            Assert.Equal(10, simulated[0].ContractStatusId);
        }
    }
}
