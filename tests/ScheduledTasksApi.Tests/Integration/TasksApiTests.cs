using System.Net;
using System.Net.Http.Json;
using NSubstitute;
using ScheduledTasksApi.Models;

namespace ScheduledTasksApi.Tests.Integration;

public class TasksApiTests : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client;
    private readonly ApiFixture _fixture;

    public TasksApiTests(ApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task ListTasks_ReturnsOk()
    {
        _fixture.TaskSchedulerService.FindTasks("*", "*")
            .Returns(new List<TaskItem>
            {
                new() { Name = "Test", Path = "\\Test", State = "Ready", Enabled = true, IsActive = true, LastRunTime = DateTime.MinValue, LastTaskResult = 0, NextRunTime = DateTime.MaxValue }
            });

        var response = await _client.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tasks = await response.Content.ReadFromJsonAsync<List<TaskItem>>();
        Assert.NotNull(tasks);
        Assert.Single(tasks);
    }

    [Fact]
    public async Task GetTask_NotFound_Returns404()
    {
        _fixture.TaskSchedulerService.FindTask("nope", "*").Returns((TaskItem?)null);

        var response = await _client.GetAsync("/api/tasks/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RunTask_Success_ReturnsAccepted()
    {
        _fixture.TaskSchedulerService.FindTask("MyTask", "*")
            .Returns(new TaskItem { Name = "MyTask", Path = "\\MyTask", State = "Ready", Enabled = true, IsActive = true, LastRunTime = DateTime.MinValue, LastTaskResult = 0, NextRunTime = DateTime.MaxValue });

        var response = await _client.PostAsync("/api/tasks/MyTask/run", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task RunTask_AlreadyRunning_ReturnsConflict()
    {
        _fixture.TaskSchedulerService.FindTask("Running", "*")
            .Returns(new TaskItem { Name = "Running", Path = "\\Running", State = "Running", Enabled = true, IsActive = true, LastRunTime = DateTime.MinValue, LastTaskResult = 0, NextRunTime = DateTime.MaxValue });

        var response = await _client.PostAsync("/api/tasks/Running/run", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
