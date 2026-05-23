#!/usr/bin/env bash
set -euo pipefail

# Publishes and installs ScheduledTasksApi as a systemd service.
#
# Usage:
#   sudo ./deploy-linux.sh
#   sudo ./deploy-linux.sh /opt/scheduled-tasks-api scheduled-tasks-api
#
# Parameters:
#   $1 - Install path (default: /opt/scheduled-tasks-api)
#   $2 - Service name (default: scheduled-tasks-api)

INSTALL_PATH="${1:-/opt/scheduled-tasks-api}"
SERVICE_NAME="${2:-scheduled-tasks-api}"
SERVICE_USER="scheduledtasks"
UNIT_FILE="/etc/systemd/system/${SERVICE_NAME}.service"

echo "==> Publishing application to ${INSTALL_PATH}..."
dotnet publish src/ScheduledTasksApi/ScheduledTasksApi.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained \
    -p:PublishSingleFile=true \
    -o "${INSTALL_PATH}"

chmod +x "${INSTALL_PATH}/ScheduledTasksApi"

# Create service user if it doesn't exist
if ! id "${SERVICE_USER}" &>/dev/null; then
    echo "==> Creating service user '${SERVICE_USER}'..."
    useradd --system --no-create-home --shell /usr/sbin/nologin "${SERVICE_USER}"
fi

chown -R "${SERVICE_USER}:${SERVICE_USER}" "${INSTALL_PATH}"

# Stop existing service if running
if systemctl is-active --quiet "${SERVICE_NAME}" 2>/dev/null; then
    echo "==> Stopping existing service..."
    systemctl stop "${SERVICE_NAME}"
fi

echo "==> Creating systemd unit file..."
cat > "${UNIT_FILE}" <<EOF
[Unit]
Description=Scheduled Tasks API
After=network.target

[Service]
Type=notify
ExecStart=${INSTALL_PATH}/ScheduledTasksApi
WorkingDirectory=${INSTALL_PATH}
User=${SERVICE_USER}
Group=${SERVICE_USER}
Restart=on-failure
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=${SERVICE_NAME}

# Environment
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://+:5000
Environment=AllowedTasks=*
Environment=AllowedServices=*

# Security hardening
NoNewPrivileges=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=${INSTALL_PATH}

[Install]
WantedBy=multi-user.target
EOF

echo "==> Reloading systemd and starting service..."
systemctl daemon-reload
systemctl enable "${SERVICE_NAME}"
systemctl start "${SERVICE_NAME}"

echo "==> Done! Service '${SERVICE_NAME}' is running."
echo "    Swagger UI: http://localhost:5000/swagger"
echo "    Status:     systemctl status ${SERVICE_NAME}"
echo "    Logs:       journalctl -u ${SERVICE_NAME} -f"
