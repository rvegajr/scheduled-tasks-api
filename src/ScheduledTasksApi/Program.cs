using Microsoft.AspNetCore.Authentication.Negotiate;
using ScheduledTasksApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});

builder.Services.AddSingleton<ITaskSchedulerService, TaskSchedulerService>();
builder.Services.AddSingleton<IWindowsServiceManager, WindowsServiceManager>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Expose for WebApplicationFactory in integration tests
public partial class Program;
