@echo off
REM =====================================================
REM Batch Script untuk Setup Database KWHMonitoring
REM Run sebagai administrator jika perlu
REM =====================================================

echo ============================================
echo KWHMonitoring Database Setup
echo ============================================
echo.

REM Check if dotnet is installed
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: .NET Core SDK tidak ditemukan!
    echo Silakan install .NET Core SDK 2.1 atau lebih tinggi
    pause
    exit /b 1
)

echo .NET Core SDK version:
dotnet --version
echo.

REM Navigate to project directory
cd /d "%~dp0KWHMonitoring"
if errorlevel 1 (
    echo ERROR: Tidak dapat masuk ke folder KWHMonitoring
    pause
    exit /b 1
)

echo Project directory:
dir /b
echo.

REM Restore packages
echo [1/3] Restoring NuGet packages...
dotnet restore
if errorlevel 1 (
    echo ERROR: Package restore failed!
    pause
    exit /b 1
)
echo OK: Packages restored.
echo.

REM Check if EF tools installed
dotnet ef --version >nul 2>&1
if errorlevel 1 (
    echo [2/3] Installing Entity Framework tools...
    dotnet tool install --global dotnet-ef --version 2.1.1
    if errorlevel 1 (
        echo WARNING: Tool install failed, trying with existing tools...
    )
)

REM Run migration
echo [3/3] Running database migration...
dotnet ef database update
if errorlevel 1 (
    echo ERROR: Migration failed!
    echo Cek appsettings.json untuk connection string yang benar
    pause
    exit /b 1
)

echo.
echo ============================================
echo DATABASE SETUP SELESAI!
echo ============================================
echo.
echo Database 'KWHMonitoring' sudah dibuat dengan tabel berikut:
echo   - KWHData
echo   - AnomalyLogs
echo   - AppSettings
echo   - HourlyEnergy
echo   - DailyEnergy
echo   - MonthlyEnergy
echo   - YearlyEnergy
echo.
echo Database siap digunakan!
echo.

pause
