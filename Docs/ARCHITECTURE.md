# Zootopia Mobile – Mimari

Durum: Faz 0 (repo analizi) tamamlandı. Tam rapor ve yol haritası: "Zootopia Mobile – Repo Analizi ve Yol Haritası" dokümanı.

## Bugünkü yapı

- Unity 2022.3.20f1, Built-in render pipeline, Gamma, Post Processing v2.
- Tek sahne `Assets/Scenes/Main.unity` (build script'i oluşturur). Sahne boştur; `GameBootstrap` dünyayı, oyuncuyu ve tüm UI'ı koddan kurar.
- Oyun kodu `Assets/Scripts/Game` altında, tek assembly (Assembly-CSharp).
- Android: GameCI (`.github/workflows`), IL2CPP ARM64, release `son-apk`.
- Çevrimiçi sunucu: aynı oyunun Linux derlemesi (Mono, `-batchmode -nographics -server`), release `son-server`.

## Çevrimiçi (Faz 6, v1)

```
Telefon ──HTTP──> orchestrator.py (8080)  hızlı maç / oda kur / oda bul → {port, kod}
Telefon ──UDP───> maç süreci (7777–7799)  bekleme odası → maç → sonuç, sonra süreç kapanır
```

- `Server/orchestrator.py`: kiralık sunucuda çalışır; her maç için bir Unity sunucu süreci açar, yeni sunucu
  derlemesini GitHub'dan kendisi indirir. Sunucunun adresi `Server/endpoint.json`'da (telefon oradan okur).
- `Game/Net`: paket gerektirmeyen UDP katmanı. `NetConnection` aynı paket içinde güvenilir (sıralı, yeniden
  gönderilen) ve güvenilmez (hareket, anlık durum) mesaj taşır. Mesajlar ve alanlar: `NetProtocol`.
- Otorite: sunucu botları, bölgeyi, uçağı, sandıkları ve kapıları yürütür; telefonun bildirdiği her vuruşu
  menzil/hasar/hız sınırıyla denetler; ölümleri ve kazananı o belirler. Her telefon kendi oyuncusunu hareket
  ettirir ve kendi canını tutar (hasar sunucudan gelir). Diğer herkes `NetPuppet` olarak 0,12 sn geriden
  ara değerlemeyle çizilir. Sunucuda telefondaki oyuncuyu `ServerHuman` temsil eder (botlar onu görür, vurur).
- Sürüm: CI, telefon ve sunucuya aynı commit'i `Resources/zm_version.txt` olarak gömer; farklıysa bağlanılmaz.
- v1'de çevrimiçi kapalı olanlar: sınıf yetenekleri, jetonlar, araçlar, güçlendirme noktaları, yere düşme.

## Hedef klasör yapısı (fazlar ilerledikçe taşınır)

| Klasör | İçerik |
| --- | --- |
| `Game/Core` | bootstrap, ayarlar, hata raporlayıcı, performans profili |
| `Game/Input` | `IPlayerInput` ve kaynakları (dokunmatik, bot, ağ) |
| `Game/Player` | hareket, kamera, savaş bileşenleri |
| `Game/Combat` | silah tanımları, hasar hattı, isabet bölgeleri, atılabilirler |
| `Game/Inventory` | eşya tanımları, envanter modeli, loot |
| `Game/Match` | maç durum makinesi, bölge, uçak, ikmal |
| `Game/Bots` | bot beyni, algı, davranışlar |
| `Game/World` | harita verisi, şehir, arazi, çimen |
| `Game/UI` | ekranlar, HUD, HUD düzenleyici |
| `Game/Net` | ağ katmanı: UDP bağlantı, protokol, sunucu, telefon istemcisi, çevrimiçi menüler |
| `Data/` | ScriptableObject asset'leri (silah, eşya, mod, bölge fazı) |

Kural: dosya taşıma işlemi ayrı bir commit'tir ve davranış değiştirmez. Assembly (asmdef) ayrımı, klasörler oturduktan sonra yapılır.

## Kurallar

- Çalışan sistem yeniden yazılmaz; önce sarmalanır, sonra adım adım değiştirilir.
- Her commit derlenir; CI kırmızıysa sonraki iş başlamaz.
- Conventional commits: `feat:`, `fix:`, `perf:`, `refactor:`, `docs:`, `ci:`.
- Oyun verisi (silah, eşya, bölge) kodda değil asset'te tutulur (Faz 2'den itibaren).
- `PlayerController` girdiyi yalnızca `IPlayerInput` üzerinden okur (Faz 1a).
- Kaydedilmiş oyuncu verisi (PlayerPrefs anahtarları) geriye uyumlu kalır.
- Başka oyunların isim, logo, harita ve varlıkları kopyalanmaz; dış varlıkların lisansı `ASSETS.md`'ye yazılır.
