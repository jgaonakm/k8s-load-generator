using System.Collections.Concurrent;
using System.Diagnostics;
using Models;

namespace Services;

public sealed class LoadService : ILoadService
{
    private readonly ConcurrentDictionary<Guid, JobRuntime> _jobs = new();
    private readonly int _maxThreads;
    private readonly int _maxMemMb;
    private readonly int _maxDuration;
    private ILogger<LoadService> _logger;


    public LoadService(IConfiguration cfg, ILogger<LoadService> logger)
    {
        _maxThreads = Math.Max(1, cfg.GetValue("MAX_CPU_THREADS", Environment.ProcessorCount));
        _maxMemMb = Math.Max(32, cfg.GetValue("MAX_MEM_MB", 4096)); // 4 GB default cap
        _maxDuration = Math.Max(5, cfg.GetValue("MAX_DURATION_SEC", 1800)); // 30 min
        _logger = logger;
        _logger.LogInformation($"Running with: max threads={_maxThreads}, max memory MB={_maxMemMb}, max duration sec={_maxDuration}");
    }

    public Guid StartCpuLoad(CpuLoadRequest req)
    {
        _logger.LogInformation($"Starting CPU load: threads={req.Threads}, intensity={req.IntensityPercent}%, duration={req.DurationSeconds}s");
        var jobId = Guid.NewGuid();
        var threads = Math.Min(Math.Max(1, req.Threads), _maxThreads);
        var intensity = Math.Clamp(req.IntensityPercent, 1, 100);
        var duration = Math.Min(Math.Max(1, req.DurationSeconds), _maxDuration);

        var status = new JobStatus
        {
            JobId = jobId,
            Type = JobType.Cpu,
            StartedAtUtc = DateTime.UtcNow,
            DurationSeconds = duration,
            Details = $"threads={threads}, intensity={intensity}%"
        };

        var cts = new CancellationTokenSource();
        var tasks = new List<Task>();

        for (int t = 0; t < threads; t++)
        {
            _logger.LogInformation($"Starting CPU worker thread {t + 1}/{threads}");
            tasks.Add(Task.Run(() => CpuWorker(intensity, duration, cts.Token, ref _logger)));
        }

        Track(jobId, status, cts, tasks, onFinish: () => status.State = JobState.Completed);
        return jobId;
    }

    public Guid StartMemoryLoad(MemoryLoadRequest req)
    {
        _logger.LogInformation($"Starting Memory load: mb={req.Megabytes}, holdUntilStopped={req.HoldUntilStopped}, duration={req.DurationSeconds}s");
        var jobId = Guid.NewGuid();
        var mb = Math.Min(Math.Max(1, req.Megabytes), _maxMemMb);
        var duration = req.HoldUntilStopped ? (int?)null : Math.Min(Math.Max(1, req.DurationSeconds), _maxDuration);

        var status = new JobStatus
        {
            JobId = jobId,
            Type = JobType.Memory,
            StartedAtUtc = DateTime.UtcNow,
            DurationSeconds = duration,
            Details = $"mb={mb}, hold={req.HoldUntilStopped}"
        };

        var cts = new CancellationTokenSource();
        var task = Task.Run(() => MemoryWorker(mb, duration, cts.Token, status, ref _logger), cts.Token);

        Track(jobId, status, cts, new[] { task }, onFinish: () =>
        {
            if (status.State == JobState.Running) status.State = JobState.Completed;
        });

        return jobId;
    }

    public bool Stop(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var rt))
        {
            _logger.LogInformation($"Stopping job {jobId}-{rt.Status.Type}");
            rt.Cts.Cancel();
            return true;
        }
        return false;
    }

    public JobStatus? Get(Guid jobId) => _jobs.TryGetValue(jobId, out var rt) ? rt.Status : null;

    public IEnumerable<JobStatus> List() => _jobs.Values.Select(v => v.Status);

    public void StopAll()
    {
        foreach (var j in _jobs.Values) j.Cts.Cancel();
    }

    // ---------- workers ----------

    private static void CpuWorker(int intensityPercent, int durationSec, CancellationToken ct, ref ILogger<LoadService> _logger)
    {

        // Duty-cycle busy loop in 100ms windows
        var windowMs = 100;
        var busyMs = windowMs * intensityPercent / 100;
        var stopAt = DateTime.UtcNow.AddSeconds(durationSec);
        var sw = new Stopwatch();
        _logger.LogInformation($"Starting CPU Worker. Stopping in {durationSec}s");
        while (DateTime.UtcNow < stopAt && !ct.IsCancellationRequested)
        {
            sw.Restart();
            // Busy phase
            while (sw.ElapsedMilliseconds < busyMs && !ct.IsCancellationRequested)
            {
                // tight spin
                _ = Math.Sqrt(12345.6789); // do some work
            }
            // Sleep phase
            var sleep = windowMs - (int)sw.ElapsedMilliseconds;
            if (sleep > 0) Task.Delay(sleep, ct).TryWait();
        }
        _logger.LogInformation("CPU Worker finished");
    }

    private static void MemoryWorker(int megabytes, int? durationSec, CancellationToken ct, JobStatus status, ref ILogger<LoadService> _logger)
    {
        var chunks = new List<byte[]>();
        const int chunkSize = 1024 * 1024; // 1MB
        _logger.LogInformation($"Starting Memory Worker. Stopping in {durationSec}s");
        try
        {
            for (int i = 0; i < megabytes && !ct.IsCancellationRequested; i++)
            {
                var arr = GC.AllocateUninitializedArray<byte>(chunkSize, pinned: false);
                // touch memory to ensure physical commitment
                for (int k = 0; k < arr.Length; k += 4096) arr[k] = 1;
                chunks.Add(arr);
            }

            if (ct.IsCancellationRequested) { status.State = JobState.Cancelled; return; }

            if (durationSec is null)
            {
                // hold until stop
                ct.WaitHandle.WaitOne();
                status.State = JobState.Cancelled;
            }
            else
            {
                Task.Delay(TimeSpan.FromSeconds(durationSec.Value), ct).TryWait();
                status.State = ct.IsCancellationRequested ? JobState.Cancelled : JobState.Completed;
            }
        }
        catch (Exception ex)
        {
            status.State = JobState.Failed;
            var error = (status.Details ?? "") + $" | err={ex.GetType().Name}";
            status = status with { Details = error };
            _logger.LogError(error);
        }
        finally
        {
            // release
            chunks.Clear();
            GC.Collect();
            _logger.LogInformation("Memory worker finished");
        }
    }

    // ---------- tracking ----------
    private void Track(Guid id, JobStatus status, CancellationTokenSource cts, IEnumerable<Task> tasks, Action onFinish)
    {
        _logger.LogInformation($"Tracking job {id} of type {status.Type} with {tasks.Count()} tasks");
        var rt = new JobRuntime(status, cts, Task.WhenAll(tasks).ContinueWith(_ =>
        {
            if (cts.IsCancellationRequested && status.State == JobState.Running)
                status.State = JobState.Cancelled;
            onFinish();
            _jobs.TryRemove(id, out JobRuntime? _);
        }));
        _jobs[id] = rt;
    }

    private sealed record JobRuntime(JobStatus Status, CancellationTokenSource Cts, Task Done);
}