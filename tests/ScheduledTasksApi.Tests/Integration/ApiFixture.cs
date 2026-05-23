using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ScheduledTasksApi.Services;

namespace ScheduledTasksApi.Tests.Integration;

public class ApiFixture : WebApplicationFactory<Program>
{
    public ITaskSchedulerService TaskSchedulerService { get; } = Substitute.For<ITaskSchedulerService>();
    public IServiceManager ServiceManager { get; } = Substitute.For<IServiceManager>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove real service registrations
            var taskDesc = services.FirstOrDefault(d => d.ServiceType == typeof(ITaskSchedulerService));
            if (taskDesc != null) services.Remove(taskDesc);
            var svcDesc = services.FirstOrDefault(d => d.ServiceType == typeof(IServiceManager));
            if (svcDesc != null) services.Remove(svcDesc);

            services.AddSingleton(TaskSchedulerService);
            services.AddSingleton(ServiceManager);

            // Remove all auth scheme registrations and replace with fake
            var authDescriptors = services.Where(d => d.ServiceType.FullName?.Contains("Authentication") == true).ToList();
            foreach (var d in authDescriptors) services.Remove(d);

            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = "Test";
                o.DefaultChallengeScheme = "Test";
            }).AddScheme<AuthenticationSchemeOptions, FakeAuthHandler>("Test", null);
        });

        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AllowedTasks"] = "*",
                ["AllowedServices"] = "*",
                ["RestartTimeoutSeconds"] = "5"
            });
        });
    }
}

public class FakeAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "TestUser") }, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
