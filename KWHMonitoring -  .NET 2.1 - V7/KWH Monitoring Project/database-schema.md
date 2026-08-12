---
name: Database Schema
description: Complete database schema documentation
type: project
---

# KWH Monitoring Project - Database Schema

## Database Schema

### Core Tables

#### KWHData (Raw Data Table)
Stores timestamped energy consumption measurements from monitoring devices.

**Columns:**
- `Id` (bigint, Primary Key): Unique identifier for each record
- `DeviceKey` (nvarchar(50)): Identifier for the monitoring device
- `DeviceId` (nvarchar(50)): Human-readable device identifier
- `GroupName` (nvarchar(100)): Group assignment for devices
- `Waktu_Device` (datetime2): Timestamp from the device
- `Waktu_Server` (datetime2): Timestamp when data was received by server
- `Volt_R` (decimal(18,4)): Voltage measurement for phase R
- `Volt_S` (decimal(18,4), nullable): Voltage measurement for phase S
- `Volt_T` (decimal(18,4), nullable): Voltage measurement for phase T
- `Amp_R` (decimal(18,4)): Current measurement for phase R
- `Amp_S` (decimal(18,4), nullable): Current measurement for phase S
- `Amp_T` (decimal(18,4), nullable): Current measurement for phase T
- `Cos_Phi` (decimal(18,4)): Power factor
- `Daya_Watt` (decimal(18,4)): Power consumption in watts
- `TotalW1M_Wh` (decimal(18,4)): Total energy in watt-hours (W1M)
- `Energi_Aktif_Wh` (decimal(18,4)): Active energy in watt-hours
- `Total_Energy_Wh` (decimal(18,4)): Total energy in watt-hours
- `Frekuensi_Hz` (decimal(18,4)): Frequency in hertz

#### HourlyEnergy (Aggregated Data)
Stores hourly energy consumption data.

**Columns:**
- `Id` (bigint, Primary Key): Unique identifier
- `DeviceKey` (nvarchar(50)): Device identifier
- `Hour` (datetime2): Hour of aggregation
- `EnergyKWh` (decimal(18,4)): Energy consumption in kilowatt-hours
- `CalculatedAt` (datetime2): When the aggregation was calculated

#### DailyEnergy (Aggregated Data)
Stores daily energy consumption data.

**Columns:**
- `Id` (bigint, Primary Key): Unique identifier
- `DeviceKey` (nvarchar(50)): Device identifier
- `Date` (date): Date of aggregation
- `EnergyKWh` (decimal(18,4)): Energy consumption in kilowatt-hours
- `CalculatedAt` (datetime2): When the aggregation was calculated

#### MonthlyEnergy (Aggregated Data)
Stores monthly energy consumption data.

**Columns:**
- `Id` (bigint, Primary Key): Unique identifier
- `DeviceKey` (nvarchar(50)): Device identifier
- `Year` (int): Year of aggregation
- `Month` (int): Month of aggregation
- `EnergyKWh` (decimal(18,4)): Energy consumption in kilowatt-hours
- `CalculatedAt` (datetime2): When the aggregation was calculated

#### YearlyEnergy (Aggregated Data)
Stores yearly energy consumption data.

**Columns:**
- `Id` (bigint, Primary Key): Unique identifier
- `DeviceKey` (nvarchar(50)): Device identifier
- `Year` (int): Year of aggregation
- `EnergyKWh` (decimal(18,4)): Energy consumption in kilowatt-hours
- `CalculatedAt` (datetime2): When the aggregation was calculated

#### AppSettingsRecords (Configuration)
Stores application configuration settings.

**Columns:**
- `Id` (bigint, Primary Key): Unique identifier
- `SettingKey` (nvarchar(100)): Name of the setting
- `SettingValue` (nvarchar(max)): Value of the setting
- `Description` (nvarchar(500)): Description of the setting

### Relationships and Constraints

1. **Primary Keys**: All tables have unique primary keys
2. **Foreign Keys**: Aggregated tables reference `KWHData` through `DeviceKey`
3. **Indexes**:
   - Primary keys automatically indexed
   - DeviceKey and timestamp combinations for efficient queries
   - Composite indexes on time-based aggregations

### Data Types and Constraints

- **Numeric Fields**: Decimal with appropriate precision for electrical measurements
- **String Fields**: NVARCHAR with appropriate lengths
- **DateTime Fields**: DATETIME2 for high precision timestamps
- **Nullable Fields**: Properly marked for optional measurements (three-phase voltages/currents)