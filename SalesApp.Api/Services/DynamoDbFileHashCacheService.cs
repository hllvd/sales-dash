using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SalesApp.DTOs;
using SalesApp.ReportFilters.Settings;

namespace SalesApp.Services
{
    public class DynamoDbFileHashCacheService : IFileHashCacheService
    {
        private readonly IAmazonDynamoDB? _dynamoDb;
        private readonly string _tableName;
        private readonly ILogger<DynamoDbFileHashCacheService> _logger;
        private readonly bool _useMemoryOnly;

        public const string SkMetadata = "METADATA";
        public const int DefaultTtlDays = 30;

        // In-memory fallback for local environments, integration tests and E2E isolation
        private static readonly ConcurrentDictionary<string, FileHashMetadata> _inMemoryFallback =
            new(StringComparer.OrdinalIgnoreCase);

        public DynamoDbFileHashCacheService(
            IOptions<DynamoDbSettings> settings,
            ILogger<DynamoDbFileHashCacheService> logger,
            IAmazonDynamoDB? dynamoDb = null,
            IHostEnvironment? hostEnvironment = null)
        {
            _dynamoDb = dynamoDb;
            _tableName = settings.Value.DataCacheTable ?? "salesapp-data-cache";
            _logger = logger;
            var envName = hostEnvironment?.EnvironmentName ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            _useMemoryOnly = string.Equals(envName, "E2E", StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildPk(string fileHash) => $"FILE_HASH#{fileHash.Trim().ToLowerInvariant()}";

        public static long ComputeTtlEpoch(DateTime nowUtc, int days = DefaultTtlDays)
        {
            return new DateTimeOffset(nowUtc.AddDays(days)).ToUnixTimeSeconds();
        }

        public async Task<FileHashMetadata?> GetHashMetadataAsync(string fileHash)
        {
            if (string.IsNullOrWhiteSpace(fileHash))
            {
                return null;
            }

            var normalizedHash = fileHash.Trim().ToLowerInvariant();

            if (!_useMemoryOnly && _dynamoDb != null && !string.IsNullOrWhiteSpace(_tableName))
            {
                try
                {
                    var request = new GetItemRequest
                    {
                        TableName = _tableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["PK"] = new AttributeValue { S = BuildPk(normalizedHash) },
                            ["SK"] = new AttributeValue { S = SkMetadata }
                        }
                    };

                    var response = await _dynamoDb.GetItemAsync(request);
                    if (response.IsItemSet && response.Item != null && response.Item.Count > 0)
                    {
                        var item = response.Item;

                        // Check TTL
                        var nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        long ttl = 0;
                        if (item.TryGetValue("TTL", out var ttlVal) && long.TryParse(ttlVal.N, out var parsedTtl))
                        {
                            ttl = parsedTtl;
                            if (parsedTtl > 0 && parsedTtl < nowEpoch)
                            {
                                // Expired
                                return null;
                            }
                        }

                        DateTime uploadedAt = DateTime.UtcNow;
                        if (item.TryGetValue("UploadedAt", out var upVal) && DateTime.TryParse(upVal.S, out var parsedDate))
                        {
                            uploadedAt = parsedDate;
                        }

                        long fileSize = 0;
                        if (item.TryGetValue("FileSize", out var sizeVal) && long.TryParse(sizeVal.N, out var parsedSize))
                        {
                            fileSize = parsedSize;
                        }

                        int totalRows = 0;
                        if (item.TryGetValue("TotalRows", out var rowsVal) && int.TryParse(rowsVal.N, out var parsedRows))
                        {
                            totalRows = parsedRows;
                        }

                        return new FileHashMetadata
                        {
                            FileHash = normalizedHash,
                            FileName = item.TryGetValue("FileName", out var fnVal) ? fnVal.S : string.Empty,
                            FileSize = fileSize,
                            UploadedByEmail = item.TryGetValue("UploadedByEmail", out var emailVal) ? emailVal.S : string.Empty,
                            UploadedByName = item.TryGetValue("UploadedByName", out var nameVal) ? nameVal.S : string.Empty,
                            UploadedAt = uploadedAt,
                            TotalRows = totalRows,
                            UploadId = item.TryGetValue("UploadId", out var uidVal) ? uidVal.S : string.Empty,
                            Ttl = ttl
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DynamoDB unavailable for FileHash lookup. Falling back to in-memory.");
                }
            }

            if (_inMemoryFallback.TryGetValue(normalizedHash, out var cached))
            {
                var nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (cached.Ttl > 0 && cached.Ttl < nowEpoch)
                {
                    _inMemoryFallback.TryRemove(normalizedHash, out _);
                    return null;
                }
                return cached;
            }

            return null;
        }

        public async Task<Dictionary<string, FileHashCheckResultDto>> CheckHashesAsync(IEnumerable<string> fileHashes)
        {
            var results = new Dictionary<string, FileHashCheckResultDto>(StringComparer.OrdinalIgnoreCase);

            if (fileHashes == null)
            {
                return results;
            }

            foreach (var rawHash in fileHashes.Where(h => !string.IsNullOrWhiteSpace(h)))
            {
                var normalized = rawHash.Trim().ToLowerInvariant();
                if (results.ContainsKey(normalized))
                {
                    continue;
                }

                var metadata = await GetHashMetadataAsync(normalized);
                if (metadata != null)
                {
                    results[normalized] = new FileHashCheckResultDto
                    {
                        FileHash = normalized,
                        AlreadyImported = true,
                        FileName = metadata.FileName,
                        FileSize = metadata.FileSize,
                        UploadedByEmail = metadata.UploadedByEmail,
                        UploadedByName = metadata.UploadedByName,
                        UploadedAt = metadata.UploadedAt,
                        TotalRows = metadata.TotalRows,
                        UploadId = metadata.UploadId
                    };
                }
                else
                {
                    results[normalized] = new FileHashCheckResultDto
                    {
                        FileHash = normalized,
                        AlreadyImported = false
                    };
                }
            }

            return results;
        }

        public async Task SaveHashMetadataAsync(FileHashMetadata metadata)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.FileHash))
            {
                return;
            }

            var normalizedHash = metadata.FileHash.Trim().ToLowerInvariant();
            var ttl = metadata.Ttl > 0 ? metadata.Ttl : ComputeTtlEpoch(DateTime.UtcNow, DefaultTtlDays);

            metadata.FileHash = normalizedHash;
            metadata.Ttl = ttl;

            // Always update in-memory fallback
            _inMemoryFallback[normalizedHash] = metadata;

            if (_useMemoryOnly || _dynamoDb == null || string.IsNullOrWhiteSpace(_tableName))
            {
                return;
            }

            try
            {
                var item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = BuildPk(normalizedHash) },
                    ["SK"] = new AttributeValue { S = SkMetadata },
                    ["FileHash"] = new AttributeValue { S = normalizedHash },
                    ["FileName"] = new AttributeValue { S = metadata.FileName ?? string.Empty },
                    ["FileSize"] = new AttributeValue { N = metadata.FileSize.ToString() },
                    ["UploadedByEmail"] = new AttributeValue { S = metadata.UploadedByEmail ?? string.Empty },
                    ["UploadedByName"] = new AttributeValue { S = metadata.UploadedByName ?? string.Empty },
                    ["UploadedAt"] = new AttributeValue { S = metadata.UploadedAt.ToString("O") },
                    ["TotalRows"] = new AttributeValue { N = metadata.TotalRows.ToString() },
                    ["UploadId"] = new AttributeValue { S = metadata.UploadId ?? string.Empty },
                    ["TTL"] = new AttributeValue { N = ttl.ToString() }
                };

                var putRequest = new PutItemRequest
                {
                    TableName = _tableName,
                    Item = item
                };

                await _dynamoDb.PutItemAsync(putRequest);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist FileHash metadata to DynamoDB table {Table}. Stored in-memory only.", _tableName);
            }
        }
    }
}
