using System.Text.Json;
using ScheduledTasksApi.Models;

namespace ScheduledTasksApi.Services.Parsing;

public static class SystemdOutputParser
{
    public static Dictionary<string, string> ParseShowOutput(string output)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in output.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrEmpty(line))
                continue;

            var eqIndex = line.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            var key = line[..eqIndex];
            var value = line[(eqIndex + 1)..];
            props[key] = value;
        }

        return props;
    }

    public static IReadOnlyList<TaskItem> ParseTimersJson(string json)
    {
        var tasks = new List<TaskItem>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            foreach (var timer in root.EnumerateArray())
            {
                var unit = timer.GetPropertyOrDefault("unit", "");
                var next = timer.GetPropertyOrDefault("next", "");
                var last = timer.GetPropertyOrDefault("last", "");
                var activates = timer.GetPropertyOrDefault("activates", unit.Replace(".timer", ".service"));

                tasks.Add(new TaskItem
                {
                    Name = unit,
                    Path = activates,
                    State = "Active",
                    Enabled = true,
                    IsActive = true,
                    LastRunTime = ParseSystemdTimestamp(last),
                    LastTaskResult = 0,
                    NextRunTime = ParseSystemdTimestamp(next),
                    Source = "SystemdTimer"
                });
            }
        }
        catch (JsonException)
        {
            // Malformed JSON — return empty
        }

        return tasks;
    }

    public static IReadOnlyList<ServiceItem> ParseUnitsJson(string json)
    {
        var services = new List<ServiceItem>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            foreach (var unit in root.EnumerateArray())
            {
                var name = unit.GetPropertyOrDefault("unit", "");
                var load = unit.GetPropertyOrDefault("load", "");
                var active = unit.GetPropertyOrDefault("active", "");
                var sub = unit.GetPropertyOrDefault("sub", "");
                var description = unit.GetPropertyOrDefault("description", "");

                services.Add(new ServiceItem
                {
                    ServiceName = name,
                    DisplayName = description,
                    Status = $"{active}/{sub}",
                    ServiceType = "Systemd",
                    StartType = load
                });
            }
        }
        catch (JsonException)
        {
            // Malformed JSON — return empty
        }

        return services;
    }

    public static ServiceItemDetail MapShowToServiceDetail(string serviceName, Dictionary<string, string> props)
    {
        return new ServiceItemDetail
        {
            ServiceName = serviceName,
            DisplayName = props.GetValueOrDefault("Description", serviceName),
            Status = $"{props.GetValueOrDefault("ActiveState", "unknown")}/{props.GetValueOrDefault("SubState", "unknown")}",
            ServiceType = props.GetValueOrDefault("Type", "Systemd"),
            StartType = props.GetValueOrDefault("UnitFileState", "unknown"),
            MachineName = null,
            CanStop = props.GetValueOrDefault("CanStop", "no") == "yes",
            CanPauseAndContinue = false,
            CanShutdown = false,
            DependentServices = props.GetValueOrDefault("WantedBy", "").Split(' ', StringSplitOptions.RemoveEmptyEntries),
            ServicesDependedOn = props.GetValueOrDefault("After", "").Split(' ', StringSplitOptions.RemoveEmptyEntries),
            Description = props.GetValueOrDefault("Description"),
            ImagePath = props.GetValueOrDefault("ExecStart"),
            ServiceAccount = props.GetValueOrDefault("User"),
            ProcessId = int.TryParse(props.GetValueOrDefault("MainPID"), out var pid) && pid > 0 ? pid : null,
            ErrorControl = null
        };
    }

    public static TaskItemDetail MapShowToTimerDetail(string timerName, Dictionary<string, string> props)
    {
        var triggers = new List<TaskTriggerItem>();
        var onCalendar = props.GetValueOrDefault("OnCalendar");
        if (!string.IsNullOrEmpty(onCalendar))
        {
            triggers.Add(new TaskTriggerItem
            {
                Type = "Calendar",
                Enabled = true,
                Repetition = onCalendar
            });
        }

        return new TaskItemDetail
        {
            Name = timerName,
            Path = props.GetValueOrDefault("Unit", timerName.Replace(".timer", ".service")),
            State = props.GetValueOrDefault("ActiveState", "unknown"),
            Enabled = props.GetValueOrDefault("UnitFileState", "") == "enabled",
            IsActive = props.GetValueOrDefault("ActiveState", "") == "active",
            LastRunTime = ParseSystemdTimestamp(props.GetValueOrDefault("LastTriggerUSec")),
            LastTaskResult = 0,
            NextRunTime = ParseSystemdTimestamp(props.GetValueOrDefault("NextElapseUSecRealtime")),
            Source = "SystemdTimer",
            Description = props.GetValueOrDefault("Description"),
            Triggers = triggers,
            Actions =
            [
                new TaskActionItem
                {
                    Type = "SystemdUnit",
                    Path = props.GetValueOrDefault("Unit", timerName.Replace(".timer", ".service"))
                }
            ],
            Principal = new TaskPrincipalItem
            {
                UserId = props.GetValueOrDefault("User")
            }
        };
    }

    private static DateTime ParseSystemdTimestamp(string? timestamp)
    {
        if (string.IsNullOrWhiteSpace(timestamp) || timestamp == "n/a")
            return DateTime.MinValue;

        // systemd timestamps can be in various formats
        if (DateTimeOffset.TryParse(timestamp, out var dto))
            return dto.LocalDateTime;

        // Try microsecond epoch format
        if (long.TryParse(timestamp, out var usec) && usec > 0)
            return DateTimeOffset.FromUnixTimeMilliseconds(usec / 1000).LocalDateTime;

        return DateTime.MinValue;
    }

    private static string GetPropertyOrDefault(this JsonElement element, string property, string defaultValue = "")
    {
        return element.TryGetProperty(property, out var prop) ? prop.GetString() ?? defaultValue : defaultValue;
    }
}
