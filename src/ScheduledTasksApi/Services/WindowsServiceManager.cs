using System.ServiceProcess;
using ScheduledTasksApi.Extensions;
using ScheduledTasksApi.Models;

namespace ScheduledTasksApi.Services;

public class WindowsServiceManager : IWindowsServiceManager
{
    public IReadOnlyList<ServiceItem> FindServices(string pattern, string allowedFilter)
    {
        var regex = pattern.ToWildcardRegex();
        var all = ServiceController.GetServices();

        return all
            .Select(MapService)
            .FilterByWildcard(allowedFilter, s => [s.ServiceName, s.DisplayName])
            .Where(s => regex.IsMatch(s.ServiceName) || regex.IsMatch(s.DisplayName))
            .ToList();
    }

    public ServiceItem? FindService(string name, string allowedFilter)
    {
        var services = FindServices(name, allowedFilter);
        return services.Count == 1 ? services[0] : null;
    }

    public async Task StartServiceAsync(string serviceName, CancellationToken ct = default)
    {
        using var sc = new ServiceController(serviceName);

        if (sc.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
            throw new InvalidOperationException("Service is already running");

        sc.Start();
        await WaitForStatusAsync(sc, ServiceControllerStatus.Running, TimeSpan.FromSeconds(30), ct);
    }

    public async Task StopServiceAsync(string serviceName, TimeSpan timeout, CancellationToken ct = default)
    {
        using var sc = new ServiceController(serviceName);

        if (sc.Status == ServiceControllerStatus.Stopped)
            throw new InvalidOperationException("Service is already stopped");

        sc.Stop();
        await WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, timeout, ct);
    }

    public async Task RestartServiceAsync(string serviceName, TimeSpan timeout, CancellationToken ct = default)
    {
        using var sc = new ServiceController(serviceName);

        if (sc.Status is ServiceControllerStatus.Running or ServiceControllerStatus.Paused or ServiceControllerStatus.PausePending)
        {
            sc.Stop();
            await WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, timeout, ct);
        }

        sc.Start();
        await WaitForStatusAsync(sc, ServiceControllerStatus.Running, timeout, ct);
    }

    private static async Task WaitForStatusAsync(
        ServiceController sc,
        ServiceControllerStatus target,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            sc.Refresh();
            if (sc.Status == target) return;
            await Task.Delay(1000, ct);
        }

        throw new System.TimeoutException($"Service did not reach '{target}' within {timeout.TotalSeconds}s");
    }

    private static ServiceItem MapService(ServiceController sc) => new()
    {
        ServiceName = sc.ServiceName,
        DisplayName = sc.DisplayName,
        Status = sc.Status.ToString(),
        ServiceType = sc.ServiceType.ToString(),
        StartType = sc.StartType.ToString(),
        MachineName = sc.MachineName,
        CanStop = sc.CanStop,
        CanPauseAndContinue = sc.CanPauseAndContinue,
        CanShutdown = sc.CanShutdown,
        DependentServices = sc.DependentServices.Select(d => d.ServiceName).ToArray(),
        ServicesDependedOn = sc.ServicesDependedOn.Select(d => d.ServiceName).ToArray()
    };
}
