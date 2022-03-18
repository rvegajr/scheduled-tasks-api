namespace ScheduledTasks.Controllers;

public static class ServicesExtentions
{
    public static HashSet<ServiceItem> Filter(this HashSet<ServiceItem> services, string filterListCSV)
    {
        var filterList = filterListCSV.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        var filteredItems = new HashSet<ServiceItem>();
        foreach (var svc in services)
        {
            foreach (var filterItem in filterList)
            {
                var regExfilterItem = new Regex("^" + Regex.Escape(filterItem).Replace("\\*", ".*").Replace("\\?", "."));
                if ((regExfilterItem.Match(svc.DisplayName).Success) || (regExfilterItem.Match(svc.ServiceName).Success))
                {
                    filteredItems.Add(svc);
                }
            }
        }
        return filteredItems;
    }
}

[Authorize]
[ApiController]
[Route("services")]
public class ServicesController : ControllerBase
{
    private readonly ILogger<TasksController> _logger;
    protected readonly IConfiguration _configuration;
    /// <summary>
    /// 
    /// </summary>
    /// <param name="logger"></param>
    public ServicesController(ILogger<TasksController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }
    /// <summary>
    /// Returns a list of tasks
    /// </summary>
    /// <param name="name">Wild card (* or ?) of the task to search for</param>
    /// <returns></returns>
    [HttpGet]
    [Route("{name?}")]
    public IEnumerable<ServiceItem> Get(string name)
    {
        var rawServices = ServiceController.GetServices();
        var services = ServiceItem.Create(rawServices).Filter(_configuration.GetValue<string>("AllowedServices"));
        var retServices = new HashSet<ServiceItem>();
        var regExName = new Regex("^" + Regex.Escape(name).Replace("\\*", ".*").Replace("\\?", "."));
        foreach (var svc in services)
            if ((regExName.Match(svc.DisplayName).Success) || (regExName.Match(svc.ServiceName).Success))
            {
                retServices.Add(svc);
            }
        return retServices;
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
        var list = this.Get(name).ToList();
        if (list.Count == 0) NotFound($"name {name} was not found in Services");
        if (list.Count > 1) BadRequest($"name {name} was not found multiple times in Services");
        return Ok(list.FirstOrDefault()?.Status);
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
        var list = this.Get(name).ToList();
        if (list.Count == 0) NotFound($"name {name} was not found in Services");
        if (list.Count > 1) BadRequest($"name {name} was not found multiple times in Services");
        ;
        using (ServiceController sc = new ServiceController(list[0].ServiceName))
        {
            if ((sc.Status.Equals(ServiceControllerStatus.Stopped)) || (sc.Status.Equals(ServiceControllerStatus.StopPending)))
                sc.Start();
            else
                return BadRequest("Service is already running.. skipping call");
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
        var list = this.Get(name).ToList();
        if (list.Count == 0) NotFound($"name {name} was not found in Services");
        if (list.Count > 1) BadRequest($"name {name} was not found multiple times in Services");
        using (ServiceController sc = new ServiceController(list[0].ServiceName))
        {
            if ((sc.Status.Equals(ServiceControllerStatus.Stopped)) || (sc.Status.Equals(ServiceControllerStatus.StopPending)))
                return BadRequest("Service is already stopped.. skipping call");
            else
                sc.Stop();
        }
        return Ok("Stopped Successfully");
    }

    /// <summary>
    /// Stops a Scheduled Task
    /// </summary>
    /// <param name="name">Name of the task to stop</param>
    /// <returns></returns>
    [HttpPost]
    [Route("{name}/restart")]
    public ActionResult<string> RestartTask(string name)
    {
        var list = this.Get(name).ToList();
        if (list.Count == 0) NotFound($"name {name} was not found in Services");
        if (list.Count > 1) BadRequest($"name {name} was not found multiple times in Services");
        var restartTimeout = _configuration.GetValue<int>("RestartTimeoutSeconds");
        var tasks = "";
        using (ServiceController sc = new ServiceController(list[0].ServiceName))
        {
            if ((sc.Status.Equals(ServiceControllerStatus.Running)) || (sc.Status.Equals(ServiceControllerStatus.Paused)) || (sc.Status.Equals(ServiceControllerStatus.PausePending)))
            {
                tasks += "Stopping. ";
                sc.Stop();
                System.Threading.Thread.Sleep(2000);
                sc.Refresh();
                System.Threading.Thread.Sleep(1000);
            } else
            {
                tasks += "Already Stopped. ";
            }
            var waitCounter = 0;
            if (sc.Status.Equals(ServiceControllerStatus.Stopped)) tasks += "Stopped. ";
            while (!sc.Status.Equals(ServiceControllerStatus.Stopped))
            {
                System.Threading.Thread.Sleep(1000);
                waitCounter++;
                if (waitCounter > restartTimeout) return BadRequest($"Timeout ({restartTimeout} seconds) while waiting for service to stop Actions: " + tasks);
                sc.Refresh();
                if (sc.Status.Equals(ServiceControllerStatus.Stopped)) tasks += "Stopped. ";
            }
            tasks += "Starting. ";
            sc.Start();
        }
        return Ok("Restarted Successfully: Actions: " + tasks);
    }
}
public class ServiceItem
{
    public bool CanPauseAndContinue { get; set; }

    public bool CanShutdown { get; set; }

    public bool CanStop { get; set; }

    public ServiceItem[] DependentServices { get; set; }

    public string DisplayName { get; set; }

    public string MachineName { get; set; }

    public string ServiceName { get; set; }

    public ServiceItem[] ServicesDependedOn { get; set; }

    public string ServiceType { get; set; }

    public string StartType { get; set; }

    public string Status { get; set; }

    public static HashSet<ServiceItem> Create(ServiceController[] tasksFrom)
    {
        return Create(tasksFrom, true);
    }

    public static HashSet<ServiceItem> Create(ServiceController[] tasksFrom, bool followNested)
    {
        var retTasks = new HashSet<ServiceItem>();
        foreach (var task in tasksFrom) retTasks.Add(ServiceItem.Create(task, followNested));
        return retTasks;
    }
    public static ServiceItem Create(ServiceController taskFrom)
    {
        return Create(taskFrom, true);

    }
    public static ServiceItem Create(ServiceController taskFrom, bool followNested)
    {
        ServiceItem task = new ServiceItem();
        if (taskFrom != null)
        {
            task.CanPauseAndContinue = taskFrom.CanPauseAndContinue;
            task.CanShutdown = taskFrom.CanShutdown;
            task.CanStop = taskFrom.CanStop;
            try
            {
                if (followNested) task.DependentServices = ServiceItem.Create(taskFrom.DependentServices.ToArray(), false).ToArray();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }
            task.DisplayName = taskFrom.DisplayName;
            task.MachineName = taskFrom.MachineName;
            task.ServiceName = taskFrom.ServiceName;
            try
            {
                if (followNested) task.ServicesDependedOn = ServiceItem.Create(taskFrom.ServicesDependedOn.ToArray(), false).ToArray();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }
            task.ServiceType = taskFrom.ServiceType.ToString();
            task.StartType = taskFrom.StartType.ToString();
            task.Status = taskFrom.Status.ToString();
            return task;
        }
        return null;
    }
}
