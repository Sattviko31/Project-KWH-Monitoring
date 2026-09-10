# KWHMonitoring

Aplikasi Windows Service untuk monitoring data KWH dan relay dari perangkat IoT melalui protokol MQTT, lalu menyimpannya ke Microsoft SQL Server.

## Overview

`KWHMonitoring` adalah service .NET 8 Worker yang berjalan di background Windows. Ia melakukan:

- Subscribe ke broker MQTT pada semua topik (`#`).
- Menerima payload JSON dari perangkat.
- Memetakan nama kolom MQTT ke kolom database melalui `ColumnMapping`.
- Menyimpan data KWH ke tabel `KWHData`.
- Menyimpan data relay ke tabel `RelayControl`.
- Melakukan smart migration ke SQL Server saat pertama kali dijalankan.
- Menyediakan wizard konsol untuk konfigurasi dan auto-install Windows Service.

## Arsitektur

```text
[Perangkat IoT] --MQTT--> [KWHMonitoring Service] --T-SQL--> [SQL Server]
```

Komponen utama:

| Folder/File | Deskripsi |
|-------------|-----------|
| `Program.cs` | Entry point, mode service vs wizard, DPAPI config, install service. |
| `Configuration/AppConfig.cs` | Kelas konfigurasi MQTT, Database, dan Sampling. |
| `Services/KwhMonitoringService.cs` | Background service: MQTT client, queue processing, migration, save ke DB. |
| `appsettings.json` | Konfigurasi default (tidak menyimpan password). |
| `appsettings.user.json` | Konfigurasi user terenkripsi dengan DPAPI. |

## Alur Data

1. MQTT client menerima pesan dan memasukkannya ke `ConcurrentQueue<MqttMessageBuffer>`.
2. `ProcessQueueAsync` mengambil pesan dan memanggil `ProcessSingleMessageAsync`.
3. Payload JSON di-parse dan dipetakan nama kolomnya melalui `ApplyColumnMapping`.
4. Kolom dibagi menjadi dua kategori:
   - `KWHDataColumns` (PHASE_R/S/T, AMPERE_R/S/T, COSPHI, W, AKTIF_POWER, TOTALW, F).
   - `RelayControlColumns` (RC, RCI).
5. Data disimpan ke SQL Server melalui `SaveKWHDataAsync` dan `SaveRelayControlAsync`.

## Fitur

### 1. Setup Wizard & Auto Install Service

Jalankan executable tanpa argumen untuk masuk ke wizard:

- Konfigurasi MQTT (broker, port, TLS, sertifikat, username/password).
- Konfigurasi SQL Server (server, database, autentikasi, enkripsi).
- Konfigurasi Sampling Data (interval dalam detik).
- Jika dijalankan sebagai Administrator, service akan di-install dan di-start otomatis.

### 2. Smart Database Migration

Saat startup, service memastikan database dan tabel sudah ada. Jika belum ada, akan dibuat otomatis berdasarkan skrip migration yang tertanam di dalam kode.

Tabel utama:

- `DeviceRegistry`
- `KWHData`
- `RelayControl`
- `FailedMessages`
- `AnomalyLogs`
- `AppSettings`, `AppLog`, `ColumnMapping`, `ColumnScaleConfig`
- View: `vLatestKWHData`, `vDeviceSummary`, `vDailyEnergy`, `vLatestRelayControl`

### 3. Column Mapping

Mapping kolom MQTT ke kolom database dapat dikonfigurasi di tabel `ColumnMapping`. Mapping default:

- `VR` -> `PHASE_R`
- `VS` -> `PHASE_S`
- `VT` -> `PHASE_T`
- `AKTIF_W` -> `AKTIF_POWER`

### 4. Sampling Data

Dikonfigurasi melalui properti `Sampling.IntervalSeconds`.

- **KWHData**: disimpan sesuai interval waktu per perangkat. Misalnya interval 5 detik berarti 1 data per perangkat setiap 5 detik.
- **RC (Relay Control)**: selalu disimpan setiap ada data masuk.
- **RCI (Relay Counter/Index)**: hanya disimpan jika nilainya berbeda dari nilai terakhir per perangkat.

Default `IntervalSeconds = 0` berarti semua data disimpan (tidak ada sampling untuk KWHData).

## Konfigurasi

### appsettings.json

```json
{
  "Mqtt": {
    "BrokerIp": "192.168.150.10",
    "Port": 1883,
    "UseTls": false,
    "UseClientCertificate": false,
    "ClientCertificatePath": "",
    "ClientCertificatePassword": "",
    "Username": "",
    "Password": ""
  },
  "Database": {
    "Server": "192.168.168.38",
    "DatabaseName": "HaiwellElectrical",
    "UseWindowsAuthentication": false,
    "Username": "kwhapp",
    "Password": "",
    "Encrypt": true,
    "TrustServerCertificate": false
  },
  "Sampling": {
    "IntervalSeconds": 0
  }
}
```

### appsettings.user.json

File ini dibuat otomatis oleh wizard dan disimpan di folder output executable. Isinya dienkripsi dengan DPAPI agar password tidak tersimpan sebagai plain text.

## Build & Run

### Build Debug

```bash
dotnet build KWHMonitoring.slnx
```

### Run as Console (Wizard)

```bash
dotnet run --project KWHMonitoring/KWHMonitoring.csproj
# atau
.\KWHMonitoring\bin\Debug\net8.0\win-x64\KWHMonitoring.exe
```

### Run as Service

```bash
.\KWHMonitoring\bin\Debug\net8.0\win-x64\KWHMonitoring.exe --run-as-service
```

Saat dijalankan tanpa argumen, aplikasi akan masuk mode wizard dan otomatis menginstall service jika running sebagai Administrator.

## Logging

Log disimpan di folder `logs\kwh-monitoring-.<tanggal>.log` dengan rolling harian dan retensi 2 file.

Contoh log saat service berjalan dengan sampling aktif:

```text
[*] Sampling data aktif: 1 data per 5 detik per perangkat
```

Atau jika non-aktif:

```text
[*] Sampling data: non-aktif (semua data disimpan)
```

## Changelog

### 2026-09-10

- **Log lebih bersih**: menghapus log `[DB]` per insert dan log periodik `[HEALTH]`. Log sekarang hanya berupa `[STATS]` yang muncul real-time setiap data berhasil tersimpan dan setiap 30 detik.
- **Health di log STATS**: setiap baris `[STATS]` sekarang menyertakan status `MQTT`, status `DB`, dan `Health` (`Healthy`/`Degraded`).
- **Auto-reconnect SQL Server**: service menunggu dan retry hingga SQL Server tersedia saat startup, dan otomatis kembali menyimpan data setelah SQL down.
- **Auto-reconnect MQTT**: menambahkan loop reconnect MQTT yang terus mencoba koneksi ulang jika broker/MQTT mati, serta subscribe ulang setelah reconnect.

### 2026-09-09

- **Sampling Data Wizard**: menambahkan input manual interval sampling (detik) di setup wizard.
- **Sampling KWHData**: data KWH hanya disimpan sesuai interval per perangkat.
- **Sampling Relay**:
  - `RC` selalu disimpan setiap ada data masuk.
  - `RCI` hanya disimpan jika nilainya berubah dari nilai terakhir per perangkat.
- Menambahkan default konfigurasi `Sampling.IntervalSeconds` di `appsettings.json`.

### 2026-09-09 (2)

- **Realtime DB Log**: setiap penyimpanan ke `KWHData` dan `RelayControl` langsung ditulis ke log.
- **Health Check**: log status kesehatan service setiap 60 detik, mencakup status MQTT, jumlah pesan, dan tingkat kesehatan (`Healthy`/`Degraded`).

### 2026-09-09 (3)

- **RelayControl Hanya Latest**: tabel `RelayControl` hanya menyimpan 1 baris terbaru per perangkat. Data lama dihapus otomatis setiap ada data baru. Nilai `RC` dan `RCI` terakhir digabung sebelum insert agar tidak hilang.
