# zootopiayazilim.com (Shopify) için Rise of Davraz sayfaları

Her .html dosyası Shopify sayfa düzenleyicisine (HTML görünümü `<>`) yapıştırılacak sayfa içeriğidir.

| Dosya | Sayfa başlığı | URL tanıtıcısı (handle) |
| --- | --- | --- |
| 1-gizlilik.html | Rise of Davraz – Gizlilik Politikası | rise-of-davraz-gizlilik |
| 2-hesap-silme.html | Rise of Davraz – Hesap ve Veri Silme | rise-of-davraz-hesap-silme |
| 3-kullanim-kosullari.html | Rise of Davraz – Kullanım Koşulları | rise-of-davraz-kullanim-kosullari |
| 4-oyun-sayfasi.html | Rise of Davraz | rise-of-davraz |

Sayfalar birbirine `/pages/<tanıtıcı>` ile bağlanır; tanıtıcılar tablodakiyle aynı olmalı.

`app-ads.txt`: Shopify > İçerik > Dosyalar'a yükle, sonra `/app-ads.txt` adresini dosyanın URL'sine yönlendir
(Online Mağaza > Navigasyon > URL yönlendirmeleri). Kontrol: https://zootopiayazilim.com/app-ads.txt

## Oyun tanıtım sayfası (tasarımlı)

`5-oyun-sitesi.liquid`: /pages/rise-of-davraz için hazır tasarım (Türkçe, telefona uyumlu).
1. Shopify > İçerik > Dosyalar: `img/` içindeki 4 resmi aynı adlarla yükle.
2. Online Mağaza > Temalar > Özelleştir > Sayfalar > Şablon oluştur (`rise-of-davraz`), "Sayfa" bölümünü gizle,
   Bölüm ekle > Özel Liquid > dosyanın tamamını yapıştır > Kaydet.
3. Sayfalar > Rise of Davraz > Tema şablonu: `rise-of-davraz`.
Oyun herkese açık olunca ilk satırdaki `rod_yayinda = false` değerini `true` yap (düğmeler "Google Play'den indir" olur).
