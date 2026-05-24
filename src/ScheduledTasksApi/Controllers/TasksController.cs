using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScheduledTasksApi.Models;
using ScheduledTasksApi.Services;

namespace ScheduledTasksApi.Controllers;

[Authorize]
[ApiController]
[Route("api/tasks")]
public class TasksController(ITaskSchedulerService taskService, IConfiguration configuration) : ControllerBase
{
    private string AllowedFilter => configuration.GetValue<string>("AllowedTasks") ?? "";

    /// <summary>
    /// List tasks matching a wildcard pattern.
    /// </summary>
    /// <remarks>
    /// **curl examples:**
    ///
    ///     # Linux/macOS (API key auth)
    ///     curl http://localhost:5000/api/tasks -H "X-Api-Key: YOUR_KEY"
    ///
    ///     # With pattern filter
    ///     curl http://localhost:5000/api/tasks?pattern=*Backup* -H "X-Api-Key: YOUR_KEY"
    ///
    ///     # Windows (Negotiate auth)
    ///     curl --negotiate -u : https://localhost:5001/api/tasks
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TaskItem>), 200)]
    public ActionResult<IReadOnlyList<TaskItem>> List([FromQuery] string pattern = "*")
    {
        var tasks = taskService.FindTasks(pattern, AllowedFilter);
        return Ok(tasks);
    }

    /// <summary>
    /// Get a single task with full detail (actions, triggers, settings, principal).
    /// </summary>
    /// <remarks>
    /// **curl examples:**
    ///
    ///     curl http://localhost:5000/api/tasks/BackupTask -H "X-Api-Key: YOUR_KEY"
    ///
    ///     # Windows
    ///     curl --negotiate -u : https://localhost:5001/api/tasks/BackupTask
    /// </remarks>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(TaskItemDetail), 200)]
    [ProducesResponseType(404)]
    public ActionResult<TaskItemDetail> Get(string name)
    {
        var task = taskService.FindTaskDetail(name, AllowedFilter);
        if (task is null)
            return NotFound($"Task '{name}' not found or not allowed");
        return Ok(task);
    }

    /// <summary>
    /// Get the current state of a task.
    /// </summary>
    /// <remarks>
    /// **curl example:**
    ///
    ///     curl http://localhost:5000/api/tasks/BackupTask/status -H "X-Api-Key: YOUR_KEY"
    /// </remarks>
    [HttpGet("{name}/status")]
    [ProducesResponseType(typeof(string), 200)]
    [ProducesResponseType(404)]
    public ActionResult<string> GetStatus(string name)
    {
        var task = taskService.FindTask(name, AllowedFilter);
        if (task is null)
            return NotFound($"Task '{name}' not found or not allowed");
        return Ok(task.State);
    }

    /// <summary>
    /// Get the event log history of a task.
    /// </summary>
    /// <remarks>
    /// **curl example:**
    ///
    ///     curl http://localhost:5000/api/tasks/BackupTask/history -H "X-Api-Key: YOUR_KEY"
    /// </remarks>
    [HttpGet("{name}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<EventItem>), 200)]
    [ProducesResponseType(404)]
    public ActionResult<IReadOnlyList<EventItem>> GetHistory(string name)
    {
        var task = taskService.FindTask(name, AllowedFilter);
        if (task is null)
            return NotFound($"Task '{name}' not found or not allowed");

        var history = taskService.GetTaskHistory(task.Name);
        return Ok(history);
    }

    /// <summary>
    /// Run a scheduled task.
    /// </summary>
    /// <remarks>
    /// **curl example:**
    ///
    ///     curl -X POST http://localhost:5000/api/tasks/BackupTask/run -H "X-Api-Key: YOUR_KEY"
    /// </remarks>
    [HttpPost("{name}/run")]
    [ProducesResponseType(202)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public ActionResult Run(string name)
    {
        var task = taskService.FindTask(name, AllowedFilter);
        if (task is null)
            return NotFound($"Task '{name}' not found or not allowed");

        if (task.State == "Running")
            return Conflict("Task is already running");

        taskService.RunTask(task.Name);
        return Accepted();
    }

    /// <summary>
    /// Stop a running task.
    /// </summary>
    /// <remarks>
    /// **curl example:**
    ///
    ///     curl -X POST http://localhost:5000/api/tasks/BackupTask/stop -H "X-Api-Key: YOUR_KEY"
    /// </remarks>
    [HttpPost("{name}/stop")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public ActionResult Stop(string name)
    {
        var task = taskService.FindTask(name, AllowedFilter);
        if (task is null)
            return NotFound($"Task '{name}' not found or not allowed");

        if (task.State != "Running")
            return Conflict("Task is not running");

        taskService.StopTask(task.Name);
        return Ok("Task stopped");
    }
}
