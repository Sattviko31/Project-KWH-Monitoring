-- =====================================================
-- Quick Setup Script for New Device
-- KWHMonitoring Database - Complete Auto-Generate
-- =====================================================

-- 1. CREATE DATABASE
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'KWHMonitoring')
BEGIN
    CREATE DATABASE [KWHMonitoring];
    PRINT '[OK] Database KWHMonitoring created successfully.';
END
ELSE
BEGIN
    PRINT '[INFO] Database KWHMonitoring already exists. Skipping creation.';
END
GO

USE [KWHMonitoring];
GO

-- 2. CREATE ALL TABLES
PRINT '';
PRINT '=== Creating Tables ===';

-- Table: KWHData
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[KWHData]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[KWHData](
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [DeviceKey] NVARCHAR(50) NOT NULL,
        [DeviceId] NVARCHAR(50) NOT NULL,
        [GroupName] NVARCHAR(100) NOT NULL,
        [TerminalTime] DATETIME2 NOT NULL,
        [ReceivedTime] DATETIME2 NOT NULL,
        [PHASE_R] DECIMAL(18,4) NOT NULL,
        [PHASE_S] DECIMAL(18,4) NULL,
        [PHASE_T] DECIMAL(18,4) NULL,
        [AMPERE_R] DECIMAL(18,4) NOT NULL,
        [AMPERE_S] DECIMAL(18,4) NULL,
        [AMPERE_T] DECIMAL(18,4) NULL,
        [CosPhi] DECIMAL(18,4) NOT NULL,
        [W] DECIMAL(18,4) NOT NULL,
        [TotalW1M] DECIMAL(18,4) NOT NULL,
        [Aktif_Power] DECIMAL(18,4) NOT NULL,
        [TotalW] DECIMAL(18,4) NOT NULL,
        [F] DECIMAL(18,4) NOT NULL,
        CONSTRAINT [PK_KWHData] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT '[OK] Table KWHData created.';
END

-- Table: AnomalyLogs
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AnomalyLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AnomalyLogs](
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [DeviceKey] NVARCHAR(100) NOT NULL,
        [DeviceId] NVARCHAR(100) NOT NULL,
        [AnomalyType] NVARCHAR(500) NOT NULL,
        [PowerValue] DECIMAL(18,4) NOT NULL,
        [ThresholdValue] DECIMAL(18,4) NOT NULL,
        [Deviation] DECIMAL(18,4) NOT NULL,
        [DetectedTime] DATETIME2 NOT NULL,
        [EMAValue] DECIMAL(18,4) NULL,
        [ThresholdMode] NVARCHAR(50) NOT NULL,
        [Acknowledged] BIT NOT NULL DEFAULT ((0)),
        [AcknowledgedTime] DATETIME2 NULL,
        [Notes] NVARCHAR(500) NOT NULL,
        CONSTRAINT [PK_AnomalyLogs] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT '[OK] Table AnomalyLogs created.';
END

-- Table: AppSettings
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AppSettings]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AppSettings](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [SettingKey] NVARCHAR(100) NOT NULL,
        [SettingValue] NVARCHAR(500) NOT NULL,
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_AppSettings] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT '[OK] Table AppSettings created.';
END

-- Table: HourlyEnergy
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[HourlyEnergy]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[HourlyEnergy](
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [DeviceKey] NVARCHAR(100) NOT NULL,
        [Hour] DATETIME2 NOT NULL,
        [EnergyKWh] DECIMAL(18,4) NOT NULL,
        [CalculatedAt] DATETIME2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_HourlyEnergy] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT '[OK] Table HourlyEnergy created.';
END

-- Table: DailyEnergy
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DailyEnergy]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[DailyEnergy](
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [DeviceKey] NVARCHAR(100) NOT NULL,
        [Date] DATE NOT NULL,
        [EnergyKWh] DECIMAL(18,4) NOT NULL,
        [CalculatedAt] DATETIME2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_DailyEnergy] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT '[OK] Table DailyEnergy created.';
END

-- Table: MonthlyEnergy
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MonthlyEnergy]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[MonthlyEnergy](
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [DeviceKey] NVARCHAR(100) NOT NULL,
        [Year] INT NOT NULL,
        [Month] INT NOT NULL,
        [EnergyKWh] DECIMAL(18,4) NOT NULL,
        [CalculatedAt] DATETIME2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_MonthlyEnergy] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT '[OK] Table MonthlyEnergy created.';
END

-- Table: YearlyEnergy
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[YearlyEnergy]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[YearlyEnergy](
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [DeviceKey] NVARCHAR(100) NOT NULL,
        [Year] INT NOT NULL,
        [EnergyKWh] DECIMAL(18,4) NOT NULL,
        [CalculatedAt] DATETIME2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_YearlyEnergy] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT '[OK] Table YearlyEnergy created.';
END
GO

-- 3. CREATE INDEXES
PRINT '';
PRINT '=== Creating Indexes ===';

-- KWHData indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[KWHData]') AND name = N'IX_KWHData_DeviceKey_TerminalTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_KWHData_DeviceKey_TerminalTime] 
    ON [dbo].[KWHData]([DeviceKey], [TerminalTime]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    PRINT '[OK] Index IX_KWHData_DeviceKey_TerminalTime created.';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[KWHData]') AND name = N'IX_KWHData_ReceivedTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_KWHData_ReceivedTime] 
    ON [dbo].[KWHData]([ReceivedTime]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    PRINT '[OK] Index IX_KWHData_ReceivedTime created.';
END

-- AnomalyLogs indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AnomalyLogs]') AND name = N'IX_AnomalyLogs_DeviceKey_DetectedTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AnomalyLogs_DeviceKey_DetectedTime] 
    ON [dbo].[AnomalyLogs]([DeviceKey], [DetectedTime]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    PRINT '[OK] Index IX_AnomalyLogs_DeviceKey_DetectedTime created.';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AnomalyLogs]') AND name = N'IX_AnomalyLogs_Acknowledged')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AnomalyLogs_Acknowledged] 
    ON [dbo].[AnomalyLogs]([Acknowledged]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    PRINT '[OK] Index IX_AnomalyLogs_Acknowledged created.';
END

-- AppSettings index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AppSettings]') AND name = N'IX_AppSettings_SettingKey')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_AppSettings_SettingKey] 
    ON [dbo].[AppSettings]([SettingKey]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    PRINT '[OK] Unique Index IX_AppSettings_SettingKey created.';
END

-- Aggregated data indexes (UNIQUE)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[HourlyEnergy]') AND name = N'IX_HourlyEnergy_DeviceKey_Hour')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_HourlyEnergy_DeviceKey_Hour] 
    ON [dbo].[HourlyEnergy]([DeviceKey], [Hour]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    PRINT '[OK] Unique Index IX_HourlyEnergy_DeviceKey_Hour created.';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[DailyEnergy]') AND name = N'IX_DailyEnergy_DeviceKey_Date')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_DailyEnergy_DeviceKey_Date] 
    ON [dbo].[DailyEnergy]([DeviceKey], [Date]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    PRINT '[OK] Unique Index IX_DailyEnergy_DeviceKey_Date created.';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[MonthlyEnergy]') AND name = N'IX_MonthlyEnergy_DeviceKey_Year_Month')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_MonthlyEnergy_DeviceKey_Year_Month] 
    ON [dbo].[MonthlyEnergy]([DeviceKey], [Year], [Month]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    PRINT '[OK] Unique Index IX_MonthlyEnergy_DeviceKey_Year_Month created.';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[YearlyEnergy]') AND name = N'IX_YearlyEnergy_DeviceKey_Year')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_YearlyEnergy_DeviceKey_Year] 
    ON [dbo].[YearlyEnergy]([DeviceKey], [Year]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    PRINT '[OK] Unique Index IX_YearlyEnergy_DeviceKey_Year created.';
END
GO

-- 4. SEED DEFAULT DATA
PRINT '';
PRINT '=== Seeding Default Data ===';

INSERT INTO [dbo].[AppSettings] ([SettingKey], [SettingValue], [UpdatedAt]) 
VALUES 
    ('MaxCapacity', '100000', GETDATE()),
    ('NotificationEnabled', 'false', GETDATE()),
    ('CheckIntervalSeconds', '30', GETDATE())
ON CONFLICT WHERE SettingKey IN ('MaxCapacity', 'NotificationEnabled', 'CheckIntervalSeconds') DO NOTHING;

-- SQL Server doesn't have ON CONFLICT, so use this instead
IF NOT EXISTS (SELECT 1 FROM [AppSettings] WHERE [SettingKey] = 'MaxCapacity')
    INSERT INTO [AppSettings] ([SettingKey], [SettingValue], [UpdatedAt]) VALUES ('MaxCapacity', '100000', GETDATE());

IF NOT EXISTS (SELECT 1 FROM [AppSettings] WHERE [SettingKey] = 'NotificationEnabled')
    INSERT INTO [AppSettings] ([SettingKey], [SettingValue], [UpdatedAt]) VALUES ('NotificationEnabled', 'false', GETDATE());

IF NOT EXISTS (SELECT 1 FROM [AppSettings] WHERE [SettingKey] = 'CheckIntervalSeconds')
    INSERT INTO [AppSettings] ([SettingKey], [SettingValue], [UpdatedAt]) VALUES ('CheckIntervalSeconds', '30', GETDATE());

PRINT '[OK] Default settings seeded.';
GO

-- 5. FINAL VERIFICATION
PRINT '';
PRINT '======================================================';
PRINT 'DATABASE SETUP COMPLETE!';
PRINT '======================================================';
PRINT '';
PRINT 'Database: KWHMonitoring';
PRINT 'Tables: ';
SELECT ' - ' + TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = 'dbo' ORDER BY TABLE_NAME;
PRINT '';
PRINT 'Default Settings:';
SELECT ' - ' + SettingKey + ': ' + SettingValue FROM AppSettings ORDER BY SettingKey;
PRINT '';
PRINT 'Database is ready to use!';
PRINT '======================================================';
GO
