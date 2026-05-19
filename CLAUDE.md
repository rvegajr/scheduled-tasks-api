# CLAUDE.md

## Project Overview
ScheduledTasksApi — a .NET 10 Windows-only REST API for managing Windows Scheduled Tasks and Windows Services remotely via authenticated HTTP endpoints.

## Build & Run
```bash
dotnet build
dotnet run --project src/ScheduledTasksApi
dotnet test
```

## Conventions
- Target: `net10.0-windows`
- Async/await throughout — never use `Thread.Sleep`, use `Task.Delay`
- Interface-based DI for all OS-level services (enables unit testing)
- Controllers are thin — business logic lives in service classes
- Always `return` ActionResult responses (NotFound, BadRequest, etc.)
- Models use `TaskItem` (not `Task`) to avoid ambiguity with `System.Threading.Tasks.Task`
- Wildcard filtering uses shared extension methods
- Authentication: Windows Negotiate (Kerberos/NTLM)
- Tests: xUnit + NSubstitute

## API Design
- RESTful routes: `/api/tasks`, `/api/services`
- Actions as sub-resources: `POST /api/tasks/{name}/run`, `POST /api/services/{name}/stop`
- Consistent JSON response envelope not required — use raw ActionResult
- Return proper HTTP status codes with meaningful error messages

## Project Structure
- `src/ScheduledTasksApi/` — main API project
- `tests/ScheduledTasksApi.Tests/` — unit tests
- Models, Services, Extensions, Controllers in separate folders
