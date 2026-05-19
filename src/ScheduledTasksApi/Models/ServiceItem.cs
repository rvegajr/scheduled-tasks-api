namespace ScheduledTasksApi.Models;

public record ServiceItem
{
    public required string ServiceName { get; init; }
    public required string DisplayName { get; init; }
    public required string Status { get; init; }
    public required string ServiceType { get; init; }
    public required string StartType { get; init; }
    public string? MachineName { get; init; }
    public bool CanStop { get; init; }
    public bool CanPauseAndContinue { get; init; }
    public bool CanShutdown { get; init; }
    public string[]? DependentServices { get; init; }
    public string[]? ServicesDependedOn { get; init; }
}
