# Scripts & Database Setup

Folder ini berisi script dan file SQL untuk setup database KWHMonitoring.

## 📁 File-Folder

### Migration Files (Entity Framework)
- `MigrationInitialCreate.cs` - File migration EF Core pertama kali
- `README.md` - Dokumentasi lengkap migration

### SQL Scripts
- `database_schema.sql` - Script SQL lengkap untuk create database + semua tabel
- `quickstart.sql` - Script quick start dengan pesan konfirmasi
- `PANDUAN_SETUP.md` - Panduan dalam Bahasa Indonesia

### Automation Scripts
- `setup-database.bat` - Batch script untuk Windows (klik dua kali!)
- `setup-database.sh` - Shell script untuk Linux/Mac

## 🚀 Cara Cepat Setup

### Opsi 1: Double Click (Windows)
```bash
# Klik kanan -> Run as Administrator
setup-database.bat
```

### Opsi 2: Command Line (Semua Platform)
```bash
cd "KWHMonitoring"
dotnet ef database update
```

### Opsi 3: SQL Server Management Studio
```bash
# Copy isi file quickstart.sql
# Paste ke SSMS dan Execute
```

## ✅ Yang Terjadi Setelah Setup

Database akan dibuat otomatis dengan struktur lengkap:

1. **7 Tabel** dibuat
   - KWHData (data mentah)
   - AnomalyLogs (log alarm)
   - AppSettings (konfigurasi)
   - HourlyEnergy (agregat jam)
   - DailyEnergy (agregat hari)
   - MonthlyEnergy (agregat bulan)
   - YearlyEnergy (agregat tahun)

2. **Indexes** ditambahkan untuk performa optimal

3. **Default Data** diseed
   - MaxCapacity = 100000
   - NotificationEnabled = false
   - CheckIntervalSeconds = 30

## 📋 Prerequisites

- .NET Core SDK 2.1+ (untuk EF migrations)
- SQL Server (local/remote/cloud)
- Connection string yang benar di appsettings.json

## 🔧 Troubleshooting

| Error | Solusi |
|-------|--------|
| `.NET Core not found` | Install .NET Core SDK 2.1+ |
| `Login failed` | Cek user/password di appsettings.json |
| `Connection refused` | Pastikan SQL Server running, port 1433 terbuka |
| `Table already exists` | Normal, script skip jika tabel sudah ada |

## 📖 Dokumentasi Lengkap

Lihat file `Migrations/README.md` untuk dokumentasi detail struktur database.
