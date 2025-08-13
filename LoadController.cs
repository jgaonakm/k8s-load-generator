using Microsoft.AspNetCore.Mvc;
using Models;
using Services;

namespace Controllers;

[ApiController]
[Route("load")]
public class LoadController : ControllerBase
{
    private readonly ILoadService _svc;

    public LoadController(ILoadService svc) => _svc = svc;

    [HttpPost("cpu")]
    public ActionResult<Guid> StartCpu([FromBody] CpuLoadRequest req)
        => Ok(_svc.StartCpuLoad(req));

    [HttpPost("memory")]
    public ActionResult<Guid> StartMemory([FromBody] MemoryLoadRequest req)
        => Ok(_svc.StartMemoryLoad(req));

    [HttpPost("{jobId:guid}/stop")]
    public IActionResult Stop(Guid jobId)
        => _svc.Stop(jobId) ? NoContent() : NotFound();

    [HttpGet("status")]
    public ActionResult<IEnumerable<JobStatus>> List() => Ok(_svc.List());

    [HttpGet("{jobId:guid}")]
    public ActionResult<JobStatus> Get(Guid jobId)
        => _svc.Get(jobId) is { } s ? Ok(s) : NotFound();
}
