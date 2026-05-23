namespace ScheduledTasksApi.Models;

public record TaskActionItem
{
    public required string Type { get; init; }
    public string? Path { get; init; }
    public string? Arguments { get; init; }
    public string? WorkingDirectory { get; init; }
}

public record TaskTriggerItem
{
    public required string Type { get; init; }
    public bool Enabled { get; init; }
    public DateTime? StartBoundary { get; init; }
    public DateTime? EndBoundary { get; init; }
    public string? Repetition { get; init; }
    public string? DaysOfWeek { get; init; }
    public string? DaysOfMonth { get; init; }
    public string? Months { get; init; }
}

public record TaskSettingsItem
{
    public TimeSpan? ExecutionTimeLimit { get; init; }
    public bool AllowHardTerminate { get; init; }
    public bool RunOnlyIfIdle { get; init; }
    public bool DisallowStartIfOnBatteries { get; init; }
    public bool StopIfGoingOnBatteries { get; init; }
    public bool AllowStartOnDemand { get; init; }
    public bool Hidden { get; init; }
    public string? MultipleInstances { get; init; }
    public int Priority { get; init; }
    public bool RunOnlyIfNetworkAvailable { get; init; }
    public bool WakeToRun { get; init; }
    public bool StartWhenAvailable { get; init; }
    public TimeSpan? RestartInterval { get; init; }
    public int RestartCount { get; init; }
    public TimeSpan? DeleteExpiredTaskAfter { get; init; }
}

public record TaskPrincipalItem
{
    public string? UserId { get; init; }
    public string? LogonType { get; init; }
    public string? RunLevel { get; init; }
    public string? GroupId { get; init; }
    public string? DisplayName { get; init; }
}

public record TaskItemDetail : TaskItem
{
    public string? Author { get; init; }
    public string? Description { get; init; }
    public DateTime? RegistrationDate { get; init; }
    public string? Documentation { get; init; }
    public string? RegistrationSource { get; init; }
    public string? URI { get; init; }
    public string? Version { get; init; }

    public IReadOnlyList<TaskActionItem> Actions { get; init; } = [];
    public IReadOnlyList<TaskTriggerItem> Triggers { get; init; } = [];
    public TaskSettingsItem? Settings { get; init; }
    public TaskPrincipalItem? Principal { get; init; }
}
