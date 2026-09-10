# Checkpoint Sebelum Refactor Energy Aggregation

**Tanggal:** 2026-09-10  
**Git Commit:** `48ef77a`  
**Git Tag:** `before-energy-aggregation-refactor`

## Arsitektur Sebelum Refactor

### Web App (KWHMonitoring - .NET 2.1 - V7)

`EnergyAggregationBackgroundService` berjalan di web app sebagai `IHostedService`.

**File terkait:**
- `KWHMonitoring - .NET 2.1 - V7/KWHMonitoring/Services/EnergyAggregationBackgroundService.cs`
- `KWHMonitoring - .NET 2.1 - V7/KWHMonitoring/Startup.cs` (registrasi service)
- `KWHMonitoring - .NET 2.1 - V7/KWHMonitoring/Controllers/ApiController.cs` (endpoint `trigger-aggregation`)

**Logika:**
- Berjalan setiap 1 menit
- Mengagregasi jam sebelumnya saja
- Menggunakan Entity Framework Core 2.1 (`ApplicationDbContext`)
- Metode: `AggregateHourlyAsync`, `AggregateDailyAsync`, `AggregateMonthlyAsync`, `AggregateYearlyAsync`, `BackfillAllAsync`

### Listener (KWHMonitoringListener)

- Hanya menerima data MQTT dan menyimpan ke `KWHData`/`RelayControl`
- Belum memiliki logika agregasi energi

## Tujuan Refactor

Memindahkan agregasi energi dari web app ke listener dengan pendekatan **Hybrid**:
1. Real-time tracking saat data MQTT masuk
2. Flush ke database setiap 30 detik
3. Finalisasi jam yang sudah lewat
4. Backfill otomatis saat startup

## Catatan Keamanan

File `token.txt` sudah dihapus dan `.gitignore` sudah di-update di commit sebelumnya.
