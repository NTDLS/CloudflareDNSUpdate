using System.Text.Json.Serialization;

namespace CloudflareDNSUpdate.Records
{
    public sealed class CfEnvelope<T>
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("result")] public T Result { get; set; } = default!;
        [JsonPropertyName("errors")] public List<CfError>? Errors { get; set; }
    }
}
