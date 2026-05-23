namespace ScheduledTasksApi.Models;

public record ServiceItemDetail : ServiceItem
{
    public string? Description { get; init; }
    public string? ImagePath { get; init; }
    public string? ServiceAccount { get; init; }
    public int? ProcessId { get; init; }
    public string? ErrorControl { get; init; }
}
