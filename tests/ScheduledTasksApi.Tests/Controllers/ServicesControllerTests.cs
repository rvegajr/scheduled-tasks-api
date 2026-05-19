using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ScheduledTasksApi.Controllers;
using ScheduledTasksApi.Models;
using ScheduledTasksApi.Services;

namespace ScheduledTasksApi.Tests.Controllers;

public class ServicesControllerTests
{
    private readonly IWindowsServiceManager _serviceManager = Substitute.For<IWindowsServiceManager>();
    private readonly IConfiguration _configuration;
    private readonly ServicesController _controller;

    public ServicesControllerTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AllowedServices"] = "*",
                ["RestartTimeoutSeconds"] = "30"
            })
            .Build();
        _controller = new ServicesController(_serviceManager, _configuration);
    }

    [Fact]
    public void List_ReturnsOkWithServices()
    {
        var services = new List<ServiceItem> { CreateService("Svc1") };
        _serviceManager.FindServices("*", "*").Returns(services);

        var result = _controller.List();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(services, ok.Value);
    }

    [Fact]
    public void Get_NotFound_Returns404()
    {
        _serviceManager.FindService("missing", "*").Returns((ServiceItem?)null);

        var result = _controller.Get("missing");

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Start_ServiceNotFound_Returns404()
    {
        _serviceManager.FindService("missing", "*").Returns((ServiceItem?)null);

        var result = await _controller.Start("missing", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Start_AlreadyRunning_ReturnsConflict()
    {
        var svc = CreateService("MySvc");
        _serviceManager.FindService("MySvc", "*").Returns(svc);
        _serviceManager.StartServiceAsync("MySvc", Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Service is already running"));

        var result = await _controller.Start("MySvc", CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Stop_Timeout_Returns504()
    {
        var svc = CreateService("MySvc");
        _serviceManager.FindService("MySvc", "*").Returns(svc);
        _serviceManager.StopServiceAsync("MySvc", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Throws(new TimeoutException("timed out"));

        var result = await _controller.Stop("MySvc", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(504, statusResult.StatusCode);
    }

    [Fact]
    public async Task Restart_Success_ReturnsOk()
    {
        var svc = CreateService("MySvc");
        _serviceManager.FindService("MySvc", "*").Returns(svc);

        var result = await _controller.Restart("MySvc", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    private static ServiceItem CreateService(string name) => new()
    {
        ServiceName = name,
        DisplayName = $"{name} Display",
        Status = "Running",
        ServiceType = "Win32OwnProcess",
        StartType = "Automatic"
    };
}
