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

## Yönetim paneli

Tarayıcıda `http://<IP>:8080/admin` → şifreyle giriş. İlk şifreyi sunucu kendisi üretir; terminalde görmek için:

```
cat /opt/zootopia/admin_password.txt
```

Panelde ilk iş Ayarlar → şifreni değiştir (ilk şifre dosyası o zaman silinir). Panelde: çevrimiçi oyuncular, süren
maçlar, sunucu yükü, oyuncu arama, yasaklama / yasağı kaldırma, şikayetler, hata bildirimleri (cihaz kayıtlarıyla),
biten maçlar ve sunucu kayıtları. Veriler `/opt/zootopia/zootopia.db` (SQLite) içinde.

## Google ile giriş (Google Play Games)

Telefon Play Games'e girince Google'dan tek kullanımlık bir kod alır ve `POST /account/google` ile sunucuya yollar.
Sunucu bu kodu Google'da doğrular, oyuncunun Play Games kimliğini hesaba bağlar; aynı Google hesabıyla başka telefondan
ya da oyunu silip yükledikten sonra girilince eski hesap (oyuncu kodu, arkadaşlar, mesajlar) geri gelir.

Bir kez yapılacak: yönetim paneli → **Ayarlar → Google ile giriş** kutusuna Google Cloud'daki "Rise of Davraz Sunucu"
(Web uygulaması) istemcisinin **gizli anahtarını** yaz. Anahtar yalnız sunucuda `/opt/zootopia/google.json` içinde durur
(GitHub'a girmez). Girilmezse Google ile giriş "sunucuda henüz ayarlanmadı" der, oyunun geri kalanı etkilenmez.

## Nasıl çalışır

- `orchestrator.py` (servis adı `zootopia`) 8080 portunda maç yöneticisidir:
  - `POST /quick?mode=solo|duo|squad` → bekleyen bir hızlı maça katar ya da yenisini açar
  - `POST /room/create?mode=...` → özel oda açar, 6 haneli kod verir
  - `GET /room/<kod>` → o odanın portu
  - `GET /status` → çalışan maçlar
  - hesaplar: `/account/register`, `/account/hello`, `/account/google`; arkadaşlar: `/friends` (+ `add`, `accept`, `remove`, `invite`,
    `dismiss`); `/block`, `/unblock`, `/report/player`, `/bug` (istekler `X-ZM-Id` / `X-ZM-Secret` başlığıyla)
- Her maç ayrı bir Unity sunucu sürecidir (UDP 7777–7799). Bekleme odası: hızlı maç ilk oyuncu gelince 30 sn
  sonra (16 kişi dolarsa 5 sn) başlar; özel odayı lider BAŞLAT ile başlatır. Boş yerleri botlar doldurur.
  Maç bitince süreç kendiliğinden kapanır.
- Yeni sunucu derlemesi GitHub release `son-server` içindeki `ZootopiaServer.tar.gz`'den otomatik alınır; çalışan
  maçlar eski derlemeyle biter. Telefonun "çevrimiçi sürümü" sunucununkiyle aynı olmalı; bu yalnız ağ kodu ya da
  harita değişince değişir (APK ile sunucu aynı Actions çalıştırmasında derlenir).
- Kayıtlar: `/opt/zootopia/logs/` (`orchestrator.log`, her maç için `match-<kod>.log`).
