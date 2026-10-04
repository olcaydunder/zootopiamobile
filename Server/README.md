# Zootopia Mobile oyun sunucusu

Sunucu tek satırla kurulur ve oyunun yeni sürümlerini kendisi indirir; sunucuda ya da GitHub'da şifre/anahtar tutulmaz.

## Kurulum (bir kez)

1. Hetzner Cloud (veya başka bir sağlayıcı) → yeni sunucu: **Ubuntu 24.04**, **x86 (CX33: 4 vCPU, 8 GB)**.
2. "Cloud config" (user data) kutusuna şunu yapıştır:

   ```
   #!/bin/bash
   curl -fsSL https://raw.githubusercontent.com/olcaydunder/zootopiamobile/main/Server/setup.sh | bash
   ```
3. Sunucu açılınca IP adresini `Server/endpoint.json` dosyasındaki `host` alanına yaz (telefon buradan okur;
   değişirse yeni APK gerekmez).

Kontrol: tarayıcıda `http://<IP>:8080/status` → `"ok": true` ve `"version"` görünmeli. Sürüm `null` ise sunucu
henüz oyunun sunucu derlemesini indirmemiştir (GitHub Actions derlemesi bittikten sonra en geç 2 dakika).

## Nasıl çalışır

- `orchestrator.py` (servis adı `zootopia`) 8080 portunda maç yöneticisidir:
  - `POST /quick?mode=solo|duo|squad` → bekleyen bir hızlı maça katar ya da yenisini açar
  - `POST /room/create?mode=...` → özel oda açar, 6 haneli kod verir
  - `GET /room/<kod>` → o odanın portu
  - `GET /status` → çalışan maçlar
- Her maç ayrı bir Unity sunucu sürecidir (UDP 7777–7799). Bekleme odası: hızlı maç ilk oyuncu gelince 30 sn
  sonra (16 kişi dolarsa 5 sn) başlar; özel odayı lider BAŞLAT ile başlatır. Boş yerleri botlar doldurur.
  Maç bitince süreç kendiliğinden kapanır.
- Yeni sunucu derlemesi GitHub release `son-server` içindeki `ZootopiaServer.tar.gz`'den otomatik alınır; çalışan
  maçlar eski derlemeyle biter. Telefon ile sunucu aynı commit'ten derlenmiş olmalı (APK ile aynı Actions
  çalıştırmasında derlenir).
- Kayıtlar: `/opt/zootopia/logs/` (`orchestrator.log`, her maç için `match-<kod>.log`).
