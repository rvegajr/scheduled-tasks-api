#if !WINDOWS
using NSubstitute;
using ScheduledTasksApi.Services;
using ScheduledTasksApi.Services.Linux;

namespace ScheduledTasksApi.Tests.Services;

public class LinuxServiceManagerTests
{
    private readonly IProcessRunner _runner = Substitute.For<IProcessRunner>();
    private readonly LinuxServiceManager _service;

    public LinuxServiceManagerTests()
    {
        _service = new LinuxServiceManager(_runner);
    }

    [Fact]
    public void FindServices_ReturnsSystemdUnits()
    {
        var json = """
            [
                {"unit":"nginx.service","load":"loaded","active":"active","sub":"running","description":"nginx web server"},
                {"unit":"sshd.service","load":"loaded","active":"active","sub":"running","description":"OpenSSH Daemon"}
            ]
            """;

        _runner.RunAsync("systemctl", "list-units --type=service --all --output=json", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, json, ""));

        var services = _service.FindServices("*", "*");

        Assert.Equal(2, services.Count);
        Assert.Equal("nginx.service", services[0].ServiceName);
        Assert.Equal("nginx web server", services[0].DisplayName);
    }

    [Fact]
    public void FindServiceDetail_ReturnsDetailFromShow()
    {
        var listJson = """[{"unit":"nginx.service","load":"loaded","active":"active","sub":"running","description":"nginx"}]""";
        var showOutput = """
            Description=nginx web server
            ActiveState=active
            SubState=running
            MainPID=5678
            ExecStart=/usr/sbin/nginx
            User=www-data
            UnitFileState=enabled
            CanStop=yes
            """;

        _runner.RunAsync("systemctl", "list-units --type=service --all --output=json", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, listJson, ""));

        _runner.RunAsync("systemctl", "show nginx.service", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, showOutput, ""));

        var detail = _service.FindServiceDetail("nginx.service", "*");

        Assert.NotNull(detail);
        Assert.Equal("nginx web server", detail.Description);
        Assert.Equal("/usr/sbin/nginx", detail.ImagePath);
        Assert.Equal("www-data", detail.ServiceAccount);
        Assert.Equal(5678, detail.ProcessId);
        Assert.True(detail.CanStop);
    }

    [Fact]
    public async Task StartServiceAsync_Success_CallsSystemctl()
    {
        _runner.RunAsync("systemctl", "start nginx.service", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "", ""));

        await _service.StartServiceAsync("nginx.service");

        await _runner.Received(1).RunAsync("systemctl", "start nginx.service", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartServiceAsync_Failure_ThrowsInvalidOperation()
    {
        _runner.RunAsync("systemctl", "start fail.service", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", "Access denied"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.StartServiceAsync("fail.service"));
    }

    [Fact]
    public async Task StopServiceAsync_WaitsForInactive()
    {
        _runner.RunAsync("systemctl", "stop nginx.service", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "", ""));

        _runner.RunAsync("systemctl", "is-active nginx.service", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "inactive\n", ""));

        await _service.StopServiceAsync("nginx.service", TimeSpan.FromSeconds(10));

        await _runner.Received(1).RunAsync("systemctl", "stop nginx.service", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestartServiceAsync_Success_CallsSystemctl()
    {
        _runner.RunAsync("systemctl", "restart nginx.service", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "", ""));

        _runner.RunAsync("systemctl", "is-active nginx.service", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "active\n", ""));

        await _service.RestartServiceAsync("nginx.service", TimeSpan.FromSeconds(10));

        await _runner.Received(1).RunAsync("systemctl", "restart nginx.service", Arg.Any<CancellationToken>());
    }
}
#endif
