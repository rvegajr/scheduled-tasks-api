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
    [HttpGet]
    public ActionResult<IReadOnlyList<TaskItem>> List([FromQuery] string pattern = "*")
    {
        var tasks = taskService.FindTasks(pattern, AllowedFilter);
        return Ok(tasks);
    }

    /// <summary>
    /// Get a single task by exact name.
    /// </summary>
    [HttpGet("{name}")]
    public ActionResult<TaskItem> Get(string name)
    {
        var task = taskService.FindTask(name, AllowedFilter);
        if (task is null)
            return NotFound($"Task '{name}' not found or not allowed");
        return Ok(task);
    }

    /// <summary>
    /// Get the current state of a task.
    /// </summary>
    [HttpGet("{name}/status")]
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
    [HttpGet("{name}/history")]
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
    [HttpPost("{name}/run")]
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
    [HttpPost("{name}/stop")]
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
