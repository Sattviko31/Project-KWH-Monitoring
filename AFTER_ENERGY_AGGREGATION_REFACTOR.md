# Setelah Refactor Energy Aggregation

**Tanggal:** 2026-09-10

## Arsitektur Baru

Agregasi energi telah dipindahkan dari **KWHMonitoring Web App** ke **KWHMonitoringListener**.

```text
[Perangkat IoT] --MQTT--> [KWHMonitoringListener]
                              |
                              |-- Simpan ke KWHData
                              |-- Track energy real-time (in-memory)
                              |-- Flush tiap 30 detik ke HourlyEnergy
                              |-- Agregasi Daily/Monthly/Yearly
                              v
                           [SQL Server]
```

## File Baru

### Listener
- `KWHMonitoringListener/KWHMonitoring/Services/IEnergyAggregationTracker.cs`
- `KWHMonitoringListener/KWHMonitoring/Services/EnergyAggregationTracker.cs`
- `KWHMonitoringListener/KWHMonitoring/Services/EnergyAggregationService.cs`
- `KWHMonitoringListener/KWHMonitoring/Models/EnergyAggregationModels.cs`
- `KWHMonitoringListener/global.json`

## File yang Dihapus

### Web App
- `KWHMonitoring - .NET 2.1 - V7/KWHMonitoring/Services/EnergyAggregationBackgroundService.cs`

## File yang Dimodifikasi

### Listener
- `KWHMonitoringListener/KWHMonitoring/Program.cs` — registrasi tracker dan service
- `KWHMonitoringListener/KWHMonitoring/Services/KwhMonitoringService.cs` — integrasi tracker saat data MQTT masuk
- `KWHMonitoringListener/KWHMonitoring/Services/KwhMonitoringService.cs` — tambahan UNIQUE INDEX untuk HourlyEnergy, DailyEnergy, MonthlyEnergy, YearlyEnergy

### Web App
- `KWHMonitoring - .NET 2.1 - V7/KWHMonitoring/Startup.cs` — hapus registrasi EnergyAggregationBackgroundService
- `KWHMonitoring - .NET 2.1 - V7/KWHMonitoring/Controllers/ApiController.cs` — hapus endpoint trigger-aggregation dan AggregationRequest

## Cara Kerja Hybrid

1. **Saat data MQTT masuk**: `KwhMonitoringService` menyimpan ke `KWHData` lalu memanggil `IEnergyAggregationTracker.TrackReading()`
2. **In-memory state**: `EnergyAggregationTracker` menyimpan state per device per jam, mengakumulasi energi dengan trapezoidal rule
3. **Flush tiap 30 detik**: `EnergyAggregationService` memanggil `FlushAsync()` untuk menyimpan state ke `HourlyEnergy`
4. **Finalisasi jam**: State jam yang sudah lewat difinalisasi dan dihapus dari memory
5. **Agregasi Daily/Monthly/Yearly**: Dijalankan pada jadwal yang ditentukan
6. **Backfill saat startup**: Menghitung ulang data historis untuk memastikan tidak ada gap

## Keunggulan

- ✅ Real-time (delay max 30 detik)
- ✅ Hemat storage dan bandwidth DB
- ✅ Akurasi tinggi dengan trapezoidal rule
- ✅ Web app fokus ke UI/API, listener fokus ke data pipeline

## Verifikasi

- Build listener: `dotnet build KWHMonitoring/KWHMonitoring.csproj` ✅
- Build web app: `dotnet build "KWHMonitoring - .NET 2.1 - V7/KWHMonitoring/KWHMonitoring.csproj"` ✅
