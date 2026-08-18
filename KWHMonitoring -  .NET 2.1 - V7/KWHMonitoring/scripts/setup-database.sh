#!/bin/bash
# =====================================================
# Shell Script untuk Setup Database KWHMonitoring
# Untuk Linux/Mac environment
# =====================================================

echo "============================================"
echo "KWHMonitoring Database Setup"
echo "============================================"
echo ""

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET Core SDK tidak ditemukan!"
    echo "Silakan install .NET Core SDK 2.1 atau lebih tinggi"
    exit 1
fi

echo ".NET Core SDK version:"
dotnet --version
echo ""

# Navigate to project directory
PROJECT_DIR="$(dirname "$0")/KWHMonitoring"
cd "$PROJECT_DIR" || {
    echo "ERROR: Tidak dapat masuk ke folder KWHMonitoring"
    exit 1
}

echo "Project directory:"
ls -1
echo ""

# Restore packages
echo "[1/3] Restoring NuGet packages..."
dotnet restore
if [ $? -ne 0 ]; then
    echo "ERROR: Package restore failed!"
    exit 1
fi
echo "OK: Packages restored."
echo ""

# Install EF tools if not installed
if ! command -v dotnet-ef &> /dev/null; then
    echo "[2/3] Installing Entity Framework tools..."
    dotnet tool install --global dotnet-ef --version 2.1.1 || {
        echo "WARNING: Tool install failed, trying with existing tools..."
    }
fi

# Run migration
echo "[3/3] Running database migration..."
dotnet ef database update
if [ $? -ne 0 ]; then
    echo "ERROR: Migration failed!"
    echo "Cek appsettings.json untuk connection string yang benar"
    exit 1
fi

echo ""
echo "============================================"
echo "DATABASE SETUP SELESAI!"
echo "============================================"
echo ""
echo "Database 'KWHMonitoring' sudah dibuat dengan tabel berikut:"
echo "  - KWHData"
echo "  - AnomalyLogs"
echo "  - AppSettings"
echo "  - HourlyEnergy"
echo "  - DailyEnergy"
echo "  - MonthlyEnergy"
echo "  - YearlyEnergy"
echo ""
echo "Database siap digunakan!"
echo ""
