using ScheduledTasksApi.Models;

namespace ScheduledTasksApi.Services.Parsing;

public static class CrontabParser
{
    public static IReadOnlyList<TaskItem> Parse(string crontabOutput)
    {
        var tasks = new List<TaskItem>();

        foreach (var line in crontabOutput.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            var parts = line.Split([' ', '\t'], 6, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6)
                continue;

            var schedule = $"{parts[0]} {parts[1]} {parts[2]} {parts[3]} {parts[4]}";
            var command = parts[5];
            var name = ExtractName(command);

            tasks.Add(new TaskItem
            {
                Name = name,
                Path = command,
                State = "Scheduled",
                Enabled = true,
                IsActive = true,
                LastRunTime = DateTime.MinValue,
                LastTaskResult = 0,
                NextRunTime = DateTime.MinValue,
                Source = "Cron"
            });
        }

        return tasks;
    }

    public static TaskTriggerItem ParseSchedule(string cronExpression)
    {
        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
            return new TaskTriggerItem { Type = "Cron", Enabled = true, Repetition = cronExpression };

        var daysOfWeek = parts[4] != "*" ? MapDaysOfWeek(parts[4]) : null;
        var daysOfMonth = parts[2] != "*" ? parts[2] : null;
        var months = parts[3] != "*" ? parts[3] : null;

        return new TaskTriggerItem
        {
            Type = "Cron",
            Enabled = true,
            Repetition = cronExpression,
            DaysOfWeek = daysOfWeek,
            DaysOfMonth = daysOfMonth,
            Months = months
        };
    }

    private static string ExtractName(string command)
    {
        // Use the basename of the first token as the task name
        var firstToken = command.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries)[0];
        var name = System.IO.Path.GetFileName(firstToken);
        return string.IsNullOrEmpty(name) ? firstToken : name;
    }

    private static string MapDaysOfWeek(string cronDow)
    {
        var dayNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        var result = new List<string>();

        foreach (var part in cronDow.Split(','))
        {
            if (int.TryParse(part.Trim(), out var num) && num is >= 0 and <= 7)
                result.Add(dayNames[num % 7]);
            else
                result.Add(part.Trim());
        }

        return string.Join(", ", result);
    }
}
