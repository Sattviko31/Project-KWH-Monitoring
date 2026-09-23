# Rencana Transformasi: Anomaly Information Center

> Dokumen ini mencatat kondisi **sekarang** sistem anomaly detection dan merancang perubahan menjadikan halaman **Anomaly Logs** sebagai pusat informasi anomali yang lengkap, real-time, dan dapat dianalisis — termasuk saran tindakan otomatis dan laporan bulanan.

---

## 1. Tujuan

Membuat tab **Anomaly Logs** bukan lagi sekadar tabel log, melainkan menjadi **Anomaly Information Center** yang memberikan:

1. **Detail informasi setiap anomali secara real-time**
2. **Analisis otomatis** untuk setiap anomali
3. **Saran tindakan (action recommendation)** berdasarkan jenis dan konteks anomali
4. **Laporan bulanan** yang merangkum semua analisis anomali per bulan
5. **Akses berbasis peran (Viewer / Operator / Admin)** yang mengatur siapa boleh melihat, bertindak, atau menerima laporan tindakan

---

## 2. Kondisi Saat Ini

### 2.1 File & Komponen Utama

| Komponen | Lokasi | Fungsi |
|----------|--------|--------|
| `AnomalyLogs.cshtml` | `KWHMonitoring/Views/Monitoring/AnomalyLogs.cshtml` | Tampilan utama tab Anomaly Logs (tabel log saja) |
| `AnomalyLog.cs` | `KWHMonitoring/Models/AnomalyLog.cs` | Entity model log anomali |
| `ApiController.cs` | `KWHMonitoring/Controllers/ApiController.cs` | Endpoint API anomaly: summary, list, log-anomaly, reset, delete, clear |
| `_AnomalyService.cshtml` | `KWHMonitoring/Views/Shared/_AnomalyService.cshtml` | JavaScript background service deteksi anomali real-time |
| `AnomalyNotificationBackgroundService.cs` | `KWHMonitoring/Services/AnomalyNotificationBackgroundService.cs` | Background service kirim laporan periodik (hourly/daily/monthly) |
| `NotificationService.cs` | `KWHMonitoring/Services/NotificationService.cs` | Pembangun & pengirim email/WhatsApp laporan & alert |
| `ApplicationDbContext.cs` | `KWHMonitoring/Models/ApplicationDbContext.cs` | DbContext, tabel `AnomalyLogs` |
| `KwhMonitoringService.cs` (Listener) | `KWHMonitoringListener/KWHMonitoring/Services/KwhMonitoringService.cs` | Definisi DDL tabel `AnomalyLogs` |

### 2.2 Sistem Role yang Sudah Ada

Project ini sudah memiliki sistem autentikasi dan otorisasi berbasis role melalui **ASP.NET Core Identity/Cookie Authentication** di `Startup.cs`:

```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireOperator", policy => policy.RequireRole("Operator", "Admin"));
    options.AddPolicy("RequireViewer", policy => policy.RequireRole("Viewer", "Operator", "Admin"));
});
```

Model `ApplicationUser` memiliki properti:

```csharp
public string Role { get; set; } = UserRoles.None;   // None | Viewer | Operator | Admin
```

Policy yang ada sudah bisa langsung dimanfaatkan untuk membatasi akses halaman dan API anomaly. Namun saat ini halaman **Anomaly Logs** dan endpoint anomaly belum menerapkan kebijakan role tersebut secara spesifik.

### 2.3 Skema Data AnomalyLog Saat Ini

```csharp
public class AnomalyLog
{
    public long Id { get; set; }
    public string DeviceKey { get; set; }
    public string DeviceId { get; set; }
    public string AnomalyType { get; set; }      // OVERLOAD | DROP | DEVICE_DROP
    public decimal PowerValue { get; set; }
    public decimal ThresholdValue { get; set; }
    public decimal Deviation { get; set; }
    public DateTime DetectedTime { get; set; }
    public decimal? EMAValue { get; set; }
    public string ThresholdMode { get; set; }   // manual | ema | fibonacci
    public bool? Acknowledged { get; set; }
    public DateTime? AcknowledgedTime { get; set; }
    public string Notes { get; set; }
}
```

> **Catatan**: Kolom `Notes` saat ini hanya digunakan untuk mencatat anomali selama downtime (overload saat jam mati). Belum ada kolom untuk analisis, severity, rekomendasi, ataupun status penanganan.

### 2.3 Alur Deteksi Anomali (Real-Time)

1. `_AnomalyService.cshtml` berjalan di background setiap interval (default mengikuti chart refresh interval).
2. Mengambil data chart per device via `/api/Api/panels/{deviceKey}/chart`.
3. Menghitung EMA dan threshold upper/lower.
4. Deteksi anomali:
   - **OVERLOAD**: daya > upper threshold (atau > EMA saat downtime).
   - **DROP**: daya < lower threshold.
5. Menggunakan **konfirmasi 3 sampel berturut-turut** sebelum log disimpan.
6. Setelah log terkirim, masuk ke kondisi **cooldown** sampai daya kembali normal.
7. Log dikirim ke endpoint `POST /api/Api/log-anomaly` → disimpan ke `AnomalyLogs`.

### 2.4 Endpoint API yang Sudah Ada

| Endpoint | Metode | Keterangan |
|----------|--------|------------|
| `/api/Api/anomaly-logs/summary` | GET | Total log, overload, drop, active devices |
| `/api/Api/anomaly-logs/{deviceKey}` | GET | Daftar log dengan paging (`ALL` untuk semua) |
| `/api/Api/anomaly-status` | GET | Status anomali device dalam 5 menit terakhir |
| `/api/Api/log-anomaly` | POST | Mencatat anomali baru |
| `/api/Api/reset-anomaly-alert` | POST | Reset active alert per device |
| `/api/Api/anomaly-logs/{id}` | DELETE | Hapus log tunggal |
| `/api/Api/anomaly-logs/clear-all` | DELETE | Hapus semua log |
| `/api/Api/get-downtime-settings` | GET | Pengaturan downtime |
| `/api/Api/save-downtime-settings` | POST | Simpan pengaturan downtime |
| `/api/Api/get-ema-settings` | GET | Pengaturan EMA & threshold |
| `/api/Api/save-ema-settings` | POST | Simpan pengaturan EMA |
| `/api/Api/get-notification-settings` | GET | Pengaturan notifikasi & laporan |
| `/api/Api/save-notification-settings` | POST | Simpan pengaturan notifikasi |

### 2.5 Tampilan AnomalyLogs.cshtml Saat Ini

- Breadcrumb + header halaman.
- Tab navigasi (Panel Monitoring, All Charts, Usage Stats, Anomaly Logs).
- Summary cards: Overload, Device Drop, Total Logs, Active Devices.
- DataGrid DevExtreme yang menampilkan kolom:
  - ID
  - DetectedTime
  - DeviceKey
  - AnomalyType
  - Power (W)
  - Threshold (W)
  - Deviation (%)
  - Threshold Mode
  - Acknowledged Status
  - Action Delete
- Modal konfirmasi hapus dan clear all.
- Auto refresh setiap 30 detik.

> **Kelemahan saat ini**: Tidak ada detail per anomali, tidak ada analisis, tidak ada saran tindakan, tidak ada laporan bulanan di UI.

### 2.6 Laporan Bulanan yang Sudah Ada (Tapi Hanya Notifikasi)

`NotificationService.SendRealtimeMonthlyReportAsync()` sudah membuat laporan bulanan via email/WhatsApp yang berisi:

- Periode laporan
- Summary dashboard (active panels, total power, total energy, dll)
- Anomaly Summary (total, overload, drop, affected devices)
- Panel details

Namun laporan ini hanya untuk **dikirim via notifikasi**, tidak ada versi UI/web yang dapat dilihat, di-download, atau diproses lebih lanjut.

---

## 3. Perencanaan Perubahan

### 3.1 Gambaran Umum Arsitektur Baru

```
┌─────────────────────────────────────────────────────────────┐
│                    Anomaly Information Center               │
│                         (AnomalyLogs.cshtml)                │
├─────────────────────────────────────────────────────────────┤
│  Dashboard Ringkasan Anomali (KPI Cards)                      │
│  ├── Total anomaly hari ini / minggu ini / bulan ini          │
│  ├── Tren anomali (grafik)                                    │
│  ├── Device paling sering bermasalah                          │
│  └── Distribusi jenis anomali                                 │
├─────────────────────────────────────────────────────────────┤
│  Daftar Anomali (DataGrid) dengan:                          │
│  ├── Status real-time (New / Acknowledged / Resolved)         │
│  ├── Severity badge                                           │
│  └── Tombol "Detail" untuk membuka panel analisis             │
├─────────────────────────────────────────────────────────────┤
│  Panel Detail Anomali (slide-in / modal / row expand):       │
│  ├── Informasi dasar anomali                                  │
│  ├── Analisis otomatis (rule-based)                           │
│  ├── Saran tindakan                                           │
│  ├── Riwayat anomali device serupa                            │
│  └── Tombol acknowledge, tambah catatan, export              │
├─────────────────────────────────────────────────────────────┤
│  Laporan Bulanan Anomali:                                    │
│  ├── Pilih bulan & tahun                                      │
│  ├── Ringkasan anomali bulanan                                │
│  ├── Tren harian                                              │
│  ├── Analisis per device & kategori                          │
│  ├── Rekomendasi tindakan berdasarkan data                   │
│  └── Export PDF / CSV / Email                                 │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 Perubahan Model Data (Backend)

#### 3.2.1 Perluas Tabel AnomalyLogs

Menambahkan kolom-kolom berikut (dengan migration baru):

```csharp
// Di AnomalyLog.cs
public string Severity { get; set; } = "medium";      // low | medium | high | critical
public string RootCause { get; set; }                 // Analisis otomatis
public string RecommendedAction { get; set; }         // Saran tindakan
public string HandledBy { get; set; }                 // User yang handle
public DateTime? ResolvedTime { get; set; }
public string ResolutionNotes { get; set; }
public bool IsResolved { get; set; }
public int? DurationMinutes { get; set; }             // Durasi anomali (jika bisa dihitung)
```

> **Catatan**: Jika ingin tetap sederhana, beberapa kolom dapat disimpan di tabel terpisah `AnomalyAnalysis` (1-to-1 dengan AnomalyLog) agar tabel utama tetap ringan.

#### 3.2.2 (Opsional) Tabel AnomalyAnalysis

```csharp
public class AnomalyAnalysis
{
    public long Id { get; set; }
    public long AnomalyLogId { get; set; }
    public string Severity { get; set; }
    public string RootCause { get; set; }
    public string RecommendedAction { get; set; }
    public string ImpactAssessment { get; set; }
    public DateTime CreatedAt { get; set; }
    public AnomalyLog AnomalyLog { get; set; }
}
```

#### 3.2.3 Tabel AnomalyMonthlyReport (Untuk Laporan Bulanan)

```csharp
public class AnomalyMonthlyReport
{
    public long Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int TotalAnomalies { get; set; }
    public int OverloadCount { get; set; }
    public int DropCount { get; set; }
    public int AffectedDevices { get; set; }
    public decimal AverageDeviation { get; set; }
    public string TopAffectedDevice { get; set; }
    public string SummaryText { get; set; }
    public string Recommendations { get; set; }
    public DateTime GeneratedAt { get; set; }
}
```

#### 3.2.4 Chart Snapshot pada Saat Anomali

Karena chart di UI tidak disimpan secara utuh di database, maka pada saat anomali terkonfirmasi perlu menyimpan snapshot data chart sebelum dan sesudah titik anomali. Snapshot ini berguna untuk investigasi dan laporan bulanan.

**Pendekatan**: Simpan snapshot sebagai JSON di tabel terpisah agar tabel `AnomalyLogs` tetap ringan.

```csharp
public class AnomalyChartSnapshot
{
    public long Id { get; set; }
    public long AnomalyLogId { get; set; }

    // Waktu terjadinya anomali
    public DateTime DetectedTime { get; set; }

    // Data sebelum anomali (50 titik)
    public string BeforeDataJson { get; set; }

    // Data sesudah anomali (50 titik)
    public string AfterDataJson { get; set; }

    // Metadata interval, unit (Watt), threshold saat itu, nilai EMA
    public decimal UpperThreshold { get; set; }
    public decimal LowerThreshold { get; set; }
    public decimal? EMAValue { get; set; }

    // Navigasi ke AnomalyLog
    public AnomalyLog AnomalyLog { get; set; }
}

// Struktur JSON untuk setiap titik data
public class ChartDataPoint
{
    public DateTime Timestamp { get; set; }
    public decimal Power { get; set; }
    public decimal? Upper { get; set; }
    public decimal? Lower { get; set; }
    public decimal? EMA { get; set; }
}
```

**Catatan**: 50 titik sebelum dan 50 titik sesudah adalah ukuran default. Jumlah ini dapat dikonfigurasi melalui pengaturan EMA/system settings.

### 3.3 Perubahan Backend (C# / API)

#### 3.3.1 Service Anomaly Analysis

Buat `IAnomalyAnalysisService` / `AnomalyAnalysisService` yang menerima data `AnomalyLog` dan menghasilkan analisis otomatis berbasis aturan:

```csharp
public interface IAnomalyAnalysisService
{
    AnomalyAnalysis Analyze(AnomalyLog log, IEnumerable<AnomalyLog> recentHistory);
    string RecommendAction(AnomalyLog log, AnomalyAnalysis analysis);
    string AssessSeverity(AnomalyLog log);
}
```

**Contoh rule analisis**:

| Kondisi | Root Cause | Severity | Recommended Action |
|---------|-----------|----------|---------------------|
| OVERLOAD + deviation > 50% + downtime | Daya menyala saat jam mati — kemungkinan relay tidak mati / beban tetap aktif | Critical | Periksa relay/jadwal downtime; verifikasi beban yang tetap aktif |
| OVERLOAD + deviation 30-50% | Beban melebihi threshold EMA/manual | High | Kurangi beban; tinjau kapasitas MCB / panel |
| OVERLOAD + deviation 10-30% | Peningkatan beban ringan | Medium | Pantau tren; lakukan audit perangkat |
| DROP saat downtime | Diharapkan (listrik sengaja dimatikan) | Low | Tidak perlu tindakan; tetap catat |
| DROP di luar downtime | Device offline / mati listrik / gangguan komunikasi | High | Periksa koneksi MQTT / power supply / MCB panel |
| Device yang sama > 3 anomaly dalam 24 jam | Pola berulang — perlu investigasi mendalam | Critical | Audit panel; periksa kualitas relay/beban/kabel |

#### 3.3.2 Endpoint API Baru

| Endpoint | Metode | Fungsi |
|----------|--------|--------|
| `/api/Api/anomaly-logs/{id}/analysis` | GET | Ambil detail + analisis satu anomali |
| `/api/Api/anomaly-logs/{id}/acknowledge` | POST | Tandai anomali sebagai acknowledged & catat user |
| `/api/Api/anomaly-logs/{id}/resolve` | POST | Tandai anomali sebagai resolved dengan catatan |
| `/api/Api/anomaly-logs/{id}/notes` | PUT | Update catatan/resolution notes |
| `/api/Api/anomaly-dashboard` | GET | Data ringkasan dashboard anomaly center |
| `/api/Api/anomaly-trends` | GET | Data tren anomali (hari/minggu/bulan) |
| `/api/Api/anomaly-monthly-report` | GET | Laporan bulanan berdasarkan query year/month |
| `/api/Api/anomaly-monthly-report/generate` | POST | Generate/refresh laporan bulanan |
| `/api/Api/anomaly-monthly-report/export/pdf` | GET | Export laporan ke PDF |

#### 3.3.3 Modifikasi LogAnomaly Endpoint

Saat menyimpan log baru, otomatis jalankan analisis dan simpan hasilnya:

```csharp
// Di ApiController.LogAnomaly
var analysis = _analysisService.Analyze(log, recentHistory);
var recommendation = _analysisService.RecommendAction(log, analysis);

log.Severity = analysis.Severity;
log.RootCause = analysis.RootCause;
log.RecommendedAction = recommendation;
```

#### 3.3.4 Chart Capture saat Anomali Terdeteksi

Saat anomali terkonfirmasi dan disimpan, sistem harus mengambil snapshot 50 titik data sebelum dan sesudah waktu deteksi. Alur lengkapnya:

1. **Trigger**: JavaScript di `_AnomalyService.cshtml` mendeteksi anomali setelah 3 sampel berturut-turut.
2. **Ambil data chart**: Lakukan request ke endpoint chart dengan `points=100` (atau nilai yang cukup untuk 50 sebelum + 50 sesudah + titik saat ini):
   ```
   GET /api/Api/panels/{deviceKey}/chart?points=100
   ```
3. **Kirim ke server**: Payload `LogAnomaly` ditambah field `chartSnapshot` yang berisi data sebelum dan sesudah anomali, plus nilai upper/lower threshold & EMA saat itu.
4. **Simpan snapshot**: Di server, simpan ke tabel `AnomalyChartSnapshot` dengan FK ke `AnomalyLog`.

**Contoh payload tambahan di `AnomalyLogRequest`**:

```csharp
public class AnomalyLogRequest
{
    public string DeviceKey { get; set; }
    public string DeviceId { get; set; }
    public string AnomalyType { get; set; }
    public decimal PowerValue { get; set; }
    public decimal ThresholdValue { get; set; }
    public decimal Deviation { get; set; }
    public decimal? EMAValue { get; set; }
    public string ThresholdMode { get; set; }

    // Chart snapshot data
    public ChartSnapshotData ChartSnapshot { get; set; }
}

public class ChartSnapshotData
{
    public List<ChartDataPoint> Before { get; set; }   // 50 titik sebelum anomali
    public List<ChartDataPoint> After { get; set; }    // 50 titik sesudah anomali
    public decimal UpperThreshold { get; set; }
    public decimal LowerThreshold { get; set; }
    public decimal? EMAValue { get; set; }
}
```

**Endpoint API tambahan**:

| Endpoint | Metode | Fungsi |
|----------|--------|--------|
| `/api/Api/anomaly-logs/{id}/chart-snapshot` | GET | Ambil snapshot chart milik satu anomali |

**Tantangan Teknis**: Pada saat anomali terdeteksi, browser baru memiliki 50 titik **sebelum** anomali, tetapi belum memiliki 50 titik **sesudah**. Solusinya:

1. Saat anomali terkonfirmasi, simpan dulu 50 titik sebelum anomali di memori (`anomalyChartBuffer`).
2. Lanjutkan pantauan dan kumpulkan 50 titik sesudah anomali.
3. Setelah 50 titik sesudah terkumpul (atau saat daya kembali normal / cooldown selesai), kirim snapshot lengkap ke server dengan PATCH/POST update.
4. Server mengupdate record `AnomalyChartSnapshot` yang sebelumnya hanya berisi `BeforeDataJson`.

**Fallback**: Jika browser di-reload sebelum 50 titik sesudah terkumpul, snapshot akan tetap menyimpan data sebelum saja; sistem tidak boleh menghalangi logging anomali.

**Catatan Penting**:
- Capture chart dilakukan di **client-side** karena data chart sudah tersedia di browser dan tidak selalu tersistensikan.
- Jika browser ter-closing atau network gagal saat pengiriman, sistem tetap menyimpan log anomali tanpa snapshot (fallback).
- Pastikan ukuran JSON tidak terlalu besar; gunakan kompresi atau batasi 50+50 titik.
- Untuk laporan bulanan, snapshot dapat digunakan untuk menampilkan mini chart per anomali.

### 3.4 Perubahan Frontend (AnomalyLogs.cshtml)

#### 3.4.1 Layout Baru

Ubah layout menjadi multi-section:

1. **Header & Controls**
   - Judul "Anomaly Information Center"
   - Filter tanggal, device, jenis anomali, severity
   - Tombol "Generate Monthly Report"
   - Toggle auto-refresh

2. **KPI Dashboard Cards**
   - Today's Anomalies
   - Critical Anomalies
   - Most Affected Device
   - Unresolved Anomalies

3. **Grafik Tren**
   - Grafik anomali per jam/hari (DevExtreme Chart)
   - Pie chart distribusi jenis anomali
   - Bar chart top affected devices

4. **Tabel Anomali**
   - Kolom baru: Severity, Status, Aksi (Detail / Acknowledge / Resolve)
   - Row expansion atau modal detail

5. **Panel Detail Anomali**
   - Info dasar (device, waktu, nilai daya, threshold, deviasi)
   - **Chart Snapshot: grafik 50 titik sebelum & sesudah anomali**
   - Analisis & Root Cause
   - Recommended Action
   - Riwayat anomali device yang sama
   - Form acknowledge / resolve / notes

6. **Tab Laporan Bulanan**
   - Selector bulan/tahun
   - Ringkasan bulanan
   - Grafik tren harian
   - Tabel rekomendasi
   - Tombol export/email

#### 3.4.2 Komponen UI yang Diperlukan

- DevExtreme DataGrid (sudah ada)
- DevExtreme Chart / PieChart / BarChart
- **Chart Snapshot di detail anomali**: render line chart dari `AnomalyChartSnapshot.BeforeDataJson` dan `AfterDataJson`
- Modal detail anomali
- Date picker untuk filter & laporan
- Badges severity dengan warna:
  - `critical` → merah gelap
  - `high` → merah
  - `medium` → kuning
  - `low` → biru/hijau

### 3.5 Laporan Bulanan Anomali

#### 3.5.1 Data yang Ditampilkan

- **Header**: Periode laporan (bulan/tahun), tanggal generate, total hari
- **Ringkasan**:
  - Total anomali
  - Overload vs Drop
  - Device terdampak
  - Device dengan anomali terbanyak
  - Rata-rata deviasi
  - Jumlah anomali critical/high/medium/low
- **Tren Harian**: grafik jumlah anomali per hari
- **Analisis per Kategori / Device**:
  - Tabel device dengan jumlah anomali
  - Jenis anomali dominan per device
  - Rekomendasi per device
- **Rekomendasi Umum**: 3-5 saran tindakan prioritas utama
- **Aksi**: Download PDF, Download CSV, Kirim Email

#### 3.5.2 Proses Generate Laporan

1. User memilih bulan & tahun.
2. Frontend memanggil `POST /api/Api/anomaly-monthly-report/generate`.
3. Backend mengambil semua `AnomalyLogs` dalam periode tersebut.
4. Backend menghitung statistik dan menghasilkan rekomendasi.
5. Hasil disimpan/dikembalikan sebagai DTO.
6. Frontend menampilkan laporan.
7. User dapat export/email.

### 3.6 Integrasi dengan Sistem yang Ada

#### 3.6.1 Deteksi Real-Time

- `_AnomalyService.cshtml` sudah mengirim data ke `log-anomaly`.
- Tidak perlu ubah logika deteksi, hanya perlu tambahkan payload opsional jika perlu.
- Endpoint `log-anomaly` otomatis menjalankan analisis setelah menyimpan log.

#### 3.6.2 Notifikasi Email/WhatsApp

- Laporan bulanan yang sudah ada di `NotificationService.SendRealtimeMonthlyReportAsync` dapat dimodifikasi agar menyertakan:
  - Ringkasan analisis anomali
  - Rekomendasi tindakan
  - Link ke Anomaly Information Center (jika aplikasi dapat diakses)
- Tombol "Kirim Laporan via Email" di UI dapat memanggil endpoint test/report yang sudah ada atau endpoint baru khusus laporan anomaly.

#### 3.6.3 Background Service

- `AnomalyNotificationBackgroundService` tetap mengirim laporan periodik.
- Pertimbangkan menambahkan laporan anomaly bulanan otomatis pada jadwal yang sama atau terpisah.

### 3.7 Keamanan & Audit

- Setiap aksi acknowledge/resolve harus dicatat ke `SecurityAuditLog`.
- Hanya user dengan role tertentu yang boleh menghapus log.
- Validasi input untuk ID dan deviceKey.

### 3.8 Role-Based Access Control (RBAC) untuk Anomaly Center

Berdasarkan role yang sudah ada, alokasikan hak akses anomaly center sebagai berikut:

#### 3.8.1 Hak Akses per Role

| Role | Hak Akses di Anomaly Information Center |
|------|----------------------------------------|
| **Viewer** | Melihat dashboard ringkasan anomali, tabel log, detail anomali, analisis, dan saran tindakan. Tidak boleh melakukan aksi (acknowledge, resolve, edit, hapus, generate laporan). |
| **Operator** | Semua hak Viewer, **plus** dapat: acknowledge anomali, menambahkan catatan tindakan, menandai resolved, membuat laporan bulanan, dan mengambil tindakan perbaikan. |
| **Admin** | Semua hak Operator, **plus** dapat: menghapus log anomali, mengelola pengaturan anomaly, menerima laporan tindakan Operator, melihat audit trail tindakan, dan mengelola user. |

#### 3.8.2 Penerapan Role di Backend (API Authorization)

Gunakan policy yang sudah ada di `Startup.cs` dengan `[Authorize(Policy = "...")]` pada controller atau action.

Contoh penerapan pada `ApiController` untuk endpoint anomaly:

```csharp
[Authorize(Policy = "RequireViewer")]
[HttpGet("anomaly-logs/summary")]
public async Task<IActionResult> GetAnomalyLogsSummary() { ... }

[Authorize(Policy = "RequireViewer")]
[HttpGet("anomaly-logs/{deviceKey}")]
public async Task<IActionResult> GetAnomalyLogs(...) { ... }

[Authorize(Policy = "RequireViewer")]
[HttpGet("anomaly-logs/{id}/analysis")]
public async Task<IActionResult> GetAnomalyAnalysis(long id) { ... }

[Authorize(Policy = "RequireOperator")]
[HttpPost("anomaly-logs/{id}/acknowledge")]
public async Task<IActionResult> AcknowledgeAnomaly(long id, [FromBody] AcknowledgeRequest request) { ... }

[Authorize(Policy = "RequireOperator")]
[HttpPost("anomaly-logs/{id}/resolve")]
public async Task<IActionResult> ResolveAnomaly(long id, [FromBody] ResolveRequest request) { ... }

[Authorize(Policy = "RequireOperator")]
[HttpPost("anomaly-monthly-report/generate")]
public async Task<IActionResult> GenerateMonthlyReport(...) { ... }

[Authorize(Policy = "RequireAdmin")]
[HttpDelete("anomaly-logs/{id}")]
public async Task<IActionResult> DeleteAnomalyLog(long id) { ... }

[Authorize(Policy = "RequireAdmin")]
[HttpDelete("anomaly-logs/clear-all")]
public async Task<IActionResult> ClearAllAnomalyLogs() { ... }
```

#### 3.8.3 Penerapan Role di Frontend (AnomalyLogs.cshtml)

Role user harus tersedia di view, misalnya melalui `ViewBag.CurrentUserRole` atau model:

```csharp
// Di MonitoringController.AnomalyLogs
ViewBag.CurrentUserRole = User.IsInRole(UserRoles.Admin) ? "Admin" :
                          User.IsInRole(UserRoles.Operator) ? "Operator" : "Viewer";
```

Kemudian di view/JS:

```javascript
var currentUserRole = '@ViewBag.CurrentUserRole';

function renderActionButtons(data) {
    if (currentUserRole === 'Viewer') {
        // Hanya tombol View Detail
        return '<button class="btn btn-sm btn-info" onclick="viewDetail(' + data.id + ')">View</button>';
    }

    var buttons = '<button class="btn btn-sm btn-info" onclick="viewDetail(' + data.id + ')">Detail</button>';

    if (currentUserRole === 'Operator' || currentUserRole === 'Admin') {
        if (!data.acknowledged) {
            buttons += ' <button class="btn btn-sm btn-warning" onclick="acknowledge(' + data.id + ')">Acknowledge</button>';
        }
        if (!data.isResolved) {
            buttons += ' <button class="btn btn-sm btn-success" onclick="resolve(' + data.id + ')">Resolve</button>';
        }
    }

    if (currentUserRole === 'Admin') {
        buttons += ' <button class="btn btn-sm btn-outline-danger" onclick="deleteLog(' + data.id + ')">Delete</button>';
    }

    return buttons;
}
```

#### 3.8.4 Kolom Tambahan untuk Melacak Tindakan Operator

Perluas `AnomalyLog` agar bisa melacak siapa yang menangani:

```csharp
public string AcknowledgedBy { get; set; }      // Email/Username Operator
public string ResolvedBy { get; set; }          // Email/Username Operator
public string OperatorAction { get; set; }      // Jenis tindakan yang diambil
public string OperatorNotes { get; set; }       // Catatan tindakan perbaikan
```

#### 3.8.5 Audit Trail & Laporan Admin

Setiap aksi Operator harus tercatat:

```csharp
await LogSecurityActionAsync(
    SecurityAction.AnomalyAcknowledged,   // atau AnomalyResolved, AnomalyActionTaken
    deviceKey: anomaly.DeviceKey,
    details: $"User {User.Identity.Name} acknowledged anomaly #{id}",
    success: true);
```

Endpoint khusus Admin untuk melihat laporan tindakan Operator:

| Endpoint | Metode | Fungsi |
|----------|--------|--------|
| `/api/Api/anomaly-operator-actions` | GET | Daftar semua aksi operator (acknowledge, resolve, notes) dalam periode tertentu |
| `/api/Api/anomaly-operator-actions/summary` | GET | Ringkasan jumlah tindakan per operator |
| `/api/Api/anomaly-operator-actions/{id}` | GET | Detail satu aksi operator |

Admin dapat menerima notifikasi/email berisi:
- Ringkasan tindakan operator hari ini/minggu ini/bulan ini
- Anomali yang belum di-resolve
- Anomali critical yang baru saja ditangani operator

#### 3.8.6 Notifikasi Role-Aware

- **Viewer**: hanya menerima notifikasi/informasi anomali (email/WhatsApp alert biasa).
- **Operator**: menerima alert anomali dan reminder jika ada anomali belum di-resolve.
- **Admin**: menerima alert anomali + laporan tindakan operator (daily/weekly summary).

Di `NotificationService`, tambahkan metode:

```csharp
Task SendOperatorActionSummaryAsync(DateTime date);     // Harian ke Admin
Task SendUnassignedAnomaliesReminderAsync();            // Reminder ke Operator
```

---

## 4. Rencana Implementasi (Urutan Langkah)

### Fase 1: Persiapan Model & Database

1. Update model `AnomalyLog` (tambah kolom severity, root cause, recommended action, resolved status, acknowledgedBy, resolvedBy, operatorAction, operatorNotes, dll).
2. Buat model `AnomalyAnalysis` (opsional jika ingin normalisasi).
3. Buat model `AnomalyMonthlyReport`.
4. **Buat model `AnomalyChartSnapshot` untuk menyimpan 50 titik sebelum & sesudah anomali.**
5. Pastikan enum/constant `SecurityAction` memiliki nilai baru: `AnomalyAcknowledged`, `AnomalyResolved`, `AnomalyActionTaken`.
6. Buat migration Entity Framework Core.
7. Update `ApplicationDbContext`.
8. Update DDL di `KwhMonitoringService.cs` (Listener) agar skema tabel konsisten.

### Fase 2: Logika Analisis & API

1. Buat `IAnomalyAnalysisService` dan implementasi `AnomalyAnalysisService`.
2. Modifikasi `ApiController.LogAnomaly` agar otomatis memanggil analysis service.
3. Tambahkan endpoint API baru (detail, acknowledge, resolve, dashboard, trends, monthly report, operator actions).
4. Terapkan `[Authorize(Policy = "RequireViewer/Operator/Admin")]` pada setiap endpoint anomaly sesuai hak akses.
5. Tambahkan validasi dan audit logging.

### Fase 3: Frontend Anomaly Information Center

1. Refactor `AnomalyLogs.cshtml` dengan layout baru (dashboard + grid + detail panel).
2. Integrasikan DevExtreme Chart untuk grafik tren dan distribusi.
3. Implementasi modal/panel detail anomali dengan analisis & rekomendasi.
4. Implementasi tombol aksi acknowledge/resolve dengan pengecekan role (Viewer hanya View, Operator/Admin bisa action).
5. **Tambahkan rendering chart snapshot (50 titik sebelum & sesudah) di panel detail anomali.**
6. Sediakan `ViewBag.CurrentUserRole` di `MonitoringController.AnomalyLogs`.
7. Implementasi tab laporan bulanan dengan selector periode dan export.
8. Tambahkan halaman/section "Operator Actions Audit" khusus Admin.

### Fase 4: Laporan Bulanan & Laporan Operator

1. Buat service pembangun laporan bulanan anomaly.
2. Integrasikan dengan UI (tab laporan bulanan).
3. Implementasi export PDF (gunakan library seperti iTextSharp / DinkToPDF / Rotativa) dan CSV.
4. Integrasikan pengiriman laporan via email/WhatsApp.
5. Buat laporan tindakan Operator untuk Admin (daily/weekly summary).
6. Implementasi notifikasi Admin saat Operator melakukan tindakan pada anomali.

### Fase 5: Testing & Deployment

1. Unit test untuk `AnomalyAnalysisService`.
2. Manual test alur deteksi → log → analisis → detail → acknowledge → resolve.
3. Manual test generate & export laporan bulanan.
4. Manual test skenario role: Viewer tidak bisa action, Operator bisa acknowledge/resolve, Admin bisa hapus log.
5. Update dokumentasi (README, env setup jika perlu).

---

## 5. Jaminan Sistem Berjalan Lancar (Reliability & Safety)

Agar sistem anomaly center berjalan tanpa masalah setelah implementasi, terapkan hal berikut:

### 5.1 Validasi & Error Handling

- Selalu bungkus query database dengan `try-catch` dan return response yang konsisten `{ success, data, error }`.
- Validasi `id`, `deviceKey`, dan input date sebelum diproses.
- Gunakan `CancellationToken` untuk operasi background agar tidak hang saat aplikasi di-stop.
- Tangani kasus null/empty untuk `AcknowledgedBy`, `ResolvedBy`, dan `OperatorNotes`.

### 5.2 Transaksi Database

- Untuk aksi yang melibatkan multiple query (acknowledge + audit log + notifikasi), gunakan `DbContext.Database.BeginTransactionAsync()` agar data tetap konsisten.
- Jika audit log gagal, pertimbangkan apakah aksi utama tetap boleh dilanjutkan (fail-safe: log utama tetap disimpan, audit error dicatat di log file).

### 5.3 Caching untuk Performa

- Gunakan `IMemoryCache` untuk menyimpan summary dashboard anomaly (TTL 30–60 detik) agar load halaman tidak selalu hit database.
- Cache daftar user/role jika diperlukan untuk rendering UI.

### 5.4 Audit & Monitoring

- Setiap aksi Operator (`acknowledge`, `resolve`, `add notes`) harus tercatat di `SecurityAuditLog`.
- Log error background service ke `ILogger` agar bisa ditelusuri.
- Buat health-check sederhana untuk memastikan API anomaly tetap responsif.

### 5.5 Testing Menyeluruh Sebelum Deploy

| Skenario | Ekspektasi |
|----------|------------|
| Viewer membuka Anomaly Logs | Dashboard & tabel tampil, tombol aksi tidak ada |
| Operator acknowledge anomali | Status berubah, `AcknowledgedBy` tercatat, audit log masuk, Admin terima notif |
| Operator resolve dengan catatan | Status resolved, `ResolvedBy` & `OperatorNotes` tersimpan |
| Admin menghapus log | Log terhapus, audit log masuk, summary terrefresh |
| Generate laporan bulanan | Laporan terbentuk, grafik tampil, export PDF/CSV berhasil |
| Background service laporan bulanan berjalan | Tidak ada duplikat pengiriman (cek `LastMonthlyReportSentDate`) |
| Deteksi anomali real-time tetap jalan | `_AnomalyService.cshtml` tetap memicu `log-anomaly` tanpa error |

### 5.6 Backup & Rollback

- Sebelum menerapkan migration baru, **backup database**.
- Jika terjadi error setelah deploy, siapkan rollback script untuk revert kolom baru jika diperlukan.
- Jalankan migration di environment staging sebelum production.

---

## 7. Risiko & Mitigasi

| Risiko | Mitigasi |
|--------|----------|
| Perubahan skema tabel memerlukan migrasi database yang hati-hati | Backup database sebelum migrasi; gunakan EF migrations |
| Analisis rule-based terlalu sederhana | Mulai dengan rule; siapkan ekstensi untuk machine learning di masa depan |
| Performa dashboard berat karena banyak data | Gunakan server-side aggregation; tambahkan caching (IMemoryCache) |
| Kompatibilitas dengan listener project | Update DDL di `KwhMonitoringService.cs` agar konsisten |

---

## 8. Catatan Tambahan

- Semua perubahan UI harus mengikuti **Bootstrap 5** dan **palet warna BS5** yang sudah distandarisasi (merah `#dc3545`, kuning `#ffc107`, hijau `#198754`, biru `#0d6efd`).
- Pertahankan fitur auto-refresh dan real-time yang sudah ada.
- Pertimbangkan untuk memanfaatkan `QwenChatController` atau service AI jika ingin analisis anomali yang lebih cerdas (natural language explanation) di masa depan.

---

## 9. Checklist Penerimaan (Acceptance Criteria)

- [ ] Halaman Anomaly Logs menampilkan dashboard ringkasan dengan KPI.
- [ ] Tabel anomali memiliki kolom Severity dan Status.
- [ ] Klik satu anomali menampilkan detail + analisis + saran tindakan + chart snapshot 50 titik sebelum & sesudah.
- [ ] User dapat acknowledge dan resolve anomali dengan catatan.
- [ ] Sistem otomatis memberikan severity dan rekomendasi saat log baru tercatat.
- [ ] Tab laporan bulanan dapat menampilkan ringkasan, tren, dan rekomendasi.
- [ ] Laporan bulanan dapat di-export ke PDF dan CSV.
- [ ] Notifikasi email/WhatsApp bulanan tetap berfungsi dan dapat menyertakan laporan anomaly.
- [ ] Viewer hanya dapat melihat informasi, tidak bisa melakukan aksi.
- [ ] Operator dapat acknowledge, resolve, dan menambahkan catatan tindakan.
- [ ] Admin dapat menghapus log dan melihat audit tindakan Operator.
- [ ] Setiap aksi Operator tercatat di `SecurityAuditLog`.
- [ ] Admin menerima notifikasi/laporan ringkasan tindakan Operator.
- [ ] Build & migration berhasil tanpa error.

---

*Dokumen ini akan menjadi panduan utama untuk implementasi perubahan sistem anomaly detection menjadi Anomaly Information Center.*
