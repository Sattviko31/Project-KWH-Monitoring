# KWH Monitoring Project - Comprehensive Memory Documentation

## Project Overview

The KWH Monitoring project is a comprehensive .NET 2.1 web application designed to monitor energy consumption data from electrical devices. This system provides real-time visibility into power usage patterns, historical analysis capabilities, and automated anomaly detection for efficient energy management in industrial or commercial environments.

### Purpose and Objectives
- Monitor real-time energy consumption of electrical devices
- Provide historical analysis and reporting capabilities
- Detect and alert on unusual power consumption patterns
- Enable data-driven decisions for energy management
- Support compliance with energy efficiency regulations

### Target Users
- Facility managers
- Energy analysts
- Maintenance personnel
- Operations supervisors
- IT administrators

## System Architecture

### Overall Architecture
The system follows a layered architecture pattern with clear separation of concerns:

#### Presentation Layer
- **Web Interface**: ASP.NET MVC with Razor Views
- **Responsive Design**: Bootstrap-based interface for desktop and mobile
- **Interactive Elements**: JavaScript-enhanced UI components
- **Charting**: Data visualization for historical trends

#### Application Layer
- **Controllers**: `MonitoringController`, `ApiController`, `HomeController`
- **Services**: Business logic implementations
- **ViewModels**: Data transfer objects for UI presentation
- **API Endpoints**: RESTful interfaces for external access

#### Domain Layer
- **Models**: Data entities representing business concepts
- **Business Logic**: Core processing algorithms
- **Validation**: Data integrity checks

#### Data Access Layer
- **Entity Framework Core**: ORM for database operations
- **ApplicationDbContext**: Database context for all entities
- **Repository Pattern**: Abstracted data access operations

#### Infrastructure Layer
- **Background Services**: Asynchronous processing
- **Configuration Management**: Application settings
- **Logging**: System monitoring and diagnostics

### Technology Stack

#### Backend Technologies
- **Framework**: .NET Core 2.1
- **Language**: C#
- **Database**: SQL Server 2016+
- **ORM**: Entity Framework Core 2.1
- **Web Server**: Kestrel (ASP.NET Core)
- **Dependency Injection**: Built-in DI container

#### Frontend Technologies
- **HTML5**: Semantic markup
- **CSS3**: Responsive styling with Bootstrap
- **JavaScript**: DOM manipulation and AJAX
- **Razor Views**: Server-side rendering
- **AJAX**: Asynchronous data updates

#### Development Tools
- **IDE**: Visual Studio or VS Code
- **Version Control**: Git
- **Package Manager**: NuGet
- **Testing**: Unit testing frameworks

## Detailed Component Structure

### Main Application Directory (KWHMonitoring/)

#### Controllers/
This directory contains all MVC controllers:

1. **MonitoringController.cs**
   - Main dashboard functionality
   - Device monitoring and status display
   - Charts and data visualization
   - Historical data access
   - Anomaly logs
   - Device details and history
   - Export functionality

2. **ApiController.cs**
   - RESTful API endpoints
   - Data access for external systems
   - Configuration management
   - Statistics retrieval

3. **HomeController.cs**
   - Basic home page functionality
   - Welcome and navigation
   - System information

#### Models/
Data models and domain entities:

1. **KWHData.cs** - Raw energy consumption measurements
2. **HourlyEnergy.cs** - Hourly aggregated energy data
3. **DailyEnergy.cs** - Daily aggregated energy data
4. **MonthlyEnergy.cs** - Monthly aggregated energy data
5. **YearlyEnergy.cs** - Yearly aggregated energy data
6. **AppSettings.cs** - Application configuration
7. **NotificationSettings.cs** - Notification configuration
8. **PanelViewModel.cs** - UI presentation model for device panels
9. **DashboardViewModel.cs** - UI presentation model for dashboard
10. **UsageStatistics.cs** - Statistical data models
11. **AnomalyLog.cs** - Anomaly detection records
12. **ApplicationDbContext.cs** - Entity Framework database context
13. **AppSettingsRecord.cs** - Configuration records

#### Services/
Background services and business logic:

1. **EnergyAggregationBackgroundService.cs**
   - Main data aggregation service
   - Implements trapezoidal rule for energy calculation
   - Handles hourly, daily, monthly, and yearly aggregations
   - Thread-safe execution with semaphore locking

2. **AnomalyNotificationBackgroundService.cs**
   - Anomaly detection service
   - Monitors for unusual power consumption
   - Generates anomaly logs
   - Triggers notifications when needed

3. **NotificationService.cs**
   - Notification delivery service
   - Supports various notification channels
   - Configuration management for notifications

#### Views/
Razor views for the web interface:

1. **Monitoring/**
   - Index.cshtml: Main dashboard view
   - Charts.cshtml: Data visualization
   - AnomalyLogs.cshtml: Anomaly history
   - Details.cshtml: Device detail view
   - History.cshtml: Historical data view
   - UsageStatistics.cshtml: Statistical analysis
   - Settings.cshtml: Configuration management

2. **Home/**
   - Index.cshtml: Home page

3. **Shared/**
   - _Layout.cshtml: Main layout template
   - _ValidationScriptsPartial.cshtml: Validation scripts
   - Error.cshtml: Error page template

#### wwwroot/
Static assets directory:

1. **css/**
   - Site.css: Main stylesheet
   - bootstrap.min.css: Bootstrap framework
   - custom styles for monitoring interface

2. **js/**
   - site.js: Main JavaScript functionality
   - bootstrap.bundle.min.js: Bootstrap JavaScript
   - chart.js: Charting library
   - jquery.min.js: jQuery library
   - moment.min.js: Date/time handling

3. **images/**
   - Logo and branding images
   - Icons for UI elements

#### Properties/
Assembly and project properties:

1. **AssemblyInfo.cs**: Assembly metadata
2. **launchSettings.json**: Development server settings

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

## Data Processing and Aggregation

### Trapezoidal Rule Implementation

The system implements the trapezoidal rule for accurate hourly energy calculation from time-series data points.

#### Algorithm Details
1. **Data Collection**: Gather all raw measurements for a specific hour for each device
2. **Boundary Handling**: Include boundary readings from adjacent periods
3. **Interval Calculation**: For each time interval between consecutive readings:
   - Calculate time difference
   - Determine average power between the two points
   - Calculate energy for the interval: `energy = average_power × time_interval`
4. **Summation**: Sum all intervals to get total hourly energy

#### Mathematical Formula
For each interval between readings i and i+1:
```
Δt = t[i+1] - t[i]  // Time difference
P_avg = (P[i] + P[i+1]) / 2  // Average power
ΔE = P_avg × Δt  // Energy for interval
```

#### Implementation Characteristics
- **Accuracy**: Provides more accurate energy calculation than simple averaging
- **Robustness**: Handles data gaps gracefully
- **Performance**: Optimized for database operations
- **Consistency**: Produces predictable results across different time periods

### Aggregation Schedule

#### Hourly Aggregation
- Runs every minute to check for data to aggregate
- Aggregates data for the previous complete hour
- Uses trapezoidal rule for energy calculation
- Processes all devices in parallel

#### Daily Aggregation
- Runs at minute 0-2 of each hour
- Aggregates data for the previous complete day
- Sums hourly energy records
- Updates daily statistics

#### Monthly Aggregation
- Runs at day 1, hour 0, minute 0-2
- Aggregates data for the previous complete month
- Sums daily energy records
- Updates monthly statistics

#### Yearly Aggregation
- Runs at Jan 1, hour 0, minute 0-2
- Aggregates data for the previous complete year
- Sums monthly energy records
- Updates yearly statistics

### Data Quality Controls

#### Validation Checks
1. **Temporal Consistency**: Ensures timestamps are in logical order
2. **Physical Limits**: Validates voltage/current values against reasonable ranges
3. **Data Completeness**: Verifies sufficient data points for accurate calculations
4. **Duplicate Detection**: Identifies and handles duplicate readings

#### Error Handling
1. **Missing Data**: Uses boundary values when gaps exist
2. **Outlier Detection**: Flags unusual readings for review
3. **Unit Conversion**: Ensures consistent units (Wh to kWh conversion)
4. **Null Value Management**: Proper handling of nullable fields

## Services Architecture

### EnergyAggregationBackgroundService

#### Responsibilities
1. **Periodic Execution**: Runs every minute to check for aggregation opportunities
2. **Thread Safety**: Uses semaphore to prevent concurrent execution
3. **Smart Timing**: Only aggregates when appropriate (e.g., at the start of each hour/day/month/year)
4. **Error Handling**: Graceful error recovery with logging

#### Key Methods
1. **ExecuteAsync**: Main service execution loop
2. **TryAggregateAsync**: Attempts to perform aggregation
3. **AggregateHourlyAsync**: Performs hourly data aggregation
4. **AggregateDailyAsync**: Performs daily data aggregation
5. **AggregateMonthlyAsync**: Performs monthly data aggregation
6. **AggregateYearlyAsync**: Performs yearly data aggregation
7. **BackfillAllAsync**: Processes historical data for initial population

#### Performance Considerations
- Batch processing to reduce database round trips
- Parallel processing for different devices
- Async/await patterns for I/O-bound operations
- Semaphore-based locking for thread safety

### AnomalyNotificationBackgroundService

#### Responsibilities
1. **Continuous Monitoring**: Constantly checks for abnormal conditions
2. **Threshold Detection**: Compares current readings against defined thresholds
3. **Log Generation**: Records all anomaly detections
4. **Notification Triggering**: Initiates notification processes when anomalies occur

#### Anomaly Detection Logic
1. **High Load Detection**: >20,000W power consumption
2. **Medium Load Detection**: >10,000W power consumption
3. **Normal Load**: ≤10,000W power consumption
4. **Voltage/Current Issues**: Out-of-range measurements

### NotificationService

#### Responsibilities
1. **Notification Delivery**: Sends alerts via configured channels
2. **Queue Management**: Manages notification queue
3. **Configuration Handling**: Reads notification settings
4. **Error Recovery**: Handles delivery failures gracefully

## Web Interface Features

### Dashboard (Monitoring/Index)
- Real-time device status display
- Color-coded indicators for voltage and power levels
- Summary statistics for all monitored devices
- Quick access to device details and history
- Responsive layout for different screen sizes

### Charts (Monitoring/Charts)
- Interactive time-series visualizations
- Comparison of different devices
- Historical trend analysis
- Export capabilities for charts

### Device Details (Monitoring/Details)
- Comprehensive device information
- Current measurements and status
- Historical data for the specific device
- Device-specific statistics

### Historical Data (Monitoring/History)
- Device-specific history view
- Date range filtering
- Pagination for large datasets
- CSV export functionality

### Anomaly Logs (Monitoring/AnomalyLogs)
- Detailed anomaly detection records
- Timestamped logs of abnormal conditions
- Device-specific anomaly reports
- Filtering and search capabilities

### Settings (Monitoring/Settings)
- Application configuration management
- Notification settings
- Data retention policies
- System-wide parameters

## Security Considerations

### Authentication and Authorization
- Session-based authentication
- Role-based access control (if implemented)
- Secure cookie handling
- CSRF protection for forms

### Data Protection
- Encrypted connection strings in configuration
- Parameterized queries to prevent SQL injection
- Input validation and sanitization
- Secure transmission of data (HTTPS)

### Privacy and Compliance
- Data retention policies
- Access logging for sensitive operations
- Compliance with data protection regulations
- Audit trails for critical operations

## Performance Optimization

### Database Optimization
1. **Indexing Strategy**:
   - Primary keys automatically indexed
   - DeviceKey and timestamp combinations for fast queries
   - Composite indexes on time-based aggregations

2. **Query Optimization**:
   - Efficient LINQ queries with appropriate filtering
   - Selective data retrieval
   - Batch operations for data aggregation

### Caching Strategies
1. **Memory Caching**: Frequently accessed data
2. **Response Compression**: For static assets and API responses
3. **Database Connection Pooling**: Efficient resource utilization

### Background Processing
1. **Asynchronous Operations**: Non-blocking data processing
2. **Thread Safety**: Proper synchronization for concurrent operations
3. **Resource Management**: Efficient use of system resources

## Deployment Configuration

### appsettings.json Structure
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=192.168.168.38,1433;Database=HaiwellElectrical;User Id=kwhapp;Password=kwhapp1234;TrustServerCertificate=True;"
  },
  "Wablas": {
    "ServerUrl": "https://pati.wablas.com",
    "Token": "",
    "SecretKey": "",
    "PhoneNumbers": "",
    "EnableWhatsApp": false
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Environment-Specific Configurations
1. **Development**: Local SQL Server, detailed logging
2. **Staging**: Test database, moderate logging
3. **Production**: Production database, minimal logging

## Development and Testing

### Development Environment Setup
1. Install .NET Core 2.1 SDK
2. Install SQL Server or SQL Server Express
3. Install Visual Studio or VS Code
4. Clone repository
5. Restore NuGet packages
6. Build and run application

### Testing Approaches
1. **Unit Testing**: Individual method testing
2. **Integration Testing**: Database and service interactions
3. **UI Testing**: Browser-based interface testing
4. **Performance Testing**: Load and stress testing

### Code Quality Standards
1. **Naming Conventions**: PascalCase for C# identifiers
2. **Documentation**: XML comments for public APIs
3. **Error Handling**: Comprehensive exception handling
4. **Logging**: Appropriate log levels and messages

## Maintenance and Operations

### Regular Maintenance Tasks
1. **Database Maintenance**:
   - Index rebuilding/reorganizing
   - Statistics updates
   - Log file cleanup
   - Archive old data if needed

2. **Application Maintenance**:
   - Review and update dependencies
   - Apply security patches
   - Monitor application logs
   - Check for performance degradation

3. **Monitoring Tasks**:
   - Review anomaly detection reports
   - Verify data integrity
   - Check notification delivery
   - Validate backup procedures

### Backup Strategy
1. **Database Backups**:
   - Full database backups
   - Transaction log backups
   - Automated backup verification

2. **Application Backups**:
   - Application file backups
   - Configuration file backups
   - Version-controlled source code

3. **Disaster Recovery**:
   - Recovery procedures documentation
   - Backup storage locations
   - Restoration testing schedule

## Troubleshooting Guide

### Common Issues and Solutions

#### Database Connectivity Issues
1. Verify connection string in configuration
2. Check SQL Server service status
3. Confirm network connectivity to database server
4. Validate database user permissions

#### Performance Degradation
1. Monitor database query performance
2. Check for missing indexes
3. Review memory usage patterns
4. Examine CPU utilization

#### Service Failures
1. Check Windows Event Logs
2. Verify service account permissions
3. Confirm application dependencies
4. Review application logs for errors

#### Data Aggregation Problems
1. Verify background service is running
2. Check database connectivity from service
3. Review logs for aggregation errors
4. Confirm data availability in source tables

## Future Enhancements

### Planned Improvements
1. **Enhanced Analytics**: Advanced statistical analysis
2. **Machine Learning**: Predictive modeling for energy consumption
3. **Mobile Application**: Native mobile interface
4. **Cloud Integration**: Azure/AWS hosting options
5. **Advanced Notifications**: Multi-channel alerting
6. **Data Export Formats**: Additional export formats beyond CSV

### Technical Debt Reduction
1. **Code Modernization**: Migrate to newer .NET versions
2. **Architecture Refinement**: Improve modularity
3. **Testing Coverage**: Expand unit and integration tests
4. **Documentation Updates**: Keep documentation current with code changes

## Change Log

### Version History
- **V1.0**: Initial release with core monitoring functionality
- **V2.0**: Added advanced aggregation algorithms and improved UI
- **V3.0**: Enhanced security features and performance optimizations

## References and Resources

### External Dependencies
1. **Entity Framework Core 2.1**: Data access framework
2. **Bootstrap 4**: Responsive UI framework
3. **jQuery**: JavaScript library
4. **Chart.js**: Data visualization library
5. **Newtonsoft.Json**: JSON serialization

### Documentation Resources
1. .NET Core 2.1 Documentation
2. Entity Framework Core Documentation
3. SQL Server Documentation
4. ASP.NET Core MVC Documentation

This comprehensive memory documentation captures all aspects of the KWH Monitoring project, from its architecture and implementation details to deployment considerations and future enhancements.