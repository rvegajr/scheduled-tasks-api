#if !WINDOWS
using ScheduledTasksApi.Extensions;
using ScheduledTasksApi.Models;
using ScheduledTasksApi.Services.Parsing;

namespace ScheduledTasksApi.Services.Mac;

public class MacTaskService(IProcessRunner runner) : ITaskSchedulerService
{
    private static readonly string[] PlistDirs =
    [
        "/Library/LaunchDaemons",
        "/Library/LaunchAgents",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/LaunchAgents")
    ];

    public IReadOnlyList<TaskItem> FindTasks(string pattern, string allowedFilter)
    {
        var regex = pattern.ToWildcardRegex();
        var tasks = new List<TaskItem>();

        // Cron entries
        var cronResult = runner.RunAsync("crontab", "-l").GetAwaiter().GetResult();
        if (cronResult.ExitCode == 0)
            tasks.AddRange(CrontabParser.Parse(cronResult.StandardOutput));

        // Launchd entries
        var launchctlResult = runner.RunAsync("launchctl", "list").GetAwaiter().GetResult();
        if (launchctlResult.ExitCode == 0)
            tasks.AddRange(LaunchdPlistParser.ParseLaunchctlList(launchctlResult.StandardOutput));

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

        if (task.Source == "Launchd")
        {
            // Find and parse the plist file
            var plistPath = FindPlistPath(name);
            if (plistPath != null && File.Exists(plistPath))
            {
                var plistXml = File.ReadAllText(plistPath);
                return LaunchdPlistParser.ParsePlist(name, plistXml);
            }
        }

        // Cron entry
        var cronResult = runner.RunAsync("crontab", "-l").GetAwaiter().GetResult();
        if (cronResult.ExitCode == 0)
        {
            foreach (var line in cronResult.StandardOutput.Split('\n', StringSplitOptions.TrimEntries))
            {
                if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;
                var parts = line.Split([' ', '\t'], 6, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 6) continue;

                var command = parts[5];
                var extractedName = Path.GetFileName(command.Split([' ', '\t'], 2)[0]);
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
        var uid = Environment.GetEnvironmentVariable("UID") ?? "501";
        // Try system domain first, then user domain
        var result = runner.RunAsync("launchctl", $"kickstart system/{taskName}").GetAwaiter().GetResult();
        if (result.ExitCode != 0)
        {
            result = runner.RunAsync("launchctl", $"kickstart gui/{uid}/{taskName}").GetAwaiter().GetResult();
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Failed to start '{taskName}': {result.StandardError}");
        }
    }

    public void StopTask(string taskName)
    {
        var uid = Environment.GetEnvironmentVariable("UID") ?? "501";
        var result = runner.RunAsync("launchctl", $"kill SIGTERM system/{taskName}").GetAwaiter().GetResult();
        if (result.ExitCode != 0)
        {
            result = runner.RunAsync("launchctl", $"kill SIGTERM gui/{uid}/{taskName}").GetAwaiter().GetResult();
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Failed to stop '{taskName}': {result.StandardError}");
        }
    }

    public IReadOnlyList<EventItem> GetTaskHistory(string taskName)
    {
        // macOS has limited log access; try the unified log
        var result = runner.RunAsync("log", $"show --predicate 'subsystem == \"{taskName}\"' --last 24h --style json").GetAwaiter().GetResult();
        if (result.ExitCode != 0)
            return [];

        // Basic parsing — unified log output is complex
        return [];
    }

    private static string? FindPlistPath(string label)
    {
        foreach (var dir in PlistDirs)
        {
            if (!Directory.Exists(dir)) continue;
            var path = Path.Combine(dir, $"{label}.plist");
            if (File.Exists(path)) return path;
        }
        return null;
    }
}
#endif
