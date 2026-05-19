namespace ScheduledTasksApi.Models;

public record EventItem
{
    public string? EventId { get; init; }
    public string? ProviderName { get; init; }
    public DateTime? TimeCreated { get; init; }
    public string? OpcodeDisplayName { get; init; }
    public string? ActivityId { get; init; }
    public string? Description { get; init; }
}
