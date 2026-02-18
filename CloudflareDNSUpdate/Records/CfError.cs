using System.Text.Json.Serialization;

namespace CloudflareDNSUpdate.Records
{
    public sealed class CfError
    {
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("message")] public string Message { get; set; } = "";
    }
}
