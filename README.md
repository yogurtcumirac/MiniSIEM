# MiniSIEM

C# (Windows Forms) ve SQL Server kullanılarak geliştirilmiş temel seviye bir Güvenlik Bilgi ve Olay Yönetimi (SIEM) masaüstü uygulaması.

## Özellikler

- **Canlı İzleme:** Belirli aralıklarla veritabanını sorgulayarak tabloyu güncel tutar (kaydırma konumunu ve seçili satırı bozmadan).
- **İkili Görünüm:** Sadece kritik alarmları (`KritikAlarmlar`) veya tüm ağ olaylarını (`TehditLoglari`) ayrı ayrı listeleme.
- **Otomatik Alarm Tetikleyici (SQL Trigger):** `TehditLoglari` tablosuna `SQL`, `DDoS`, `Brute`, `Exploit`, `Malware`, `Kritik` gibi anahtar kelimeler içeren bir kayıt düştüğünde SQL Server bunu otomatik olarak `KritikAlarmlar` tablosuna aktarır ve `LogID` ile ilişkilendirir.
- **Görsel Vurgulama:** Kritik alarmlar hem kritik görünümde hem de genel log listesinde kırmızı renkle vurgulanır.
- **IPv4 Doğrulama ve Giriş Kolaylığı:** IP giriş alanına yalnızca geçerli IPv4 adresleri kabul edilir; harf girişi engellenir ve 3 basamakta bir otomatik nokta eklenir.
- **Canlı IP Arama:** IP adresine göre anlık filtreleme.
- **SQL Injection Koruması:** Tüm sorgular parametreli olarak ve Stored Procedure (`sp_YeniTehditEkle`) üzerinden çalıştırılır.

## Kurulum

1. **Veritabanını Oluşturun:**
   SQL Server Management Studio (SSMS) üzerinden `database.sql` dosyasını çalıştırın. Bu işlem `CyberSecurityDB` veritabanını, tabloları, prosedürü ve trigger'ı otomatik kurar.

2. **Bağlantı Ayarını Yapın:**
   Proje ana klasöründeki `db_config.example.txt` dosyasının adını `db_config.txt` olarak kopyalayın ve kendi SQL Server örneğinizi yazın:
   ```
   Server=.\SQLEXPRESS; Database=CyberSecurityDB; Integrated Security=True;
   ```

3. **Projeyi Çalıştırın:**
   `MiniSIEM.sln` dosyasını Visual Studio ile açın ve `F5` ile çalıştırın.

## Teknolojiler

- C# / .NET Framework 4.7.2 (Windows Forms)
- Microsoft SQL Server (Stored Procedure, Trigger)
- ADO.NET (`System.Data.SqlClient`)
