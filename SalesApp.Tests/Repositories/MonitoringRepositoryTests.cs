using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Repositories;
using Xunit;

namespace SalesApp.Tests.Repositories
{
    public class MonitoringRepositoryTests
    {
        private readonly AppDbContext _context;
        private readonly MonitoringRepository _repository;

        public MonitoringRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options, new Mock<IHttpContextAccessor>().Object);
            _repository = new MonitoringRepository(_context);
        }

        [Fact]
        public async Task GetMatriculaHealthAsync_ShouldOnlyConsiderContractsWithinLast14Months()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var activeStatus = new ContractStatusEntity { Id = 1, Name = "Ativo" };
            var desistenteStatus = new ContractStatusEntity { Id = 2, Name = "Desistente" };
            _context.ContractStatuses.AddRange(activeStatus, desistenteStatus);

            var matricula1 = new Matricula { Id = 1, MatriculaNumber = "1001", Status = "active" };
            var matriculaOldOnly = new Matricula { Id = 2, MatriculaNumber = "2002", Status = "active" };
            _context.Matriculas.AddRange(matricula1, matriculaOldOnly);

            // Matricula 1001:
            // Contract 1: 2 months ago (Within 14 months) - updated 2 hours ago
            var c1 = new Contract
            {
                Id = 1,
                ContractNumber = "CTR-RECENT-1",
                MatriculaId = matricula1.Id,
                ContractStatusId = activeStatus.Id,
                ContractStatus = activeStatus,
                IsActive = true,
                SaleStartDate = now.AddMonths(-2),
                UpdatedAt = now.AddHours(-2)
            };

            // Contract 2: 10 months ago (Within 14 months) - updated 5 hours ago
            var c2 = new Contract
            {
                Id = 2,
                ContractNumber = "CTR-RECENT-2",
                MatriculaId = matricula1.Id,
                ContractStatusId = activeStatus.Id,
                ContractStatus = activeStatus,
                IsActive = true,
                SaleStartDate = now.AddMonths(-10),
                UpdatedAt = now.AddHours(-5)
            };

            // Contract 3: 16 months ago (Older than 14 months) - updated 16 months ago
            var c3 = new Contract
            {
                Id = 3,
                ContractNumber = "CTR-OLD",
                MatriculaId = matricula1.Id,
                ContractStatusId = activeStatus.Id,
                ContractStatus = activeStatus,
                IsActive = true,
                SaleStartDate = now.AddMonths(-16),
                UpdatedAt = now.AddMonths(-16)
            };

            // Matricula 2002: only has contracts older than 14 months
            var cOld = new Contract
            {
                Id = 4,
                ContractNumber = "CTR-OLD-ONLY",
                MatriculaId = matriculaOldOnly.Id,
                ContractStatusId = activeStatus.Id,
                ContractStatus = activeStatus,
                IsActive = true,
                SaleStartDate = now.AddMonths(-20),
                UpdatedAt = now.AddMonths(-20)
            };

            // Inactive / desistente within 14 months (should be ignored)
            var cDesistente = new Contract
            {
                Id = 5,
                ContractNumber = "CTR-DESISTENTE",
                MatriculaId = matricula1.Id,
                ContractStatusId = desistenteStatus.Id,
                ContractStatus = desistenteStatus,
                IsActive = true,
                SaleStartDate = now.AddMonths(-1),
                UpdatedAt = now.AddMinutes(-10)
            };

            _context.Contracts.AddRange(c1, c2, c3, cOld, cDesistente);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetMatriculaHealthAsync();

            // Assert
            result.Should().NotBeNull();
            // Matricula 2002 should NOT be in the results as all its contracts are older than 14 months
            result.Should().NotContain(r => r.Matricula == "2002");

            // Matricula 1001 should be present
            var m1Health = result.Find(r => r.Matricula == "1001");
            m1Health.Should().NotBeNull();
            // Only 2 contracts are within 14 months (c1 and c2), excluding c3 (old) and cDesistente
            m1Health!.ContractCount.Should().Be(2);
            // Last update should be from c1 (updated 2 hours ago)
            m1Health.LastUpdate.Should().BeCloseTo(c1.UpdatedAt, TimeSpan.FromSeconds(5));
            m1Health.Status.Should().Be("Healthy");
        }
    }
}
