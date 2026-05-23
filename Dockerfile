FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore
COPY src/ScheduledTasksApi/ScheduledTasksApi.csproj src/ScheduledTasksApi/
RUN dotnet restore src/ScheduledTasksApi/ScheduledTasksApi.csproj

# Copy everything and publish
COPY . .
RUN dotnet publish src/ScheduledTasksApi/ScheduledTasksApi.csproj \
    -c Release \
    --framework net10.0 \
    -o /app \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app .

# Default config via environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV AllowedTasks=*
ENV AllowedServices=*

EXPOSE 8080

ENTRYPOINT ["dotnet", "ScheduledTasksApi.dll"]
