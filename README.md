# Zootopia Mobile

3D mobil battle royale oyunu (Unity, Android).
**Yapımcı:** Olcay Yasin Dünder

25 kişilik ada maçı: daralan güvenli bölge, ganimet sandıkları, silah değiştirme ve yapay zekâlı rakipler. Solo, Duo, Squad ve 5v5 modları; botlarla internetsiz ya da çevrimiçi (eksik oyuncuların yerini botlar alır).

## Özellikler

- **Uçaktan atlama:** Maç başında uçak adanın üstünden geçer; istediğin yerde ATLA, serbest düşüşte yönlen, paraşütle in
- **Üç gerçek harita, lobiden seçilir (HARİTA düğmesi):**
  - **Ekşioğlu (Çekmeköy, İstanbul):** Zootopia Veteriner Kliniği'nin çevresindeki 700×700 m'lik gerçek mahalle: OpenStreetMap'teki 320 bina (pencereli apartmanlar, kiremit çatılar, cami ve minaresi, sanayi binaları), gerçek sokaklar, parklar, koru ve gerçek arazi yükseltisi. 70 binanın zemin katına girilebilir; klinik tabelasıyla lobinin arka planında. Haritanın etrafı deniz.
  - **Senir Kasabası (Keçiborlu, Isparta):** Burdur Gölü kıyısından kasabanın arkasındaki ormanlık dağa kadar bütün kasaba (1,1×1,1 km oyun alanı). 3,4 km uzunluğundaki kasaba haritaya sığsın diye uzunlamasına sıkıştırıldı, evler gerçek boyutunda; sokaklar OpenStreetMap'ten, ~400 bahçeli ev sokaklara göre yerleştirildi (OSM'de Senir'in evleri çizili değil). Tarlalar, meyve bahçeleri, gölde yüzme ve tekne, "SENİR — KASABAMIZA HOŞ GELDİNİZ" tabelası.
  - **Fırat Üniversitesi (Rektörlük Kampüsü, Elazığ):** gerçek ölçekli 1×1 km kampüs: fakülteler, rektörlük binası (tabelalı), kampüs yolları, çevre mahalleler; çevresi tepelerle kapalı.
  - Haritalar `Tools/build_map.py <harita>` ile `MapData/<harita>/` verisinden üretilir (veri: Actions → "Harita verisi indir").
- **Yakınlaşan mini harita:** Yerdeyken oyuncunun çevresini yakın gösterir, uçakta tüm haritayı
- **Yapılar:** Kapılı/pencereli evler, depolar, çam ve yaprak ağaçlar, kayalar, saklanılabilen çalılar; kum torbası, bariyer, konteyner, varil gibi 3D siper modelleri
- **Karakterler:** Animasyonlu 3D karakter modelleri (asker, işçi, kovboy, ninja, doktor); bekleme, koşma, ateş etme, darbe alma ve ölme animasyonları
- **Silahlar:** Elde taşınan 3D silah modelleri: tabanca, SMG, tüfek, pompalı ve keskin nişancı; iki silah yuvası, kafadan vuruşta 2x hasar, geri tepme
- **Ganimet:** Silah, mermi, ilk yardım, enerji içeceği, el bombası, zırh; elenen botlar sandık bırakır
- **Araçlar:** Binilebilen ciplerle hızlı ulaşım ve ezme hasarı
- **Güvenli bölge:** 6 aşamada daralan mavi duvar, sonraki bölge çemberi
- **Botlar:** Paraşütle iner, görüş hattı, tepki süresi, isabet sapması, el bombası, bölgeden kaçma; takım arkadaşları seni takip eder
- **Arayüz:** Mini harita, isabet işareti, hasar sayıları, hasar yönü göstergesi, öldürme akışı, yükseklik/hız göstergesi, duraklatma menüsü
- **Nişan alma:** NİŞAN butonu ile yakınlaştırma, daha az sekme; keskin nişancı ve 3x/6x dürbünde tam ekran dürbün görünümü
- **Silah Atölyesi:** 5 aparat yuvası (namlu, nişangâh, alt namlu, şarjör, dipçik) ile 17 aparat, 9 kamuflaj (desenli, parlayan efsanevi/mitik olanlar dahil), hasar/atış hızı/isabet/mobilite/menzil/kontrol çubuklarında artı-eksi gösterimi, döndürülebilir 3D silah önizlemesi; seçimler maçta aldığın silahlara otomatik uygulanır
- **Lobi:** Karakterin silahıyla (ve maskesiyle) ortada; profil (dokununca PROFİL ekranı), GÜÇ ve KUPA, şans çarkı, OYUN MODU kartı, günlük zaferler, BAŞLAT, Silah Atölyesi, Karakterler, Kariyer, Envanter, Mağaza
- **Oyun modları (OYUN MODU ekranı):** Battle Royale (Solo/Duo/Squad), 5v5 Takım Ölüm Maçı, **Hakimiyet** (A/B/C bölgelerini tut, 200 puan), **Herkes Tek** (8 oyuncu, 20 öldürme), **Soygun** (kasadaki para çantasını üssüne taşı, 5 çanta). Hakimiyet ve Herkes Tek çevrimiçi de oynanır; Soygun şimdilik botlarla
- **Envanter:** Kask, yelek, bot (zırh); patlayıcı, taktik ve sağlık eşyası (teçhizat); 3 güçlendirici kart; 10 hayvan maskesi. Kutulardan gelen kartlar ve Kredi ile seviye atlar (zırh/teçhizat 10, güçlendirici 5 yıldız); GÜÇ hepsini toplar. Maçta etkili: kafa vuruşu ve arkadan hasar azaltma, başlangıç zırhı, hız, sessiz adım, hızlı şarjör, keskin göz, son nefes, toparlanma, avcı, bombacı, doktor, gece görüşü
- **Atılabilirler:** El bombası, molotof (yeri yakar), patlayıcı paket (yapışır, büyük patlama), sis (görüşü keser, botlar da göremez), flaş (bakanı kör eder), gaz (zırhı deler); BOMBA ve TAKTİK düğmeleri
- **Maskeler:** Kedi, köpek, tavşan, ayı, rakun, baykuş, penguen, kurt, aslan, ejderha; karakterin kafasına takılır, çevrimiçi herkes görür, botlar da takar
- **Profil:** KUPA ve lig (bronz → usta), maç, zafer, kazanma oranı, öldürme, K/D, en çok öldürme, kafadan vuruş, toplam hasar, oyun süresi, kuşanılanlar
- **Fotoğraf tabanlı dokular:** Poly Haven (CC0) asfalt, kaldırım, çim, orman zemini, toprak, sıva, beton, oluklu sac, kiremit, ağaç kabuğu; arazi 5 katmanlı karışım + normal haritalar, cephelerde gerçek pencereler (perde, jaluzi, ışıklı oda), camlarda şehir yansıması, balkonlar, şeritli/bordürlü yollar
- **Hissiyat:** Her düğmede basılma animasyonu, tık sesi ve kısa titreşim; isabet/öldürme/hasar titreşimleri; ekranlar yumuşak açılır
- **Giriş ekranı:** Yükleme çubuğu ve ipuçlarıyla açılış, ardından şehrin üstünde süzülen kamerayla "DOKUNARAK BAŞLA" ekranı; oyun genelinde Barlow Condensed yazı tipi
- **Hazırlık ekranı:** BAŞLAT'tan sonra karakter (Kasap Leydi dahil 7 karakter) ve maça birlikte girdiğin birincil silah seçilir; silah modelleri (AK-19 Taktik, Gölge Avcı, Alev Kartalı tabanca) seçilebilir
- **Profesyonel ayarlar:** Temel (nişan yardımı, ateş etme modu: tek dokunuşla nişangâh / nişan almadan / otomatik / kişisel), Kontroller (sabit/takip ateş düğmesi, sol oyun kolu modu, sol ateş düğmesi, düğme görünürlüğü), Ses ve Grafikler (Düşük–Maks. kalite, 30/60/Maks. FPS, düzgünleştirme, gölgeler, parlaklık), Hassasiyet (hız ivmesi, kamera/nişangâh/dürbün hassasiyeti, jiroskop), Künye
- **Alan daralması:** Her aşama 30 saniye bekler, sonra daralır; botlar haritanın farklı bölgelerine dağılarak atlar
- **Hata modu:** Ayarlar → Hata modu AÇIK. Ekranda FPS ve kırmızı HATA rozeti görünür; HATA EKRANI tüm hataları `ZM-...` kodlarıyla listeler, RAPORU KOPYALA cihaz bilgisiyle birlikte panoya kopyalar. Önceki oturumun hataları da saklanır (`zm_hata.log`)
- **Yere düşme:** Duo/Squad'da can bitince yere düşersin; bot takım arkadaşın gelip 5 saniyede kaldırır
- **5v5 takım ölüm maçı:** İki takım, seçili haritanın dar bir bölgesinde (Ekşioğlu: apartmanlar, Senir: kasaba sokakları, Fırat: fakülteler); ölünce 5 saniyede takımının tarafında yeniden doğarsın, 40 öldürmeye ulaşan ya da 8 dakika sonunda önde olan takım kazanır. Özel odada grup aynı takımda; eksik yerleri botlar doldurur
- **Arkadaşlar:** Oyuncu arama, arkadaş ekleme, takip et / takipten çık, özel mesajlaşma (küfür süzgeçli), arkadaşlara Kredi, hediye kutusu, karakter ya da kamuflaj gönderme; lobide odadaki herkesin gördüğü sohbet ve komik ifadeler
- **Mağaza (yalnız Kredi):** FIRSATLAR (günlük ücretsiz hediye, her gün 6 indirimli fırsat, haftalık teklif paketleri), 5 kutu (ahşap, bronz, gümüş, altın, elmas — iyi kutular nadir ödül garantiler), karakterler, TEÇHİZAT (zırh, eşya, güçlendirici, maske) ve kamuflajlar; kutular 4-5 dokunuşta sallanıp çatlayarak açılır, ödüller nadirliğine göre ışıklarla gelir; her şey arkadaşa hediye edilebilir
- **Şans çarkı:** Günde bir ücretsiz çeviriş, sonra Kredi ile 5 çeviriş daha; Kredi, kutu ve eşya kartları
- **Görevler:** Her gün 3 günlük (+ hepsini bitirene bonus kutu) ve her hafta 3 haftalık görev; 4 haftalık sezonda 5 aşamalı 7 sezon görevi; toplanan görevlerle dolan haftalık GÖREV SANDIĞI (1/5/8); günün ilk 6 zaferine birer kutu (GÜNLÜK ZAFERLER)
- **Karakter hareketleri:** Yürüme/koşma hıza göre senkron (ayak kaymaz), yana ve geriye yürürken kalça yürüme yönüne döner; eğilince dizler bükülür; silah nişanda omuza yaslanır, iki el silahta (IK); vurulunca kamera vuruş yönüne sarsılır
- **Adım sesleri:** Ayak yere değdiği anda, zemine göre (asfalt, çim, toprak/çakıl, bina döşemesi, su) farklı ve her adımda değişen sesler; yakındaki rakiplerin adımları duyulur
- **Silah aparatları silahla bütün:** Her silah modelinin ray, namlu ekseni, el koruması, şarjör ve dipçik noktaları ölçüldü; susturucu namlunun ucuna, dürbün raya, tutamak el korumasının altına, şarjör uzatması şarjörün ucuna oturur
- **Ayarlar:** Bakış hassasiyeti, grafik kalitesi (düşük/orta/yüksek), ses; düşük RAM'li telefonlarda otomatik düşük kalite
- **İlk maç ipuçları**, rakip adım sesleri, oyuncu seviyesine göre zorlaşan botlar, uygulama ikonu
- **Grafik:** Post Processing Stack v2 (bloom, renk düzenleme, vinyet, yüksekte ortam gölgelemesi), MSAA/FXAA kenar yumuşatma, yumuşak gölgeler, rüzgârda sallanan çimenler (GPU instancing), dalgalı ve yansımalı deniz ile kıyı köpüğü, normal haritalı arazi, pürüzsüz (smooth) model yüzeyleri
- **Ses ve efekt:** Silah, patlama, adım, uçak, rüzgâr, motor sesleri; namlu alevi, kıvılcım, toz, patlama efektleri
- **Kayıt:** Seviye, XP, Kredi, envanter, istatistikler cihazda kaydedilir
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
- `UIManager`, `TouchControls`, `HoldButton`, `UIUtil`, `Theme`
- `Gunsmith` (aparat/kamuflaj verisi ve kayıt), `GunsmithScreen` (atölye ekranı), `WeaponDressing` (aparat modelleri ve kamuflaj)
- `ErrorReporter` – hata kodları, hata ekranı, rapor

`Assets/Editor/ZootopiaBuild.cs` – derleme öncesi otomatik proje kurulumu.

## Lisans

MIT. [ebito-coder/battle-island-mobile](https://github.com/ebito-coder/battle-island-mobile) prototipi temel alınarak geliştirilmiştir; orijinal telif bildirimi `LICENSE` dosyasında korunmaktadır.
