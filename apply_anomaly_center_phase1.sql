-- Migration: AnomalyCenterPhase1
-- Applied manually because the KWHMonitoring service is running and cannot be stopped

USE [HaiwellElectrical];
GO

-- ============================================================
-- 1. Add columns to AnomalyLogs
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AnomalyLogs]') AND name = 'AcknowledgedBy')
    ALTER TABLE [dbo].[AnomalyLogs] ADD [AcknowledgedBy] [nvarchar](256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AnomalyLogs]') AND name = 'ResolvedBy')
    ALTER TABLE [dbo].[AnomalyLogs] ADD [ResolvedBy] [nvarchar](256) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AnomalyLogs]') AND name = 'ResolvedTime')
    ALTER TABLE [dbo].[AnomalyLogs] ADD [ResolvedTime] [datetime2](7) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AnomalyLogs]') AND name = 'IsResolved')
    ALTER TABLE [dbo].[AnomalyLogs] ADD [IsResolved] [bit] NOT NULL DEFAULT ((0));
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AnomalyLogs]') AND name = 'OperatorAction')
    ALTER TABLE [dbo].[AnomalyLogs] ADD [OperatorAction] [nvarchar](100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AnomalyLogs]') AND name = 'OperatorNotes')
    ALTER TABLE [dbo].[AnomalyLogs] ADD [OperatorNotes] [nvarchar](1000) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AnomalyLogs]') AND name = 'Severity')
    ALTER TABLE [dbo].[AnomalyLogs] ADD [Severity] [nvarchar](20) NULL DEFAULT ('medium');
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AnomalyLogs]') AND name = 'RootCause')
    ALTER TABLE [dbo].[AnomalyLogs] ADD [RootCause] [nvarchar](500) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AnomalyLogs]') AND name = 'RecommendedAction')
    ALTER TABLE [dbo].[AnomalyLogs] ADD [RecommendedAction] [nvarchar](1000) NULL;
GO

-- ============================================================
-- 2. Create AnomalyChartSnapshots table
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AnomalyChartSnapshots')
BEGIN
    CREATE TABLE [dbo].[AnomalyChartSnapshots](
        [Id] [bigint] IDENTITY(1,1) NOT NULL,
        [AnomalyLogId] [bigint] NOT NULL,
        [DetectedTime] [datetime2](7) NOT NULL,
        [BeforeDataJson] [nvarchar](max) NULL,
        [AfterDataJson] [nvarchar](max) NULL,
        [UpperThreshold] [decimal](18, 2) NOT NULL,
        [LowerThreshold] [decimal](18, 2) NOT NULL,
        [EMAValue] [decimal](18, 2) NULL,
        [SnapshotStatus] [nvarchar](20) NULL DEFAULT ('before'),
        [CreatedAt] [datetime2](7) NOT NULL DEFAULT (getdate()),
        [UpdatedAt] [datetime2](7) NULL,
        CONSTRAINT [PK_AnomalyChartSnapshots] PRIMARY KEY CLUSTERED ([Id] ASC)
        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY];
END
GO

-- ============================================================
-- 3. Create AnomalyMonthlyReports table
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AnomalyMonthlyReports')
BEGIN
    CREATE TABLE [dbo].[AnomalyMonthlyReports](
        [Id] [bigint] IDENTITY(1,1) NOT NULL,
        [Year] [int] NOT NULL,
        [Month] [int] NOT NULL,
        [TotalAnomalies] [int] NOT NULL,
        [OverloadCount] [int] NOT NULL,
        [DropCount] [int] NOT NULL,
        [AffectedDevices] [int] NOT NULL,
        [AverageDeviation] [decimal](5, 2) NOT NULL,
        [TopAffectedDevice] [nvarchar](50) NULL,
        [SummaryText] [nvarchar](2000) NULL,
        [Recommendations] [nvarchar](2000) NULL,
        [GeneratedBy] [nvarchar](256) NULL,
        [GeneratedAt] [datetime2](7) NOT NULL DEFAULT (getdate()),
        CONSTRAINT [PK_AnomalyMonthlyReports] PRIMARY KEY CLUSTERED ([Id] ASC)
        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY];
END
GO

-- ============================================================
-- 4. Indexes
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AnomalyLogs_Severity' AND object_id = OBJECT_ID(N'[dbo].[AnomalyLogs]'))
    CREATE NONCLUSTERED INDEX [IX_AnomalyLogs_Severity] ON [dbo].[AnomalyLogs]([Severity] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AnomalyLogs_IsResolved' AND object_id = OBJECT_ID(N'[dbo].[AnomalyLogs]'))
    CREATE NONCLUSTERED INDEX [IX_AnomalyLogs_IsResolved] ON [dbo].[AnomalyLogs]([IsResolved] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AnomalyChartSnapshots_AnomalyLogId' AND object_id = OBJECT_ID(N'[dbo].[AnomalyChartSnapshots]'))
    CREATE NONCLUSTERED INDEX [IX_AnomalyChartSnapshots_AnomalyLogId] ON [dbo].[AnomalyChartSnapshots]([AnomalyLogId] ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AnomalyMonthlyReports_Year_Month' AND object_id = OBJECT_ID(N'[dbo].[AnomalyMonthlyReports]'))
    CREATE NONCLUSTERED INDEX [IX_AnomalyMonthlyReports_Year_Month] ON [dbo].[AnomalyMonthlyReports]([Year] ASC, [Month] ASC);
GO

-- ============================================================
-- 5. Foreign key
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AnomalyChartSnapshots_AnomalyLogs_AnomalyLogId')
    ALTER TABLE [dbo].[AnomalyChartSnapshots] WITH CHECK ADD CONSTRAINT [FK_AnomalyChartSnapshots_AnomalyLogs_AnomalyLogId] FOREIGN KEY([AnomalyLogId])
    REFERENCES [dbo].[AnomalyLogs] ([Id])
    ON DELETE CASCADE;
GO

ALTER TABLE [dbo].[AnomalyChartSnapshots] CHECK CONSTRAINT [FK_AnomalyChartSnapshots_AnomalyLogs_AnomalyLogId];
GO

-- ============================================================
-- 6. Register migration in EF history table
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = '2026091401_AnomalyCenterPhase1')
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('2026091401_AnomalyCenterPhase1', '2.1.14-servicing-32113');
GO
