#if !WINDOWS
using ScheduledTasksApi.Extensions;
using ScheduledTasksApi.Models;
using ScheduledTasksApi.Services.Parsing;

namespace ScheduledTasksApi.Services.Linux;

public class LinuxTaskService(IProcessRunner runner) : ITaskSchedulerService
{
    public IReadOnlyList<TaskItem> FindTasks(string pattern, string allowedFilter)
    {
        var regex = pattern.ToWildcardRegex();
        var tasks = new List<TaskItem>();

        // Cron entries
        var cronResult = runner.RunAsync("crontab", "-l").GetAwaiter().GetResult();
        if (cronResult.ExitCode == 0)
            tasks.AddRange(CrontabParser.Parse(cronResult.StandardOutput));

        // Systemd timers
        var timerResult = runner.RunAsync("systemctl", "list-timers --all --output=json").GetAwaiter().GetResult();
        if (timerResult.ExitCode == 0)
            tasks.AddRange(SystemdOutputParser.ParseTimersJson(timerResult.StandardOutput));

        return tasks
            .FilterByWildcard(allowedFilter, t => [t.Name])
            .Where(t => regex.IsMatch(t.Name))
            .ToList();
    }

    public TaskItem? FindTask(string name, string allowedFilter)
    {
        var tasks = FindTasks(name, allowedFilter);
        return tasks.Count == 1 ? tasks[0] : null;
    }

    public TaskItemDetail? FindTaskDetail(string name, string allowedFilter)
    {
        var task = FindTask(name, allowedFilter);
        if (task is null) return null;

        if (task.Source == "SystemdTimer")
        {
            var result = runner.RunAsync("systemctl", $"show {name}").GetAwaiter().GetResult();
            if (result.ExitCode == 0)
            {
                var props = SystemdOutputParser.ParseShowOutput(result.StandardOutput);
                return SystemdOutputParser.MapShowToTimerDetail(name, props);
            }
        }

        // Cron entry — return detail with parsed schedule
        var cronResult = runner.RunAsync("crontab", "-l").GetAwaiter().GetResult();
        if (cronResult.ExitCode == 0)
        {
            foreach (var line in cronResult.StandardOutput.Split('\n', StringSplitOptions.TrimEntries))
            {
                if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;
                var parts = line.Split([' ', '\t'], 6, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 6) continue;

                var command = parts[5];
                var extractedName = System.IO.Path.GetFileName(command.Split([' ', '\t'], 2)[0]);
                if (extractedName != name && command != name) continue;

                var schedule = $"{parts[0]} {parts[1]} {parts[2]} {parts[3]} {parts[4]}";
                var trigger = CrontabParser.ParseSchedule(schedule);

                return new TaskItemDetail
                {
                    Name = name,
                    Path = command,
                    State = "Scheduled",
                    Enabled = true,
                    IsActive = true,
                    LastRunTime = DateTime.MinValue,
                    LastTaskResult = 0,
                    NextRunTime = DateTime.MinValue,
                    Source = "Cron",
                    Actions = [new TaskActionItem { Type = "Execute", Path = command }],
                    Triggers = [trigger]
                };
            }
        }

        return null;
    }

    public void RunTask(string taskName)
    {
        // Only systemd timers can be run on demand
        var result = runner.RunAsync("systemctl", $"start {taskName.Replace(".timer", ".service")}").GetAwaiter().GetResult();
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Failed to start '{taskName}': {result.StandardError}");
    }

    public void StopTask(string taskName)
    {
        var result = runner.RunAsync("systemctl", $"stop {taskName.Replace(".timer", ".service")}").GetAwaiter().GetResult();
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Failed to stop '{taskName}': {result.StandardError}");
    }

    public IReadOnlyList<EventItem> GetTaskHistory(string taskName)
    {
        var unitName = taskName.Replace(".timer", ".service");
        var result = runner.RunAsync("journalctl", $"-u {unitName} --since \"24 hours ago\" -o json --no-pager").GetAwaiter().GetResult();
        if (result.ExitCode != 0)
            return [];

        var events = new List<EventItem>();
        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(line);
                var root = doc.RootElement;

                events.Add(new EventItem
                {
                    EventId = root.TryGetProperty("SYSLOG_PID", out var pid) ? pid.GetString() : null,
                    ProviderName = root.TryGetProperty("SYSLOG_IDENTIFIER", out var ident) ? ident.GetString() : null,
                    TimeCreated = root.TryGetProperty("__REALTIME_TIMESTAMP", out var ts) && long.TryParse(ts.GetString(), out var usec)
                        ? DateTimeOffset.FromUnixTimeMilliseconds(usec / 1000).LocalDateTime
                        : null,
                    Description = root.TryGetProperty("MESSAGE", out var msg) ? msg.GetString() : null
                });
            }
            catch (System.Text.Json.JsonException)
            {
                // Skip malformed lines
            }
        }

        return events;
    }
}
#endif
