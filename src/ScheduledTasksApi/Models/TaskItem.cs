namespace ScheduledTasksApi.Models;

public record TaskItem
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public string? Folder { get; init; }
    public required string State { get; init; }
    public bool Enabled { get; init; }
    public bool IsActive { get; init; }
    public DateTime LastRunTime { get; init; }
    public int LastTaskResult { get; init; }
    public DateTime NextRunTime { get; init; }
}
