using ScheduledTasksApi.Services.Parsing;

namespace ScheduledTasksApi.Tests.Services;

public class SystemdOutputParserTests
{
    [Fact]
    public void ParseShowOutput_ValidKeyValuePairs_ReturnsDict()
    {
        var input = """
            Description=Daily Backup Timer
            ActiveState=active
            SubState=waiting
            MainPID=0
            ExecStart=/usr/bin/backup.sh
            User=root
            UnitFileState=enabled
            CanStop=yes
            """;

        var props = SystemdOutputParser.ParseShowOutput(input);

        Assert.Equal("Daily Backup Timer", props["Description"]);
        Assert.Equal("active", props["ActiveState"]);
        Assert.Equal("waiting", props["SubState"]);
        Assert.Equal("0", props["MainPID"]);
        Assert.Equal("/usr/bin/backup.sh", props["ExecStart"]);
        Assert.Equal("root", props["User"]);
        Assert.Equal("enabled", props["UnitFileState"]);
        Assert.Equal("yes", props["CanStop"]);
    }

    [Fact]
    public void ParseShowOutput_EmptyInput_ReturnsEmpty()
    {
        var props = SystemdOutputParser.ParseShowOutput("");
        Assert.Empty(props);
    }

    [Fact]
    public void ParseShowOutput_ValueWithEquals_PreservesFullValue()
    {
        var input = "ExecStart=/usr/bin/env VAR=value command";
        var props = SystemdOutputParser.ParseShowOutput(input);
        Assert.Equal("/usr/bin/env VAR=value command", props["ExecStart"]);
    }

    [Fact]
    public void ParseTimersJson_ValidJson_ReturnsTimers()
    {
        var json = """
            [
                {"unit":"backup.timer","next":"Mon 2026-05-25 05:00:00 UTC","last":"Sun 2026-05-24 05:00:00 UTC","activates":"backup.service"},
                {"unit":"logrotate.timer","next":"Mon 2026-05-25 00:00:00 UTC","last":"Sun 2026-05-24 00:00:00 UTC","activates":"logrotate.service"}
            ]
            """;

        var tasks = SystemdOutputParser.ParseTimersJson(json);

        Assert.Equal(2, tasks.Count);
        Assert.Equal("backup.timer", tasks[0].Name);
        Assert.Equal("backup.service", tasks[0].Path);
        Assert.Equal("SystemdTimer", tasks[0].Source);
        Assert.Equal("Active", tasks[0].State);

        Assert.Equal("logrotate.timer", tasks[1].Name);
    }

    [Fact]
    public void ParseTimersJson_EmptyArray_ReturnsEmpty()
    {
        var tasks = SystemdOutputParser.ParseTimersJson("[]");
        Assert.Empty(tasks);
    }

    [Fact]
    public void ParseTimersJson_InvalidJson_ReturnsEmpty()
    {
        var tasks = SystemdOutputParser.ParseTimersJson("not json");
        Assert.Empty(tasks);
    }

    [Fact]
    public void ParseUnitsJson_ValidJson_ReturnsServices()
    {
        var json = """
            [
                {"unit":"nginx.service","load":"loaded","active":"active","sub":"running","description":"A high performance web server"},
                {"unit":"sshd.service","load":"loaded","active":"active","sub":"running","description":"OpenSSH Daemon"}
            ]
            """;

        var services = SystemdOutputParser.ParseUnitsJson(json);

        Assert.Equal(2, services.Count);
        Assert.Equal("nginx.service", services[0].ServiceName);
        Assert.Equal("A high performance web server", services[0].DisplayName);
        Assert.Equal("active/running", services[0].Status);
        Assert.Equal("Systemd", services[0].ServiceType);
        Assert.Equal("loaded", services[0].StartType);
    }

    [Fact]
    public void MapShowToServiceDetail_PopulatesAllFields()
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Description"] = "My Service",
            ["ActiveState"] = "active",
            ["SubState"] = "running",
            ["Type"] = "simple",
            ["UnitFileState"] = "enabled",
            ["CanStop"] = "yes",
            ["ExecStart"] = "/usr/bin/myservice",
            ["User"] = "appuser",
            ["MainPID"] = "1234",
            ["After"] = "network.target syslog.target",
            ["WantedBy"] = "multi-user.target"
        };

        var detail = SystemdOutputParser.MapShowToServiceDetail("myservice.service", props);

        Assert.Equal("myservice.service", detail.ServiceName);
        Assert.Equal("My Service", detail.DisplayName);
        Assert.Equal("active/running", detail.Status);
        Assert.Equal("enabled", detail.StartType);
        Assert.True(detail.CanStop);
        Assert.Equal("/usr/bin/myservice", detail.ImagePath);
        Assert.Equal("appuser", detail.ServiceAccount);
        Assert.Equal(1234, detail.ProcessId);
        Assert.Contains("network.target", detail.ServicesDependedOn!);
        Assert.Contains("multi-user.target", detail.DependentServices!);
    }

    [Fact]
    public void MapShowToServiceDetail_ZeroPID_ReturnsNull()
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MainPID"] = "0",
            ["ActiveState"] = "inactive",
            ["SubState"] = "dead"
        };

        var detail = SystemdOutputParser.MapShowToServiceDetail("stopped.service", props);
        Assert.Null(detail.ProcessId);
    }

    [Fact]
    public void MapShowToTimerDetail_WithOnCalendar_ReturnsTrigger()
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Description"] = "Daily backup",
            ["OnCalendar"] = "*-*-* 05:00:00",
            ["Unit"] = "backup.service",
            ["ActiveState"] = "active",
            ["UnitFileState"] = "enabled",
            ["LastTriggerUSec"] = "n/a"
        };

        var detail = SystemdOutputParser.MapShowToTimerDetail("backup.timer", props);

        Assert.Equal("backup.timer", detail.Name);
        Assert.Equal("Daily backup", detail.Description);
        Assert.Equal("SystemdTimer", detail.Source);
        Assert.Single(detail.Triggers);
        Assert.Equal("Calendar", detail.Triggers[0].Type);
        Assert.Equal("*-*-* 05:00:00", detail.Triggers[0].Repetition);
        Assert.Single(detail.Actions);
        Assert.Equal("backup.service", detail.Actions[0].Path);
    }
}
