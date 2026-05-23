using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduledTasksApi.Models;
using ScheduledTasksApi.Services;

namespace ScheduledTasksApi.Controllers;

[Authorize]
[ApiController]
[Route("api/services")]
public class ServicesController(IServiceManager serviceManager, IConfiguration configuration) : ControllerBase
{
    private string AllowedFilter => configuration.GetValue<string>("AllowedServices") ?? "";
    private TimeSpan Timeout => TimeSpan.FromSeconds(configuration.GetValue<int>("RestartTimeoutSeconds", 120));

    /// <summary>
    /// List services matching a wildcard pattern.
    /// </summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<ServiceItem>> List([FromQuery] string pattern = "*")
    {
        var services = serviceManager.FindServices(pattern, AllowedFilter);
        return Ok(services);
    }

    /// <summary>
    /// Get a single service with full detail (description, image path, account, PID).
    /// </summary>
    [HttpGet("{name}")]
    public ActionResult<ServiceItemDetail> Get(string name)
    {
        var service = serviceManager.FindServiceDetail(name, AllowedFilter);
        if (service is null)
            return NotFound($"Service '{name}' not found or not allowed");
        return Ok(service);
    }

    /// <summary>
    /// Get the current status of a service.
    /// </summary>
    [HttpGet("{name}/status")]
    public ActionResult<string> GetStatus(string name)
    {
        var service = serviceManager.FindService(name, AllowedFilter);
        if (service is null)
            return NotFound($"Service '{name}' not found or not allowed");
        return Ok(service.Status);
    }

    /// <summary>
    /// Start a stopped service.
    /// </summary>
    [HttpPost("{name}/start")]
    public async Task<ActionResult> Start(string name, CancellationToken ct)
    {
        var service = serviceManager.FindService(name, AllowedFilter);
        if (service is null)
            return NotFound($"Service '{name}' not found or not allowed");

        try
        {
            await serviceManager.StartServiceAsync(service.ServiceName, ct);
            return Ok("Service started");
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Stop a running service.
    /// </summary>
    [HttpPost("{name}/stop")]
    public async Task<ActionResult> Stop(string name, CancellationToken ct)
    {
        var service = serviceManager.FindService(name, AllowedFilter);
        if (service is null)
            return NotFound($"Service '{name}' not found or not allowed");

        try
        {
            await serviceManager.StopServiceAsync(service.ServiceName, Timeout, ct);
            return Ok("Service stopped");
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (TimeoutException ex)
        {
            return StatusCode(504, ex.Message);
        }
    }

    /// <summary>
    /// Restart a service (stop then start).
    /// </summary>
    [HttpPost("{name}/restart")]
    public async Task<ActionResult> Restart(string name, CancellationToken ct)
    {
        var service = serviceManager.FindService(name, AllowedFilter);
        if (service is null)
            return NotFound($"Service '{name}' not found or not allowed");

        try
        {
            await serviceManager.RestartServiceAsync(service.ServiceName, Timeout, ct);
            return Ok("Service restarted");
        }
        catch (TimeoutException ex)
        {
            return StatusCode(504, ex.Message);
        }
    }
}
