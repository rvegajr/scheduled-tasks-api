using System.Diagnostics.Eventing.Reader;
using Microsoft.Win32.TaskScheduler;
using ScheduledTasksApi.Extensions;
using ScheduledTasksApi.Models;

namespace ScheduledTasksApi.Services;

public class TaskSchedulerService : ITaskSchedulerService
{
    public IReadOnlyList<TaskItem> FindTasks(string pattern, string allowedFilter)
    {
        var regex = pattern.ToWildcardRegex();

        using var ts = new TaskService();
        var allTasks = ts.FindAllTasks(regex);

        return allTasks
            .Select(MapTask)
            .FilterByWildcard(allowedFilter, t => [t.Name])
            .ToList();
    }

    public TaskItem? FindTask(string name, string allowedFilter)
    {
        var tasks = FindTasks(name, allowedFilter);
        return tasks.Count == 1 ? tasks[0] : null;
    }

    public void RunTask(string taskName)
    {
        using var ts = new TaskService();
        var task = ts.FindTask(taskName) ?? throw new InvalidOperationException($"Task '{taskName}' not found");
        task.Run();
    }

    public void StopTask(string taskName)
    {
        using var ts = new TaskService();
        var task = ts.FindTask(taskName) ?? throw new InvalidOperationException($"Task '{taskName}' not found");
        task.Stop();
    }

    public IReadOnlyList<EventItem> GetTaskHistory(string taskName)
    {
        var events = new List<EventItem>();

        using var log = new EventLogReader("Microsoft-Windows-TaskScheduler/Operational");
        for (var entry = log.ReadEvent(); entry != null; entry = log.ReadEvent())
        {
            if (!entry.Properties.Select(p => p.Value).Contains($"\\{taskName}"))
                continue;

            events.Add(new EventItem
            {
                EventId = entry.Id.ToString(),
                ProviderName = entry.ProviderName,
                TimeCreated = entry.TimeCreated,
                OpcodeDisplayName = entry.OpcodeDisplayName,
                ActivityId = entry.ActivityId?.ToString(),
                Description = TryFormatDescription(entry)
            });
        }

        return events;
    }

    private static TaskItem MapTask(Microsoft.Win32.TaskScheduler.Task t) => new()
    {
        Name = t.Name,
        Path = t.Path,
        Folder = t.Folder?.ToString(),
        State = t.State.ToString(),
        Enabled = t.Enabled,
        IsActive = t.IsActive,
        LastRunTime = t.LastRunTime,
        LastTaskResult = t.LastTaskResult,
        NextRunTime = t.NextRunTime
    };

    private static string? TryFormatDescription(EventRecord entry)
    {
        try { return entry.FormatDescription(); }
        catch (EventLogException) { return null; }
    }
}
