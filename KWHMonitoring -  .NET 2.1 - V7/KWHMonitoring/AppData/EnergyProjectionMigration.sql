IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE TABLE [AnomalyLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [DeviceKey] nvarchar(50) NOT NULL,
        [DeviceId] nvarchar(50) NULL,
        [AnomalyType] nvarchar(20) NOT NULL,
        [PowerValue] decimal(18,2) NOT NULL,
        [ThresholdValue] decimal(18,2) NOT NULL,
        [Deviation] decimal(5,2) NOT NULL,
        [DetectedTime] datetime2 NOT NULL,
        [EMAValue] decimal(18,2) NULL,
        [ThresholdMode] nvarchar(20) NULL,
        [Acknowledged] bit NULL,
        [AcknowledgedTime] datetime2 NULL,
        [Notes] nvarchar(500) NULL,
        CONSTRAINT [PK_AnomalyLogs] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE TABLE [AppLog] (
        [Id] bigint NOT NULL IDENTITY,
        [LogLevel] varchar(20) NOT NULL,
        [Message] nvarchar(max) NOT NULL,
        [Topic] varchar(200) NULL,
        [DeviceKey] varchar(20) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AppLog] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE TABLE [AppSettings] (
        [Id] int NOT NULL IDENTITY,
        [SettingKey] nvarchar(100) NOT NULL,
        [SettingValue] nvarchar(500) NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_AppSettings] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE TABLE [ColumnMapping] (
        [Id] int NOT NULL IDENTITY,
        [OldColumnName] varchar(50) NOT NULL,
        [NewColumnName] varchar(50) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ColumnMapping] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE TABLE [ColumnScaleConfig] (
        [ColumnName] varchar(50) NOT NULL,
        [ScaleFactor] decimal(18,5) NOT NULL,
        [RegisterAddress] varchar(10) NULL,
        [DataType] varchar(20) NOT NULL,
        [Unit] varchar(50) NULL,
        [Category] varchar(50) NULL,
        [Description] varchar(500) NULL,
        [IsDynamic] bit NOT NULL,
        [LastUpdated] datetime2 NOT NULL,
        CONSTRAINT [PK_ColumnScaleConfig] PRIMARY KEY ([ColumnName])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE TABLE [DailyEnergy] (
        [Id] bigint NOT NULL IDENTITY,
        [DeviceKey] nvarchar(100) NOT NULL,
        [Date] date NOT NULL,
        [EnergyKWh] decimal(18,4) NOT NULL,
        [CalculatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DailyEnergy] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE TABLE [DeviceRegistry] (
        [Id] int NOT NULL IDENTITY,
        [DeviceKey] varchar(20) NOT NULL,
        [DeviceId] varchar(50) NOT NULL,
        [GroupName] varchar(100) NULL,
        [Location] varchar(200) NULL,
        [FirstSeen] datetime2 NOT NULL,
        [LastSeen] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [MessageCount] bigint NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DeviceRegistry] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_DeviceRegistry_DeviceKey] UNIQUE ([DeviceKey])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE TABLE [FailedMessages] (
        [Id] bigint NOT NULL IDENTITY,
        [Topic] varchar(500) NOT NULL,
        [Payload] nvarchar(max) NOT NULL,
        [Reason] nvarchar(500) NULL,
        [RetryCount] int NOT NULL,
        [IsResolved] bit NOT NULL,
        [ReceivedAt] datetime2 NOT NULL,
        [ResolvedAt] datetime2 NULL,
        CONSTRAINT [PK_FailedMessages] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE TABLE [HourlyEnergy] (
        [Id] bigint NOT NULL IDENTITY,
        [DeviceKey] nvarchar(100) NOT NULL,
        [Hour] datetime2 NOT NULL,
        [EnergyKWh] decimal(18,4) NOT NULL,
        [CalculatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_HourlyEnergy] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE TABLE [KWHData_History] (
        [HistoryId] bigint NOT NULL IDENTITY,
        [OriginalId] bigint NOT NULL,
        [DeviceKey] varchar(20) NOT NULL,
        [TerminalTime] datetime2 NULL,
        [ReceivedTime] datetime2 NOT NULL,
        [GroupName] nvarchar(100) NULL,
        [DeviceId] nvarchar(50) NULL,
        [PHASE_R] decimal(18,2) NULL,
        [PHASE_S] decimal(18,2) NULL,
        [PHASE_T] decimal(18,2) NULL,
        [AMPERE_R] decimal(18,3) NULL,
        [AMPERE_S] decimal(18,3) NULL,
        [AMPERE_T] decimal(18,3) NULL,
        [W] decimal(18,1) NULL,
        [CosPhi] decimal(18,3) NULL,
        [F] decimal(18,2) NULL,
        [Aktif_Power] decimal(18,2) NULL,
        [TotalW] decimal(18,2) NULL,
        [TotalW1M] decimal(18,2) NULL,
        [ArchivedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_KWHData_History] PRIMARY KEY ([HistoryId])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE TABLE [MonthlyEnergy] (
        [Id] bigint NOT NULL IDENTITY,
        [DeviceKey] nvarchar(100) NOT NULL,
        [Year] int NOT NULL,
        [Month] int NOT NULL,
        [EnergyKWh] decimal(18,4) NOT NULL,
        [CalculatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MonthlyEnergy] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE TABLE [YearlyEnergy] (
        [Id] bigint NOT NULL IDENTITY,
        [DeviceKey] nvarchar(100) NOT NULL,
        [Year] int NOT NULL,
        [EnergyKWh] decimal(18,4) NOT NULL,
        [CalculatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_YearlyEnergy] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE TABLE [KWHData] (
        [Id] bigint NOT NULL IDENTITY,
        [DeviceKey] varchar(20) NOT NULL,
        [TerminalTime] datetime2 NULL,
        [ReceivedTime] datetime2 NOT NULL,
        [GroupName] nvarchar(100) NULL,
        [DeviceId] nvarchar(50) NULL,
        [PHASE_R] decimal(18,2) NULL,
        [PHASE_S] decimal(18,2) NULL,
        [PHASE_T] decimal(18,2) NULL,
        [AMPERE_R] decimal(18,3) NULL,
        [AMPERE_S] decimal(18,3) NULL,
        [AMPERE_T] decimal(18,3) NULL,
        [W] decimal(18,1) NULL,
        [CosPhi] decimal(18,3) NULL,
        [F] decimal(18,2) NULL,
        [Aktif_Power] decimal(18,2) NULL,
        [TotalW] decimal(18,2) NULL,
        [TotalW1M] decimal(18,2) NULL,
        CONSTRAINT [PK_KWHData] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_KWHData_DeviceRegistry] FOREIGN KEY ([DeviceKey]) REFERENCES [DeviceRegistry] ([DeviceKey]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE INDEX [IX_AnomalyLogs_DetectedTime] ON [AnomalyLogs] ([DetectedTime]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE INDEX [IX_AnomalyLogs_DeviceKey] ON [AnomalyLogs] ([DeviceKey]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE INDEX [IX_AppLog_CreatedAt] ON [AppLog] ([CreatedAt]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE INDEX [IX_AppLog_Level] ON [AppLog] ([LogLevel]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE UNIQUE INDEX [IX_AppSettings_SettingKey] ON [AppSettings] ([SettingKey]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE UNIQUE INDEX [IX_ColumnMapping_OldName] ON [ColumnMapping] ([OldColumnName]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE INDEX [IX_DeviceRegistry_DeviceId] ON [DeviceRegistry] ([DeviceId]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE UNIQUE INDEX [IX_DeviceRegistry_DeviceKey] ON [DeviceRegistry] ([DeviceKey]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE INDEX [IX_FailedMessages_IsResolved] ON [FailedMessages] ([IsResolved]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE INDEX [IX_FailedMessages_ReceivedAt] ON [FailedMessages] ([ReceivedAt]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE INDEX [IX_KWHData_DeviceKey] ON [KWHData] ([DeviceKey]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE INDEX [IX_KWHData_TerminalTime] ON [KWHData] ([TerminalTime]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE INDEX [IX_KWHData_ReceivedTime] ON [KWHData] ([ReceivedTime]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE INDEX [IX_KWHData_DeviceKey_TerminalTime] ON [KWHData] ([DeviceKey], [ReceivedTime]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE INDEX [IX_KWHData_History_ArchivedAt] ON [KWHData_History] ([ArchivedAt]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    CREATE INDEX [IX_KWHData_History_DeviceKey] ON [KWHData_History] ([DeviceKey]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260826064232_initialMigration')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260826064232_initialMigration', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916092238_InitialCreate')
BEGIN
    CREATE TABLE [ApplicationUsers] (
        [Id] int NOT NULL IDENTITY,
        [Email] nvarchar(256) NOT NULL,
        [NormalizedEmail] nvarchar(256) NOT NULL,
        [DisplayName] nvarchar(256) NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [Role] nvarchar(50) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [EmailConfirmed] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        [LockoutEnd] datetime2 NULL,
        CONSTRAINT [PK_ApplicationUsers] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916092238_InitialCreate')
BEGIN
    CREATE TABLE [RelayControl] (
        [Id] int NOT NULL IDENTITY,
        [DeviceKey] nvarchar(100) NULL,
        [RCI] nvarchar(10) NULL,
        [DeviceId] nvarchar(50) NULL,
        [GroupName] nvarchar(100) NULL,
        [ReceivedTime] datetime2 NOT NULL,
        CONSTRAINT [PK_RelayControl] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916092238_InitialCreate')
BEGIN
    CREATE TABLE [SecurityAuditLogs] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [Email] nvarchar(256) NULL,
        [Action] int NOT NULL,
        [TargetDevice] nvarchar(100) NULL,
        [IpAddress] nvarchar(50) NULL,
        [UserAgent] nvarchar(500) NULL,
        [Details] nvarchar(500) NULL,
        [Success] bit NOT NULL,
        [Timestamp] datetime2 NOT NULL,
        CONSTRAINT [PK_SecurityAuditLogs] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916092238_InitialCreate')
BEGIN
    CREATE TABLE [EmailVerificationTokens] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [TokenHash] nvarchar(128) NOT NULL,
        [Purpose] int NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [IsUsed] bit NOT NULL,
        [UsedAt] datetime2 NULL,
        [RequestedRole] nvarchar(50) NULL,
        CONSTRAINT [PK_EmailVerificationTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmailVerificationTokens_ApplicationUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [ApplicationUsers] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916092238_InitialCreate')
BEGIN
    CREATE UNIQUE INDEX [IX_ApplicationUsers_NormalizedEmail] ON [ApplicationUsers] ([NormalizedEmail]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916092238_InitialCreate')
BEGIN
    CREATE INDEX [IX_EmailVerificationTokens_ExpiresAt] ON [EmailVerificationTokens] ([ExpiresAt]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916092238_InitialCreate')
BEGIN
    CREATE INDEX [IX_EmailVerificationTokens_UserId] ON [EmailVerificationTokens] ([UserId]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916092238_InitialCreate')
BEGIN
    CREATE INDEX [IX_EmailVerificationTokens_Token_Purpose] ON [EmailVerificationTokens] ([TokenHash], [Purpose]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916092238_InitialCreate')
BEGIN
    CREATE INDEX [IX_SecurityAuditLogs_Action] ON [SecurityAuditLogs] ([Action]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916092238_InitialCreate')
BEGIN
    CREATE INDEX [IX_SecurityAuditLogs_Timestamp] ON [SecurityAuditLogs] ([Timestamp]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916092238_InitialCreate')
BEGIN
    CREATE INDEX [IX_SecurityAuditLogs_UserId] ON [SecurityAuditLogs] ([UserId]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916092238_InitialCreate')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260916092238_InitialCreate', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916095322_AnomalyCenterPhase1')
BEGIN
    ALTER TABLE [AnomalyLogs] ADD [AcknowledgedBy] nvarchar(256) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916095322_AnomalyCenterPhase1')
BEGIN
    ALTER TABLE [AnomalyLogs] ADD [IsResolved] bit NOT NULL DEFAULT 0;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916095322_AnomalyCenterPhase1')
BEGIN
    ALTER TABLE [AnomalyLogs] ADD [OperatorAction] nvarchar(100) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916095322_AnomalyCenterPhase1')
BEGIN
    ALTER TABLE [AnomalyLogs] ADD [OperatorNotes] nvarchar(1000) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916095322_AnomalyCenterPhase1')
BEGIN
    ALTER TABLE [AnomalyLogs] ADD [RecommendedAction] nvarchar(1000) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916095322_AnomalyCenterPhase1')
BEGIN
    ALTER TABLE [AnomalyLogs] ADD [ResolvedBy] nvarchar(256) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916095322_AnomalyCenterPhase1')
BEGIN
    ALTER TABLE [AnomalyLogs] ADD [ResolvedTime] datetime2 NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916095322_AnomalyCenterPhase1')
BEGIN
    ALTER TABLE [AnomalyLogs] ADD [RootCause] nvarchar(500) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916095322_AnomalyCenterPhase1')
BEGIN
    ALTER TABLE [AnomalyLogs] ADD [Severity] nvarchar(20) NULL DEFAULT N'medium';
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916095322_AnomalyCenterPhase1')
BEGIN
    CREATE TABLE [AnomalyChartSnapshots] (
        [Id] bigint NOT NULL IDENTITY,
        [AnomalyLogId] bigint NOT NULL,
        [DetectedTime] datetime2 NOT NULL,
        [BeforeDataJson] nvarchar(max) NULL,
        [AfterDataJson] nvarchar(max) NULL,
        [UpperThreshold] decimal(18,2) NOT NULL,
        [LowerThreshold] decimal(18,2) NOT NULL,
        [EMAValue] decimal(18,2) NULL,
        [SnapshotStatus] nvarchar(20) NULL DEFAULT N'before',
        [CreatedAt] datetime2 NOT NULL DEFAULT (getdate()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_AnomalyChartSnapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AnomalyChartSnapshots_AnomalyLogs_AnomalyLogId] FOREIGN KEY ([AnomalyLogId]) REFERENCES [AnomalyLogs] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916095322_AnomalyCenterPhase1')
BEGIN
    CREATE TABLE [AnomalyMonthlyReports] (
        [Id] bigint NOT NULL IDENTITY,
        [Year] int NOT NULL,
        [Month] int NOT NULL,
        [TotalAnomalies] int NOT NULL,
        [OverloadCount] int NOT NULL,
        [DropCount] int NOT NULL,
        [AffectedDevices] int NOT NULL,
        [AverageDeviation] decimal(5,2) NOT NULL,
        [TopAffectedDevice] nvarchar(50) NULL,
        [SummaryText] nvarchar(2000) NULL,
        [Recommendations] nvarchar(2000) NULL,
        [GeneratedBy] nvarchar(256) NULL,
        [GeneratedAt] datetime2 NOT NULL DEFAULT (getdate()),
        CONSTRAINT [PK_AnomalyMonthlyReports] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916095322_AnomalyCenterPhase1')
BEGIN
    CREATE INDEX [IX_AnomalyLogs_IsResolved] ON [AnomalyLogs] ([IsResolved]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916095322_AnomalyCenterPhase1')
BEGIN
    CREATE INDEX [IX_AnomalyLogs_Severity] ON [AnomalyLogs] ([Severity]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916095322_AnomalyCenterPhase1')
BEGIN
    CREATE UNIQUE INDEX [IX_AnomalyChartSnapshots_AnomalyLogId] ON [AnomalyChartSnapshots] ([AnomalyLogId]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916095322_AnomalyCenterPhase1')
BEGIN
    CREATE INDEX [IX_AnomalyMonthlyReports_Year_Month] ON [AnomalyMonthlyReports] ([Year], [Month]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916095322_AnomalyCenterPhase1')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260916095322_AnomalyCenterPhase1', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260916100423_LastTabel')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260916100423_LastTabel', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260921120000_AddDeviceSettings')
BEGIN
    CREATE TABLE [DeviceSettings] (
        [Id] int NOT NULL IDENTITY,
        [DeviceKey] varchar(20) NOT NULL,
        [MaxCapacity] decimal(18,2) NOT NULL,
        [DeviceCategory] nvarchar(100) NULL,
        [DowntimeEnabled] bit NOT NULL,
        [DowntimeStart] time NOT NULL,
        [DowntimeEnd] time NOT NULL,
        [TariffPerKWh] decimal(18,2) NOT NULL,
        [LoadNormalThreshold] int NOT NULL,
        [LoadMediumThreshold] int NOT NULL,
        [EmaUpperThreshold] int NOT NULL,
        [EmaLowerThreshold] int NOT NULL,
        [EmaFibUpper] float NOT NULL,
        [EmaFibLower] float NOT NULL,
        [ControlMode] varchar(50) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DeviceSettings] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260921120000_AddDeviceSettings')
BEGIN
    CREATE UNIQUE INDEX [IX_DeviceSettings_DeviceKey] ON [DeviceSettings] ([DeviceKey]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260921120000_AddDeviceSettings')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260921120000_AddDeviceSettings', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260924085035_AddIsMasterAdmin')
BEGIN

                    IF OBJECT_ID(N'[dbo].[RelayControl]', N'U') IS NULL
                    BEGIN
                        CREATE TABLE [dbo].[RelayControl] (
                            [Id] bigint IDENTITY(1,1) NOT NULL,
                            [DeviceKey] nvarchar(100) NULL,
                            [RCI] int NULL,
                            [DeviceId] nvarchar(50) NULL,
                            [GroupName] nvarchar(100) NULL,
                            [ReceivedTime] datetime2 NOT NULL,
                            CONSTRAINT [PK_RelayControl] PRIMARY KEY CLUSTERED ([Id] ASC)
                        );
                    END
                
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260924085035_AddIsMasterAdmin')
BEGIN
    ALTER TABLE [ApplicationUsers] ADD [IsMasterAdmin] bit NOT NULL DEFAULT 0;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260924085035_AddIsMasterAdmin')
BEGIN
    CREATE INDEX [IX_ApplicationUsers_IsMasterAdmin] ON [ApplicationUsers] ([IsMasterAdmin]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260924085035_AddIsMasterAdmin')
BEGIN

                    UPDATE AU
                    SET AU.IsMasterAdmin = 1
                    FROM ApplicationUsers AU
                    INNER JOIN AppSettings AR ON AR.SettingKey = 'Notification.MasterAdminEmail'
                    WHERE AU.Email = AR.SettingValue
                      AND AU.IsActive = 1;

                    UPDATE ApplicationUsers
                    SET IsMasterAdmin = 1
                    WHERE Id = (
                        SELECT MIN(Id) FROM ApplicationUsers
                        WHERE Role = 'Admin' AND IsActive = 1
                          AND NOT EXISTS (
                              SELECT 1 FROM ApplicationUsers WHERE IsMasterAdmin = 1
                          )
                    );
                
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260924085035_AddIsMasterAdmin')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260924085035_AddIsMasterAdmin', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260925032156_changeDeviceKeyType')
BEGIN

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RelayControl_DeviceKey' AND object_id = OBJECT_ID('RelayControl'))
                    BEGIN
                        DROP INDEX [IX_RelayControl_DeviceKey] ON [RelayControl];
                    END
                
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260925032156_changeDeviceKeyType')
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RelayControl]') AND [c].[name] = N'DeviceKey');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [RelayControl] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [RelayControl] ALTER COLUMN [DeviceKey] varchar(20) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260925032156_changeDeviceKeyType')
BEGIN

                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RelayControl_DeviceKey' AND object_id = OBJECT_ID('RelayControl'))
                    BEGIN
                        CREATE NONCLUSTERED INDEX [IX_RelayControl_DeviceKey] ON [RelayControl]([DeviceKey] ASC);
                    END
                
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260925032156_changeDeviceKeyType')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260925032156_changeDeviceKeyType', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260925033136_addRCColumns')
BEGIN
    ALTER TABLE [RelayControl] ADD [RC] int NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260925033136_addRCColumns')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260925033136_addRCColumns', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260925033343_addingNewColumn')
BEGIN
    ALTER TABLE [RelayControl] ADD [TerminalTime] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260925033343_addingNewColumn')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260925033343_addingNewColumn', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260928091102_AddFinancialAndRevenueFieldsToDeviceSettings')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [BudgetKWh] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260928091102_AddFinancialAndRevenueFieldsToDeviceSettings')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [RevenuePerHour] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260928091102_AddFinancialAndRevenueFieldsToDeviceSettings')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [SurfaceArea] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260928091102_AddFinancialAndRevenueFieldsToDeviceSettings')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [TariffLWBP] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260928091102_AddFinancialAndRevenueFieldsToDeviceSettings')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [TariffWBP] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260928091102_AddFinancialAndRevenueFieldsToDeviceSettings')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [WbpEndHour] int NOT NULL DEFAULT 22;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260928091102_AddFinancialAndRevenueFieldsToDeviceSettings')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [WbpStartHour] int NOT NULL DEFAULT 18;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260928091102_AddFinancialAndRevenueFieldsToDeviceSettings')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260928091102_AddFinancialAndRevenueFieldsToDeviceSettings', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261008092719_AddInvertRelayCommandToDeviceSettings')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [InvertRelayCommand] bit NOT NULL DEFAULT 0;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261008092719_AddInvertRelayCommandToDeviceSettings')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008092719_AddInvertRelayCommandToDeviceSettings', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261012014819_20261009081305_AddBillingAnomalyFields')
BEGIN
    ALTER TABLE [AnomalyLogs] ADD [BillingAnomalyType] nvarchar(30) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261012014819_20261009081305_AddBillingAnomalyFields')
BEGIN
    ALTER TABLE [AnomalyLogs] ADD [PreviousBilling] decimal(18,2) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261012014819_20261009081305_AddBillingAnomalyFields')
BEGIN
    ALTER TABLE [AnomalyLogs] ADD [PreviousBillingPeriod] date NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261012014819_20261009081305_AddBillingAnomalyFields')
BEGIN
    ALTER TABLE [AnomalyLogs] ADD [PreviousErpBillingId] bigint NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261012014819_20261009081305_AddBillingAnomalyFields')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012014819_20261009081305_AddBillingAnomalyFields', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261012085233_AddBillingCutoffToDeviceSettings')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [BillingCutoffDay] int NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261012085233_AddBillingCutoffToDeviceSettings')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [BillingCutoffEnabled] bit NOT NULL DEFAULT 0;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261012085233_AddBillingCutoffToDeviceSettings')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012085233_AddBillingCutoffToDeviceSettings', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [BillingAnomalyThresholdMode] varchar(20) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [BillingAnomalyThresholdPercent] decimal(9,4) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [BillingTrendToleranceAmount] decimal(18,2) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [BillingTrendToleranceMode] varchar(20) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [BillingTrendTolerancePercent] decimal(9,4) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [MonthlyBillingBudgetAmount] decimal(18,2) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [MonthlyBillingBudgetBaselineVersion] int NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [MonthlyBillingBudgetManualOverride] bit NOT NULL DEFAULT 0;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [MonthlyBillingBudgetSource] varchar(20) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [MonthlyBillingBudgetTrendProfileId] bigint NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    ALTER TABLE [DeviceSettings] ADD [MonthlyBillingBudgetUpdatedAt] datetime2 NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    CREATE TABLE [BillingBudgetHistories] (
        [Id] bigint NOT NULL IDENTITY,
        [DeviceKey] varchar(20) NOT NULL,
        [PreviousAmount] decimal(18,2) NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Source] varchar(20) NOT NULL,
        [TrendProfileId] bigint NULL,
        [BaselineVersion] int NULL,
        [IdempotencyKey] varchar(100) NOT NULL,
        [ChangedBy] nvarchar(256) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_BillingBudgetHistories] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    CREATE TABLE [BillingConfirmationTokens] (
        [Id] bigint NOT NULL IDENTITY,
        [TokenHash] nvarchar(128) NOT NULL,
        [Purpose] varchar(30) NOT NULL,
        [TrendProfileId] bigint NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UsedAt] datetime2 NULL,
        [RevokedAt] datetime2 NULL,
        [UsedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_BillingConfirmationTokens] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    CREATE TABLE [BillingNotificationOutbox] (
        [Id] bigint NOT NULL IDENTITY,
        [EventKey] varchar(100) NOT NULL,
        [Channel] varchar(30) NOT NULL,
        [NotificationType] varchar(30) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [Status] varchar(20) NOT NULL,
        [AttemptCount] int NOT NULL,
        [ClaimedAt] datetime2 NULL,
        [SentAt] datetime2 NULL,
        [NextAttemptAt] datetime2 NULL,
        [LastError] nvarchar(1000) NULL,
        [ProviderMessageId] nvarchar(200) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_BillingNotificationOutbox] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    CREATE TABLE [BillingTrendConfirmations] (
        [Id] bigint NOT NULL IDENTITY,
        [TrendProfileId] bigint NOT NULL,
        [AnomalyLogId] bigint NULL,
        [DeviceKey] varchar(20) NOT NULL,
        [BillingPeriod] date NOT NULL,
        [ObservedAmount] decimal(18,2) NOT NULL,
        [Decision] varchar(20) NOT NULL,
        [Reason] nvarchar(1000) NULL,
        [ConfirmedBy] nvarchar(256) NULL,
        [Channel] varchar(30) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_BillingTrendConfirmations] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    CREATE TABLE [BillingTrendProfiles] (
        [Id] bigint NOT NULL IDENTITY,
        [DeviceKey] varchar(20) NOT NULL,
        [Status] varchar(20) NOT NULL,
        [AnchorBillingPeriod] date NOT NULL,
        [AnchorAmount] decimal(18,2) NOT NULL,
        [ActiveBaselineAmount] decimal(18,2) NULL,
        [BaselineVersion] int NOT NULL,
        [ValidConfirmationCount] int NOT NULL,
        [AcceptedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_BillingTrendProfiles] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    CREATE UNIQUE INDEX [IX_BillingBudgetHistories_IdempotencyKey] ON [BillingBudgetHistories] ([IdempotencyKey]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    CREATE UNIQUE INDEX [IX_BillingConfirmationTokens_Hash_Purpose] ON [BillingConfirmationTokens] ([TokenHash], [Purpose]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    CREATE UNIQUE INDEX [IX_BillingOutbox_Event_Channel_Type] ON [BillingNotificationOutbox] ([EventKey], [Channel], [NotificationType]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    CREATE UNIQUE INDEX [IX_BillingTrendConfirmations_Device_Period] ON [BillingTrendConfirmations] ([DeviceKey], [BillingPeriod]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    CREATE UNIQUE INDEX [IX_BillingTrendProfiles_Device_Status] ON [BillingTrendProfiles] ([DeviceKey], [Status]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015044248_AddBillingBudgetAndTrendWorkflow')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015044248_AddBillingBudgetAndTrendWorkflow', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015062143_AddBillingConfirmationRecipientsAndNotes')
BEGIN
    ALTER TABLE [BillingConfirmationTokens] ADD [AnomalyLogId] bigint NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015062143_AddBillingConfirmationRecipientsAndNotes')
BEGIN
    ALTER TABLE [BillingConfirmationTokens] ADD [UserId] int NOT NULL DEFAULT 0;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015062143_AddBillingConfirmationRecipientsAndNotes')
BEGIN
    ALTER TABLE [ApplicationUsers] ADD [WhatsAppNumber] nvarchar(30) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015062143_AddBillingConfirmationRecipientsAndNotes')
BEGIN
    CREATE TABLE [BillingAnomalyNotes] (
        [Id] bigint NOT NULL IDENTITY,
        [AnomalyLogId] bigint NOT NULL,
        [UserId] int NULL,
        [Author] nvarchar(256) NOT NULL,
        [NoteType] varchar(30) NOT NULL,
        [Note] nvarchar(2000) NOT NULL,
        [Decision] varchar(30) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_BillingAnomalyNotes] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015062143_AddBillingConfirmationRecipientsAndNotes')
BEGIN
    CREATE TABLE [BillingConfirmationRecipients] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [IsSelected] bit NOT NULL,
        [SelectedBy] nvarchar(256) NULL,
        [SelectedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_BillingConfirmationRecipients] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015062143_AddBillingConfirmationRecipientsAndNotes')
BEGIN
    CREATE INDEX [IX_BillingAnomalyNotes_Anomaly_Created] ON [BillingAnomalyNotes] ([AnomalyLogId], [CreatedAt]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015062143_AddBillingConfirmationRecipientsAndNotes')
BEGIN
    CREATE UNIQUE INDEX [IX_BillingConfirmationRecipients_UserId] ON [BillingConfirmationRecipients] ([UserId]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015062143_AddBillingConfirmationRecipientsAndNotes')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015062143_AddBillingConfirmationRecipientsAndNotes', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261015070424_HardenBillingPresetDefaults')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015070424_HardenBillingPresetDefaults', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261019041758_AddBillingAnomalyEventUniqueIndex')
BEGIN
    DROP INDEX [IX_AnomalyLogs_BillingDedup] ON [AnomalyLogs];
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261019041758_AddBillingAnomalyEventUniqueIndex')
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AnomalyLogs]') AND [c].[name] = N'BillingAnomalyType');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [AnomalyLogs] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [AnomalyLogs] ALTER COLUMN [BillingAnomalyType] nvarchar(100) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261019041758_AddBillingAnomalyEventUniqueIndex')
BEGIN
    CREATE UNIQUE INDEX [IX_AnomalyLogs_BillingDedup] ON [AnomalyLogs] ([DeviceKey], [BillingPeriod], [ErpBillingId], [AnomalyType]) WHERE [AnomalyType] = 'BILLING_VARIANCE' AND [BillingPeriod] IS NOT NULL AND [ErpBillingId] IS NOT NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261019041758_AddBillingAnomalyEventUniqueIndex')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019041758_AddBillingAnomalyEventUniqueIndex', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261019052000_AddBillingPeriodDifferenceFields')
BEGIN
    ALTER TABLE [AnomalyLogs] ADD [PeriodDifference] decimal(18,2) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261019052000_AddBillingPeriodDifferenceFields')
BEGIN
    ALTER TABLE [AnomalyLogs] ADD [PeriodDifferencePercent] decimal(18,4) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261019052000_AddBillingPeriodDifferenceFields')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019052000_AddBillingPeriodDifferenceFields', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261020091510_AddBillingEstimateSnapshots')
BEGIN
    CREATE TABLE [BillingEstimateSnapshots] (
        [Id] bigint NOT NULL IDENTITY,
        [ReportType] varchar(20) NOT NULL,
        [PeriodStart] datetime2 NOT NULL,
        [PeriodEnd] datetime2 NOT NULL,
        [DeviceKey] varchar(100) NOT NULL,
        [EnergyKWh] decimal(18,4) NOT NULL,
        [WbpKWh] decimal(18,4) NOT NULL,
        [LwbpKWh] decimal(18,4) NOT NULL,
        [FlatTariffPerKWh] decimal(18,2) NOT NULL,
        [TariffWBP] decimal(18,2) NOT NULL,
        [TariffLWBP] decimal(18,2) NOT NULL,
        [WbpCost] decimal(18,2) NOT NULL,
        [LwbpCost] decimal(18,2) NOT NULL,
        [EstimatedCost] decimal(18,2) NOT NULL,
        [TariffMode] varchar(20) NOT NULL,
        [DataStatus] varchar(30) NOT NULL,
        [CalculatedAt] datetime2 NOT NULL,
        [SentAt] datetime2 NULL,
        CONSTRAINT [PK_BillingEstimateSnapshots] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261020091510_AddBillingEstimateSnapshots')
BEGIN
    CREATE UNIQUE INDEX [IX_BillingEstimateSnapshots_Report_Period_Device] ON [BillingEstimateSnapshots] ([ReportType], [PeriodStart], [PeriodEnd], [DeviceKey]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261020091510_AddBillingEstimateSnapshots')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020091510_AddBillingEstimateSnapshots', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261020101727_AddBillingSnapshotLocationFields')
BEGIN
    ALTER TABLE [BillingEstimateSnapshots] ADD [GroupName] varchar(100) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261020101727_AddBillingSnapshotLocationFields')
BEGIN
    ALTER TABLE [BillingEstimateSnapshots] ADD [KodeLokasi] varchar(100) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261020101727_AddBillingSnapshotLocationFields')
BEGIN
    ALTER TABLE [BillingEstimateSnapshots] ADD [IDPelanggan] varchar(100) NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261020101727_AddBillingSnapshotLocationFields')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020101727_AddBillingSnapshotLocationFields', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261026025051_AddActiveAnomalyStatesAndCleanupLegacyState')
BEGIN
    CREATE TABLE [ActiveAnomalyStates] (
        [Id] bigint NOT NULL IDENTITY,
        [DeviceKey] nvarchar(50) NOT NULL,
        [AnomalyType] nvarchar(20) NOT NULL,
        [ConfirmationCount] int NOT NULL,
        [MaxConfirmations] int NOT NULL,
        [IsConfirmed] bit NOT NULL,
        [AnomalyLogId] bigint NULL,
        [FirstDetectedAt] datetime2 NOT NULL,
        [LastDetectedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ActiveAnomalyStates] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261026025051_AddActiveAnomalyStatesAndCleanupLegacyState')
BEGIN
    CREATE UNIQUE INDEX [IX_ActiveAnomalyStates_DeviceKey] ON [ActiveAnomalyStates] ([DeviceKey]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261026025051_AddActiveAnomalyStatesAndCleanupLegacyState')
BEGIN
    CREATE INDEX [IX_ActiveAnomalyStates_UpdatedAt] ON [ActiveAnomalyStates] ([UpdatedAt]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261026025051_AddActiveAnomalyStatesAndCleanupLegacyState')
BEGIN
    DELETE FROM [AppSettings] WHERE [SettingKey] LIKE 'AnomalyState.%';
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261026025051_AddActiveAnomalyStatesAndCleanupLegacyState')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261026025051_AddActiveAnomalyStatesAndCleanupLegacyState', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261106000000_AddLastSampleIdToActiveAnomalyStates')
BEGIN
    ALTER TABLE [ActiveAnomalyStates] ADD [LastSampleId] bigint NULL;
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261106000000_AddLastSampleIdToActiveAnomalyStates')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106000000_AddLastSampleIdToActiveAnomalyStates', N'2.1.14-servicing-32113');
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261109045629_AddEnergyProjectionTables')
BEGIN
    CREATE TABLE [EnergyProjectionGaps] (
        [Id] bigint NOT NULL IDENTITY,
        [DeviceKey] varchar(20) NOT NULL,
        [GapStartUtc] datetime2 NOT NULL,
        [GapEndUtc] datetime2 NULL,
        [Status] varchar(20) NOT NULL,
        [DetectionSource] varchar(40) NULL,
        [Reason] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ClosedAtUtc] datetime2 NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_EnergyProjectionGaps] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261109045629_AddEnergyProjectionTables')
BEGIN
    CREATE TABLE [EnergyProjectionValues] (
        [Id] bigint NOT NULL IDENTITY,
        [DeviceKey] varchar(20) NOT NULL,
        [BucketStartUtc] datetime2 NOT NULL,
        [ProjectedKWh] decimal(18,4) NOT NULL,
        [LowerBoundKWh] decimal(18,4) NULL,
        [UpperBoundKWh] decimal(18,4) NULL,
        [ConfidenceScore] decimal(9,4) NULL,
        [ConfidenceLevel] varchar(20) NULL,
        [Status] varchar(20) NOT NULL,
        [ProjectionMethod] varchar(80) NOT NULL,
        [SampleCount] int NOT NULL,
        [GapId] bigint NULL,
        [SourcePeriodStartUtc] datetime2 NULL,
        [SourcePeriodEndUtc] datetime2 NULL,
        [CalculationVersion] varchar(30) NOT NULL,
        [CalculatedAtUtc] datetime2 NOT NULL,
        [ReconciledAtUtc] datetime2 NULL,
        CONSTRAINT [PK_EnergyProjectionValues] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261109045629_AddEnergyProjectionTables')
BEGIN
    CREATE INDEX [IX_EnergyProjectionGaps_Status] ON [EnergyProjectionGaps] ([Status]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261109045629_AddEnergyProjectionTables')
BEGIN
    CREATE INDEX [IX_EnergyProjectionGaps_Open_Device_Start] ON [EnergyProjectionGaps] ([Status], [DeviceKey], [GapStartUtc]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261109045629_AddEnergyProjectionTables')
BEGIN
    CREATE INDEX [IX_EnergyProjectionGaps_Device_GapStart] ON [EnergyProjectionGaps] ([DeviceKey], [GapStartUtc]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261109045629_AddEnergyProjectionTables')
BEGIN
    CREATE INDEX [IX_EnergyProjectionValues_Gap_Status] ON [EnergyProjectionValues] ([GapId], [Status]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261109045629_AddEnergyProjectionTables')
BEGIN
    CREATE INDEX [IX_EnergyProjectionValues_Status_Bucket] ON [EnergyProjectionValues] ([Status], [BucketStartUtc]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261109045629_AddEnergyProjectionTables')
BEGIN
    CREATE UNIQUE INDEX [IX_EnergyProjectionValues_Device_Bucket_Version] ON [EnergyProjectionValues] ([DeviceKey], [BucketStartUtc], [CalculationVersion]);
END;

GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261109045629_AddEnergyProjectionTables')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109045629_AddEnergyProjectionTables', N'2.1.14-servicing-32113');
END;

GO

