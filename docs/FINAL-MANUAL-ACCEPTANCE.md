# Final manuel kabul raporu — OzelYetenekSinavSistemi v1.2.0

## Özet

| Alan | Değer |
|------|--------|
| Proje | OzelYetenekSinavSistemi |
| Test tarihi | 24.07.2026 |
| Test edilen branch | `release/final-production-candidate` |
| Test edilen commit | `189b9d1413434426a84bac16c11592e60fffe67f` |
| Test ortamı | Yerel Development / HTTPS (`localhost`) |
| Testi gerçekleştiren | Proje sahibi |

**Sonuç:** Proje sahibi, final manuel uçtan uca kabul testlerinin tamamını
gerçekleştirdiğini ve **bütün testlerin başarıyla geçtiğini** bildirmiştir.

## Test kapsamı

### Anonim kullanıcı

- Login sayfası
- CAPTCHA yenileme ve doğrulama
- Kayıt
- T.C. / YKN / Pasaport
- Kimlik türüne göre mesajlar
- Uluslararası telefon
- Doğum tarihi
- Fotoğraf seçimi ve önizleme
- Şifremi Unuttum
- Belge doğrulama

### Aday

- Giriş
- Profil görüntüleme ve güncelleme
- Profil fotoğrafı güncelleme
- Aktif sınav dönemlerini görüntüleme
- Başvuru oluşturma
- Tercih seçme, sıralama, kaldırma ve kaydetme
- Başvuru güncelleme
- Sınava giriş belgesi
- Sonuç görüntüleme
- Yetkisiz başka aday verisine erişememe

### Başvuru Yöneticisi

- Yalnız atanan sınav dönemine erişim
- Aday listesi
- Değerlendirme
- Girdi / Girmedi / İptal
- Ondalıklı puan
- Puan sıralaması
- Açıklama
- Excel ve PDF dışa aktarma

### Super Admin

- Sınav dönemi yönetimi
- Yönetici atama
- Tercih seçenekleri
- AJAX server-side DataTables
- Arama, sıralama ve sayfalama
- Kullanıcı ve yetki kontrolleri
- Log ve sistem işlemleri

### Güvenlik

- Yetkisiz URL erişimlerinin engellenmesi
- Teknik SQL hata mesajı sızmaması
- Fotoğraf yükleme kontrolleri
- Kullanıcı dostu Türkçe hata mesajları
- Dosya ve MSSQL loglarının oluşması
- Hassas bilgilerin açık loglanmaması

## Kabul sınırları ve sorumluluklar

- Testler **yerel kabul ortamında** yapılmıştır.
- Gerçek IIS, HTTPS sertifikası, SMTP, production SQL bağlantısı,
  Data Protection key dizini ve klasör izinleri **kurum teknik ekibi
  tarafından deployment sırasında** doğrulanacaktır.
- Hiçbir yazılım için **mutlak sıfır hata garantisi** verilemez.
- Bu kabul sonucunda **bilinen kritik veya yüksek önemde uygulama hatası
  bulunmamaktadır**.

## Notlar

Bu belge kişisel veri, parola, e-posta, telefon, connection string veya
gerçek production secret içermez.
