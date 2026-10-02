# Zootopia Mobile

3D mobil battle royale oyunu (Unity, Android).
**Yapımcı:** Olcay Yasin Dünder

25 kişilik ada maçı: daralan güvenli bölge, ganimet sandıkları, silah değiştirme ve yapay zekâlı rakipler. Solo, Duo ve Squad modları (takım arkadaşları bot). İnternet gerektirmez.

## Özellikler

- Üçüncü şahıs nişancı kamera, dokunmatik kontroller (kayan joystick, sağ tarafta kaydırarak nişan, iki ateş butonu, zıpla/eğil/doldur/koş/ilk yardım)
- 5 silah: Tabanca P9, Şimşek SMG, Bozkurt AR, Kaya-12 pompalı, Kartal SR keskin nişancı
- Ganimet: silah, mermi, ilk yardım çantası, zırh
- 6 aşamalı daralan güvenli bölge (bölge dışında hasar)
- Botlar: görüş hattı kontrolü, tepki süresi, isabet sapması, bölgeye kaçma
- Profil: seviye, XP, altın, maç/zafer/öldürme istatistikleri (cihazda kaydedilir)
- Evler, ağaçlar, kayalar ile siper alınabilen ada

## Derleme (bilgisayar gerekmez)

Proje hiçbir sahne veya editör ayarı gerektirmez; her şey koddan kurulur.

**GitHub Actions (ücretsiz, önerilen)**
1. Bu depoda *Settings → Secrets and variables → Actions* altına `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` gizli ayarlarını ekle.
2. *Actions → Android APK (ücretsiz) → Run workflow*.
3. Bitince APK: `https://github.com/olcaydunder/zootopiamobile/releases/download/son-apk/ZootopiaMobile.apk`

**Unity Build Automation (alternatif)**
1. Unity Cloud'da yeni proje → bu GitHub reposunu bağla (branch: `main`).
2. Hedef: Android, Unity sürümü: en güncel 2022.3 LTS.
3. Gelişmiş ayarlar → **Pre-Export Method:** `ZootopiaBuild.PreExport`
4. Build al → APK/AAB indir.

`PreExport` şunları otomatik yapar: `Assets/Scenes/Main.unity` sahnesini oluşturur, build listesine ekler, paket adını `com.olcayasindunder.zootopiamobile`, IL2CPP + ARM64 ve yatay ekranı ayarlar.

**Unity Editör (isteğe bağlı)**
Projeyi Unity Hub'da aç; ilk açılışta sahne kendiliğinden oluşur (veya menü: *Zootopia → Projeyi Hazırla*). Play'e bas.

Masaüstü test kontrolleri: WASD hareket, sağ fare tuşu basılı nişan, sol tık ateş, Shift koş, Space zıpla, C eğil, R doldur, X ilk yardım.

## Kod yapısı

`Assets/Scripts/Game/`
- `GameBootstrap` – giriş noktası, adayı/ışığı/oyuncuyu oluşturur
- `GameManager` – maç akışı, takımlar, kazanma/kaybetme
- `PlayerController`, `BotAgent`, `WeaponController`, `WeaponData`
- `SafeZoneController`, `LootSystem`, `ProfileData`, `Inventory`
- `UIManager`, `TouchControls`, `HoldButton`, `UIUtil`

`Assets/Editor/ZootopiaBuild.cs` – derleme öncesi otomatik proje kurulumu.

## Lisans

MIT. [ebito-coder/battle-island-mobile](https://github.com/ebito-coder/battle-island-mobile) prototipi temel alınarak geliştirilmiştir; orijinal telif bildirimi `LICENSE` dosyasında korunmaktadır.
