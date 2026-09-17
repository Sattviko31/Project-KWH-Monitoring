-- ============================================================
-- Fix __EFMigrationsHistory & index discrepancy
-- Run in SSMS against [HaiwellElectrical] database
-- ============================================================

USE [HaiwellElectrical];
GO

-- 1. Hapus record migration ID lama yang tidak match dengan file migration saat ini
DELETE FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = '2026091401_AnomalyCenterPhase1';
GO

-- 2. Perbaiki index IX_AnomalyChartSnapshots_AnomalyLogId: harus UNIQUE (sesuai FluentAPI)
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AnomalyChartSnapshots_AnomalyLogId' AND object_id = OBJECT_ID(N'[dbo].[AnomalyChartSnapshots]'))
    DROP INDEX [IX_AnomalyChartSnapshots_AnomalyLogId] ON [dbo].[AnomalyChartSnapshots];
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AnomalyChartSnapshots_AnomalyLogId' AND object_id = OBJECT_ID(N'[dbo].[AnomalyChartSnapshots]'))
    CREATE UNIQUE NONCLUSTERED INDEX [IX_AnomalyChartSnapshots_AnomalyLogId] ON [dbo].[AnomalyChartSnapshots]([AnomalyLogId] ASC);
GO

-- 3. Daftarkan migration Phase1 yang BENAR sebagai sudah diterapkan
--    (semua kolom/tabel/index/FK sudah ada di DB dari apply_anomaly_center_phase1.sql)
IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = '20260916095322_AnomalyCenterPhase1')
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260916095322_AnomalyCenterPhase1', '2.1.14-servicing-32113');
GO

-- 4. Verifikasi: harus ada 2 record
SELECT [MigrationId], [ProductVersion] FROM [dbo].[__EFMigrationsHistory] ORDER BY [MigrationId];
GO
