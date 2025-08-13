namespace Models;

public enum JobType { Cpu, Memory }
public enum JobState { Running, Completed, Cancelled, Failed }

public record JobStatus
{
    public Guid JobId { get; init; }
    public JobType Type { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public int? DurationSeconds { get; init; }
    public string? Details { get; init; }
    public JobState State { get; set; } = JobState.Running;
}