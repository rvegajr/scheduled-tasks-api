using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using ScheduledTasksApi.Controllers;
using ScheduledTasksApi.Models;
using ScheduledTasksApi.Services;

namespace ScheduledTasksApi.Tests.Controllers;

public class TasksControllerTests
{
    private readonly ITaskSchedulerService _taskService = Substitute.For<ITaskSchedulerService>();
    private readonly IConfiguration _configuration;
    private readonly TasksController _controller;

    public TasksControllerTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AllowedTasks"] = "*"
            })
            .Build();
        _controller = new TasksController(_taskService, _configuration);
    }

    [Fact]
    public void List_ReturnsOkWithTasks()
    {
        var tasks = new List<TaskItem> { CreateTask("Task1") };
        _taskService.FindTasks("*", "*").Returns(tasks);

        var result = _controller.List();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(tasks, ok.Value);
    }

    [Fact]
    public void Get_NotFound_Returns404()
    {
        _taskService.FindTask("missing", "*").Returns((TaskItem?)null);

        var result = _controller.Get("missing");

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public void Get_Found_ReturnsTask()
    {
        var task = CreateTask("MyTask");
        _taskService.FindTask("MyTask", "*").Returns(task);

        var result = _controller.Get("MyTask");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(task, ok.Value);
    }

    [Fact]
    public void Run_TaskNotFound_Returns404()
    {
        _taskService.FindTask("missing", "*").Returns((TaskItem?)null);

        var result = _controller.Run("missing");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void Run_TaskAlreadyRunning_ReturnsConflict()
    {
        var task = CreateTask("RunningTask", "Running");
        _taskService.FindTask("RunningTask", "*").Returns(task);

        var result = _controller.Run("RunningTask");

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public void Run_Success_ReturnsAccepted()
    {
        var task = CreateTask("IdleTask", "Ready");
        _taskService.FindTask("IdleTask", "*").Returns(task);

        var result = _controller.Run("IdleTask");

        Assert.IsType<AcceptedResult>(result);
        _taskService.Received(1).RunTask("IdleTask");
    }

    [Fact]
    public void Stop_TaskNotRunning_ReturnsConflict()
    {
        var task = CreateTask("IdleTask", "Ready");
        _taskService.FindTask("IdleTask", "*").Returns(task);

        var result = _controller.Stop("IdleTask");

        Assert.IsType<ConflictObjectResult>(result);
    }

    private static TaskItem CreateTask(string name, string state = "Ready") => new()
    {
        Name = name,
        Path = $"\\{name}",
        State = state,
        Enabled = true,
        IsActive = true,
        LastRunTime = DateTime.MinValue,
        LastTaskResult = 0,
        NextRunTime = DateTime.MaxValue
    };
}
