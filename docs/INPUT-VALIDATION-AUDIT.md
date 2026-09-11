# Girdi Doğrulama Denetimi

**Branch:** `feature/prominent-candidate-application-cta`
**Base commit:** `d0661eb05b41dec894bd7ea02ac6e9693186e29a`
**Denetim tarihi:** 2026-07-22 (merge öncesi kapanış)

Bu belge, projede kullanıcı girdisi alan yönetim formlarının denetim matrisidir.

## Özet

| Metrik | Değer |
|--------|-------|
| Yönetim POST formu (veri girişi) | 10 |
| İncelenen ViewModel (form) | 14 |
| Kullanıcı girdisi string property | 30 |
| Ham `name=` düzeltilen alan | 4 (`PreferenceName`, `DisplayOrder`, `SettingKey`, `SettingValue`) |
| Merkezi validator | `InputTextRules` + 3 attribute + `SystemSettingValidator` |
| Client-side sınıf | **A** (unobtrusive + `input-text-validation.js`) |
| Yetkili HTTP/integration test | 22 admin + 8 register (`Validation.Http` + `Validation.Admin`) |
| Geçici test DB | `OYS_ValidationHttp_<timestamp>_<guid>` — dispose'da drop + doğrulama |
| Test sayısı (son) | **876/876** (3 ardışık tam çalıştırma başarılı) |

## Merkezi Bileşenler

| Bileşen | Amaç |
|---------|------|
| `InputTextRules` | Unicode farkındalıklı normalizasyon ve anlamlılık kuralları |
| `HumanNameAttribute` | Ad/soyad (insan adı) |
| `MeaningfulTitleAttribute` | Başlık, tercih adı |
| `MeaningfulTextAttribute` | Adres, açıklama, serbest metin (opsiyonel/zorunlu) |
| `SystemSettingValidator` | Bilinen ayar anahtarları için tür-güvenli doğrulama |
| `InputTextClientModelValidatorProvider` | Client `data-val-*` metadata üretimi |
| `input-text-validation.js` | jQuery unobtrusive ile server parity client kuralları |

**Normalizasyon:** Tek satırlı alanlarda trim + iç boşluk birleştirme. Çok satırlı açıklamalarda satır sonları korunur. Parola/token alanlarına uygulanmaz.

**Otorite:** Server-side (`ValidationAttribute` + servis katmanı) esastır. Client-side sınıf **A**: jQuery Validate unobtrusive — submit öncesi alan hatası; geçersiz formda `oysBindSingleSubmitForms` kilidi/overlay açılmaz.

## Yönetim Form Envanteri

| Form | View | POST action | ViewModel / property | `asp-for` | `data-val` | Client adapter | Server validator | Servis validator | Normalizasyon | Persistence | Antiforgery | Yetki |
|------|------|-------------|----------------------|-----------|------------|----------------|------------------|------------------|---------------|-------------|-------------|-------|
| Personel oluşturma | `UserManagement/Create.cshtml` | `UserManagement/Create` | `CreateStaffUserViewModel.FirstName/LastName` | Evet | `data-val-humanname` | `input-text-validation.js` | `HumanNameAttribute` | `UserManagementService` | `InputTextRules` | `Users` INSERT | Evet | SuperAdminOnly |
| Personel düzenleme | `UserManagement/Edit.cshtml` | `UserManagement/Edit` | `EditStaffUserViewModel.FirstName/LastName` | Evet | `data-val-humanname` | JS | `HumanNameAttribute` | Servis | Evet | UPDATE | Evet | SuperAdminOnly |
| Profil güncelleme | `CandidateProfile/Index.cshtml` | `CandidateProfile/Index` | `ProfileUpdateViewModel.*` | Evet | humanname/meaningfultext | JS | Attribute + servis | `UserService` | Evet | UPDATE | Evet | Candidate (kendi) |
| Sınav dönemi oluşturma | `ExamPeriod/Create.cshtml` | `ExamPeriod/Create` | `ExamPeriodFormViewModel.Title/Description` | Evet (partial) | meaningfultitle/text | JS | Attribute | `ExamPeriodService` | Evet | INSERT | Evet | SuperAdminOnly |
| Sınav dönemi düzenleme | `ExamPeriod/Edit.cshtml` | `ExamPeriod/Edit` | `ExamPeriodFormViewModel.*` | Evet (partial) | meaningfultitle/text | JS | Attribute | `ExamPeriodService` | Evet | UPDATE | Evet | AdminArea |
| Tercih ekleme | `ExamPreferenceOption/Manage.cshtml` | `ExamPreferenceOption/Add` | `PreferenceOptionManagePageViewModel.AddForm.*` | Evet | `data-val-meaningfultitle` | JS | Attribute | `ExamPeriodService` | Evet | INSERT | Evet | SuperAdminOnly |
| Tercih düzenleme | `_PreferenceOptionEditRow.cshtml` | `ExamPreferenceOption/Edit` | `PreferenceOptionFormViewModel.*` | Evet | meaningfultitle + range | JS | Attribute | Servis | Evet | UPDATE | Evet | SuperAdminOnly |
| Sınav sonucu | `ExamResult/Evaluate.cshtml` | `ExamResult/Save` | `ExamResultFormViewModel.AdminDescription` | Evet | `data-val-meaningfultext` | JS | Attribute + `IValidatableObject` | `ExamResultService` | Evet | UPSERT | Evet | AdminArea |
| Sistem ayarı | `_SystemSettingRow.cshtml` | `SystemSetting/Update` | `SystemSettingUpdateViewModel.*` | Evet | `data-val-range` (MaxPhotoSizeKb) | JS + range | `SystemSettingValidator` | Controller | Tür dönüşümü | UPDATE only | Evet | SuperAdminOnly |
| Dönem yöneticisi atama | `ExamPeriodManager/Manage.cshtml` | `ExamPeriodManager/Assign` | Guid parametreleri (metin girişi yok) | Kısmi (`managerUserId` select) | N/A (Guid) | N/A | Controller/servis | `ExamPeriodManagerService` | N/A | INSERT | Evet | SuperAdminOnly |

### Ham `name=` alanları — sonuç

| Konum | Önceki durum | Sonuç |
|-------|--------------|-------|
| `ExamPreferenceOption/Manage.cshtml` | `name="PreferenceName"` vb. | `PreferenceOptionManagePageViewModel` + `asp-for` |
| `ExamPreferenceOption/Manage.cshtml` (edit satırları) | Ham input | `_PreferenceOptionEditRow.cshtml` + `PreferenceOptionFormViewModel` |
| `SystemSetting/Index.cshtml` | `name="SettingKey/Value"` | `_SystemSettingRow.cshtml` + `SystemSettingUpdateViewModel` |
| `SetActive/Delete` (tercih) | `name="optionId"` | Guid-only toggle; metin girişi yok — bilinçli |
| `ExamPeriodManager/Manage.cshtml` | `name="managerUserId"` | Select2 Guid seçimi — metin doğrulama gerekmez |
| `ExamResult/Evaluate.cshtml` | `name="examPeriodId"` | Hidden Guid yönlendirme — metin doğrulama gerekmez |

## Client-side sınıf A kanıtı (form bazlı)

| Form | Kanıt |
|------|-------|
| Personel oluşturma | `ClientValidationTests` metadata; `ValidationAdminHttpTests.StaffCreate_Get_*`; POST `--------` → Türkçe hata |
| Sınav dönemi | `data-val-meaningfultitle`; HTTP POST `--------` → view + hata, DB değişmez |
| Tercih seçeneği | `PreferenceOptionManagePageViewModel` + partial `data-val-meaningfultitle`; HTTP GET/POST |
| Sınav sonucu | `data-val-meaningfultext-optional/multiline`; HTTP POST reddi + persistence yok |
| Profil | `data-val-humanname`; HTTP POST geçersiz ad reddedilir |
| Sistem ayarı | `data-val-range` 100–10240; bilinmeyen key / sınır dışı reddedilir |

**Submit kilidi:** `AdminSingleSubmitRegressionTests` + mevcut `SingleSubmitTests` — geçersiz formda POST/overlay yok; geçerli formda tek POST + kilit.

**CSP / static asset:** `CspSecurityTests` — `input-text-validation.js` `asp-append-version` ile yüklenir; inline script eklenmedi; script sırası jQuery → validate → unobtrusive → site.js.

## Yetkili HTTP / integration test ortamı

| Bileşen | Açıklama |
|---------|----------|
| `ValidationAdminTestDatabase` | `OYS_ValAdmin_<timestamp>` geçici SQL Server DB; şema `database/OzelYetenekSinavSistemi.sql` |
| `ValidationAdminFixture` | `WebApplicationFactory` + izole connection string; sentetik SuperAdmin/ApplicationManager/Candidate |
| `ValidationAdminTestAuthHandler` | Test-only `X-Test-Role` header ile gerçek authorization policy'leri |
| Cleanup | Fixture `DisposeAsync` → client/factory kapatılır, geçici DB drop edilir |
| Gerçek geliştirme DB | **Değiştirilmez** |

## Server bypass ve parity

| Test sınıfı | Kapsam |
|-------------|--------|
| `ValidationAdminHttpTests` | Antiforgery + POST bypass; geçersiz Unicode/görünmez karakter; DB yazımı yok |
| `AdminAuthorizationHttpTests` | Candidate/anon erişim reddi; SuperAdmin vs ApplicationManager ayrımı |
| `InputTextParityTests` | HumanName/MeaningfulTitle/MeaningfulText server ↔ JS mirror parity |
| `HumanNameValidationTests` / `MeaningfulTextValidationTests` | Genişletilmiş negatif/Unicode örnekleri |
| `InputNormalizationPersistenceTests` | Persistence öncesi normalizasyon |
| `SystemSettingValidationTests` | Bilinmeyen key, tür dışı, sınır dışı |

## HTTP test izolasyonu

| Bileşen | Açıklama |
|---------|----------|
| `ValidationHttpTestDatabase` | Paylaşılan geçici SQL Server DB; prefix `OYS_ValidationHttp_` |
| `TestDatabaseSafetyGuard` | `OzelYetenekSinavSistemi` ve production benzeri adları reddeder |
| `RegisterFormLiveFixture` | Register HTTP testleri — izole DB, gerçek dev DB'ye yazmaz |
| `ValidationAdminFixture` | Admin HTTP testleri — aynı DB altyapısı |
| `DevelopmentDatabaseGuard` | HTTP fixture'ları aktifken dev DB aggregate sayıları değişmez; tam suite koşularında doğrulandı |
| Cleanup | `SqlConnection.ClearAllPools()` + `SINGLE_USER DROP` + `DB_ID` doğrulaması |
| Stale orphan | Kesintili oturumlardan kalan `OYS_ValidationHttp_*` / `OYS_ValAdmin_*` DB'ler merge öncesi operasyonel temizlikle drop edilir; init sırasında da 10+ dakika eski kayıtlar temizlenir |
| Paralellik | Her collection kendi benzersiz DB'si; TestServer in-memory port |

## AdminDescription güvenliği

| Kontrol | Sonuç |
|---------|-------|
| Razor HTML encoding | `asp-for` textarea; `Html.Raw` yok (`AdminDescriptionSecurityTests`) |
| Stored XSS | Anlamlı metin kuralı + encode; `<script>alert(1)</script>` açıklama validation/display ile güvenli |
| Log sızıntısı | Servis doğrudan loglamıyor (`AdminDescriptionSecurityTests`) |

## Manuel kalan noktalar

- **Login / CAPTCHA Chrome kabulü:** Tamamlandı (bkz. `docs/FORM-VALIDATION-STATE-AUDIT.md`).
- **Diğer formlar — gerçek tarayıcı görsel smoke:** Playwright/Selenium kurulu değil; client-side submit engeli metadata/JS parity + HTTP testleri ile kanıtlandı.

## Test kanıtı (filtre özeti)

| Filtre | Geçen |
|--------|-------|
| HumanName | 62 |
| MeaningfulTitle | 24 |
| MeaningfulText | 36 |
| ClientValidation | 20 |
| RegisterFormLive | 8 |
| TestDatabaseSafety | 6 |
| Validation.Admin (HTTP) | 22 |
| Authorization | 21 |
| SingleSubmit | 12 |
| CspSecurity | 17 |
| FormValidationState / LoginCaptcha / CTA | 20 |
| **Toplam** | **876** (3× ardışık tam suite) |
