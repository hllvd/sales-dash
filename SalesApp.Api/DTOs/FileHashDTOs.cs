using System;
using System.Collections.Generic;

namespace SalesApp.DTOs
{
    public class CheckFileHashesRequest
    {
        public List<string> Hashes { get; set; } = new();
    }

    public class FileHashCheckResultDto
    {
        public string FileHash { get; set; } = string.Empty;
        public bool AlreadyImported { get; set; }
        public string? FileName { get; set; }
        public long FileSize { get; set; }
        public string? UploadedByEmail { get; set; }
        public string? UploadedByName { get; set; }
        public DateTime? UploadedAt { get; set; }
        public int TotalRows { get; set; }
        public string? UploadId { get; set; }
    }

    public class FileHashMetadata
    {
        public string FileHash { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string UploadedByEmail { get; set; } = string.Empty;
        public string UploadedByName { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public int TotalRows { get; set; }
        public string UploadId { get; set; } = string.Empty;
        public long Ttl { get; set; }
    }
}
