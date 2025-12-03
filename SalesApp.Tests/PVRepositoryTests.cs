using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Repositories;
using Xunit;

namespace SalesApp.Tests.Repositories
{
    public class PVRepositoryTests
    {
        private readonly AppDbContext _context;
        private readonly PVRepository _repository;

        public PVRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _repository = new PVRepository(_context);
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnAllPVs_OrderedByName()
        {
            // Arrange
            var pv1 = new PV { Id = 1, Name = "Loja B" };
            var pv2 = new PV { Id = 2, Name = "Loja A" };
            var pv3 = new PV { Id = 3, Name = "Loja C" };
            
            _context.PVs.AddRange(pv1, pv2, pv3);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            result.Should().HaveCount(3);
            result[0].Name.Should().Be("Loja A");
            result[1].Name.Should().Be("Loja B");
            result[2].Name.Should().Be("Loja C");
        }

        [Fact]
        public async Task GetByIdAsync_ExistingId_ShouldReturnPV()
        {
            // Arrange
            var pv = new PV { Id = 1, Name = "Loja Centro" };
            _context.PVs.Add(pv);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(1);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(1);
            result.Name.Should().Be("Loja Centro");
        }

        [Fact]
        public async Task GetByIdAsync_NonExistingId_ShouldReturnNull()
        {
            // Act
            var result = await _repository.GetByIdAsync(999);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task CreateAsync_ShouldCreatePV_WithTimestamps()
        {
            // Arrange
            var pv = new PV { Id = 1, Name = "Nova Loja" };

            // Act
            var result = await _repository.CreateAsync(pv);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(1);
            result.Name.Should().Be("Nova Loja");
            result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            var savedPV = await _context.PVs.FindAsync(1);
            savedPV.Should().NotBeNull();
        }

        [Fact]
        public async Task UpdateAsync_ShouldUpdatePV_AndUpdateTimestamp()
        {
            // Arrange
            var pv = new PV { Id = 1, Name = "Loja Original" };
            _context.PVs.Add(pv);
            await _context.SaveChangesAsync();

            var originalUpdatedAt = pv.UpdatedAt;
            await Task.Delay(10); // Small delay to ensure timestamp difference

            // Act
            pv.Name = "Loja Atualizada";
            var result = await _repository.UpdateAsync(pv);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("Loja Atualizada");
            result.UpdatedAt.Should().BeAfter(originalUpdatedAt);
        }

        [Fact]
        public async Task DeleteAsync_ExistingPV_ShouldReturnTrue_AndRemovePV()
        {
            // Arrange
            var pv = new PV { Id = 1, Name = "Loja Para Deletar" };
            _context.PVs.Add(pv);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.DeleteAsync(1);

            // Assert
            result.Should().BeTrue();
            var deletedPV = await _context.PVs.FindAsync(1);
            deletedPV.Should().BeNull();
        }

        [Fact]
        public async Task DeleteAsync_NonExistingPV_ShouldReturnFalse()
        {
            // Act
            var result = await _repository.DeleteAsync(999);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ExistsAsync_ExistingId_ShouldReturnTrue()
        {
            // Arrange
            var pv = new PV { Id = 1, Name = "Loja Teste" };
            _context.PVs.Add(pv);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.ExistsAsync(1);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ExistsAsync_NonExistingId_ShouldReturnFalse()
        {
            // Act
            var result = await _repository.ExistsAsync(999);

            // Assert
            result.Should().BeFalse();
        }
    }
}
