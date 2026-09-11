# Kullanıcı Mesajı Denetim Raporu

## Kapsam

- Branch: `feature/official-user-friendly-messages`
- Base: `feature/exam-entry-document-after-application-end` @ `49a77ff`
- Tarama: ViewModel DataAnnotations, Application/Domain validation constants,
  service/controller kullanıcı mesajları, Razor uyarı/onay metinleri,
  `site.js` istemci doğrulama mesajları, test exact-string beklentileri

## Sayılar (yaklaşık)

| Ölçüt | Değer |
|-------|-------|
| İncelenen kullanıcı mesajı | ~230–250 |
| Değiştirilen mesaj | ~110+ |
| Bilinçli değiştirilmeyen | ~120+ |
| İncelenen kaynak dosya | 40+ |

## Değiştirilmeyen ve nedeni

- **Belge erişim mesajları** (`ExamEntranceDocumentAccess`): Manuel kabul edilmiş metinler korundu.
- **T.C. kimlik / doğum yılı invalid mesajları**: Zaten kullanıcı odaklı.
- **Engel durumu zorunlu mesajı**: Zaten “giriniz” tonunda.
- **Yetki / bulunamadı mesajları**: Güvenlik ve mevcut davranış için uygun.
- **DataTables / Select2 / loading** metinleri: Bilgilendirme; kritik form hatası değil.
- **appsettings / e-posta altyapı `zorunludur`**: Geliştirici yapılandırma hataları; son kullanıcıya gösterilmez.
- **Audit log “soft-delete”**: Kullanıcıya gösterilmez.

## Validator kuralları

Regex, min/max uzunluk, zorunluluk, kimlik algoritması, fotoğraf boyutu/MIME,
saat kapısı ve yetkilendirme **değiştirilmedi**. Yalnızca `ErrorMessage` /
sabit metin / kullanıcıya dönen `Fail(...)` içerikleri güncellendi.

## İş akışları

Controller redirect/PRG, CAPTCHA, anti-forgery, form submit-lock, route ve
authorization politikaları değiştirilmedi.

## Server / client eşleşme

- HumanName / MeaningfulTitle / MeaningfulText: sunucu şablonları + unobtrusive
  metadata aynı kaynaktan (`InputTextRules`).
- Kimlik alanları required/country: `site.js` metinleri sunucu ile hizalandı.
- Telefon formatı: ViewModel `ErrorMessage` güncellendi; regex aynı.
- Fotoğraf: `PhotoUploadMessages` merkezi sabitler; testler sabite bağlı.

## Kalan düşük riskler

- HumanName validator tek ortak mesaj döndürür; rakam/noktalama/yasak karakter
  için ayrı sınıflandırma **algoritma değişikliği** gerektirir (bilinçli yapılmadı).
- Bazı uzun iş kuralı mesajları (tercih geçersiz/pasif) hâlâ biraz uzun; anlam
  korunarak bırakıldı.
- Admin yapılandırma hata metinleri (HostingSecurityOptions) kullanıcı UI’si değil.

## Test sonucu

- Build: 0 uyarı / 0 hata
- Tam paket (3 koşu): 927 / 927 / 927 başarılı, 0 fail, 0 skip
- Development DB aggregates değişmedi (Users 11, Roles 3, ExamPeriods 7, ExamPreferenceOptions 10, CandidateApplications 5)
- Orphan test DB: 0
