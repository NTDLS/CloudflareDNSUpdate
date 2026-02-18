using System.Text.Json.Serialization;

namespace CloudflareDNSUpdate.Records
{
    public sealed class Zone
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
    }
}
