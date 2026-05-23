#if !WINDOWS
using ScheduledTasksApi.Extensions;
using ScheduledTasksApi.Models;
using ScheduledTasksApi.Services.Parsing;

namespace ScheduledTasksApi.Services.Mac;

public class MacServiceManager(IProcessRunner runner) : IServiceManager
{
    public IReadOnlyList<ServiceItem> FindServices(string pattern, string allowedFilter)
    {
        var regex = pattern.ToWildcardRegex();

        var result = runner.RunAsync("launchctl", "list").GetAwaiter().GetResult();
        if (result.ExitCode != 0)
            return [];

        return LaunchdPlistParser.ParseLaunchctlList(result.StandardOutput)
            .Select(t => new ServiceItem
            {
                ServiceName = t.Name,
                DisplayName = t.Name,
                Status = t.State,
                ServiceType = "Launchd",
                StartType = "Automatic"
            })
            .FilterByWildcard(allowedFilter, s => [s.ServiceName, s.DisplayName])
            .Where(s => regex.IsMatch(s.ServiceName) || regex.IsMatch(s.DisplayName))
            .ToList();
    }

    public ServiceItem? FindService(string name, string allowedFilter)
    {
        var services = FindServices(name, allowedFilter);
        return services.Count == 1 ? services[0] : null;
    }

    public ServiceItemDetail? FindServiceDetail(string name, string allowedFilter)
    {
        var service = FindService(name, allowedFilter);
        if (service is null) return null;

        // Try to find and parse the plist for additional details
        string? description = null;
        string? imagePath = null;
        string? serviceAccount = null;
        int? processId = null;

        var plistPath = FindPlistPath(service.ServiceName);
        if (plistPath != null && File.Exists(plistPath))
        {
            var detail = LaunchdPlistParser.ParsePlist(service.ServiceName, File.ReadAllText(plistPath));
            description = detail.Description;
            imagePath = detail.Actions.FirstOrDefault()?.Path;
            serviceAccount = detail.Principal?.UserId;
        }

        // Get PID from launchctl list
        var result = runner.RunAsync("launchctl", "list").GetAwaiter().GetResult();
        if (result.ExitCode == 0)
        {
            foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.TrimEntries))
            {
                var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && parts[2] == service.ServiceName && parts[0] != "-" && int.TryParse(parts[0], out var pid))
                {
                    processId = pid;
                    break;
                }
            }
        }

        return new ServiceItemDetail
        {
            ServiceName = service.ServiceName,
            DisplayName = service.DisplayName,
            Status = service.Status,
            ServiceType = service.ServiceType,
            StartType = service.StartType,
            Description = description,
            ImagePath = imagePath,
            ServiceAccount = serviceAccount,
            ProcessId = processId
        };
    }

    public async Task StartServiceAsync(string serviceName, CancellationToken ct = default)
    {
        var uid = Environment.GetEnvironmentVariable("UID") ?? "501";
        var result = await runner.RunAsync("launchctl", $"kickstart system/{serviceName}", ct);
        if (result.ExitCode != 0)
        {
            result = await runner.RunAsync("launchctl", $"kickstart gui/{uid}/{serviceName}", ct);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Failed to start service: {result.StandardError}");
        }
    }

    public async Task StopServiceAsync(string serviceName, TimeSpan timeout, CancellationToken ct = default)
    {
        var uid = Environment.GetEnvironmentVariable("UID") ?? "501";
        var result = await runner.RunAsync("launchctl", $"kill SIGTERM system/{serviceName}", ct);
        if (result.ExitCode != 0)
        {
            result = await runner.RunAsync("launchctl", $"kill SIGTERM gui/{uid}/{serviceName}", ct);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Failed to stop service: {result.StandardError}");
        }

        // Poll until stopped or timeout
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var listResult = await runner.RunAsync("launchctl", "list", ct);
            var running = listResult.StandardOutput.Split('\n')
                .Any(line => line.Contains(serviceName) && !line.StartsWith("-"));
            if (!running) return;
            await Task.Delay(1000, ct);
        }

        throw new TimeoutException($"Service did not stop within {timeout.TotalSeconds}s");
    }

    public async Task RestartServiceAsync(string serviceName, TimeSpan timeout, CancellationToken ct = default)
    {
        try { await StopServiceAsync(serviceName, timeout, ct); } catch (InvalidOperationException) { }
        await StartServiceAsync(serviceName, ct);
    }

    private static string? FindPlistPath(string label)
    {
        string[] dirs =
        [
            "/Library/LaunchDaemons",
            "/Library/LaunchAgents",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/LaunchAgents")
        ];

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            var path = Path.Combine(dir, $"{label}.plist");
            if (File.Exists(path)) return path;
        }
        return null;
    }
}
#endif
