# Form Validation State Audit

**Branch:** `feature/prominent-candidate-application-cta`
**Date:** 2026-07-22
**Scope:** Client-side validation görünürlüğü (pristine GET, stale state, server POST hataları)

## Kök neden (Login)

| Kanıt | Bulgu |
|-------|--------|
| `site.js` `oysBindLoginIdentifierValidation()` | Kurallar eklendikten sonra `$input.valid()` çağrılıyordu |
| Chrome CDP temiz GET (ae629a1) | `aria-invalid="true"`, mesaj henüz boş olsa da jQuery validator alanı invalid işaretliyordu |
| Sunucu GET HTML | `input-validation-error` / `field-validation-error` **yok** — hata tamamen istemci init kaynaklı |
| `$input.valid()` kaldırılınca | Temiz GET’te alan pristine kalıyor |

**Sonuç:** Hata **istemci kaynaklı**; sunucu ModelState değil.

### Boş submit yalnızca ilk alan (10dee85 sonrası bulgu)

| Kanıt | Bulgu |
|-------|--------|
| Script sırası | `site.js` DOMContentLoaded, unobtrusive `$(function(){parse})` jQuery ready'de |
| `oysBindLoginIdentifierValidation` | `$form.validate()` unobtrusive'dan önce çalışınca validator erken oluşuyor |
| jQuery validate | Mevcut validator varken `validate(options)` kuralları birleştirmiyor |
| Sonuç | Yalnızca `.rules()` ile eklenen LoginIdentifier doğrulanıyor; Password/CaptchaInput kuralları yok |

**Düzeltme:** `oysEnsureUnobtrusiveFormValidator(form)` — `unobtrusive.parse(form)` ile tüm `data-val` alanları parse edilir.

## Normal refresh davranışı (Login POST)

| Senaryo | HTTP | Davranış |
|---------|------|----------|
| Adres çubuğundan `/Account/Login` | GET | Temiz form (pristine) |
| Ctrl+F5 aynı URL | GET | Temiz form |
| Başarısız POST sonrası F5 | POST yeniden gönderimi (tarayıcı) | Sunucu invalid cevabı tekrarlanır; ModelState korunur — **PRG uygulanmadı** |
| bfcache geri dönüş | persisted `pageshow` | Merkezi `oysResetClientValidationState` stale client state temizler; `data-oys-server-validation` varsa korunur |

**PRG değerlendirmesi:** Login invalid POST için PRG gerekli değil; asıl UX sorunu init’te `$input.valid()` idi. POST refresh’te sunucu hataları bilinçli olarak korunuyor.

## Merkezi politika (`site.js`)

1. **`oysMarkServerValidationForms()`** — binder’lardan önce HTML’deki gerçek sunucu hatalarını işaretler (`data-oys-server-validation="true"`).
2. **`oysResetClientValidationState(form)`** — sunucu marker yoksa client-only hata sınıfları / `aria-invalid` / validator internal state temizlenir; **değerler korunur**.
3. **`oysApplyFormValidationStatePolicy()`** — init sonunda tüm formlara uygulanır.
4. **`oysBindFormValidationStatePolicy()`** — `pageshow` + `persisted` için stale state + submit-lock sıfırlama.
5. **`oysClearCaptchaInput()`** — `validator.element()` kaldırıldı; yalnız CAPTCHA alanı DOM/validator cache temizlenir, diğer alanlar validate edilmez.

## Form denetim listesi

| Form | URL / View | data-oys-login-identifier | data-oys-single-submit | Initial GET pristine (HTML) | Invalid POST server error | Not |
|------|------------|---------------------------|------------------------|----------------------------|---------------------------|-----|
| Login | `/Account/Login` | Evet | Hayır | Evet (HTTP test) | Evet (HTTP test) | Düzeltildi |
| Register | `/Account/Register` | Hayır | Evet | Evet (HTTP test) | Mevcut testler | Merkezi politika |
| Forgot Password | `/Account/ForgotPassword` | Evet | Hayır | Evet (HTTP test) | loginidentifier binder | Merkezi politika |
| Reset Password | `/Account/ResetPassword` | Hayır | Hayır | Evet (HTTP test) | Controller return View | Merkezi politika |
| Change Password | `/Account/ChangePassword` | Hayır | Evet | Unobtrusive only | return View | Merkezi politika |
| Profile update | `/CandidateProfile` | Hayır | Evet | Unobtrusive only | return View | Merkezi politika |
| Personel oluşturma | `/UserManagement/Create` | Hayır | Evet | Unobtrusive only | return View | Merkezi politika |
| Sınav dönemi create/edit | `/ExamPeriod/*` | Hayır | Evet | Unobtrusive only | return View | Merkezi politika |
| Tercih add/edit | `/ExamPreferenceOption/Manage` | Hayır | Kısmi | Unobtrusive only | return View | Merkezi politika |
| Sınav sonucu değerlendirme | `/ExamResult/Evaluate` | Hayır | Evet | Unobtrusive only | return View | Merkezi politika |
| SystemSetting | `/SystemSetting` | Hayır | Hayır | Unobtrusive only | return View | Merkezi politika |
| Aday başvuru | `/CandidateApplication/Apply` | Hayır | Evet | Unobtrusive + identity | return View | Merkezi politika |
| Fotoğraf yükleme | Register / Profile | Hayır | Evet | Unobtrusive only | return View | Merkezi politika |
| Dönem yöneticisi | `/ExamPeriodManager/Manage` | Hayır | Hayır | Unobtrusive only | return View | Merkezi politika |
| Belge doğrulama | Public verify | Hayır | Hayır | Minimal form | N/A | Merkezi politika |

**Denetlenen POST form sayısı:** 15+
**Ek kök neden bulunan form:** Yalnız Login (`$input.valid()` init); ForgotPassword aynı binder’ı paylaşır — düzeltme ortak.
**Ayrı düzeltme gerektiren form:** Yok.

## CAPTCHA regresyonu

- `validator.element()` CAPTCHA refresh’ten kaldırıldı.
- Refresh yalnız CAPTCHA input + mesajını temizler; login identifier `.valid()` init kaldırıldığı için tetiklenmez.
- CAPTCHA input temizliği sonrası Chrome parola alanını sıfırlayabildiği için `oysSnapshotLoginFormFieldValues` / `oysRestoreLoginFormFieldValues` ile LoginIdentifier ve Password korunur.
- `oysBindCaptchaRefresh()` load-time bind korundu.

## Test kanıtları

- `FormValidationStateHttpTests` — pristine GET, site.js yapısı, Login empty POST, invalid CAPTCHA POST
- `LoginIdentityUiTests` — `$input.valid()` yok, merkezi pageshow politikası
- Mevcut `LoginCaptchaHttpTests` — CAPTCHA regresyon

## Canlı Chrome kabulü (tamamlandı)

Kullanıcı gerçek Chrome tarayıcısında aşağıdaki senaryoları onayladı:

| Senaryo | Sonuç |
|---------|--------|
| Temiz GET `/Account/Login` | Pristine; başlangıçta kırmızı hata yok |
| Boş submit | LoginIdentifier, Password ve CaptchaInput hataları aynı anda görünür |
| Alan bazlı düzeltme | Her alan doldurulunca yalnızca kendi hatası kalkar |
| CAPTCHA yenileme | Görsel değişir; CAPTCHA input temizlenir |
| Alan koruma (refresh) | LoginIdentifier ve Password korunur |
| Form davranışı | CAPTCHA yenileme form submit oluşturmaz; birkaç kez ardışık çalışır |

Otomatik testler (`FormValidationStateHttpTests`, `LoginCaptchaHttpTests`, `LoginCaptchaRefreshJsTests`) aynı davranışı HTTP ve JS yapı düzeyinde doğrular.

### Yanlış CAPTCHA POST — parola geri doldurma (beklenen güvenlik)

Geçersiz CAPTCHA ile gerçek POST sonrasında sunucu `ClearLoginSensitiveFields` ile `Password` ve `CaptchaInput` değerlerini model ve ModelState attempted value’larından temizler. Parolanın HTML’e geri yazılmaması **bilinçli güvenlik davranışıdır**; yalnızca istemci CAPTCHA yenileme (GET, form submit yok) sırasında `oysSnapshotLoginFormFieldValues` / `oysRestoreLoginFormFieldValues` ile identifier ve parola korunur.
