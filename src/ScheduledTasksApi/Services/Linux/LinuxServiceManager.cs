#if !WINDOWS
using ScheduledTasksApi.Extensions;
using ScheduledTasksApi.Models;
using ScheduledTasksApi.Services.Parsing;

namespace ScheduledTasksApi.Services.Linux;

public class LinuxServiceManager(IProcessRunner runner) : IServiceManager
{
    public IReadOnlyList<ServiceItem> FindServices(string pattern, string allowedFilter)
    {
        var regex = pattern.ToWildcardRegex();

        var result = runner.RunAsync("systemctl", "list-units --type=service --all --output=json").GetAwaiter().GetResult();
        if (result.ExitCode != 0)
            return [];

        return SystemdOutputParser.ParseUnitsJson(result.StandardOutput)
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

        var result = runner.RunAsync("systemctl", $"show {service.ServiceName}").GetAwaiter().GetResult();
        if (result.ExitCode != 0)
            return null;

        var props = SystemdOutputParser.ParseShowOutput(result.StandardOutput);
        return SystemdOutputParser.MapShowToServiceDetail(service.ServiceName, props);
    }

    public async Task StartServiceAsync(string serviceName, CancellationToken ct = default)
    {
        var result = await runner.RunAsync("systemctl", $"start {serviceName}", ct);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Failed to start service: {result.StandardError}");
    }

    public async Task StopServiceAsync(string serviceName, TimeSpan timeout, CancellationToken ct = default)
    {
        var result = await runner.RunAsync("systemctl", $"stop {serviceName}", ct);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Failed to stop service: {result.StandardError}");

        // Poll until stopped or timeout
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var status = await runner.RunAsync("systemctl", $"is-active {serviceName}", ct);
            if (status.StandardOutput.Trim() == "inactive")
                return;
            await Task.Delay(1000, ct);
        }

        throw new TimeoutException($"Service did not stop within {timeout.TotalSeconds}s");
    }

    public async Task RestartServiceAsync(string serviceName, TimeSpan timeout, CancellationToken ct = default)
    {
        var result = await runner.RunAsync("systemctl", $"restart {serviceName}", ct);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Failed to restart service: {result.StandardError}");

        // Poll until active or timeout
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var status = await runner.RunAsync("systemctl", $"is-active {serviceName}", ct);
            if (status.StandardOutput.Trim() == "active")
                return;
            await Task.Delay(1000, ct);
        }

        throw new TimeoutException($"Service did not restart within {timeout.TotalSeconds}s");
    }
}
#endif
