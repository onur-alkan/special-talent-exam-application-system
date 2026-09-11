# Production deployment ve readiness

Bu belge production yapılandırma fail-fast kurallarını, health check endpoint’lerini
ve IIS dağıtım kontrol listesini özetler. Gerçek parola, connection string veya
sunucu adı yazmayın.

## Gereksinimler

- Windows Server + IIS
- [.NET 8 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (hedef production veritabanı)
- SMTP hesabı (Şifremi Unuttum)

## Ortam değişkenleri (örnek isimler)

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ConnectionStrings__DefaultConnection = "<SECRET>"
$env:AllowedHosts = "basvuru.example.edu.tr"
$env:PublicUrl__BaseUrl = "https://basvuru.example.edu.tr"
$env:Email__DeliveryMode = "Smtp"
$env:Email__Host = "smtp.example.edu.tr"
$env:Email__Port = "587"
$env:Email__FromAddress = "no-reply@example.edu.tr"
$env:Email__Username = "<SECRET>"
$env:Email__Password = "<SECRET>"
$env:DataProtection__ApplicationName = "OzelYetenekSinavSistemi"
$env:DataProtection__KeysPath = "<NON_WEBROOT_PATH>"
$env:DataProtection__ProtectKeysAtRest = "true"
$env:PhotoUpload__StorageRootPath = "<NON_WEBROOT_PATH>"
$env:Seed__SeedTestData = "false"
$env:Seed__SuperAdminPassword = "<SECRET>"
```

Notlar:
- E-posta host alanı `Email:Host` (SmtpHost değil).
- Fotoğraf dizini `PhotoUpload:StorageRootPath` (boşsa ContentRoot `App_Data/private-uploads/photos`).
- Örnek dosya: `appsettings.Production.example.json` (kaynak şablon; publish’e girmez; ASP.NET yüklemez).

## Fail-fast (Production)

Eksik/geçersiz ayarda uygulama **başlamaz**:

| Alan | Kural |
|------|--------|
| ConnectionStrings:DefaultConnection | Zorunlu; localhost/`.\` kabul edilmez |
| PublicUrl:BaseUrl | Mutlak HTTPS; localhost yasak |
| AllowedHosts | Boş/`*`/yalnız loopback yasak; PublicUrl host ile uyumlu |
| DataProtection:KeysPath | Zorunlu; wwwroot dışı |
| PhotoUpload storage | wwwroot dışı; yazma/okuma probe |
| Email:DeliveryMode | Smtp; Host/Port/FromAddress zorunlu |
| Seed:SeedTestData | false |

Log/HTTP yanıtına connection string, parola, fiziksel yol veya stack sızdırılmaz.

## Health checks

| Endpoint | Anlamı | Bağımlılık |
|----------|--------|------------|
| `GET /health/live` | Süreç ayakta | Yok |
| `GET /health/ready` | Trafiğe hazır | SQL `SELECT 1`, DP keys, photo storage, kritik config |

- Başarı: 200; ready başarısız: 503
- JSON yalnız `status` ve güvenli check adları
- `Cache-Control: no-store, no-cache`
- SMTP’ye bağlanmaz; e-posta göndermez; migration çalıştırmaz

IIS/load balancer için örnek probe: `/health/live` (liveness), `/health/ready` (readiness).

## Reverse proxy / HTTPS

- `ReverseProxy:Enabled=true` yalnız güvenilir `KnownProxies` / `KnownNetworks` ile
- Production’da boş güvenilir proxy listesi reddedilir
- HSTS + HTTPS redirection etkin (non-Development)
- Cookie Secure/HttpOnly/SameSite mevcut ayarlarla korunur

## IIS uygulama havuzu

- .NET CLR: No Managed Code (ASP.NET Core Module)
- Identity: ApplicationPoolIdentity veya özel servis hesabı
- Bu kimliğe verin (minimum):
  - DataProtection KeysPath: Modify
  - PhotoUpload StorageRootPath: Modify
  - `logs/` dizini: Modify
- `Everyone`/`Users` geniş yazma vermeyin

## SQL

1. Backup alın (zorunlu).
2. DBA hedef DB ve uygulama login’ini oluşturur.
3. Script sırası: [`SQL-DEPLOYMENT.md`](SQL-DEPLOYMENT.md)
4. Uygulama hesabı: runtime DML; schema migration ayrı hesap tercih edilir.

Data Protection ayrıntıları: [`DATA-PROTECTION-DEPLOYMENT.md`](DATA-PROTECTION-DEPLOYMENT.md)

## İlk yayın kontrol listesi

1. `SeedTestData=false`
2. Test kullanıcılarını production’a taşımayın
3. İlk SuperAdmin parolasını secret deposundan verin; ilk girişte değiştirin
4. SMTP secret rotasyonu prosedürünü tanımlayın
5. Yayın sonrası: `/health/live` → 200, `/health/ready` → 200
6. Restart sonrası aynı health kontrollerini tekrarlayın
7. Başarısız migration: backup’tan restore ([SQL-DEPLOYMENT.md](SQL-DEPLOYMENT.md))

## Rollback

1. Önceki uygulama paketini geri yükleyin
2. Gerekirse DB backup restore
3. Environment variables / secret’ları önceki bilinen iyi değere alın
4. Health ready yeşil olana kadar trafiği kesik tutun

## Publish paketi ve secret güvenliği

- `appsettings.Development.json` sunucuya / publish paketine **kopyalanmamalıdır**
  (Web `.csproj` içinde `CopyToPublishDirectory=Never`).
- Secret’lar environment variable veya kurum secret store üzerinden sağlanmalıdır;
  publish paketine gerçek parola/connection string koymayın.
- `appsettings.Production.example.json` yalnız kaynak/şablon dosyasıdır; runtime
  publish klasörüne dahil edilmez. Deployment öncesi şablonu secret store ile
  doldurup sunucuya `appsettings.Production.json` olarak elle eklemeyin; env var tercih edin.
- Publish paketi deployment öncesi secret taramasından geçirilmelidir
  (otomatik test: `PublishArtifactSecurityTests`).
- Önceki deployment’tan kalan `appsettings.Development.json` / `.example` dosyalarını
  sunucu uygulama dizininden temizleyin.
- Publish paketine sonradan elle development config eklemeyin.

`dotnet publish` Web çıktısına test DLL’leri ve Development User Secrets dahil edilmez.
`launchSettings.json`, development/example appsettings dosyaları publish dışı bırakılır.
Yalnız secretsiz `appsettings.json` (localhost/boş CS varsayımları) pakette kalır;
Production fail-fast bu değerlerle başlamayı reddeder — gerçek değerler env ile verilir.
