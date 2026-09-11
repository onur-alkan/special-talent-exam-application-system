# Kullanıcı Mesajı Yazım Kılavuzu

## Amaç

Bu belge, Özel Yetenek Sınav Sistemi’nde kullanıcıya gösterilen hata, uyarı,
başarı ve bilgilendirme metinlerinin resmî kamu hizmeti diline uygun,
kısa ve kullanıcı odaklı yazılmasını sağlar.

Doğrulama kuralları, yetkilendirme ve iş akışları bu kılavuzla değiştirilmez;
yalnızca kullanıcıya görünen metinler düzenlenir.

## Resmî Türkçe tonu

- Kısa, açık ve saygılı cümleler kullanınız.
- Kullanıcıyı yönlendiriniz: giriniz, seçiniz, kontrol ediniz, yeniden deneyiniz.
- Kullanıcıyı suçlayan veya teknik jargona dayanan ifadelerden kaçınınız.
- Cümle başı büyük harf; cümle sonunda nokta kullanınız.

## Hata mesajı yazım ilkeleri

1. Alanı açıkça adlandırınız.
2. Ne yapılması gerektiğini söyleyiniz.
3. Teknik kuralı (regex, ModelState, SQL) anlatmayınız.
4. Aynı kural için sunucu ve istemci metinlerini eş tutunuz.
5. Genel “Geçersiz.” veya “Bu alan zorunludur.” ifadelerinden kaçınınız.

## Required mesaj şablonları

- Adınızı giriniz.
- Soyadınızı giriniz.
- E-posta adresinizi giriniz.
- Telefon numaranızı giriniz.
- Kimlik belgesi türünü seçiniz.
- Kimlik numaranızı giriniz.
- Doğum yılınızı giriniz.
- Parolanızı giriniz.
- Güvenlik kodunu giriniz.
- Sınav / başvuru adını giriniz.
- Tercih adını giriniz.

## Format mesaj şablonları

- Geçerli bir e-posta adresi giriniz.
- Telefon numarasını 5XXXXXXXXX veya 05XXXXXXXXX biçiminde giriniz.
- Geçerli bir T.C. kimlik numarası giriniz.
- Geçerli bir doğum yılı giriniz.
- Geçerli bir uyruk seçiniz.

## Length / range mesaj şablonları

- Ad en az 2, en fazla 50 karakter olabilir.
- Parola en az 8 karakter olmalıdır.
- YGS puanını 0-560 aralığında giriniz.
- Sınav puanını 0-100 aralığında giriniz.
- Görünüm sırasını 1 veya daha büyük giriniz.
- Fotoğraf en fazla 2 MB olabilir.

## İş kuralı mesajları

- Gerçek iş kuralını söyleyiniz; veritabanı kısıt adı göstermeyiniz.
- Örnek: Bu e-posta adresiyle kayıtlı bir hesap bulunmaktadır.
- Örnek: Bitiş tarihi başlangıç tarihinden sonra olmalıdır.

## Sistem hataları

Kullanıcının çözemeyeceği teknik hatalarda:

- İşlem şu anda tamamlanamadı. Daha sonra yeniden deneyiniz.
- Dosya yüklenemedi. Yeniden deneyiniz.

Exception, SQL, connection string veya stack trace göstermeyiniz.

## Başarı mesajları

- Kayıt oluşturuldu. / Kayıt güncellendi. / Kayıt silindi.
- Başvurunuz alındı.
- Parolanız güncellendi.
- Tercih seçeneği eklendi.

Gereksiz “başarıyla” tekrarlarından kaçınınız.

## Kullanılmayacak ifadeler

- ModelState, regex, null, property, SQL, constraint, exception
- validation failed, invalid format code
- soft-delete, forbidden, illegal
- Kullanıcıyı suçlayan “yanlış girdiniz / hatalı işlem yaptınız” kalıpları

## Alan terimleri sözlüğü

| Terim | Standart kullanım |
|-------|-------------------|
| E-posta | E-posta |
| T.C. kimlik numarası | T.C. kimlik numarası |
| Yabancı kimlik numarası | Yabancı kimlik numarası |
| Güvenlik kodu | Güvenlik kodu (CAPTCHA yerine) |
| Parola | Parola (şifre yerine) |
| Sınav dönemi | Sınav dönemi |
| Tercih seçeneği | Tercih seçeneği |
| Sınava giriş belgesi | Sınava giriş belgesi |

## Server / client parity

- DataAnnotation `ErrorMessage`, service `Fail(...)` ve `site.js` unobtrusive
  mesajları aynı anlamı ve mümkünse aynı metni taşımalıdır.
- İstemci doğrulaması atlanırsa sunucu aynı kuralı uygulamaya devam eder.

## Erişilebilirlik

- Alan hataları `asp-validation-for` ile ilişkilendirilmiş kalmalıdır.
- Pristine GET’te hata göstermeyiniz.
- Hassas alanlarda (parola, güvenlik kodu) değeri geri yazmayınız.

## Örnek önce / sonra

| Önce | Sonra |
|------|-------|
| Ad zorunludur. | Adınızı giriniz. |
| Ad alanı en az bir harf içermeli… | Adınızı harf kullanarak giriniz. |
| Vesikalık fotoğraf zorunludur. | Vesikalık bir fotoğraf seçiniz. |
| soft-delete … emin misiniz? | Sınav dönemini silmek istediğinize emin misiniz? |

## Yeni mesaj eklerken kontrol listesi

- [ ] Alan adı açık mı?
- [ ] Kullanıcıya düzeltme yolu söyleniyor mu?
- [ ] Resmî “-iniz/-ınız” tonu korunuyor mu?
- [ ] Teknik jargon yok mu?
- [ ] Sunucu ve istemci metinleri eş mi?
- [ ] Exact-string testleri güncellendi mi?
- [ ] Doğrulama kuralı / limit değişmedi mi?
