using System.Net;
using System.Net.Http.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ScheduledTasksApi.Models;

namespace ScheduledTasksApi.Tests.Integration;

public class ServicesApiTests : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client;
    private readonly ApiFixture _fixture;

    public ServicesApiTests(ApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task ListServices_ReturnsOk()
    {
        _fixture.ServiceManager.FindServices("*", "*")
            .Returns(new List<ServiceItem>
            {
                new() { ServiceName = "Svc1", DisplayName = "Service One", Status = "Running", ServiceType = "Win32OwnProcess", StartType = "Automatic" }
            });

        var response = await _client.GetAsync("/api/services");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var services = await response.Content.ReadFromJsonAsync<List<ServiceItem>>();
        Assert.NotNull(services);
        Assert.Single(services);
    }

    [Fact]
    public async Task StartService_NotFound_Returns404()
    {
        _fixture.ServiceManager.FindService("missing", "*").Returns((ServiceItem?)null);

        var response = await _client.PostAsync("/api/services/missing/start", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StopService_Timeout_Returns504()
    {
        _fixture.ServiceManager.FindService("SlowSvc", "*")
            .Returns(new ServiceItem { ServiceName = "SlowSvc", DisplayName = "Slow", Status = "Running", ServiceType = "Win32OwnProcess", StartType = "Automatic" });

        _fixture.ServiceManager.StopServiceAsync("SlowSvc", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Throws(new System.TimeoutException("timed out"));

        var response = await _client.PostAsync("/api/services/SlowSvc/stop", null);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    }

    [Fact]
    public async Task RestartService_Success_ReturnsOk()
    {
        _fixture.ServiceManager.FindService("MySvc", "*")
            .Returns(new ServiceItem { ServiceName = "MySvc", DisplayName = "My Service", Status = "Running", ServiceType = "Win32OwnProcess", StartType = "Automatic" });

        var response = await _client.PostAsync("/api/services/MySvc/restart", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
