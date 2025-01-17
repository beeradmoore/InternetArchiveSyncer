using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace InternetArchiveSyncer;

internal class Config
{
    [JsonPropertyName("access_key")]
    public string AccessKey { get; set; } = string.Empty;

    [JsonPropertyName("secret")]
    public string Secret { get; set; } = string.Empty;

    [JsonPropertyName("archive_configs")]
    public List<ArchiveConfig> ArchiveConfigs { get; set; } = new List<ArchiveConfig>();
}

internal class ArchiveConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;

    [JsonPropertyName("archive")]
    public string Archive { get; set; } = string.Empty;

    [JsonPropertyName("custom_path")]
    public string CustomPath { get; set; } = string.Empty;
}
