# ScheduledTasksApi

REST API for managing Windows Scheduled Tasks and Windows Services remotely.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (Windows only)
- Windows with Task Scheduler and target services running

## Quick Start

```bash
# Build
dotnet build

# Run (requires Windows + admin for service control)
dotnet run --project src/ScheduledTasksApi

# Run tests
dotnet test

# Swagger UI
# Navigate to https://localhost:5001/swagger
```

## Configuration

Edit `src/ScheduledTasksApi/appsettings.json`:

```json
{
  "AllowedTasks": "*Google*",
  "AllowedServices": "*Endpoint*,*Peer*",
  "RestartTimeoutSeconds": 120
}
```

Wildcards (`*`, `?`) control which tasks/services are exposed via the API.

## API Endpoints

### Tasks

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/tasks?pattern=*` | List tasks matching wildcard |
| GET | `/api/tasks/{name}` | Get single task details |
| GET | `/api/tasks/{name}/status` | Get task state |
| GET | `/api/tasks/{name}/history` | Get task event log history |
| POST | `/api/tasks/{name}/run` | Run a scheduled task |
| POST | `/api/tasks/{name}/stop` | Stop a running task |

### Services

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/services?pattern=*` | List services matching wildcard |
| GET | `/api/services/{name}` | Get single service details |
| GET | `/api/services/{name}/status` | Get service status |
| POST | `/api/services/{name}/start` | Start a stopped service |
| POST | `/api/services/{name}/stop` | Stop a running service |
| POST | `/api/services/{name}/restart` | Restart a service |

## Authentication

All endpoints require Windows Authentication (Negotiate/Kerberos/NTLM).

## License

See [LICENSE](LICENSE).
