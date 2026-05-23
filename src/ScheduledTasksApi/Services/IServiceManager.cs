using ScheduledTasksApi.Models;

namespace ScheduledTasksApi.Services;

public interface IServiceManager
{
    IReadOnlyList<ServiceItem> FindServices(string pattern, string allowedFilter);
    ServiceItem? FindService(string name, string allowedFilter);
    ServiceItemDetail? FindServiceDetail(string name, string allowedFilter);
    Task StartServiceAsync(string serviceName, CancellationToken ct = default);
    Task StopServiceAsync(string serviceName, TimeSpan timeout, CancellationToken ct = default);
    Task RestartServiceAsync(string serviceName, TimeSpan timeout, CancellationToken ct = default);
}
