using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Authentication;
using ScheduledTasksApi.Services;
#if WINDOWS
using Microsoft.AspNetCore.Authentication.Negotiate;
#else
using ScheduledTasksApi.Authentication;
using ScheduledTasksApi.Services.Linux;
using ScheduledTasksApi.Services.Mac;
#endif

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

#if WINDOWS
builder.Host.UseWindowsService();

builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.AddSingleton<ITaskSchedulerService, TaskSchedulerService>();
builder.Services.AddSingleton<IServiceManager, WindowsServiceManager>();
#else
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>("ApiKey", null);

builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();

if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    builder.Services.AddSingleton<ITaskSchedulerService, LinuxTaskService>();
    builder.Services.AddSingleton<IServiceManager, LinuxServiceManager>();
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    builder.Services.AddSingleton<ITaskSchedulerService, MacTaskService>();
    builder.Services.AddSingleton<IServiceManager, MacServiceManager>();
}
#endif

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Expose for WebApplicationFactory in integration tests
public partial class Program;
