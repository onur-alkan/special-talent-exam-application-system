# Tercih Seçeneği Yönetimi — Kabul Özeti

**Sürüm adayı:** v1.1.2 (patch)
**Feature branch:** `feature/fix-preference-option-exam-period-binding`
**Kapsam:** ExamPreferenceOption Manage/Add/Edit/Delete arayüzü ve binding düzeltmeleri

## Binding kök nedeni

Manage sayfasındaki ekleme formu iç içe model (`AddForm.*`) alan adlarıyla post ediliyordu.
`Add` aksiyonu ise düz `PreferenceOptionFormViewModel` bekliyordu.

Sonuç:

- `ExamPeriodId` bağlanmıyor (`Guid.Empty`)
- Kullanıcıya “Sınav dönemi bulunamadı.” mesajı görünüyordu
- Yeni sınav dönemine tercih eklenemiyordu

## Kalıcı çözüm

- Ekleme formu `_PreferenceOptionAddForm.cshtml` partial’ına alındı
- Form alan adları düz model ile uyumlu:
  - `ExamPeriodId`
  - `PreferenceName`
  - `DisplayOrder`
  - `IsActive`
- Geçersiz gönderimde PRG yerine aynı Manage sayfası validation mesajlarıyla
  yeniden render edilir; alt tablo kaybolmaz

## Yeni yönetim arayüzü

Manage sayfası kurumsal iki kart düzenine alındı:

1. **Üst kart:** Yeni Tercih Seçeneği Ekle
2. **Alt kart:** Tüm Tercih Seçenekleri (DataTables)

Davranışlar:

- Liste satırları salt okunur metin
- Arama ve sayfalama (mevcut DataTables + TR dil paketi)
- Sayfa boyutu seçenekleri: **10 / 25 / 50 / 100**
- Ayrı Edit sayfası
- Silme yalnız POST + anti-forgery + onay penceresi
- IsActive checkbox binding korunur; rozetler Aktif/Pasif gösterir
- Responsive/dar ekran kabul edildi

## DataTables length menu düzeltmesi

`data-length-menu` özniteliği DataTables HTML5 config tarafından ham string
olarak okununca seçenekler karakter karakter üretiliyordu.

Çözüm:

- `data-oys-length-menu` kullanımı
- `oysParseDataTablesLengthMenu` ile güvenli sayı dizisi
- Init öncesi HTML5 `lengthMenu` attribute/data temizliği

## Validation politikası

- Mevcut `MeaningfulTitle` / domain kuralları korunur
- Tek karakterli tercih adı için yeni minimum uzunluk kuralı **eklenmedi**
- Duplicate tercih adı / sıra kısıtları mevcut domain kurallarıyla çalışır

## Chrome manuel kabul

Kullanıcı gerçek Chrome ortamında aşağıdaki maddeleri onayladı:

- Yeni sınav dönemi tercih listesinde görünür
- Tercihleri Yönet doğru dönemle açılır
- Binding hatası giderildi
- Geçerli tercih ilgili döneme eklenir
- Üst form + alt tablo düzeni
- Salt okunur liste satırları
- Length menu yalnız 10/25/50/100
- Arama çalışır
- Duplicate uyarıları çalışır
- Edit sayfası doğru değerlerle açılır
- Güncelleme (ad/sıra/aktiflik) doğru
- Silme onayı / iptal / onaylı silme
- Aktif/Pasif rozetleri
- Responsive görünüm

## Otomatik doğrulama

- Release build: 0 uyarı, 0 hata
- Son bilinen tam suite: en az **898** test

## Notlar

- Bu belge gerçek GUID, kimlik, parola veya connection string içermez
- Development DB’deki manuel test kayıtları otomatik temizlenmez
- v1.1.1 tag ve paketleri bu patch ile değiştirilmez
