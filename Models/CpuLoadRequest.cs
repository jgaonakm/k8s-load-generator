namespace Models;

public record CpuLoadRequest
{
    // Number of busy worker threads; default: Environment.ProcessorCount
    public int Threads { get; init; } = Environment.ProcessorCount;
    // 1..100; duty cycle per 100ms window
    public int IntensityPercent { get; init; } = 100;
    // seconds to run; default 60
    public int DurationSeconds { get; init; } = 60;
}
