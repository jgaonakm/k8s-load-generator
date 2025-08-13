using Models;
namespace Services;

public interface ILoadService
{
    Guid StartCpuLoad(CpuLoadRequest req);
    Guid StartMemoryLoad(MemoryLoadRequest req);
    bool Stop(Guid jobId);
    JobStatus? Get(Guid jobId);
    IEnumerable<JobStatus> List();
    void StopAll();
}