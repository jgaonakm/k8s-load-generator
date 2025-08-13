namespace Models;

public record MemoryLoadRequest
{
    // Total MB to allocate and touch
    public int Megabytes { get; init; } = 512;
    // seconds to hold allocation; default 60
    public int DurationSeconds { get; init; } = 60;
    // If true, keeps memory until explicitly stopped
    public bool HoldUntilStopped { get; init; } = false;
}

