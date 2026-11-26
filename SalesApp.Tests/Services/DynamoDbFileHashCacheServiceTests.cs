using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SalesApp.DTOs;
using SalesApp.ReportFilters.Settings;
using SalesApp.Services;
using Xunit;

namespace SalesApp.Tests.Services
{
    public class DynamoDbFileHashCacheServiceTests
    {
        private readonly DynamoDbFileHashCacheService _service;

        public DynamoDbFileHashCacheServiceTests()
        {
            var mockLogger = new Mock<ILogger<DynamoDbFileHashCacheService>>();
            var settings = Options.Create(new DynamoDbSettings
            {
                DataCacheTable = "salesapp-data-cache"
            });

            // Running without IAmazonDynamoDB utilizes the resilient in-memory fallback
            _service = new DynamoDbFileHashCacheService(settings, mockLogger.Object, null, null);
        }

        [Fact]
        public async Task SaveAndGet_ShouldPersistAndRetrieveMetadata()
        {
            var hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
            var metadata = new FileHashMetadata
            {
                FileHash = hash,
                FileName = "Carteira.xlsx",
                FileSize = 1024,
                UploadedByEmail = "admin@salesapp.com",
                UploadedByName = "Admin User",
                UploadedAt = DateTime.UtcNow,
                TotalRows = 500,
                UploadId = "20261126-test"
            };

            await _service.SaveHashMetadataAsync(metadata);
            var retrieved = await _service.GetHashMetadataAsync(hash);

            Assert.NotNull(retrieved);
            Assert.Equal(hash.ToLowerInvariant(), retrieved.FileHash);
            Assert.Equal("Carteira.xlsx", retrieved.FileName);
            Assert.Equal(1024, retrieved.FileSize);
            Assert.Equal("admin@salesapp.com", retrieved.UploadedByEmail);
            Assert.Equal("Admin User", retrieved.UploadedByName);
            Assert.Equal(500, retrieved.TotalRows);
            Assert.Equal("20261126-test", retrieved.UploadId);
            Assert.True(retrieved.Ttl > DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        [Fact]
        public async Task CheckHashesAsync_ShouldIdentifyDuplicatesAndMissing()
        {
            var hash1 = "1111111111111111111111111111111111111111111111111111111111111111";
            var hash2 = "2222222222222222222222222222222222222222222222222222222222222222";

            await _service.SaveHashMetadataAsync(new FileHashMetadata
            {
                FileHash = hash1,
                FileName = "File1.xlsx",
                UploadedByEmail = "user1@salesapp.com",
                UploadedByName = "User One",
                TotalRows = 100,
                UploadId = "upload-1"
            });

            var checkResults = await _service.CheckHashesAsync(new[] { hash1, hash2 });

            Assert.Equal(2, checkResults.Count);
            Assert.True(checkResults[hash1].AlreadyImported);
            Assert.Equal("File1.xlsx", checkResults[hash1].FileName);
            Assert.Equal("user1@salesapp.com", checkResults[hash1].UploadedByEmail);
            Assert.Equal(100, checkResults[hash1].TotalRows);

            Assert.False(checkResults[hash2].AlreadyImported);
            Assert.Null(checkResults[hash2].FileName);
        }

        [Fact]
        public async Task GetHashMetadataAsync_ShouldReturnNull_WhenExpired()
        {
            var hash = "expiredhash1234567890abcdef1234567890abcdef";
            var pastTtl = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();

            await _service.SaveHashMetadataAsync(new FileHashMetadata
            {
                FileHash = hash,
                FileName = "Old.csv",
                Ttl = pastTtl
            });

            var retrieved = await _service.GetHashMetadataAsync(hash);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task GetHashMetadataAsync_ShouldBeCaseInsensitive()
        {
            var hashUpper = "ABCDEF1234567890ABCDEF1234567890";
            var hashLower = "abcdef1234567890abcdef1234567890";

            await _service.SaveHashMetadataAsync(new FileHashMetadata
            {
                FileHash = hashUpper,
                FileName = "CaseTest.xlsx",
                TotalRows = 42
            });

            var retrieved = await _service.GetHashMetadataAsync(hashLower);
            Assert.NotNull(retrieved);
            Assert.Equal("CaseTest.xlsx", retrieved.FileName);
        }

        [Fact]
        public void ComputeTtlEpoch_ShouldCalculate30DaysByDefault()
        {
            var now = new DateTime(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);
            var ttl = DynamoDbFileHashCacheService.ComputeTtlEpoch(now);

            var expected = new DateTimeOffset(now.AddDays(30)).ToUnixTimeSeconds();
            Assert.Equal(expected, ttl);
        }
    }
}
