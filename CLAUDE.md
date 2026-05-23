# CLAUDE.md

## Project Overview
ScheduledTasksApi — a cross-platform .NET 10 REST API for managing scheduled tasks and services remotely. Supports Windows (Task Scheduler + Windows Services), Linux (systemd + cron), and macOS (launchd + cron).

## Build & Run
```bash
dotnet build
dotnet run --project src/ScheduledTasksApi
dotnet test
```

## Multi-Target
- `net10.0` — cross-platform (Linux/macOS)
- `net10.0-windows` — Windows with Task Scheduler and ServiceController
- `#if WINDOWS` guards Windows-specific code; `#if !WINDOWS` guards Linux/Mac code

## Conventions
- Async/await throughout — never use `Thread.Sleep`, use `Task.Delay`
- Interface-based DI for all OS-level services (enables unit testing)
- Controllers are thin — business logic lives in service classes
- Always `return` ActionResult responses (NotFound, BadRequest, etc.)
- Models use `TaskItem` (not `Task`) to avoid ambiguity with `System.Threading.Tasks.Task`
- `TaskItemDetail : TaskItem` and `ServiceItemDetail : ServiceItem` — inheritance for detail endpoints
- Wildcard filtering uses shared extension methods
- All shell commands go through `IProcessRunner` for testability
- Parsers are static pure-function classes — no state, no dependencies
- Authentication: Windows Negotiate on Windows, API Key (`X-Api-Key` header) on Linux/macOS
- Tests: xUnit + NSubstitute
- `TaskItem.Source` identifies the platform backend: `"WindowsTaskScheduler"`, `"Cron"`, `"SystemdTimer"`, `"Launchd"`

## API Design
- RESTful routes: `/api/tasks`, `/api/services`
- List endpoints return lightweight summary models (`TaskItem`, `ServiceItem`)
- Detail endpoints (`GET /api/tasks/{name}`, `GET /api/services/{name}`) return enriched models with actions, triggers, settings, principal
- Actions as sub-resources: `POST /api/tasks/{name}/run`, `POST /api/services/{name}/stop`
- Return proper HTTP status codes with meaningful error messages

## Project Structure
```
src/ScheduledTasksApi/
  Authentication/         ApiKeyAuthHandler (#if !WINDOWS)
  Controllers/            TasksController, ServicesController
  Extensions/             WildcardFilterExtensions
  Models/                 TaskItem, TaskItemDetail, ServiceItem, ServiceItemDetail, EventItem
  Services/
    Parsing/              CrontabParser, SystemdOutputParser, LaunchdPlistParser
    Linux/                LinuxTaskService, LinuxServiceManager (#if !WINDOWS)
    Mac/                  MacTaskService, MacServiceManager (#if !WINDOWS)
    ITaskSchedulerService, IServiceManager, IProcessRunner
    TaskSchedulerService  (#if WINDOWS)
    WindowsServiceManager (#if WINDOWS)
    ProcessRunner

tests/ScheduledTasksApi.Tests/
  Controllers/            Unit tests (mock service interfaces)
  Integration/            WebApplicationFactory tests (ApiFixture + FakeAuthHandler)
  Services/               Parser tests + Linux/Mac service tests (mock IProcessRunner)
  Extensions/             WildcardFilterExtensionsTests
```

## Key Interfaces
- `ITaskSchedulerService` — FindTasks, FindTask, FindTaskDetail, RunTask, StopTask, GetTaskHistory
- `IServiceManager` — FindServices, FindService, FindServiceDetail, StartServiceAsync, StopServiceAsync, RestartServiceAsync
- `IProcessRunner` — RunAsync(fileName, arguments) → ProcessResult(ExitCode, StdOut, StdErr)
