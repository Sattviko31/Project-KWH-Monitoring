---
name: Component Structure and Directory Layout
description: Detailed breakdown of project components and directory structure
type: project
---

# KWH Monitoring Project - Component Structure

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