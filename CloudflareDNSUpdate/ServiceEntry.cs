using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Dashboard.BasicAuthorization;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace CloudflareDNSUpdate
{
    internal class ServiceEntry
    {
        private readonly IConfiguration _configuration;

        public ServiceEntry(IConfigurationRoot configuration)
        {
            Log.Verbose("Initializing service.");
            try
            {
                _configuration = configuration;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to initialize service.");
                throw;
            }
        }

        public void Stop()
        {
        }

        public void Start()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            var builder = WebApplication.CreateBuilder();

            // Add Hangfire with in-memory storage (no persistence)
            builder.Services.AddHangfire(config => config.UseMemoryStorage());
            builder.Services.AddHangfireServer();

            //builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

            builder.Services.AddSingleton(configuration);
            builder.Services.AddSingleton<EmailHelper>();

            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromDays(7);
                options.Cookie.HttpOnly = false;
                options.Cookie.IsEssential = true;
            });

            var app = builder.Build();

            if (app.Environment.IsDevelopment() == false)
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            IDashboardAuthorizationFilter[] authFilters = [];

            var webUsername = _configuration.GetValue<string>("WebInterface:WebUsername");
            var webPassword = _configuration.GetValue<string>("WebInterface:WebPassword");

            if (string.IsNullOrEmpty(webUsername) || string.IsNullOrEmpty(webPassword))
            {
                Log.Warning("Web username or password is not set. Hangfire dashboard will not be secured.");
            }
            else
            {
                authFilters = new[]
                {
                    new BasicAuthAuthorizationFilter(new BasicAuthAuthorizationFilterOptions
                    {
                        SslRedirect = false,
                        RequireSsl = false,
                        LoginCaseSensitive = true,
                        Users = new []
                        {
                            new BasicAuthAuthorizationUser
                            {
                                Login = webUsername,
                                PasswordClear = webPassword
                            }
                        }
                    })
                };
            }

            app.UseHangfireDashboard("", new DashboardOptions
            {
                IgnoreAntiforgeryToken = true,
                Authorization = authFilters
            });

            var jobs = new Jobs(_configuration);

            var cronExpression = _configuration.GetValue<string?>("Service:CronExpression")
                ?? throw new Exception("Missing configuration: Service:CronExpression");

            RecurringJob.AddOrUpdate("Update DNS Entries", () => jobs.Execute(), cronExpression, new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });

            app.RunAsync();
        }
    }
}
