/* =====================================================================================
   OPSİYONEL BOOTSTRAP — yalnızca yerel/varsayılan geliştirme veritabanı adı
   -------------------------------------------------------------------------------------
   Bu dosya migration/schema scriptlerinin parçası değildir.
   Staging ve production kendi veritabanı adlarını DBA oluşturur; bu script zorunlu değildir.

   Kullanım (isteğe bağlı):
     sqlcmd -S "<SERVER>" -E -d master -b -i database\bootstrap\CreateDatabase.OzelYetenekSinavSistemi.sql

   Ardından şema/migration scriptlerini hedef DB ile çalıştırın:
     sqlcmd -S "<SERVER>" -E -d "<DATABASE>" -b -i database\OzelYetenekSinavSistemi.sql
   ===================================================================================== */
SET NOCOUNT ON;
GO

IF DB_ID(N'OzelYetenekSinavSistemi') IS NULL
BEGIN
    CREATE DATABASE [OzelYetenekSinavSistemi];
END;
GO
