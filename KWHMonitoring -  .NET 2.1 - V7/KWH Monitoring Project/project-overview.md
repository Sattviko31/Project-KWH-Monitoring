---
name: Project Overview and Architecture
description: Project overview, architecture, and technology stack
type: project
---

# KWH Monitoring Project - Overview and Architecture

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