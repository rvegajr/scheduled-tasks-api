using ScheduledTasksApi.Models;

namespace ScheduledTasksApi.Services;

public interface ITaskSchedulerService
{
    IReadOnlyList<TaskItem> FindTasks(string pattern, string allowedFilter);
    TaskItem? FindTask(string name, string allowedFilter);
    TaskItemDetail? FindTaskDetail(string name, string allowedFilter);
    void RunTask(string taskName);
    void StopTask(string taskName);
    IReadOnlyList<EventItem> GetTaskHistory(string taskName);
}
