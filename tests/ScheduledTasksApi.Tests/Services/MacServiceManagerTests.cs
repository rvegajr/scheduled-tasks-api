#if !WINDOWS
using NSubstitute;
using ScheduledTasksApi.Services;
using ScheduledTasksApi.Services.Mac;

namespace ScheduledTasksApi.Tests.Services;

public class MacServiceManagerTests
{
    private readonly IProcessRunner _runner = Substitute.For<IProcessRunner>();
    private readonly MacServiceManager _service;

    public MacServiceManagerTests()
    {
        _service = new MacServiceManager(_runner);
    }

    [Fact]
    public void FindServices_ReturnsLaunchdItems()
    {
        _runner.RunAsync("launchctl", "list", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "PID\tStatus\tLabel\n1234\t0\tcom.apple.Finder\n-\t0\tcom.apple.SystemStarter\n", ""));

        var services = _service.FindServices("*", "*");

        Assert.Equal(2, services.Count);
        Assert.Equal("com.apple.Finder", services[0].ServiceName);
        Assert.Equal("Running", services[0].Status);
        Assert.Equal("Launchd", services[0].ServiceType);

        Assert.Equal("com.apple.SystemStarter", services[1].ServiceName);
        Assert.Equal("Stopped", services[1].Status);
    }

    [Fact]
    public void FindServices_CommandFails_ReturnsEmpty()
    {
        _runner.RunAsync("launchctl", "list", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", "error"));

        var services = _service.FindServices("*", "*");
        Assert.Empty(services);
    }

    [Fact]
    public async Task StartServiceAsync_TriesSystemThenUser()
    {
        _runner.RunAsync("launchctl", "kickstart system/com.example.svc", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", "not found"));

        _runner.RunAsync("launchctl", Arg.Is<string>(s => s.StartsWith("kickstart gui/")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "", ""));

        await _service.StartServiceAsync("com.example.svc");

        await _runner.Received(2).RunAsync("launchctl", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartServiceAsync_BothFail_Throws()
    {
        _runner.RunAsync("launchctl", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", "not found"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.StartServiceAsync("com.example.svc"));
    }

    [Fact]
    public async Task StopServiceAsync_WaitsForStopped()
    {
        // Stop command
        _runner.RunAsync("launchctl", Arg.Is<string>(s => s.Contains("kill")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "", ""));

        // List shows service stopped (PID is -)
        _runner.RunAsync("launchctl", "list", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "PID\tStatus\tLabel\n-\t0\tcom.example.svc\n", ""));

        await _service.StopServiceAsync("com.example.svc", TimeSpan.FromSeconds(10));
    }
}
#endif
