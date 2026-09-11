# SQL kurulum ve migration dağıtımı

Schema ve migration scriptleri **sabit veritabanı adı kullanmaz**.
Hedef veritabanı bağlantıda veya `sqlcmd -d` ile seçilir.

## Dosya envanteri ve sıra

| Sıra | Dosya | Amaç |
|------|-------|------|
| 0 (opsiyonel) | `database/bootstrap/CreateDatabase.OzelYetenekSinavSistemi.sql` | Yalnız varsayılan yerel adla DB oluşturur |
| 1 | `database/OzelYetenekSinavSistemi.sql` | Idempotent şema + güvenli seed |
| 2 | `database/migrations/001_AddUsersSecurityStamp.sql` | `Users.SecurityStamp` |
| 3 | `database/migrations/002_AddPreferenceOptionUniqueness.sql` | Tercih seçeneği unique constraint |
| 4 | `database/migrations/003_AddUsersBirthYear.sql` | `Users.BirthYear` |
| 5 | `database/migrations/004_AddUserIdentityDocument.sql` | Kimlik belge sütunları |
| 6 | `database/migrations/005_FinalizeUserIdentityStorage.sql` | Kimlik finalizasyonu |
| 7 | `database/migrations/006_AddApplicantBirthDate.sql` | `Users.BirthDate` DATE NULL |

Yeni kurulumda (1) final şemayı zaten içerir; (2)–(7) no-op/idempotent olarak güvenle uygulanabilir.
Mevcut eski veritabanlarında (2)–(7) sırayla uygulanır.

## Önerilen production sırası

1. Backup alın (zorunlu). Backup olmadan production migration çalıştırmayın.
2. DBA hedef veritabanını ve login/user yetkilerini oluşturur (bootstrap scripti zorunlu değildir).
3. Connection string hedef DB’yi gösterir.
4. Scriptler sıra numarasıyla `sqlcmd -d TargetDatabase -b` ile uygulanır.
5. Uygulama sınırlı yetkili DB hesabıyla çalışır (şema migration runtime hesabından ayrılabilir).

## Komut örneği

Windows Authentication:

```powershell
sqlcmd -S "<SERVER>" -d "<DATABASE>" -E -b -I -i "database\OzelYetenekSinavSistemi.sql"
sqlcmd -S "<SERVER>" -d "<DATABASE>" -E -b -I -i "database\migrations\001_AddUsersSecurityStamp.sql"
sqlcmd -S "<SERVER>" -d "<DATABASE>" -E -b -I -i "database\migrations\002_AddPreferenceOptionUniqueness.sql"
sqlcmd -S "<SERVER>" -d "<DATABASE>" -E -b -I -i "database\migrations\003_AddUsersBirthYear.sql"
sqlcmd -S "<SERVER>" -d "<DATABASE>" -E -b -I -i "database\migrations\004_AddUserIdentityDocument.sql"
sqlcmd -S "<SERVER>" -d "<DATABASE>" -E -b -I -i "database\migrations\005_FinalizeUserIdentityStorage.sql"
sqlcmd -S "<SERVER>" -d "<DATABASE>" -E -b -I -i "database\migrations\006_AddApplicantBirthDate.sql"
```

- `-b`: SQL hatasında non-zero exit code.
- `-I`: `QUOTED_IDENTIFIER ON` (filtered index’li tablolarda UPDATE/ALTER için gerekli).
- Gerçek sunucu adı, parola veya connection string bu dokümana yazılmaz.
- SQL Authentication kullanıyorsanız parolayı düz metin olarak script/dokümana eklemeyin; güvenli secret deposu kullanın.

## Güvenlik korumaları

- Schema ve migration scriptleri `master` / sistem DB’lerinde çalışmayı reddeder.
- Migration’lar gerekli tablo/sütun ön koşullarını kontrol eder.
- `USE OzelYetenekSinavSistemi` yoktur; staging/production farklı DB adı kullanabilir.
- `DROP DATABASE` / kontrolsüz `TRUNCATE` migration scriptlerinde yoktur.

## Başarısız migration

1. Hatayı ve hangi scriptte durduğunu kaydedin.
2. Uygulamayı eski sürümde tutun veya bakıma alın.
3. Son başarılı backup’tan restore edin.
4. Sorunu düzeltip scriptleri sıfırdan (veya kalan sıradan) yeniden uygulayın.

## Dağıtım paketi notu

`dotnet publish` Web çıktısına `.sql` dosyalarını kopyalamaz. Schema/migration
scriptleri depo kökündeki `database/` klasöründen (veya release artifact’ındaki
eşdeğer kopyadan) DBA tarafından uygulanır.
