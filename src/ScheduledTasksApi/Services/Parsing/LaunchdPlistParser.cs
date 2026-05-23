using System.Xml.Linq;
using ScheduledTasksApi.Models;

namespace ScheduledTasksApi.Services.Parsing;

public static class LaunchdPlistParser
{
    public static TaskItemDetail ParsePlist(string label, string plistXml)
    {
        var doc = XDocument.Parse(plistXml);
        var dict = ParseDict(doc.Root?.Element("dict"));

        var programArgs = GetStringArray(dict, "ProgramArguments");
        var program = GetString(dict, "Program") ?? (programArgs.Count > 0 ? programArgs[0] : null);

        var actions = new List<TaskActionItem>();
        if (program != null)
        {
            actions.Add(new TaskActionItem
            {
                Type = "Execute",
                Path = program,
                Arguments = programArgs.Count > 1 ? string.Join(" ", programArgs.Skip(1)) : null,
                WorkingDirectory = GetString(dict, "WorkingDirectory")
            });
        }

        var triggers = new List<TaskTriggerItem>();

        // StartCalendarInterval
        if (dict.TryGetValue("StartCalendarInterval", out var calObj))
        {
            if (calObj is Dictionary<string, object> calDict)
                triggers.Add(ParseCalendarInterval(calDict));
            else if (calObj is List<object> calList)
                triggers.AddRange(calList.OfType<Dictionary<string, object>>().Select(ParseCalendarInterval));
        }

        // StartInterval (seconds)
        if (dict.TryGetValue("StartInterval", out var intervalObj) && intervalObj is long interval)
        {
            triggers.Add(new TaskTriggerItem
            {
                Type = "Interval",
                Enabled = true,
                Repetition = $"every {TimeSpan.FromSeconds(interval)}"
            });
        }

        var keepAlive = dict.ContainsKey("KeepAlive") && dict["KeepAlive"] is true;
        var runAtLoad = dict.ContainsKey("RunAtLoad") && dict["RunAtLoad"] is true;

        return new TaskItemDetail
        {
            Name = label,
            Path = program ?? label,
            State = keepAlive ? "KeepAlive" : (runAtLoad ? "RunAtLoad" : "Scheduled"),
            Enabled = !dict.ContainsKey("Disabled") || dict["Disabled"] is not true,
            IsActive = true,
            LastRunTime = DateTime.MinValue,
            LastTaskResult = 0,
            NextRunTime = DateTime.MinValue,
            Source = "Launchd",
            Description = GetString(dict, "Label"),
            Actions = actions,
            Triggers = triggers,
            Principal = new TaskPrincipalItem
            {
                UserId = GetString(dict, "UserName"),
                GroupId = GetString(dict, "GroupName")
            }
        };
    }

    public static IReadOnlyList<TaskItem> ParseLaunchctlList(string output)
    {
        var items = new List<TaskItem>();

        foreach (var line in output.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrEmpty(line))
                continue;

            // Skip header line: PID	Status	Label
            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || parts[0] == "PID")
                continue;

            var pid = parts[0].Trim();
            var status = parts[1].Trim();
            var label = parts[2].Trim();

            items.Add(new TaskItem
            {
                Name = label,
                Path = label,
                State = pid == "-" ? "Stopped" : "Running",
                Enabled = true,
                IsActive = pid != "-",
                LastRunTime = DateTime.MinValue,
                LastTaskResult = int.TryParse(status, out var s) ? s : 0,
                NextRunTime = DateTime.MinValue,
                Source = "Launchd"
            });
        }

        return items;
    }

    private static TaskTriggerItem ParseCalendarInterval(Dictionary<string, object> cal)
    {
        var parts = new List<string>();

        if (cal.TryGetValue("Weekday", out var wd))
        {
            var dayNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
            if (wd is long dow && dow is >= 0 and <= 6)
                parts.Add(dayNames[dow]);
        }

        string? daysOfMonth = null;
        if (cal.TryGetValue("Day", out var day))
            daysOfMonth = day.ToString();

        string? months = null;
        if (cal.TryGetValue("Month", out var month))
            months = month.ToString();

        var time = "";
        if (cal.TryGetValue("Hour", out var h))
            time += $"{h:00}";
        if (cal.TryGetValue("Minute", out var m))
            time += $":{m:00}";

        return new TaskTriggerItem
        {
            Type = "Calendar",
            Enabled = true,
            Repetition = time.Length > 0 ? time : null,
            DaysOfWeek = parts.Count > 0 ? string.Join(", ", parts) : null,
            DaysOfMonth = daysOfMonth,
            Months = months
        };
    }

    private static Dictionary<string, object> ParseDict(XElement? dict)
    {
        var result = new Dictionary<string, object>();
        if (dict == null) return result;

        var elements = dict.Elements().ToList();
        for (var i = 0; i < elements.Count - 1; i++)
        {
            if (elements[i].Name != "key") continue;

            var key = elements[i].Value;
            var valueElement = elements[i + 1];

            result[key] = ParseValue(valueElement);
            i++; // skip value element
        }

        return result;
    }

    private static object ParseValue(XElement element)
    {
        return element.Name.LocalName switch
        {
            "string" => element.Value,
            "integer" => long.TryParse(element.Value, out var l) ? l : element.Value,
            "real" => double.TryParse(element.Value, out var d) ? d : element.Value,
            "true" => (object)true,
            "false" => false,
            "dict" => ParseDict(element),
            "array" => element.Elements().Select(ParseValue).ToList(),
            "data" => element.Value,
            "date" => element.Value,
            _ => element.Value
        };
    }

    private static string? GetString(Dictionary<string, object> dict, string key)
        => dict.TryGetValue(key, out var val) ? val.ToString() : null;

    private static List<string> GetStringArray(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out var val) && val is List<object> list)
            return list.Select(o => o.ToString()!).ToList();
        return [];
    }
}
