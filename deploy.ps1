<#
.SYNOPSIS
    Publishes and installs ScheduledTasksApi as a Windows Service.

.PARAMETER InstallPath
    Target directory for the published app. Default: C:\Services\ScheduledTasksApi

.PARAMETER ServiceName
    Windows service name. Default: ScheduledTasksApi

.EXAMPLE
    .\deploy.ps1
    .\deploy.ps1 -InstallPath "D:\MyApps\TasksApi" -ServiceName "MyTasksApi"
#>
param(
    [string]$InstallPath = "C:\Services\ScheduledTasksApi",
    [string]$ServiceName = "ScheduledTasksApi"
)

$ErrorActionPreference = "Stop"

Write-Host "Publishing application..." -ForegroundColor Cyan
dotnet publish src/ScheduledTasksApi/ScheduledTasksApi.csproj `
    -c Release `
    -r win-x64 `
    --self-contained `
    -p:PublishSingleFile=true `
    -o $InstallPath

$exePath = Join-Path $InstallPath "ScheduledTasksApi.exe"

# Check if service already exists
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existing) {
    Write-Host "Stopping existing service..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Creating Windows service '$ServiceName'..." -ForegroundColor Cyan
sc.exe create $ServiceName binPath="$exePath" start=auto displayname="Scheduled Tasks API"
sc.exe description $ServiceName "REST API for managing Windows Scheduled Tasks and Services"

Write-Host "Starting service..." -ForegroundColor Cyan
Start-Service -Name $ServiceName

Write-Host "Done! Service '$ServiceName' is running." -ForegroundColor Green
Write-Host "Swagger UI: https://localhost:5001/swagger" -ForegroundColor Gray
