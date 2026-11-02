#!/bin/bash
# Oracle VM(Ubuntu 24.04)에서 처음 한 번. 사용: sudo bash setup.sh
set -euo pipefail
cd "$(dirname "$0")"

# 메모리가 1GB라 npm ci 중 메모리 부족을 피하려고 스왑 2GB
if [ ! -f /swapfile ]; then
  fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
  echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

# Oracle 이미지는 iptables 가 22번 말고 막는다. 마지막 REJECT 앞에 80 · 443 을 넣고 저장
for port in 80 443; do
  rule="-p tcp -m state --state NEW -m tcp --dport $port -j ACCEPT"
  iptables -C INPUT $rule 2>/dev/null || iptables -I INPUT 5 $rule
done
netfilter-persistent save

# 쓰지 않는 NFS 용 rpcbind(111번)를 끈다
systemctl disable --now rpcbind rpcbind.socket 2>/dev/null || true

# Node 24 (NodeSource) · Caddy (공식 저장소)
curl -fsSL https://deb.nodesource.com/setup_24.x | bash -
apt-get install -y nodejs debian-keyring debian-archive-keyring apt-transport-https
curl -1sLf https://dl.cloudsmith.io/public/caddy/stable/gpg.key | gpg --dearmor --yes -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt > /etc/apt/sources.list.d/caddy-stable.list
apt-get update && apt-get install -y caddy

# 코드 /opt/flamong/server · DB /var/lib/flamong (db.js 가 작업 폴더에 flamong.db 를 만든다) · 웹 빌드 /var/www/flamong
id flamong >/dev/null 2>&1 || useradd --system --home /var/lib/flamong --shell /usr/sbin/nologin flamong
mkdir -p /opt/flamong/server /var/www/flamong
# DB 와 백업에는 실명 · 팀이 있어 서비스 계정 · root 만 읽게 한다
install -d -m 700 /var/backups/flamong
install -d -m 700 -o flamong -g flamong /var/lib/flamong
chmod 600 /var/lib/flamong/flamong.db* 2>/dev/null || true

# 하루 한 번 DB 사본. WAL 이어도 VACUUM INTO 는 일관된 사본을 만든다. 14일 지난 것은 지운다
cat > /etc/cron.daily/flamong-backup <<'EOF'
#!/bin/sh
umask 077
node -e "new (require('node:sqlite').DatabaseSync)('/var/lib/flamong/flamong.db').exec(\"VACUUM INTO '/var/backups/flamong/flamong-$(date +%F).db'\")"
find /var/backups/flamong -name 'flamong-*.db' -mtime +14 -delete
EOF
chmod +x /etc/cron.daily/flamong-backup

cp flamong.service /etc/systemd/system/
cp Caddyfile /etc/caddy/Caddyfile
systemctl daemon-reload
systemctl enable flamong
systemctl reload caddy
