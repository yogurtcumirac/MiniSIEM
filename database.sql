-- MiniSIEM Veritabanı Kurulum Scripti (SQL Server)
-- SQL Server Management Studio (SSMS) üzerinden çalıştırılabilir.

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'CyberSecurityDB')
BEGIN
    CREATE DATABASE CyberSecurityDB;
END
GO

USE CyberSecurityDB;
GO

-- Identity sayaçlarının yeniden başlatmalarda atlamasını önlemek için
ALTER DATABASE SCOPED CONFIGURATION SET IDENTITY_CACHE = OFF;
GO

-- 1. Genel Tehdit Logları Tablosu
IF OBJECT_ID('dbo.TehditLoglari', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TehditLoglari (
        LogID INT IDENTITY(1,1) PRIMARY KEY,
        IP_Adresi NVARCHAR(50) NULL,
        TehditTuru NVARCHAR(100) NULL,
        KayitTarihi DATETIME DEFAULT GETDATE()
    );
END
GO

-- 2. Kritik Alarmlar Tablosu
IF OBJECT_ID('dbo.KritikAlarmlar', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.KritikAlarmlar (
        AlarmID INT IDENTITY(1,1) PRIMARY KEY,
        LogID INT NULL,
        IP_Adresi NVARCHAR(50) NULL,
        TehditTuru NVARCHAR(100) NULL,
        AlarmZamani DATETIME DEFAULT GETDATE()
    );
END
GO

-- 3. Yeni Tehdit Ekleme Stored Procedure
CREATE OR ALTER PROCEDURE dbo.sp_YeniTehditEkle
    @GelenIP NVARCHAR(50),
    @GelenTehdit NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.TehditLoglari (IP_Adresi, TehditTuru)
    VALUES (@GelenIP, @GelenTehdit);
END;
GO

-- 4. Kritik Kelime İçeren Logları Otomatik Alarma Çeviren Trigger
CREATE OR ALTER TRIGGER dbo.trg_TehditLoglari_KritikAlarm
ON dbo.TehditLoglari
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.KritikAlarmlar (IP_Adresi, TehditTuru, AlarmZamani, LogID)
    SELECT
        i.IP_Adresi,
        i.TehditTuru,
        ISNULL(i.KayitTarihi, GETDATE()),
        i.LogID
    FROM inserted i
    WHERE
        UPPER(i.TehditTuru) LIKE N'%KRİTİK%' OR
        UPPER(i.TehditTuru) LIKE N'%KRITIK%' OR
        UPPER(i.TehditTuru) LIKE N'%SQL%' OR
        UPPER(i.TehditTuru) LIKE N'%DDOS%' OR
        UPPER(i.TehditTuru) LIKE N'%BRUTE%' OR
        UPPER(i.TehditTuru) LIKE N'%EXPLOIT%' OR
        UPPER(i.TehditTuru) LIKE N'%ACİL%' OR
        UPPER(i.TehditTuru) LIKE N'%ACIL%' OR
        UPPER(i.TehditTuru) LIKE N'%MALWARE%';
END;
GO
