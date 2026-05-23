#if !WINDOWS
using NSubstitute;
using ScheduledTasksApi.Services;
using ScheduledTasksApi.Services.Linux;

namespace ScheduledTasksApi.Tests.Services;

public class LinuxTaskServiceTests
{
    private readonly IProcessRunner _runner = Substitute.For<IProcessRunner>();
    private readonly LinuxTaskService _service;

    public LinuxTaskServiceTests()
    {
        _service = new LinuxTaskService(_runner);
    }

    [Fact]
    public void FindTasks_CombinesCronAndSystemdTimers()
    {
        _runner.RunAsync("crontab", "-l", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "0 5 * * * /usr/bin/backup.sh\n", ""));

        _runner.RunAsync("systemctl", "list-timers --all --output=json", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, """[{"unit":"logrotate.timer","next":"Mon 2026-05-25","last":"Sun 2026-05-24","activates":"logrotate.service"}]""", ""));

        var tasks = _service.FindTasks("*", "*");

        Assert.Equal(2, tasks.Count);
        Assert.Equal("Cron", tasks[0].Source);
        Assert.Equal("SystemdTimer", tasks[1].Source);
    }

    [Fact]
    public void FindTasks_CronFails_StillReturnsSystemdTimers()
    {
        _runner.RunAsync("crontab", "-l", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", "no crontab for user"));

        _runner.RunAsync("systemctl", "list-timers --all --output=json", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, """[{"unit":"backup.timer","next":"","last":"","activates":"backup.service"}]""", ""));

        var tasks = _service.FindTasks("*", "*");

        Assert.Single(tasks);
        Assert.Equal("backup.timer", tasks[0].Name);
    }

    [Fact]
    public void FindTaskDetail_SystemdTimer_ReturnsDetailFromShow()
    {
        // FindTask needs FindTasks to return exactly one match
        _runner.RunAsync("crontab", "-l", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", ""));

        _runner.RunAsync("systemctl", "list-timers --all --output=json", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, """[{"unit":"backup.timer","next":"","last":"","activates":"backup.service"}]""", ""));

        _runner.RunAsync("systemctl", "show backup.timer", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, """
                Description=Daily Backup
                OnCalendar=*-*-* 05:00:00
                Unit=backup.service
                ActiveState=active
                UnitFileState=enabled
                """, ""));

        var detail = _service.FindTaskDetail("backup.timer", "*");

        Assert.NotNull(detail);
        Assert.Equal("Daily Backup", detail.Description);
        Assert.Equal("SystemdTimer", detail.Source);
        Assert.Single(detail.Triggers);
        Assert.Equal("*-*-* 05:00:00", detail.Triggers[0].Repetition);
    }

    [Fact]
    public void RunTask_Success_CallsSystemctlStart()
    {
        _runner.RunAsync("systemctl", "start backup.service", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "", ""));

        _service.RunTask("backup.timer");

        _runner.Received(1).RunAsync("systemctl", "start backup.service", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RunTask_Failure_ThrowsInvalidOperation()
    {
        _runner.RunAsync("systemctl", "start backup.service", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", "Failed to start"));

        Assert.Throws<InvalidOperationException>(() => _service.RunTask("backup.timer"));
    }

    [Fact]
    public void GetTaskHistory_ReturnsJournalEntries()
    {
        var journalOutput = """
            {"SYSLOG_PID":"1234","SYSLOG_IDENTIFIER":"backup","__REALTIME_TIMESTAMP":"1716600000000000","MESSAGE":"Backup started"}
            {"SYSLOG_PID":"1234","SYSLOG_IDENTIFIER":"backup","__REALTIME_TIMESTAMP":"1716603600000000","MESSAGE":"Backup completed"}
            """;

        _runner.RunAsync("journalctl", Arg.Is<string>(s => s.Contains("backup.service")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, journalOutput, ""));

        var events = _service.GetTaskHistory("backup.timer");

        Assert.Equal(2, events.Count);
        Assert.Equal("Backup started", events[0].Description);
        Assert.Equal("Backup completed", events[1].Description);
    }
}
#endif
