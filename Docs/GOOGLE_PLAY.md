# Rise of Davraz – Google Play'e çıkış rehberi

Paket adı: `com.zootopiayazilim.riseofdavraz` (Play'e ilk yüklemeden sonra değişmez) · Geliştirici: Zootopia Yazılım
Sürüm kodu her AAB derlemesinde artar (1000 + çalıştırma numarası).

## 1. Hazır olanlar

| Ne | Nerede |
| --- | --- |
| Google Play paketi (.aab), yükleme anahtarıyla imzalı | Actions → "Google Play paketi (AAB)" → `son-aab` ön sürümü |
| Yerel hata ayıklama sembolleri | `son-aab` ön sürümünde `RiseOfDavraz.symbols.zip` |
| Uygulama simgesi 512×512 | `Tools/store/icon_512.png` |
| Öne çıkan grafik 1024×500 | `Tools/store/feature_1024x500.png` |
| Gizlilik politikası | oyun sunucusu: `http://201.18.215.185:8080/gizlilik` (dosya: `Server/web/gizlilik.html`) |
| Hesap silme sayfası | oyun sunucusu: `http://201.18.215.185:8080/hesap-silme` (dosya: `Server/web/hesap-silme.html`) |
| Oyun içinde hesap ve veri silme | Ayarlar → Gizlilik → Hesabımı ve tüm verilerimi sil |
| Kredi paketleri (Google Play Billing 8) | Mağaza → KREDİ; ürün kimlikleri aşağıda |
| Satın alma imza doğrulaması (Play lisans anahtarı) | `PlayConfig.GooglePlayPublicKey`; Google imzası tutmayan sahte satın almaya Kredi verilmez |
| Ödüllü reklam + izin penceresi (AdMob + UMP) | Mağaza → KREDİ → Reklam izle (şimdilik Google test reklamı) |
| Kutu, çark, çekiliş olasılıkları | her birinde OLASILIKLAR düğmesi (Play'in rastgele öğe kuralı) |

Teknik şartlar: hedef API 36 (Android 16), Unity 2022.3.62f3 (CVE-2025-59489 güvenlik yaması, 16 KB sayfa desteği),
IL2CPP ARM64 + ARMv7, minimum Android 7.0.

## 2. Bana vermen gerekenler (hiçbiri şifre değil, sohbete yazılabilir)

1. **AdMob uygulama kimliği** – `ca-app-pub-XXXXXXXXXXXXXXXX~YYYYYYYYYY`
2. **AdMob ödüllü reklam birimi kimliği** – `ca-app-pub-XXXXXXXXXXXXXXXX/ZZZZZZZZZZ`
3. **Play Games Services "kaynaklar" XML'i** (Android) – `<?xml ...><resources>...app_id...</resources>` metni
4. **12 test kullanıcısının e-posta listesi** (kapalı test için; istersen ben Play Console'a yapıştırılacak hâle getiririm)
5. (İsteğe bağlı) gizlilik ve hesap silme sayfalarını kendi sitene koyarsan o adresler

Şifre, Play Console girişi, anahtar dosyası **gerekmez**; yükleme anahtarının parolası yalnızca GitHub gizli ayarında
(`PLAY_KEY_PASSPHRASE`) durur.

## 3. Senin yapacakların (sırayla)

### 3.1 GitHub
- ✅ Yapıldı: Settings → Secrets and variables → Actions → **`PLAY_KEY_PASSPHRASE`**. Parolayı bir
  parola yöneticisinde sakla. Kaybedersen Play Console → Uygulama bütünlüğü → "Yükleme anahtarını sıfırla" ile yenilenir.

### 3.2 Play Console hesabı
- play.google.com/console → geliştirici hesabı (tek seferlik 25 USD), kimlik doğrulama.
- **Kişisel hesap** ise (13 Kasım 2023'ten sonra açılan): üretime çıkmadan önce **en az 12 test kullanıcısıyla, 14 gün
  kesintisiz kapalı test** şart. Kuruluş (şirket) hesabında bu kural yok; kuruluş hesabı için D-U-N-S numarası gerekir.
- **Geliştirici adı:** "Zootopia" kelimesi Disney'in tescilli film adıdır. Oyunun içinden ve mağaza metinlerinden
  çıkarıldı; yalnızca geliştirici adında ("Zootopia Yazılım") ve e-postada duruyor. Play'deki geliştirici adını
  kendi adın ya da farklı bir marka (ör. "ZT Yazılım") yaparsan marka şikâyeti riski tamamen kalkar. Paket adı
  (`com.zootopiayazilim…`) oyuncuya görünmez, sorun değil.

### 3.3 Uygulamayı oluştur
- Uygulama oluştur → Ad: **Rise of Davraz** · Varsayılan dil: Türkçe · Oyun · Ücretsiz.
- **Test → Dahili test → Yeni sürüm** → `RiseOfDavraz.aab` yükle (Play App Signing'i kabul et) → sürüm notu → yayınla.
  Dahili testte kendi hesabını test kullanıcısı olarak ekle; oyunu Play'den indirip dene (ödemeler ve reklamlar ancak
  Play'den indirilen sürümde çalışır).
- App bundle explorer → `RiseOfDavraz.symbols.zip`'i "Yerel hata ayıklama sembolleri" olarak yükle.

### 3.4 Uygulama içeriği (Politika → Uygulama içeriği)
- **Gizlilik politikası:** `http://201.18.215.185:8080/gizlilik` (kendi sitene koyarsan o adres).
- **Reklamlar:** Evet, uygulamada reklam var.
- **Uygulama erişimi:** Tüm işlevler özel erişim olmadan kullanılabilir (giriş/şifre yok).
- **Reklam kimliği:** Evet, kullanılıyor (reklam ve analiz amaçlı – AdMob).
- **Hedef kitle:** 16–17 ve 18+ (gerçekçi silahlı çatışma). 13 yaş altını seçme (Aile politikası devreye girer).
- **İçerik derecelendirmesi (IARC):** kategori Oyun. Şiddet: evet – gerçekçi görünen insan karakterlere silahla şiddet,
  kan/vahşet yok. Kullanıcılar arası etkileşim: evet (mesajlaşma, arkadaşlar). Dijital satın alma: evet. Rastgele öğe
  içeren satın alma: evet (kutular, çark, çekiliş). Kumar (gerçek para kazanma): hayır. Konum paylaşımı: hayır.
- **Veri güvenliği:** bkz. bölüm 5.
- **Hesap silme:** Evet, kullanıcılar hesap ve veri silmeyi isteyebilir; URL: `http://201.18.215.185:8080/hesap-silme`.
- Devlet uygulaması / finans / sağlık / haber: hayır.

### 3.5 Mağaza girişi (Büyüme → Mağaza varlığı → Ana mağaza girişi)
- Simge ve öne çıkan grafik: `Tools/store/`.
- **Ekran görüntüleri:** telefonda oyundan en az 4 yatay görüntü al (lobi, uçaktan atlama, çatışma, harita, mağaza) ve yükle.
- Metinler: bölüm 6.
- İletişim: e-posta `zootopiayazilim@gmail.com`, web sitesi `https://zootopiayazilim.com` (AdMob'un app-ads.txt'yi
  bulacağı site budur).

### 3.6 Ödemeler (Kredi paketleri)
1. Play Console → Ayarlar → **Ödeme profili** (satıcı hesabı) oluştur, banka ve vergi bilgilerini gir.
2. AAB yüklendikten sonra **Para kazanma → Ürünler → Uygulama içi ürünler → Ürün oluştur**, aşağıdaki kimliklerle
   (kimlikler birebir aynı olmalı), hepsini **Etkin** yap:

| Ürün kimliği | Ad | Açıklama | Önerilen fiyat |
| --- | --- | --- | --- |
| `kredi_1000` | 1.000 Kredi | 1.000 oyun içi Kredi | ₺39,99 |
| `kredi_2750` | 2.750 Kredi | 2.500 + 250 bonus Kredi | ₺99,99 |
| `kredi_6000` | 6.000 Kredi | 5.000 + 1.000 bonus Kredi | ₺199,99 |
| `kredi_13000` | 13.000 Kredi | 10.000 + 3.000 bonus Kredi | ₺399,99 |
| `kredi_30000` | 30.000 Kredi | 20.000 + 10.000 bonus Kredi | ₺799,99 |

3. Ayarlar → Lisans testi: kendi Gmail'ini ekle (test satın almaları ücretsiz olur).

### 3.7 AdMob (reklamdan gelir)
1. admob.google.com → hesap aç (Google Play hesabınla aynı Google hesabı olabilir), ödeme bilgileri.
2. Uygulamalar → **Uygulama ekle** → Android → "Uygulama Google Play'de listelendi mi?" Hayır (yayından sonra bağlarsın)
   → ad Rise of Davraz → **uygulama kimliğini bana gönder**.
3. Reklam birimleri → **Ödüllü** → ad "Kredi ödülü", ödül 100 Kredi → **birim kimliğini bana gönder**.
4. Gizlilik ve mesajlaşma → **GDPR (Avrupa) mesajı** oluştur ve yayınla (oyundaki izin penceresi bunu gösterir).
5. **app-ads.txt:** AdMob'un verdiği satırı (`google.com, pub-…, DIRECT, f08c47fec0942fa0`) `https://zootopiayazilim.com/app-ads.txt`
   olarak sitende yayınla. Play mağaza girişindeki web sitesi bu alan adı olmalı. Doğrulama olmadan reklam tam gösterilmez.
6. Oyun Play'de yayınlandıktan sonra AdMob'da uygulamayı mağaza girişine bağla (uygulama hazırlık incelemesi 2–3 gün).

### 3.8 Play Games ile giriş
1. Play Console → **Play Games Services → Kurulum ve yönetim → Yapılandırma** → "Hayır, oyunumda Google API'leri
   kullanılmıyor" → oyun adı Rise of Davraz → oluştur.
2. **Kimlik bilgileri → Kimlik bilgisi ekle → Android**: Google Cloud'da OAuth izin ekranını (harici, uygulama adı
   Rise of Davraz, destek e-postası) ve Android OAuth istemcisini oluştur. SHA-1 olarak **Uygulama bütünlüğü →
   Uygulama imzalama anahtarı sertifikası**ndaki SHA-1'i kullan (ilk AAB yüklendikten sonra görünür). Yükleme
   anahtarının SHA-1'i de `play-upload-key` ön sürümünün notlarında yazar; onu da ikinci istemci olarak ekleyebilirsin.
3. Yapılandırma sayfasında **"Kaynakları al" (Get resources) → Android** → çıkan XML metnini bana gönder. Bir sonraki
   sürümde Play Games girişi açılır (oyun açılışta otomatik giriş yapar, oyuncu adı ve kimliği hesaba bağlanır).
4. Test kullanıcılarını Play Games Services → Test kullanıcıları'na da ekle (yayından önce yalnızca onlar giriş yapabilir).

### 3.9 Kapalı test ve üretim
- Test → Kapalı test → yeni kanal → test kullanıcıları (e-posta listesi ya da Google Grubu) → aynı AAB → yayınla.
- 12+ test kullanıcısı katılım bağlantısından katılıp oyunu indirmeli ve 14 gün katılımda kalmalı.
- Sonra Panel → **Üretime erişim için başvur**. Onaydan sonra Üretim → yeni sürüm.

## 4. Sonraki sürüm için benim yapacaklarım
- AdMob gerçek kimlikleri `Assets/Scripts/Game/Play/PlayConfig.cs`'ye (test reklamları biter).
- Play Games girişi (eklenti 2.3.0 + senin XML'in).
- Önerim: oyun sunucusu için bir alan adı ve HTTPS (veri güvenliği formunda "aktarımda şifreli: evet" denebilsin).

## 5. Veri güvenliği formu cevapları

Genel: Veri toplanıyor ve paylaşılıyor mu? **Evet** · Aktarım sırasında şifreleniyor mu? **Hayır** (oyun sunucusu HTTP;
HTTPS'ye geçince "Evet") · Kullanıcı silme isteyebilir mi? **Evet** (oyun içi ve web).

| Veri türü | Toplanıyor | Paylaşılıyor | İsteğe bağlı mı | Amaç |
| --- | --- | --- | --- | --- |
| Konum → Yaklaşık konum (IP'den, AdMob) | Evet | Evet (Google) | Hayır | Reklam, dolandırıcılık önleme |
| Kişisel bilgi → Kullanıcı kimlikleri (oyuncu kimliği, oyuncu adı) | Evet | Hayır | Hayır | Uygulama işlevi, hesap yönetimi, güvenlik |
| Mesajlar → Uygulama içi mesajlar | Evet | Hayır | Evet | Uygulama işlevi |
| Ses → Ses kayıtları (sesli sohbet, "geçici olarak işlenir" işaretle) | Evet | Hayır | Evet | Uygulama işlevi |
| Uygulama etkinliği → Uygulama etkileşimleri (maç istatistikleri; reklam etkileşimi) | Evet | Evet (Google, reklam) | Hayır | Uygulama işlevi, reklam, analiz |
| Uygulama bilgileri ve performansı → Kilitlenme günlükleri, Tanılama (gönderilen hata raporları; AdMob tanılama) | Evet | Evet (Google) | Evet | Hata giderme, reklam |
| Cihaz veya diğer kimlikler → Reklam kimliği, uygulama kümesi kimliği (AdMob) | Evet | Evet (Google) | Hayır | Reklam, analiz, dolandırıcılık önleme |

Toplanmayanlar: ad-soyad, e-posta, telefon, adres, fotoğraf/video, müzik/diğer ses dosyaları, dosyalar, takvim, kişiler, sağlık, finansal
bilgi (ödemeyi Google Play işler; kart bilgisi bize gelmez), hassas konum, web geçmişi.

## 6. Mağaza metinleri

**Uygulama adı (30):** Rise of Davraz

**Kısa açıklama TR (80):** Gerçek Türk şehirlerinde 25 kişilik battle royale. Atla, savaş, son kalan ol!

**Kısa açıklama EN:** 25-player battle royale in real Turkish towns. Drop in, fight, be the last!

**Tam açıklama TR:**

Rise of Davraz, gerçek Türk şehirlerinde geçen 25 kişilik bir battle royale!

Uçaktan istediğin yere atla, paraşütle in, silah ve zırh topla, daralan güvenli bölgede rakiplerini ele ve son kalan ol.

◆ GERÇEK HARİTALAR – İstanbul Ekşioğlu, Isparta Kafeler Caddesi ve Davraz Mahallesi, Senir Kasabası, Pınar Evleri ve
Fırat Üniversitesi kampüsü: gerçek sokaklar, binalar ve arazi.
◆ MODLAR – Solo, Duo, Squad battle royale; 5v5 Takım Ölüm Maçı, Hakimiyet, Herkes Tek ve Soygun.
◆ ÇEVRİMİÇİ VE BOTLARLA – arkadaşlarınla oda kur ya da hızlı maça gir; eksik yerleri akıllı botlar doldurur, internet
olmadan da oyna.
◆ 29 SİLAH MODELİ – tabanca, hafif makineli, taarruz tüfeği, pompalı ve keskin nişancı; susturucu, dürbün, tutamak,
şarjör ve dipçik aparatları; parlayan efsanevi ve mitik kamuflajlar.
◆ 24 KARAKTER VE HAYVAN MASKELERİ – askerler, operatörler, özel tim; kedi, kurt, aslan, ejderha maskeleri.
◆ TAKTİK – yat, sürün, eğil; el bombası, molotof, sis, flaş ve gaz; binilebilen arazi araçları.
◆ ARKADAŞLAR – arkadaş ekle, mesajlaş, hediye gönder, günün en iyi oyuncuları listesinde yerini al.
◆ GÖREVLER VE ÖDÜLLER – günlük ve haftalık görevler, sezon görevleri, şans çarkı, haftalık şans çekilişi ve kutular.

Oyun ücretsizdir; isteğe bağlı oyun içi satın alımlar (rastgele öğeler dahil) ve isteğe bağlı ödüllü reklamlar içerir.
Kutu ve çekiliş olasılıkları oyunda gösterilir.

**Full description EN:**

Rise of Davraz is a 25-player battle royale set in real Turkish towns!

Jump from the plane wherever you like, parachute down, loot weapons and armour, outlast the shrinking safe zone and be
the last one standing.

◆ REAL MAPS – Ekşioğlu (Istanbul), Kafeler Street and Davraz (Isparta), Senir town, Pınar Evleri and the Fırat
University campus: real streets, buildings and terrain.
◆ MODES – Solo, Duo and Squad battle royale; 5v5 Team Deathmatch, Domination, Free-for-all and Heist.
◆ ONLINE OR WITH BOTS – make a room with friends or jump into a quick match; smart bots fill the empty slots, and you
can play offline too.
◆ 29 WEAPON MODELS – pistols, SMGs, assault rifles, shotguns and sniper rifles with suppressors, scopes, grips,
magazines and stocks; glowing legendary and mythic camos.
◆ 24 CHARACTERS AND ANIMAL MASKS.
◆ TACTICS – go prone, crawl, crouch; grenades, molotovs, smoke, flash and gas; drivable off-road vehicles.
◆ FRIENDS – add friends, chat, send gifts, climb today's top players.
◆ MISSIONS AND REWARDS – daily, weekly and season missions, a lucky wheel, a weekly draw and boxes.

Free to play; contains optional in-app purchases (including random items) and optional rewarded ads. Box and draw odds
are shown in the game.
