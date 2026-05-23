#if !WINDOWS
using NSubstitute;
using ScheduledTasksApi.Services;
using ScheduledTasksApi.Services.Mac;

namespace ScheduledTasksApi.Tests.Services;

public class MacTaskServiceTests
{
    private readonly IProcessRunner _runner = Substitute.For<IProcessRunner>();
    private readonly MacTaskService _service;

    public MacTaskServiceTests()
    {
        _service = new MacTaskService(_runner);
    }

    [Fact]
    public void FindTasks_CombinesCronAndLaunchd()
    {
        _runner.RunAsync("crontab", "-l", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "0 5 * * * /usr/bin/backup.sh\n", ""));

        _runner.RunAsync("launchctl", "list", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "PID\tStatus\tLabel\n1234\t0\tcom.apple.Finder\n", ""));

        var tasks = _service.FindTasks("*", "*");

        Assert.Equal(2, tasks.Count);
        Assert.Equal("Cron", tasks[0].Source);
        Assert.Equal("Launchd", tasks[1].Source);
    }

    [Fact]
    public void FindTasks_CronFails_StillReturnsLaunchd()
    {
        _runner.RunAsync("crontab", "-l", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", "no crontab"));

        _runner.RunAsync("launchctl", "list", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "PID\tStatus\tLabel\n-\t0\tcom.example.job\n", ""));

        var tasks = _service.FindTasks("*", "*");

        Assert.Single(tasks);
        Assert.Equal("com.example.job", tasks[0].Name);
        Assert.Equal("Launchd", tasks[0].Source);
    }

    [Fact]
    public void FindTasks_BothFail_ReturnsEmpty()
    {
        _runner.RunAsync("crontab", "-l", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", "error"));

        _runner.RunAsync("launchctl", "list", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", "error"));

        var tasks = _service.FindTasks("*", "*");
        Assert.Empty(tasks);
    }

    [Fact]
    public void RunTask_TriesSystemThenUser()
    {
        // System domain fails
        _runner.RunAsync("launchctl", "kickstart system/com.example.job", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", "not found"));

        // User domain succeeds
        _runner.RunAsync("launchctl", Arg.Is<string>(s => s.StartsWith("kickstart gui/")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "", ""));

        _service.RunTask("com.example.job");

        _runner.Received(2).RunAsync("launchctl", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RunTask_BothFail_ThrowsInvalidOperation()
    {
        _runner.RunAsync("launchctl", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", "not found"));

        Assert.Throws<InvalidOperationException>(() => _service.RunTask("com.example.job"));
    }

    [Fact]
    public void GetTaskHistory_ReturnsEmptyOnMac()
    {
        _runner.RunAsync("log", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", ""));

        var events = _service.GetTaskHistory("com.example.job");
        Assert.Empty(events);
    }
}
#endif
