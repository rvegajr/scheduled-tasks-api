using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json;
using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;

namespace ScheduledTasks.Controllers
{
    [Authorize]
    [ApiController]
    [Route("tasks")]
    public class TasksController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<TasksController> _logger;

        public TasksController(ILogger<TasksController> logger)
        {
            _logger = logger;
        }
        /// <summary>
        /// Returns a list of tasks
        /// </summary>
        /// <param name="name">Wild card (* or ?) of the task to search for</param>
        /// <returns></returns>
        [HttpGet]
        [Route("{name?}")]
        public IEnumerable<Task> Get(string name)
        {
            return Task.Create(TaskService.Instance.FindAllTasks(new Regex("^" + Regex.Escape(name).Replace("\\*", ".*").Replace("\\?", ".") + "$")));
        }

        /// <summary>
        /// Returns the state of the named task
        /// </summary>
        /// <param name="name">Name of the Task</param>
        /// <returns></returns>
        [HttpGet]
        [Route("{name}/status")]
        public ActionResult<string> GetStatus(string name)
        {
            var list = Task.Create(TaskService.Instance.FindAllTasks(new Regex("^" + Regex.Escape(name).Replace("\\*", ".*").Replace("\\?", ".") + "$")));
            if (list.Count == 0) NotFound($"name {name} was not found in Schedule Tasks");
            if (list.Count > 1) BadRequest($"name {name} was not found multiple times in Schedule Tasks");
            return Ok(list.FirstOrDefault().State);
        }

        /// <summary>
        /// Starts as Scheduled Task
        /// </summary>
        /// <param name="name">Name of the task to start</param>
        /// <returns></returns>
        [HttpPost]
        [Route("{name}/start")]
        public ActionResult StartTask(string name)
        {
            var list = Task.Create(TaskService.Instance.FindAllTasks(new Regex("^" + Regex.Escape(name).Replace("\\*", ".*").Replace("\\?", ".") + "$")));
            if (list.Count == 0) NotFound($"name {name} was not found in Schedule Tasks");
            if (list.Count > 1) BadRequest($"name {name} was not found multiple times in Schedule Tasks");
            using (TaskService ts = new TaskService())
            {
                var t = ts.FindTask(list.First().Name);
                if (t.State == TaskState.Running) return BadRequest("Task is already running.. skipping call");
                if (t != null)
                    t.Run();
            }
            return Ok();
        }


        /// <summary>
        /// Stops a Scheduled Task
        /// </summary>
        /// <param name="name">Name of the task to stop</param>
        /// <returns></returns>
        [HttpPost]
        [Route("{name}/stop")]
        public ActionResult<string> StopTask(string name)
        {
            var list = Task.Create(TaskService.Instance.FindAllTasks(new Regex("^" + Regex.Escape(name).Replace("\\*", ".*").Replace("\\?", ".") + "$")));
            if (list.Count == 0) NotFound($"name {name} was not found in Schedule Tasks");
            if (list.Count > 1) BadRequest($"name {name} was not found multiple times in Schedule Tasks");
            using (TaskService ts = new TaskService())
            {
                var t = ts.FindTask(list.First().Name);
                if (t.State != TaskState.Running) return BadRequest("Task is not running.. skipping call");
                if (t != null)
                    t.Stop();
            }
            return Ok("Stopped Successfully");
        }


        /// <summary>
        /// Lists the past history of a task
        /// </summary>
        /// <param name="name">Task Name</param>
        /// <returns></returns>
        [HttpGet]
        [Route("{name}/history")]
        public ActionResult<HashSet<EventItem>> TaskHistory(string name)
        {
            var retEvents = new HashSet<EventItem>();
            var list = Task.Create(TaskService.Instance.FindAllTasks(new Regex("^" + Regex.Escape(name).Replace("\\*", ".*").Replace("\\?", ".") + "$")));
            if (list.Count == 0) NotFound($"name {name} was not found in Schedule Tasks");
            if (list.Count > 1) BadRequest($"name {name} was not found multiple times in Schedule Tasks");

            EventLogReader log2 = new EventLogReader("Microsoft-Windows-TaskScheduler/Operational");

            for (EventRecord eventInstance = log2.ReadEvent(); null != eventInstance; eventInstance = log2.ReadEvent())
            {
                if (!eventInstance.Properties.Select(p => p.Value).Contains($"\\{list.First().Name}"))
                {
                    continue;
                }
                var item = new EventItem();
                item.EventId = eventInstance.Id.ToString();
                item.ProviderName = eventInstance.ProviderName;

                try
                {
                    item.EventInstanceDescription = eventInstance.FormatDescription();
                }
                catch (EventLogException)
                {
                }

                EventLogRecord logRecord = (EventLogRecord)eventInstance;
                item.EventLogRecordDescription = logRecord.FormatDescription();
                Console.WriteLine("Description: {0}", logRecord.FormatDescription());
                item.ActivityId = logRecord.ActivityId.ToString();
                item.TimeCreated = logRecord.TimeCreated;
                item.OpcodeDisplayName = logRecord.OpcodeDisplayName;
                retEvents.Add(item);
            }
            return Ok(retEvents);
        }

        public class EventItem
        {
            public string LevelDisplayName { get; set; }
            public string OpcodeDisplayName { get; set; }
            public string ActivityId { get; set; }
            public string EventId { get; set; }
            public string ProviderName { get; set; }
            public DateTime? TimeCreated { get; set; }
            public string TaskDisplayName { get; set; }
            public int Task { get; set; }
            public string EventInstanceDescription { get; set; }
            public string EventLogRecordDescription { get; set; }
        }
        public class Task
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string Folder { get; set; }
            public string State { get; set; }
            public bool Enabled { get; set; }
            public bool IsActive { get; set; }
            public DateTime LastRunTime { get; set; }
            public int LastTaskResult { get; set; }
            public DateTime NextRunTime { get; set; }
            public string Xml { get; set; }
            public static HashSet<Task> Create(Microsoft.Win32.TaskScheduler.Task[] tasksFrom)
            {
                var retTasks = new HashSet<Task>();
                foreach (var task in tasksFrom) retTasks.Add(Task.Create(task));
                return retTasks;
            }
            public static Task Create(Microsoft.Win32.TaskScheduler.Task taskFrom)
            {
                Task task = new Task();
                if (taskFrom != null)
                {
                    task.Name = taskFrom.Name;
                    task.Path = taskFrom.Path;
                    task.Folder = taskFrom.Folder.ToString();
                    task.State = taskFrom.State.ToString();
                    task.Enabled = taskFrom.Enabled;
                    task.IsActive = taskFrom.IsActive;
                    task.LastRunTime = taskFrom.LastRunTime;
                    task.NextRunTime = taskFrom.NextRunTime;
                    task.LastTaskResult = taskFrom.LastTaskResult;
                    task.Xml = taskFrom.Xml;
                    return task;
                }
                return null;
            }
        }
    }
}