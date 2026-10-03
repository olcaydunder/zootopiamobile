# Zootopia Mobile

3D mobil battle royale oyunu (Unity, Android).
**Yapımcı:** Olcay Yasin Dünder

25 kişilik ada maçı: daralan güvenli bölge, ganimet sandıkları, silah değiştirme ve yapay zekâlı rakipler. Solo, Duo ve Squad modları (takım arkadaşları bot). İnternet gerektirmez.

## Özellikler

- **Uçaktan atlama:** Maç başında uçak adanın üstünden geçer; istediğin yerde ATLA, serbest düşüşte yönlen, paraşütle in
- **Tepeli ada:** Çim, toprak, kum ve kaya dokulu arazi, dalgalanan deniz, gökyüzü, güneş, gölgeler ve sis
- **Yapılar:** Kapılı/pencereli evler, depolar, çam ve yaprak ağaçlar, kayalar, saklanılabilen çalılar; kum torbası, bariyer, konteyner, varil gibi 3D siper modelleri
- **Karakterler:** Animasyonlu 3D karakter modelleri (asker, işçi, kovboy, ninja, doktor); bekleme, koşma, ateş etme, darbe alma ve ölme animasyonları
- **Silahlar:** Elde taşınan 3D silah modelleri: tabanca, SMG, tüfek, pompalı ve keskin nişancı; iki silah yuvası, kafadan vuruşta 2x hasar, geri tepme
- **Ganimet:** Silah, mermi, ilk yardım, enerji içeceği, el bombası, zırh; elenen botlar sandık bırakır
- **Araçlar:** Binilebilen ciplerle hızlı ulaşım ve ezme hasarı
- **Güvenli bölge:** 6 aşamada daralan mavi duvar, sonraki bölge çemberi
- **Botlar:** Paraşütle iner, görüş hattı, tepki süresi, isabet sapması, el bombası, bölgeden kaçma; takım arkadaşları seni takip eder
- **Arayüz:** Mini harita, isabet işareti, hasar sayıları, hasar yönü göstergesi, öldürme akışı, yükseklik/hız göstergesi, duraklatma menüsü
- **Nişan alma:** NİŞAN butonu ile yakınlaştırma (keskin nişancıda dürbün), daha az sekme
- **Yere düşme:** Duo/Squad'da can bitince yere düşersin; bot takım arkadaşın gelip 5 saniyede kaldırır
- **Mağaza:** Maçlardan kazanılan altınla 6 farklı karakter açılır ve kuşanılır
- **Ayarlar:** Bakış hassasiyeti, grafik kalitesi (düşük/orta/yüksek), ses; düşük RAM'li telefonlarda otomatik düşük kalite
- **İlk maç ipuçları**, rakip adım sesleri, oyuncu seviyesine göre zorlaşan botlar, uygulama ikonu
- **Ses ve efekt:** Silah, patlama, adım, uçak, rüzgâr, motor sesleri; namlu alevi, kıvılcım, toz, patlama efektleri
- **Profil:** Seviye, XP, altın, maç/zafer/öldürme istatistikleri (cihazda kaydedilir)
- Karakter, silah ve siper modelleri Quaternius'un CC0 paketlerinden (bkz. `ASSETS.md`); geri kalan her şey koddan üretilir

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

Masaüstü test kontrolleri: WASD hareket, sağ fare tuşu basılı nişan, sol tık ateş, Shift koş, Space zıpla/atla, C eğil, R doldur, X ilk yardım, V içecek, G el bombası, Q silah değiş, F araca bin/in, E nişan.

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
