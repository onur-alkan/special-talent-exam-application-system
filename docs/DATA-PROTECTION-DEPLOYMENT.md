# Data Protection (üretim key ring)

ASP.NET Core kimlik doğrulama çerezleri Data Protection ile şifrelenir.
Parola sıfırlama jetonları bu projede **ayrıca** veritabanında SHA-256 hash olarak
saklanır; e-postadaki düz jeton Data Protection’a bağlı değildir. Yine de oturum
çerezlerinin uygulama yeniden başlatmalarında geçerli kalması için key ring’in
diskte kalıcı olması gerekir.

## Yapılandırma

`appsettings.json` (secretsiz varsayımlar):

```json
"DataProtection": {
  "ApplicationName": "OzelYetenekSinavSistemi",
  "KeysPath": "",
  "ProtectKeysAtRest": false,
  "ProtectToLocalMachine": false
}
```

| Alan | Açıklama |
|------|----------|
| `ApplicationName` | Ortamlar arasında aynı tutulmalı; cookie purpose izolasyonu. |
| `KeysPath` | Key ring dizini. **wwwroot dışında** olmalı. Production’da zorunlu. |
| `ProtectKeysAtRest` | Windows üretimde DPAPI ile dosya şifreleme. |
| `ProtectToLocalMachine` | `true` ise LocalMachine DPAPI; aksi halde CurrentUser. |

### Ortam değişkeni örnekleri

Gerçek sunucu yolunu kendi ortamınıza göre verin (repoya yazmayın):

```powershell
$env:DataProtection__ApplicationName = "OzelYetenekSinavSistemi"
$env:DataProtection__KeysPath = "D:\AppData\OYS\DataProtection-Keys"
$env:DataProtection__ProtectKeysAtRest = "true"
$env:DataProtection__ProtectToLocalMachine = "false"
```

Development’ta `KeysPath` boş bırakılırsa ContentRoot altında
`App_Data/DataProtection-Keys` kullanılır (gitignore’da; commit edilmez).

## Production fail-fast

- Production’da `KeysPath` boş veya wwwroot altında ise uygulama **başlamaz**.
- Geçici / bellek içi key ring’e sessiz düşülmez.
- Hata mesajları fiziksel yolu HTTP yanıtına yazmaz.

## Windows DPAPI (tek sunucu IIS)

- `ProtectKeysAtRest=true` ve Windows Production: `ProtectKeysWithDpapi` uygulanır.
- **`ProtectToLocalMachine=false` (önerilen tek app-pool senaryosu):** CurrentUser.
  IIS uygulama havuzu kimliği (ApplicationPoolIdentity / özel servis hesabı) key’leri
  kendi kullanıcı profili kapsamında korur. App-pool kimliği değişirse mevcut key
  ring açılamayabilir; hesabı sabitleyin.
- **`ProtectToLocalMachine=true`:** makine kapsamı. Aynı Windows makinedeki diğer
  hesaplar teorik olarak erişebilir; paylaşımlı host’larda dikkatli kullanın.
  LoadUserProfile ve app-pool izolasyonu operasyon ekibiyle doğrulanmalıdır.
- Development ve Linux/macOS’ta DPAPI **çağrılmaz**.

## Web farm / birden fazla örnek

Aynı makine DPAPI’si **yeterli değildir**. Birden fazla sunucu veya container için:

1. Paylaşılan key ring dizini (SMB/NFS veya merkezi volume), **veya** Redis/Azure Blob key repository
2. Ortak `ApplicationName`
3. Anahtarların sertifika / KMS ile şifrelenmesi (`ProtectKeysWithCertificate` vb.)

Bu görevde gerçek sertifika veya KMS bağlanmaz; farm için ayrı operasyon işi gerekir.

## NTFS izinleri

- Dizini yalnız IIS uygulama havuzu kimliğine **minimum** Modify (okuma/yazma) verin.
- `Everyone` / `Users` için geniş yazma vermeyin.
- Dizin public URL ile yayınlanmamalı (wwwroot dışında tutun).
- Anahtar XML içerikleri uygulama loglarına yazılmamalıdır.

## Operasyonel kontrol listesi

1. `KeysPath`’i oluşturun ve NTFS’i sıkılaştırın.
2. Uygulamayı bir kez başlatın; dizinde `key-*.xml` oluştuğunu doğrulayın.
3. Uygulamayı durdurup yeniden başlatın; yeni rastgele key ring oluşmamalı, aynı dizin kullanılmalı.
4. Oturum çerezlerinin beklenmedik şekilde geçersiz olmadığını kontrol edin.
