using ScheduledTasksApi.Services.Parsing;

namespace ScheduledTasksApi.Tests.Services;

public class LaunchdPlistParserTests
{
    private const string SamplePlist = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
            <key>Label</key>
            <string>com.example.backup</string>
            <key>ProgramArguments</key>
            <array>
                <string>/usr/local/bin/backup.sh</string>
                <string>--full</string>
                <string>--verbose</string>
            </array>
            <key>WorkingDirectory</key>
            <string>/var/backups</string>
            <key>StartCalendarInterval</key>
            <dict>
                <key>Hour</key>
                <integer>5</integer>
                <key>Minute</key>
                <integer>0</integer>
            </dict>
            <key>UserName</key>
            <string>root</string>
            <key>RunAtLoad</key>
            <true/>
        </dict>
        </plist>
        """;

    [Fact]
    public void ParsePlist_ValidPlist_ReturnsDetail()
    {
        var detail = LaunchdPlistParser.ParsePlist("com.example.backup", SamplePlist);

        Assert.Equal("com.example.backup", detail.Name);
        Assert.Equal("/usr/local/bin/backup.sh", detail.Path);
        Assert.Equal("RunAtLoad", detail.State);
        Assert.True(detail.Enabled);
        Assert.Equal("Launchd", detail.Source);
    }

    [Fact]
    public void ParsePlist_ExtractsActions()
    {
        var detail = LaunchdPlistParser.ParsePlist("com.example.backup", SamplePlist);

        Assert.Single(detail.Actions);
        Assert.Equal("Execute", detail.Actions[0].Type);
        Assert.Equal("/usr/local/bin/backup.sh", detail.Actions[0].Path);
        Assert.Equal("--full --verbose", detail.Actions[0].Arguments);
        Assert.Equal("/var/backups", detail.Actions[0].WorkingDirectory);
    }

    [Fact]
    public void ParsePlist_ExtractsCalendarTrigger()
    {
        var detail = LaunchdPlistParser.ParsePlist("com.example.backup", SamplePlist);

        Assert.Single(detail.Triggers);
        Assert.Equal("Calendar", detail.Triggers[0].Type);
        Assert.True(detail.Triggers[0].Enabled);
    }

    [Fact]
    public void ParsePlist_ExtractsPrincipal()
    {
        var detail = LaunchdPlistParser.ParsePlist("com.example.backup", SamplePlist);

        Assert.NotNull(detail.Principal);
        Assert.Equal("root", detail.Principal.UserId);
    }

    [Fact]
    public void ParsePlist_KeepAliveService_SetsState()
    {
        var plist = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>com.example.daemon</string>
                <key>Program</key>
                <string>/usr/local/bin/daemon</string>
                <key>KeepAlive</key>
                <true/>
            </dict>
            </plist>
            """;

        var detail = LaunchdPlistParser.ParsePlist("com.example.daemon", plist);
        Assert.Equal("KeepAlive", detail.State);
    }

    [Fact]
    public void ParsePlist_DisabledJob_SetsEnabled()
    {
        var plist = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>com.example.disabled</string>
                <key>Program</key>
                <string>/usr/bin/test</string>
                <key>Disabled</key>
                <true/>
            </dict>
            </plist>
            """;

        var detail = LaunchdPlistParser.ParsePlist("com.example.disabled", plist);
        Assert.False(detail.Enabled);
    }

    [Fact]
    public void ParsePlist_StartInterval_CreatesTrigger()
    {
        var plist = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>com.example.poller</string>
                <key>Program</key>
                <string>/usr/bin/poll</string>
                <key>StartInterval</key>
                <integer>300</integer>
            </dict>
            </plist>
            """;

        var detail = LaunchdPlistParser.ParsePlist("com.example.poller", plist);

        Assert.Single(detail.Triggers);
        Assert.Equal("Interval", detail.Triggers[0].Type);
        Assert.Contains("00:05:00", detail.Triggers[0].Repetition);
    }

    [Fact]
    public void ParseLaunchctlList_ValidOutput_ReturnsItems()
    {
        var output = """
            PID	Status	Label
            1234	0	com.apple.Finder
            -	0	com.apple.SystemStarter
            5678	-1	com.example.myapp
            """;

        var items = LaunchdPlistParser.ParseLaunchctlList(output);

        Assert.Equal(3, items.Count);

        Assert.Equal("com.apple.Finder", items[0].Name);
        Assert.Equal("Running", items[0].State);
        Assert.True(items[0].IsActive);
        Assert.Equal(0, items[0].LastTaskResult);

        Assert.Equal("com.apple.SystemStarter", items[1].Name);
        Assert.Equal("Stopped", items[1].State);
        Assert.False(items[1].IsActive);

        Assert.Equal("com.example.myapp", items[2].Name);
        Assert.Equal("Running", items[2].State);
        Assert.Equal(-1, items[2].LastTaskResult);
    }

    [Fact]
    public void ParseLaunchctlList_EmptyOutput_ReturnsEmpty()
    {
        var items = LaunchdPlistParser.ParseLaunchctlList("");
        Assert.Empty(items);
    }

    [Fact]
    public void ParsePlist_MultipleCalendarIntervals_ReturnsMultipleTriggers()
    {
        var plist = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>com.example.multi</string>
                <key>Program</key>
                <string>/usr/bin/job</string>
                <key>StartCalendarInterval</key>
                <array>
                    <dict>
                        <key>Hour</key>
                        <integer>9</integer>
                        <key>Minute</key>
                        <integer>0</integer>
                        <key>Weekday</key>
                        <integer>1</integer>
                    </dict>
                    <dict>
                        <key>Hour</key>
                        <integer>17</integer>
                        <key>Minute</key>
                        <integer>0</integer>
                        <key>Weekday</key>
                        <integer>5</integer>
                    </dict>
                </array>
            </dict>
            </plist>
            """;

        var detail = LaunchdPlistParser.ParsePlist("com.example.multi", plist);

        Assert.Equal(2, detail.Triggers.Count);
        Assert.Equal("Monday", detail.Triggers[0].DaysOfWeek);
        Assert.Equal("Friday", detail.Triggers[1].DaysOfWeek);
    }
}
