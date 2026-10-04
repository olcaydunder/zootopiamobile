# Zootopia Mobile – Mimari

Durum: Faz 0 (repo analizi) tamamlandı. Tam rapor ve yol haritası: "Zootopia Mobile – Repo Analizi ve Yol Haritası" dokümanı.

## Bugünkü yapı

- Unity 2022.3.20f1, Built-in render pipeline, Gamma, Post Processing v2.
- Tek sahne `Assets/Scenes/Main.unity` (build script'i oluşturur). Sahne boştur; `GameBootstrap` dünyayı, oyuncuyu ve tüm UI'ı koddan kurar.
- Oyun kodu `Assets/Scripts/Game` altında, tek assembly (Assembly-CSharp).
- Android: GameCI (`.github/workflows`), IL2CPP ARM64, release `son-apk`.

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
| `Game/Net` | (Faz 6) ağ katmanı |
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
