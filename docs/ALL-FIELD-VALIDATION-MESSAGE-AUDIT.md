# All-Field Validation Message Audit

Bu belge, `feature/all-field-turkish-validation-messages` çalışmasında incelenen
form alanlarının mesaj kaynaklarını ve yapılan metin düzeltmelerini özetler.

Kapsam: yalnız kullanıcıya görünen mesaj yerelleştirmesi / alan bazlı metin /
placeholder value düzeltmesi / server–client parity. Doğrulama kuralları,
limitler, required/optional politikası ve iş akışları değiştirilmemiştir.

## Özet sayılar

| Ölçüt | Değer |
|-------|------:|
| İncelenen form / ekran | 18 |
| İncelenen giriş alanı (yaklaşık) | 95+ |
| İngilizce framework fallback (runtime) | 1 sınıf (ModelBindingMessageProvider varsayılanı) → kapatıldı |
| Kaynak kodda sabit İngilizce “The field / must be a number” | 0 |
| Gerçek form `value="--"` | 0 (placeholder option’lar `value=""`) |
| Değiştirilen / eklenen kullanıcı mesajı (annotation + binding katalog) | ~45 |
| Bilinçli olarak korunmuş ortak validator mesajları | HumanName, MeaningfulTitle/Text, BirthYear range, PhotoUpload, CAPTCHA |

## Global ModelBindingMessageProvider

`Program.cs` → `TurkishModelBindingMessageConfiguration.Apply`

| Durum | Yeni mesaj örneği |
|-------|-------------------|
| Sayı dönüşümü | `YGS puanını sayı olarak giriniz.` |
| Tarih dönüşümü | `Bitiş tarihini kontrol ediniz.` |
| Genel geçersiz değer | `Girilen değeri kontrol ediniz.` |
| Genel sayı | `Geçerli bir sayı giriniz.` |

Client: `data-val-number` bu provider’dan Türkçe üretilir. Ek güvenlik ağı:
`site.js` → `oysBindDefaultValidationMessages`.

## Alan tablosu (özet)

| Ekran | Alan | Tip | Req/Opt | Kaynak | Önceki | Yeni | Client | Server | Kural değişti mi? | Not |
|-------|------|-----|---------|--------|--------|------|--------|--------|-------------------|-----|
| Register | YgsScore | decimal? | Required | ModelBinding + Range | The field YGS Puanı must be a number. | YGS puanını sayı olarak giriniz. / 0-560 aralığı | OK | OK | Hayır | Binding vs Range ayrıldı |
| Profile | YgsScore | decimal? | Optional | ModelBinding + Range | English Range/Email riski | TR ErrorMessage + sayı mesajı | OK | OK | Hayır | Boş optional hata üretmez |
| ExamPeriod | MaxPreferences | int | Range | ModelBinding | The value 'abc' is not valid… | Maksimum tercih sayısını sayı olarak giriniz. | OK | OK | Hayır | |
| Preference | DisplayOrder | int | Range | ModelBinding | The value 'abc' is not valid… | Görünüm sırasını sayı olarak giriniz. | OK | OK | Hayır | |
| ExamResult | ExamScore | decimal? | Optional+Range | ModelBinding | English binding | Sınav puanını sayı olarak giriniz. | OK | OK | Hayır | |
| ExamPeriod | StartDate/EndDate | DateTime | Required | ModelBinding + IValidatableObject | English invalid | …tarihini kontrol ediniz. + çapraz kural aynı | OK | OK | Hayır | Cross-field metin korundu |
| Register | BirthYear | int? | Required | Binding + BirthYear | data-msg-number = range mesajı | Doğum yılını sayı olarak giriniz. | OK | OK | Hayır | Range mesajı ayrı kaldı |
| Profile/Register | Email | string | Required | EmailAddress | Profile’da İngilizce risk | Geçerli bir e-posta adresi giriniz. | OK | OK | Hayır | |
| Tüm select | placeholder | — | — | Razor | `value=""` + görünen `-- Seçiniz` | Aynı (model değeri değil) | OK | OK | Hayır | `value="--"` yok |
| Login/CAPTCHA | CaptchaInput | string | Required | Annotation | TR | TR (korundu) | OK | OK | Hayır | |
| HumanName | Ad/Soyad | string | Required | Attribute | TR ortak mesaj | Korundu | OK | OK | Hayır | Algoritma değişmedi |
| Photo | Photo | file | Required | PhotoUploadMessages | TR | Korundu | OK | OK | Hayır | |

## Bilinçli olarak değiştirilmeyenler

- HumanName tek ortak mesajı (algoritmik alt ayrım yok)
- MeaningfulTitle / MeaningfulText metinleri
- PhotoUploadMessages boyut/tür metinleri
- CAPTCHA “Güvenlik kodu hatalı…” metni
- Belge zaman kapısı mesajları
- LoginIdentifier helper metinleri
- DataRetentionOptions (kullanıcı formu değil)

## `--` denetimi

- Form input `value="--"`: **0**
- Select placeholder görünen metin `-- …` + `value=""`: korundu
- Tablolarda salt okunur `--`: dokunulmadı

## Server / client parity

- Sayı: `data-val-number` + ModelState binding aynı katalog
- Range/Email/Required: DataAnnotation `ErrorMessage` Türkçe
- jQuery varsayılan İngilizce number/email/date: `oysBindDefaultValidationMessages` ile bastırıldı

Gerçek kullanıcı verisi bu belgeye yazılmamıştır.
