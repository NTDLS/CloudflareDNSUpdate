using CloudflareDNSUpdate.Records;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CloudflareDNSUpdate
{
    internal class Jobs
    {
        private readonly IConfiguration _configuration;
        private readonly EmailHelper _emailHelper;

        private readonly string _cloudflareApiBase;
        private readonly string _cloudflareApiToken;
        private readonly string _publicIpv4SourceUrl;
        private readonly List<string> _emailNotifyOnFailure;

        private readonly List<string> _includeDomains;
        private readonly List<string> _excludeDomains;
        private readonly List<string> _updateRecordTypes;

        private readonly bool _updateApex;
        private readonly bool _updateWWW;
        private readonly bool _updateWildcard;
        private readonly bool _updateWildcardSubdomain;

        public Jobs(IConfiguration configuration)
        {
            _cloudflareApiBase = configuration.GetValue<string?>("Service:CloudflareApiBase")
               ?? throw new Exception("Missing configuration: Service:CloudflareApiBase");

            _cloudflareApiToken = configuration.GetValue<string?>("Service:CloudflareApiToken")
               ?? throw new Exception("Missing configuration: Service:CloudflareApiToken");

            _publicIpv4SourceUrl = configuration.GetValue<string?>("Service:PublicIpv4SourceUrl")
               ?? throw new Exception("Missing configuration: Service:PublicIpv4SourceUrl");

            _emailNotifyOnFailure = configuration.GetSection("Service:EmailNotifyOnFailure").Get<List<string>>() ?? [];

            _updateRecordTypes = configuration.GetSection("Rules:UpdateRecordTypes").Get<List<string>>() ?? new List<string>();
            _includeDomains = configuration.GetSection("Rules:IncludeDomains").Get<List<string>>() ?? [];
            _excludeDomains = configuration.GetSection("Rules:ExcludeDomains").Get<List<string>>() ?? [];
            _updateApex = configuration.GetValue<bool?>("Rules:UpdateApex") ?? false;
            _updateWWW = configuration.GetValue<bool?>("Rules:UpdateWWW") ?? false;
            _updateWildcard = configuration.GetValue<bool?>("Rules:UpdateWildcard") ?? false;
            _updateWildcardSubdomain = configuration.GetValue<bool?>("Rules:UpdateWildcardSubdomain") ?? false;

            _configuration = configuration;
            _emailHelper = new EmailHelper(_configuration);
        }

        public async Task Execute()
        {
            Log.Information("Starting update.");

            try
            {
                long alreadyRunning;
                lock (Singletons.ReentrantLock)
                {
                    alreadyRunning = Interlocked.Read(ref Singletons.ReentrantLockValue);
                    Interlocked.Increment(ref Singletons.ReentrantLockValue);
                }

                if (alreadyRunning == 0)
                {
                    await RunProcess();
                }
                else
                {
                    Log.Warning($"Job is already running. Skipping execution.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to start job {ex.Message}.");

                if (_emailNotifyOnFailure != null)
                {
                    _emailHelper.Send(
                        _emailNotifyOnFailure,
                        "DNS Update Job Failed",
                        $"DNS Update Job Failed with error: {ex.Message}");
                }
            }
            finally
            {
                Interlocked.Decrement(ref Singletons.ReentrantLockValue);
            }
        }

        public async Task RunProcess()
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _cloudflareApiToken);

            var publicIp = await GetPublicIpv4Async(http);
            Log.Information($"Public IPv4: {publicIp}");

            var zones = await GetAllZonesAsync(http);
            Log.Information($"Zones found: {zones.Count}");

            foreach (var zone in zones)
            {
                if (_includeDomains.Count > 0 && !_includeDomains.Any(d => string.Equals(d, zone.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    Log.Information($"[{zone.Name}] Not in include list (skipping).");
                    continue;
                }

                if (_excludeDomains.Any(d => string.Equals(d, zone.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    Log.Information($"[{zone.Name}] In exclude list (skipping).");
                    continue;
                }

                try
                {
                    var desiredNames = new List<string>();

                    if (_updateApex) desiredNames.Add(zone.Name);
                    if (_updateWWW) desiredNames.Add($"www.{zone.Name}");
                    if (_updateWildcardSubdomain) desiredNames.Add($"*.{zone.Name}");
                    if (_updateWildcard) desiredNames.Add($"*");

                    // Get A records for the specific names we care about
                    var records = new List<DnsRecord>();
                    foreach (var name in desiredNames)
                    {
                        foreach (var recordType in _updateRecordTypes)
                        {
                            var found = await ListDnsRecordsAsync(http, zone.Id, type: recordType, name: name);
                            records.AddRange(found);
                        }
                    }

                    if (records.Count == 0)
                    {
                        Log.Information($"[{zone.Name}] No matching A records found (skipping).");
                        continue;
                    }

                    foreach (var rec in records)
                    {
                        if (rec.Content == publicIp)
                        {
                            Log.Information($"[{zone.Name}] {rec.Name} already {publicIp} (ok).");
                            continue;
                        }

                        // Preserve existing TTL and proxied settings.
                        // Note: proxied A records typically require TTL=1 (auto); preserving is safest.
                        var updated = new DnsRecordUpdateRequest
                        {
                            Type = rec.Type,
                            Name = rec.Name,
                            Content = publicIp,
                            Ttl = rec.Ttl,
                            Proxied = rec.Proxied
                        };

                        await UpdateDnsRecordAsync(http, zone.Id, rec.Id, updated);
                        Log.Information($"[{zone.Name}] Updated {rec.Name}: {rec.Content} -> {publicIp}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"[{zone.Name}] Failed to update records: {ex.Message}");
                }
            }
        }

        private async Task<string> GetPublicIpv4Async(HttpClient http)
        {
            var json = await http.GetStringAsync(_publicIpv4SourceUrl);
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("ip").GetString() ?? throw new Exception("Could not read ipify response.");
        }

        private async Task<List<Zone>> GetAllZonesAsync(HttpClient http)
        {
            // GET /zones with pagination
            var zones = new List<Zone>();
            int page = 1;
            const int perPage = 50;

            while (true)
            {
                var url = $"{_cloudflareApiBase}/zones?per_page={perPage}&page={page}";
                var resp = await http.GetFromJsonAsync<CfEnvelope<List<Zone>>>(url)
                           ?? throw new Exception("Null zones response.");

                if (!resp.Success)
                    throw new Exception($"Cloudflare zones error: {SerializeErrors(resp.Errors)}");

                zones.AddRange(resp.Result);

                if (resp.Result.Count < perPage)
                {
                    // stop when fewer than perPage returned.
                    break;
                }
                page++;
            }

            return zones;
        }

        private async Task<List<DnsRecord>> ListDnsRecordsAsync(HttpClient http, string zoneId, string type, string name)
        {
            var url = $"{_cloudflareApiBase}/zones/{zoneId}/dns_records?type={Uri.EscapeDataString(type)}&name={Uri.EscapeDataString(name)}";
            var resp = await http.GetFromJsonAsync<CfEnvelope<List<DnsRecord>>>(url)
                       ?? throw new Exception("Null dns records response.");

            if (!resp.Success)
                throw new Exception($"Cloudflare list records error: {SerializeErrors(resp.Errors)}");

            return resp.Result;
        }

        private async Task UpdateDnsRecordAsync(HttpClient http, string zoneId, string recordId, DnsRecordUpdateRequest update)
        {
            var url = $"{_cloudflareApiBase}/zones/{zoneId}/dns_records/{recordId}";
            var json = JsonSerializer.Serialize(update, JsonOpts);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var res = await http.PutAsync(url, content);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                throw new Exception($"HTTP {(int)res.StatusCode}: {body}");

            var env = JsonSerializer.Deserialize<CfEnvelope<DnsRecord>>(body, JsonOpts)
                      ?? throw new Exception("Null update response.");

            if (!env.Success)
                throw new Exception($"Cloudflare update error: {SerializeErrors(env.Errors)}");
        }

        static string SerializeErrors(List<CfError>? errors)
            => errors == null ? "(none)" : string.Join("; ", errors.Select(e => $"{e.Code}:{e.Message}"));

        static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}
