#if WINDOWS
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

    public TaskItemDetail? FindTaskDetail(string name, string allowedFilter)
    {
        // First verify the task is allowed
        var summary = FindTask(name, allowedFilter);
        if (summary is null) return null;

        using var ts = new TaskService();
        var task = ts.FindTask(name);
        if (task is null) return null;

        return MapTaskDetail(task);
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
        NextRunTime = t.NextRunTime,
        Source = "WindowsTaskScheduler"
    };

    private static TaskItemDetail MapTaskDetail(Microsoft.Win32.TaskScheduler.Task t)
    {
        var def = t.Definition;

        var actions = def.Actions.Select(a =>
        {
            var action = new TaskActionItem { Type = a.ActionType.ToString() };
            if (a is Microsoft.Win32.TaskScheduler.ExecAction exec)
            {
                action = action with
                {
                    Path = exec.Path,
                    Arguments = exec.Arguments,
                    WorkingDirectory = exec.WorkingDirectory
                };
            }
            return action;
        }).ToList();

        var triggers = def.Triggers.Select(tr =>
        {
            var item = new TaskTriggerItem
            {
                Type = tr.TriggerType.ToString(),
                Enabled = tr.Enabled,
                StartBoundary = tr.StartBoundary == DateTime.MinValue ? null : tr.StartBoundary,
                EndBoundary = tr.EndBoundary == DateTime.MinValue ? null : tr.EndBoundary,
                Repetition = tr.Repetition?.Interval > TimeSpan.Zero
                    ? $"every {tr.Repetition.Interval}" + (tr.Repetition.Duration > TimeSpan.Zero ? $" for {tr.Repetition.Duration}" : "")
                    : null
            };

            if (tr is Microsoft.Win32.TaskScheduler.WeeklyTrigger wt)
                item = item with { DaysOfWeek = wt.DaysOfWeek.ToString() };
            else if (tr is Microsoft.Win32.TaskScheduler.MonthlyTrigger mt)
                item = item with { DaysOfMonth = string.Join(", ", mt.DaysOfMonth), Months = mt.MonthsOfYear.ToString() };
            else if (tr is Microsoft.Win32.TaskScheduler.MonthlyDOWTrigger mdow)
                item = item with { DaysOfWeek = mdow.DaysOfWeek.ToString(), Months = mdow.MonthsOfYear.ToString() };

            return item;
        }).ToList();

        var settings = new TaskSettingsItem
        {
            ExecutionTimeLimit = def.Settings.ExecutionTimeLimit > TimeSpan.Zero ? def.Settings.ExecutionTimeLimit : null,
            AllowHardTerminate = def.Settings.AllowHardTerminate,
            RunOnlyIfIdle = def.Settings.RunOnlyIfIdle,
            DisallowStartIfOnBatteries = def.Settings.DisallowStartIfOnBatteries,
            StopIfGoingOnBatteries = def.Settings.StopIfGoingOnBatteries,
            AllowStartOnDemand = def.Settings.AllowDemandStart,
            Hidden = def.Settings.Hidden,
            MultipleInstances = def.Settings.MultipleInstances.ToString(),
            Priority = (int)def.Settings.Priority,
            RunOnlyIfNetworkAvailable = def.Settings.RunOnlyIfNetworkAvailable,
            WakeToRun = def.Settings.WakeToRun,
            StartWhenAvailable = def.Settings.StartWhenAvailable,
            RestartInterval = def.Settings.RestartInterval > TimeSpan.Zero ? def.Settings.RestartInterval : null,
            RestartCount = def.Settings.RestartCount,
            DeleteExpiredTaskAfter = def.Settings.DeleteExpiredTaskAfter > TimeSpan.Zero ? def.Settings.DeleteExpiredTaskAfter : null
        };

        var principal = new TaskPrincipalItem
        {
            UserId = def.Principal.UserId,
            LogonType = def.Principal.LogonType.ToString(),
            RunLevel = def.Principal.RunLevel.ToString(),
            GroupId = def.Principal.GroupId,
            DisplayName = def.Principal.DisplayName
        };

        var reg = def.RegistrationInfo;

        return new TaskItemDetail
        {
            Name = t.Name,
            Path = t.Path,
            Folder = t.Folder?.ToString(),
            State = t.State.ToString(),
            Enabled = t.Enabled,
            IsActive = t.IsActive,
            LastRunTime = t.LastRunTime,
            LastTaskResult = t.LastTaskResult,
            NextRunTime = t.NextRunTime,
            Author = reg.Author,
            Description = reg.Description,
            RegistrationDate = reg.Date == DateTime.MinValue ? null : reg.Date,
            Documentation = reg.Documentation,
            Source = "WindowsTaskScheduler",
            RegistrationSource = reg.Source,
            URI = reg.URI,
            Version = reg.Version?.ToString(),
            Actions = actions,
            Triggers = triggers,
            Settings = settings,
            Principal = principal
        };
    }

    private static string? TryFormatDescription(EventRecord entry)
    {
        try { return entry.FormatDescription(); }
        catch (EventLogException) { return null; }
    }
}
#endif
