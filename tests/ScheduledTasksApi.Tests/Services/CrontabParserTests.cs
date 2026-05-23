using ScheduledTasksApi.Services.Parsing;

namespace ScheduledTasksApi.Tests.Services;

public class CrontabParserTests
{
    [Fact]
    public void Parse_ValidCronLines_ReturnsTasks()
    {
        var input = """
            # This is a comment
            0 5 * * * /usr/bin/backup.sh
            */15 * * * * /home/user/scripts/monitor.py --verbose
            30 2 1 * * /usr/local/bin/monthly-report
            """;

        var tasks = CrontabParser.Parse(input);

        Assert.Equal(3, tasks.Count);

        Assert.Equal("backup.sh", tasks[0].Name);
        Assert.Equal("/usr/bin/backup.sh", tasks[0].Path);
        Assert.Equal("Cron", tasks[0].Source);
        Assert.Equal("Scheduled", tasks[0].State);
        Assert.True(tasks[0].Enabled);

        Assert.Equal("monitor.py", tasks[1].Name);
        Assert.Equal("/home/user/scripts/monitor.py --verbose", tasks[1].Path);

        Assert.Equal("monthly-report", tasks[2].Name);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmpty()
    {
        var tasks = CrontabParser.Parse("");
        Assert.Empty(tasks);
    }

    [Fact]
    public void Parse_OnlyComments_ReturnsEmpty()
    {
        var input = """
            # crontab comment
            # another comment
            """;

        var tasks = CrontabParser.Parse(input);
        Assert.Empty(tasks);
    }

    [Fact]
    public void Parse_MalformedLines_SkipsThem()
    {
        var input = """
            not a cron line
            0 5 * * * /valid/command.sh
            incomplete 1 2
            """;

        var tasks = CrontabParser.Parse(input);
        Assert.Single(tasks);
        Assert.Equal("command.sh", tasks[0].Name);
    }

    [Fact]
    public void ParseSchedule_DailyAtFiveAm_ReturnsCorrectTrigger()
    {
        var trigger = CrontabParser.ParseSchedule("0 5 * * *");

        Assert.Equal("Cron", trigger.Type);
        Assert.True(trigger.Enabled);
        Assert.Equal("0 5 * * *", trigger.Repetition);
        Assert.Null(trigger.DaysOfWeek);
        Assert.Null(trigger.DaysOfMonth);
        Assert.Null(trigger.Months);
    }

    [Fact]
    public void ParseSchedule_WeekdaysOnly_ReturnsDaysOfWeek()
    {
        var trigger = CrontabParser.ParseSchedule("0 9 * * 1,2,3,4,5");

        Assert.Equal("Monday, Tuesday, Wednesday, Thursday, Friday", trigger.DaysOfWeek);
    }

    [Fact]
    public void ParseSchedule_SpecificDayAndMonth_ReturnsValues()
    {
        var trigger = CrontabParser.ParseSchedule("0 0 15 6 *");

        Assert.Equal("15", trigger.DaysOfMonth);
        Assert.Equal("6", trigger.Months);
    }

    [Fact]
    public void ParseSchedule_SundayAsZeroAndSeven_BothMapCorrectly()
    {
        var trigger0 = CrontabParser.ParseSchedule("0 0 * * 0");
        var trigger7 = CrontabParser.ParseSchedule("0 0 * * 7");

        Assert.Equal("Sunday", trigger0.DaysOfWeek);
        Assert.Equal("Sunday", trigger7.DaysOfWeek);
    }
}
