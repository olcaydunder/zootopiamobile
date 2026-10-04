#!/bin/bash
# Zootopia Mobile game server setup (Ubuntu 22.04/24.04).
# Paste this into the server's "cloud config / user data" box when creating it:
#   #!/bin/bash
#   curl -fsSL https://raw.githubusercontent.com/olcaydunder/zootopiamobile/main/Server/setup.sh | bash
# It installs the match orchestrator as a service. The orchestrator downloads the newest server build
# from the GitHub release "son-server" by itself, so nothing secret is ever stored on or sent to the server.
set -e
REPO_RAW="https://raw.githubusercontent.com/olcaydunder/zootopiamobile/main/Server"
BASE=/opt/zootopia

export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get install -y python3 curl tar ca-certificates ufw

id zoo >/dev/null 2>&1 || useradd --system --create-home --home-dir "$BASE" --shell /usr/sbin/nologin zoo
mkdir -p "$BASE/builds" "$BASE/logs"
curl -fsSL "$REPO_RAW/orchestrator.py" -o "$BASE/orchestrator.py"
chown -R zoo:zoo "$BASE"

cat > /etc/systemd/system/zootopia.service <<UNIT
[Unit]
Description=Zootopia Mobile match orchestrator
After=network-online.target
Wants=network-online.target

[Service]
User=zoo
WorkingDirectory=$BASE
ExecStart=/usr/bin/python3 $BASE/orchestrator.py
Restart=always
RestartSec=5
LimitNOFILE=65536

[Install]
WantedBy=multi-user.target
UNIT

# Firewall: SSH, the orchestrator API, and the UDP ports the matches listen on.
ufw allow 22/tcp
ufw allow 8080/tcp
ufw allow 7777:7799/udp
ufw --force enable

systemctl daemon-reload
systemctl enable --now zootopia
echo "Zootopia server ready: http://$(curl -fsSL https://api.ipify.org || hostname -I | cut -d' ' -f1):8080/status"
