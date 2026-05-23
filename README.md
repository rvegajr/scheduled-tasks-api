# ScheduledTasksApi

Cross-platform REST API for managing scheduled tasks and services. Works on **Windows** (Task Scheduler + Windows Services), **Linux** (systemd + cron), and **macOS** (launchd + cron).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Quick Start

```bash
# Build (all platforms)
dotnet build

# Run
dotnet run --project src/ScheduledTasksApi

# Run tests
dotnet test

# Swagger UI
# Navigate to https://localhost:5001/swagger (Windows)
# Navigate to http://localhost:5000/swagger (Linux/macOS)
```

## Configuration

Edit `src/ScheduledTasksApi/appsettings.json`:

```json
{
  "AllowedTasks": "*",
  "AllowedServices": "*",
  "RestartTimeoutSeconds": 120,
  "ApiKey": ""
}
```

| Setting | Description |
|---------|-------------|
| `AllowedTasks` | Comma-separated wildcard patterns for exposed tasks (e.g. `*Google*,*Backup*`) |
| `AllowedServices` | Comma-separated wildcard patterns for exposed services |
| `RestartTimeoutSeconds` | Max seconds to wait for service restart |
| `ApiKey` | API key for `X-Api-Key` header auth (Linux/macOS). Empty = anonymous access |

## API Endpoints

### Tasks

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/tasks?pattern=*` | List tasks matching wildcard (summary) |
| GET | `/api/tasks/{name}` | Get full task detail (actions, triggers, settings, principal) |
| GET | `/api/tasks/{name}/status` | Get task state |
| GET | `/api/tasks/{name}/history` | Get task event/log history |
| POST | `/api/tasks/{name}/run` | Run a scheduled task |
| POST | `/api/tasks/{name}/stop` | Stop a running task |

### Services

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/services?pattern=*` | List services matching wildcard (summary) |
| GET | `/api/services/{name}` | Get full service detail (description, executable, account, PID) |
| GET | `/api/services/{name}/status` | Get service status |
| POST | `/api/services/{name}/start` | Start a stopped service |
| POST | `/api/services/{name}/stop` | Stop a running service |
| POST | `/api/services/{name}/restart` | Restart a service |

### Task Detail Response

The `GET /api/tasks/{name}` endpoint returns comprehensive information:

```json
{
  "name": "BackupTask",
  "path": "\\BackupTask",
  "state": "Ready",
  "enabled": true,
  "source": "WindowsTaskScheduler",
  "author": "SYSTEM",
  "description": "Daily backup job",
  "actions": [
    { "type": "Execute", "path": "C:\\backup.exe", "arguments": "--full" }
  ],
  "triggers": [
    { "type": "Daily", "enabled": true, "startBoundary": "2026-01-01T05:00:00" }
  ],
  "settings": {
    "executionTimeLimit": "01:00:00",
    "allowHardTerminate": true,
    "priority": 7
  },
  "principal": {
    "userId": "SYSTEM",
    "runLevel": "Highest"
  }
}
```

### Service Detail Response

The `GET /api/services/{name}` endpoint returns:

```json
{
  "serviceName": "nginx",
  "displayName": "nginx web server",
  "status": "active/running",
  "description": "A high performance web server",
  "imagePath": "/usr/sbin/nginx",
  "serviceAccount": "www-data",
  "processId": 1234
}
```

## Platform Support

| Feature | Windows | Linux | macOS |
|---------|---------|-------|-------|
| **Tasks** | Task Scheduler | systemd timers + cron | launchd + cron |
| **Services** | Windows Services | systemd services | launchd daemons |
| **Auth** | Negotiate (Kerberos/NTLM) | API Key (`X-Api-Key`) | API Key (`X-Api-Key`) |
| **Task Source** | `WindowsTaskScheduler` | `SystemdTimer`, `Cron` | `Launchd`, `Cron` |
| **Detail** | Full (actions, triggers, settings, principal) | Full (systemd show, journal) | Full (plist parsing) |

## Deployment

### Windows (as a Windows Service)

```powershell
.\deploy.ps1
# or with custom options:
.\deploy.ps1 -InstallPath "D:\Apps\TasksApi" -ServiceName "MyTasksApi"
```

### Linux (as a systemd service)

```bash
chmod +x deploy-linux.sh
sudo ./deploy-linux.sh
# or with custom options:
sudo ./deploy-linux.sh /opt/scheduled-tasks-api scheduled-tasks-api
```

### Docker (Linux)

```bash
docker build -t scheduled-tasks-api .
docker run -d -p 5000:8080 \
  -e AllowedTasks="*" \
  -e AllowedServices="*" \
  -e ApiKey="your-secret-key" \
  scheduled-tasks-api
```

## Testing

```bash
# Run all tests (both TFMs)
dotnet test

# Run only cross-platform tests
dotnet test --framework net10.0

# Run only Windows tests
dotnet test --framework net10.0-windows
```

**Test breakdown:**
- Parser tests (CrontabParser, SystemdOutputParser, LaunchdPlistParser) — pure input/output
- Service tests (Linux, Mac) — mock `IProcessRunner` with canned command output
- Controller tests — mock service interfaces
- Integration tests — `WebApplicationFactory` with fake auth

## Architecture

```
src/ScheduledTasksApi/
  Authentication/         API key auth handler (Linux/macOS)
  Controllers/            TasksController, ServicesController
  Extensions/             Wildcard filtering
  Models/                 TaskItem, TaskItemDetail, ServiceItem, ServiceItemDetail, EventItem
  Services/
    Parsing/              CrontabParser, SystemdOutputParser, LaunchdPlistParser
    Linux/                LinuxTaskService, LinuxServiceManager
    Mac/                  MacTaskService, MacServiceManager
    ITaskSchedulerService, IServiceManager, IProcessRunner
    TaskSchedulerService  (Windows, #if WINDOWS)
    WindowsServiceManager (Windows, #if WINDOWS)
    ProcessRunner         (cross-platform shell execution)
```

## License

See [LICENSE](LICENSE).
