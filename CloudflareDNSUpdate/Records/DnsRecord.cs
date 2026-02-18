using System.Text.Json.Serialization;

namespace CloudflareDNSUpdate.Records
{
    public sealed class DnsRecord
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("content")] public string Content { get; set; } = "";
        [JsonPropertyName("ttl")] public int Ttl { get; set; }
        [JsonPropertyName("proxied")] public bool? Proxied { get; set; }
    }
}
