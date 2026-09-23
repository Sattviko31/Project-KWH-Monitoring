-- =====================================================
-- KWHMonitoring Database Schema - Complete Script
-- SQL Server (T-SQL)
-- Version: 1.0
-- =====================================================

-- Create Database
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'KWHMonitoring')
BEGIN
    CREATE DATABASE [KWHMonitoring];
    PRINT 'Database KWHMonitoring created.';
END
ELSE
BEGIN
    PRINT 'Database KWHMonitoring already exists.';
END
GO

USE [KWHMonitoring];
GO

-- =====================================================
-- Table: KWHData (Main raw data storage)
-- =====================================================
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
    ) ON [PRIMARY];
    
    PRINT 'Table KWHData created.';
END
ELSE
BEGIN
    PRINT 'Table KWHData already exists.';
END
GO

-- Indexes for KWHData
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[KWHData]') AND name = N'IX_KWHData_DeviceKey_TerminalTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_KWHData_DeviceKey_TerminalTime] 
    ON [dbo].[KWHData]([DeviceKey] ASC, [TerminalTime] ASC)
    ON [PRIMARY];
    PRINT 'Index IX_KWHData_DeviceKey_TerminalTime created.';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[KWHData]') AND name = N'IX_KWHData_ReceivedTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_KWHData_ReceivedTime] 
    ON [dbo].[KWHData]([ReceivedTime] ASC)
    ON [PRIMARY];
    PRINT 'Index IX_KWHData_ReceivedTime created.';
END
GO

-- =====================================================
-- Table: AnomalyLogs
-- =====================================================
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
        [Acknowledged] BIT NOT NULL CONSTRAINT [DF_AnomalyLogs_Acknowledged] DEFAULT ((0)),
        [AcknowledgedTime] DATETIME2 NULL,
        [Notes] NVARCHAR(500) NOT NULL,
        CONSTRAINT [PK_AnomalyLogs] PRIMARY KEY CLUSTERED ([Id] ASC)
    ) ON [PRIMARY];
    
    PRINT 'Table AnomalyLogs created.';
END
ELSE
BEGIN
    PRINT 'Table AnomalyLogs already exists.';
END
GO

-- Indexes for AnomalyLogs
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AnomalyLogs]') AND name = N'IX_AnomalyLogs_DeviceKey_DetectedTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AnomalyLogs_DeviceKey_DetectedTime] 
    ON [dbo].[AnomalyLogs]([DeviceKey] ASC, [DetectedTime] ASC)
    ON [PRIMARY];
    PRINT 'Index IX_AnomalyLogs_DeviceKey_DetectedTime created.';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AnomalyLogs]') AND name = N'IX_AnomalyLogs_Acknowledged')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AnomalyLogs_Acknowledged] 
    ON [dbo].[AnomalyLogs]([Acknowledged] ASC)
    ON [PRIMARY];
    PRINT 'Index IX_AnomalyLogs_Acknowledged created.';
END
GO

-- =====================================================
-- Table: AppSettings
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AppSettings]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AppSettings](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [SettingKey] NVARCHAR(100) NOT NULL,
        [SettingValue] NVARCHAR(500) NOT NULL,
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_AppSettings_UpdatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_AppSettings] PRIMARY KEY CLUSTERED ([Id] ASC)
    ) ON [PRIMARY];
    
    PRINT 'Table AppSettings created.';
END
ELSE
BEGIN
    PRINT 'Table AppSettings already exists.';
END
GO

-- Unique index on SettingKey
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[AppSettings]') AND name = N'IX_AppSettings_SettingKey')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_AppSettings_SettingKey] 
    ON [dbo].[AppSettings]([SettingKey] ASC)
    ON [PRIMARY];
    PRINT 'Index IX_AppSettings_SettingKey created.';
END
GO

-- =====================================================
-- Table: HourlyEnergy (Aggregated data)
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[HourlyEnergy]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[HourlyEnergy](
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [DeviceKey] NVARCHAR(100) NOT NULL,
        [Hour] DATETIME2 NOT NULL,
        [EnergyKWh] DECIMAL(18,4) NOT NULL,
        [CalculatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_HourlyEnergy_CalculatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_HourlyEnergy] PRIMARY KEY CLUSTERED ([Id] ASC)
    ) ON [PRIMARY];
    
    PRINT 'Table HourlyEnergy created.';
END
ELSE
BEGIN
    PRINT 'Table HourlyEnergy already exists.';
END
GO

-- Unique composite index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[HourlyEnergy]') AND name = N'IX_HourlyEnergy_DeviceKey_Hour')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_HourlyEnergy_DeviceKey_Hour] 
    ON [dbo].[HourlyEnergy]([DeviceKey] ASC, [Hour] ASC)
    ON [PRIMARY];
    PRINT 'Index IX_HourlyEnergy_DeviceKey_Hour created.';
END
GO

-- =====================================================
-- Table: DailyEnergy (Aggregated data)
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DailyEnergy]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[DailyEnergy](
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [DeviceKey] NVARCHAR(100) NOT NULL,
        [Date] DATE NOT NULL,
        [EnergyKWh] DECIMAL(18,4) NOT NULL,
        [CalculatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_DailyEnergy_CalculatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_DailyEnergy] PRIMARY KEY CLUSTERED ([Id] ASC)
    ) ON [PRIMARY];
    
    PRINT 'Table DailyEnergy created.';
END
ELSE
BEGIN
    PRINT 'Table DailyEnergy already exists.';
END
GO

-- Unique composite index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[DailyEnergy]') AND name = N'IX_DailyEnergy_DeviceKey_Date')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_DailyEnergy_DeviceKey_Date] 
    ON [dbo].[DailyEnergy]([DeviceKey] ASC, [Date] ASC)
    ON [PRIMARY];
    PRINT 'Index IX_DailyEnergy_DeviceKey_Date created.';
END
GO

-- =====================================================
-- Table: MonthlyEnergy (Aggregated data)
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MonthlyEnergy]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[MonthlyEnergy](
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [DeviceKey] NVARCHAR(100) NOT NULL,
        [Year] INT NOT NULL,
        [Month] INT NOT NULL,
        [EnergyKWh] DECIMAL(18,4) NOT NULL,
        [CalculatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_MonthlyEnergy_CalculatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_MonthlyEnergy] PRIMARY KEY CLUSTERED ([Id] ASC)
    ) ON [PRIMARY];
    
    PRINT 'Table MonthlyEnergy created.';
END
ELSE
BEGIN
    PRINT 'Table MonthlyEnergy already exists.';
END
GO

-- Unique composite index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[MonthlyEnergy]') AND name = N'IX_MonthlyEnergy_DeviceKey_Year_Month')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_MonthlyEnergy_DeviceKey_Year_Month] 
    ON [dbo].[MonthlyEnergy]([DeviceKey] ASC, [Year] ASC, [Month] ASC)
    ON [PRIMARY];
    PRINT 'Index IX_MonthlyEnergy_DeviceKey_Year_Month created.';
END
GO

-- =====================================================
-- Table: YearlyEnergy (Aggregated data)
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[YearlyEnergy]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[YearlyEnergy](
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [DeviceKey] NVARCHAR(100) NOT NULL,
        [Year] INT NOT NULL,
        [EnergyKWh] DECIMAL(18,4) NOT NULL,
        [CalculatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_YearlyEnergy_CalculatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_YearlyEnergy] PRIMARY KEY CLUSTERED ([Id] ASC)
    ) ON [PRIMARY];
    
    PRINT 'Table YearlyEnergy created.';
END
ELSE
BEGIN
    PRINT 'Table YearlyEnergy already exists.';
END
GO

-- Unique composite index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[YearlyEnergy]') AND name = N'IX_YearlyEnergy_DeviceKey_Year')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_YearlyEnergy_DeviceKey_Year] 
    ON [dbo].[YearlyEnergy]([DeviceKey] ASC, [Year] ASC)
    ON [PRIMARY];
    PRINT 'Index IX_YearlyEnergy_DeviceKey_Year created.';
END
GO

-- =====================================================
-- Seed Default Data
-- =====================================================

-- Insert default app settings
IF NOT EXISTS (SELECT 1 FROM [AppSettings] WHERE [SettingKey] = 'MaxCapacity')
BEGIN
    INSERT INTO [dbo].[AppSettings] ([SettingKey], [SettingValue], [UpdatedAt]) VALUES
    ('MaxCapacity', '100000', GETDATE());
    PRINT 'Seeded default setting: MaxCapacity';
END

IF NOT EXISTS (SELECT 1 FROM [AppSettings] WHERE [SettingKey] = 'NotificationEnabled')
BEGIN
    INSERT INTO [dbo].[AppSettings] ([SettingKey], [SettingValue], [UpdatedAt]) VALUES
    ('NotificationEnabled', 'false', GETDATE());
    PRINT 'Seeded default setting: NotificationEnabled';
END

IF NOT EXISTS (SELECT 1 FROM [AppSettings] WHERE [SettingKey] = 'CheckIntervalSeconds')
BEGIN
    INSERT INTO [dbo].[AppSettings] ([SettingKey], [SettingValue], [UpdatedAt]) VALUES
    ('CheckIntervalSeconds', '30', GETDATE());
    PRINT 'Seeded default setting: CheckIntervalSeconds';
END

GO

-- =====================================================
-- Verify Setup
-- =====================================================
PRINT '======================================================';
PRINT 'Database setup complete!';
PRINT '======================================================';
PRINT '';
PRINT 'Tables created:';
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = 'dbo'
ORDER BY TABLE_NAME;
PRINT '';
PRINT 'Default settings:';
SELECT SettingKey, SettingValue FROM AppSettings ORDER BY SettingKey;
GO
