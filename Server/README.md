# Zootopia Mobile oyun sunucusu

Sunucu tek satırla kurulur ve oyunun yeni sürümlerini kendisi indirir; sunucuda ya da GitHub'da şifre/anahtar tutulmaz.

1. Hetzner Cloud (veya başka bir sağlayıcı) → yeni sunucu: **Ubuntu 24.04**, **x86 (CX33: 4 vCPU, 8 GB)**.
2. "Cloud config" (user data) kutusuna şunu yapıştır:

   ```
   #!/bin/bash
   curl -fsSL https://raw.githubusercontent.com/olcaydunder/zootopiamobile/main/Server/setup.sh | bash
   ```
3. Sunucu açılınca IP adresini `Server/endpoint.json` dosyasına yaz (oyun buradan okur).

Kontrol: `http://<IP>:8080/status`

- `orchestrator.py` her maç için bir Unity sunucu süreci açar (UDP 7777–7799), oda kodu ve hızlı eşleşmeyi yönetir.
- Yeni sunucu derlemesi GitHub release `son-server` içindeki `ZootopiaServer.tar.gz`'den otomatik alınır.
- Kayıtlar: `/opt/zootopia/logs/`.
