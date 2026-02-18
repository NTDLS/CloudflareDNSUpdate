using System.Text.Json.Serialization;

namespace CloudflareDNSUpdate.Records
{
    public sealed class DnsRecordUpdateRequest
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "A";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("content")] public string Content { get; set; } = "";
        [JsonPropertyName("ttl")] public int Ttl { get; set; } = 1; // 1 = "auto"
        [JsonPropertyName("proxied")] public bool? Proxied { get; set; }
    }
}
