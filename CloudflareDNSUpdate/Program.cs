using Microsoft.Extensions.Configuration;
using Serilog;
using Topshelf;

namespace CloudflareDNSUpdate
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            try
            {
                HostFactory.Run(x =>
                {
                    x.StartAutomatically();

                    x.EnableServiceRecovery(rc =>
                    {
                        rc.RestartService(1);
                    });

                    x.Service<ServiceEntry>(s =>
                    {
                        s.ConstructUsing(hostSettings => new ServiceEntry(configuration));
                        s.WhenStarted(tc => tc.Start());
                        s.WhenStopped(tc => tc.Stop());
                    });
                    x.RunAsLocalSystem();

                    x.SetDescription("NetworkDLS Cloudflare DNS update service.");
                    x.SetDisplayName("NTDLS.CloudflareDNS");
                    x.SetServiceName("NTDLSCloudflareDNS");
                });
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to initialize.");
                throw;
            }
        }
    }
}
