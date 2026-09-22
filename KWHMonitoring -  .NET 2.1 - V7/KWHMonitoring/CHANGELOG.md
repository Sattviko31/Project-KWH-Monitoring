# KWH Monitoring — Per-Device Settings Architecture

## Tanggal: 2025-09-21
## Status: Completed

---

## Daftar Isi

1. [Gambaran Umum](#gambaran-umum)
2. [Fitur Baru](#fitur-baru)
3. [Perubahan Arsitektur](#perubahan-arsitektur)
4. [Perubahan File Backend](#perubahan-file-backend)
5. [Perubahan File Frontend](#perubahan-file-frontend)
6. [Bug Fix](#bug-fix)
7. [Data Flow](#data-flow)
8. [Breaking Changes](#breaking-changes)
9. [Catatan Deployment](#catatan-deployment)

---

## Gambaran Umum

Sistem KWH Monitoring telah direfactor dari **pengaturan global bersama** (shared global settings) menjadi **pengaturan per-device** (per-device settings). Setiap device kini memiliki konfigurasi independen yang disimpan di tabel `DeviceSettings` dan diakses melalui API `/api/Api/device-settings`.

Semua halaman (Panel Monitoring, All Charts, Detail Chart, Usage Statistics, Anomaly Logs, Anomaly Detection) kini menggunakan nilai per-device untuk:
- Max Capacity (kapasitas maksimum daya)
- EMA Upper/Lower Threshold (garis threshold anomali)
- EMA Fibonacci Upper/Lower (mode fibonacci)
- Load Normal/Medium Threshold (indikator load gauge)
- Control Mode (OnOff / Pulse / Toggle / Toggle2Relay / ToggleAll)
- Downtime Settings (jadwal downtime per device)
- Device Category (kategori device: Billboard, Videotron, LED, dll)
- Tariff Per KWh (tarif listrik per device)

---

## Fitur Baru

### 1. Tabel `DeviceSettings` (Database)
- Tabel baru untuk menyimpan pengaturan per device
- Migrasi: `20260921120000_AddDeviceSettings.cs`
- Kolom: `DeviceKey`, `MaxCapacity`, `DeviceCategory`, `DowntimeEnabled`, `DowntimeStart`, `DowntimeEnd`, `TariffPerKWh`, `LoadNormalThreshold`, `LoadMediumThreshold`, `EmaUpperThreshold`, `EmaLowerThreshold`, `EmaFibUpper`, `EmaFibLower`, `ControlMode`, `CreatedAt`, `UpdatedAt`

### 2. Service Layer: `DeviceSettingsService`
- `GetEffectiveAsync(deviceKey)` — ambil pengaturan efektif untuk satu device
- `GetAllEffectiveAsync()` — ambil semua pengaturan device sebagai dictionary
- `GetOrCreateAsync(deviceKey)` — ambil atau buat jika belum ada
- `SaveAsync(deviceKey, settings)` — simpan pengaturan device
- Normalisasi default: hanya untuk field yang memerlukan fallback (kategori, tarif, load threshold, control mode). **MaxCapacity dan EMA threshold tidak di-override — nilai 0 berarti "tidak dikonfigurasi"**

### 3. API Endpoints (ApiController)
- `GET /api/Api/device-settings` — semua device settings
- `GET /api/Api/device-settings/{deviceKey}` — satu device settings
- `POST /api/Api/device-settings/{deviceKey}` — simpan satu device
- `POST /api/Api/device-settings/bulk` — simpan bulk

### 4. Device Settings Tab (`_DeviceSettingsTab.cshtml`)
- Tab baru di halaman Settings untuk mengelola konfigurasi per device
- Form edit per device: MaxCapacity, DeviceCategory, Downtime, Tariff, Load Thresholds, EMA Thresholds, ControlMode
- Simpan per device atau bulk

### 5. Conditional Chart Lines (EMA Threshold & Max Capacity)
- Garis EMA Upper/Lower **hanya ditampilkan** jika nilai > 0 di per-device settings
- Garis Max Capacity **hanya ditampilkan** jika MaxCapacity > 0
- Jika nilai 0: garis disembunyikan (`borderColor: 'transparent'`, data kosong)

### 6. Conditional Load Gauge
- Jika MaxCapacity > 0: tampilkan persentase load dengan warna berdasarkan threshold
- Jika MaxCapacity = 0: tampilkan `"-"` dengan warna abu-abu (`bg-secondary`)

### 7. Anomaly Detection Per-Device
- Anomaly detection menggunakan EMA Upper/Lower Threshold dari per-device settings
- Jika kedua threshold = 0: anomaly detection **dilewati sepenuhnya** untuk device tersebut
- Tidak ada lagi fallback ke default global (30%/50%)

---

## Perubahan Arsitektur

### Sebelum (Global Settings)
```
┌──────────────┐
│  AppSettings │ (global, satu untuk semua device)
│  maxCapacity: 30000W  │
│  emaUpper: 30%        │
│  emaLower: 50%        │
└──────┬───────┘
       │ (shared untuk semua device)
       ▼
┌──────────────────────────────────┐
│  Charts / Details / Index        │
│  Semua device pakai nilai sama   │
└──────────────────────────────────┘
```

### Sesudah (Per-Device Settings)
```
┌──────────────────┐     ┌──────────────────┐
│  DeviceSettings  │     │  AppSettings     │ (global fallback)
│  DeviceKey: 122  │     │  LoadNormal: 30  │
│  MaxCapacity: 40k│     │  LoadMedium: 70  │
│  EmaUpper: 40%   │     │  Tariff: 1500    │
│  EmaLower: 30%   │     │  ControlMode: OnOff
│  ...             │     └──────────────────┘
└────────┬─────────┘
         │ (per-device via API)
         ▼
┌──────────────────────────────────────┐
│  _Layout.cshtml                      │
│  window.getDeviceSettings(deviceKey) │
│  window._deviceSettingsPromise       │
└────────┬─────────────────────────────┘
         │
    ┌────┴────┬──────────┬───────────┬──────────┐
    ▼         ▼          ▼           ▼          ▼
 Index    Charts    Details    UsageStats  AnomalyLogs
```

---

## Perubahan File Backend

### `Models/DeviceSettings.cs` (BARU)
```csharp
// Default values diubah dari hardcoded ke 0 (not configured):
MaxCapacity      = 0m       // was 30000m
EmaUpperThreshold = 0       // was 30
EmaLowerThreshold = 0       // was 50
EmaFibUpper       = 0       // was 1.618
EmaFibLower       = 0       // was 0.618
```

### `Models/AppSettings.cs`
```csharp
// Global EMA defaults diubah ke 0:
EmaUpperThreshold = 0       // was 30
EmaLowerThreshold = 0       // was 50
EmaFibUpper       = 0       // was 1.618
EmaFibLower       = 0       // was 0.618
```

### `Models/PanelViewModel.cs`
```csharp
// Default MaxCapacity diubah:
MaxCapacity = 0m            // was 30000m

// Guard divide-by-zero ditambahkan:
GetStatus()      → if (MaxCapacity <= 0) return "NORMAL";
GetStatusColor() → if (MaxCapacity <= 0) return "success";
GetLoadColor()   → if (MaxCapacity <= 0) return "secondary";
```

### `Models/KWHData.cs`
```csharp
// GetStatus/GetStatusColor diubah menjadi stub (real computation di controller)
// Sebelumnya: const maxCapacity = 30000m (hardcoded)
// Sekarang: return "NORMAL" / "success" sebagai fallback
```

### `Services/DeviceSettingsService.cs` (BARU)
- **Dihapus**: `if (MaxCapacity <= 0) MaxCapacity = 30000m` override
- Nilai 0 untuk MaxCapacity/EMA dipertahankan sebagai "tidak dikonfigurasi"
- Normalisasi tetap ada untuk: `DeviceCategory`, `TariffPerKWh`, `LoadNormalThreshold`, `LoadMediumThreshold`, `ControlMode`

### `Services/DbInitializer.cs`
- **Seed defaults diubah**: MaxCapacity=0, EmaUpper=0, EmaLower=0, EmaFibUpper=0, EmaFibLower=0
- Seed hanya untuk device yang belum punya row di `DeviceSettings`

### `Controllers/ApiController.cs`
- **`GetEmaSettings()`**: Default EMA diubah dari 30/50/1.618/0.618 ke 0
- **`EmaSettingsData`** (request model): Default diubah ke 0
- **`GetPanels()`**: Status panel dihitung menggunakan `GetDeviceStatus()` dengan per-device settings, bukan hardcoded maxCapacity=30000
- **`GetDeviceStatus()`** (helper baru): Hitung status per-device berdasarkan MaxCapacity dan Load Thresholds

### `Controllers/MonitoringController.cs`
- Semua referensi `settings?.MaxCapacity ?? 30000m` diubah ke `settings?.MaxCapacity ?? 0m`
- `GetEmaSettings()` di MonitoringController juga diubah default EMA ke 0

---

## Perubahan File Frontend

### `Views/Shared/_Layout.cshtml`
- **BARU**: `window._deviceSettingsPromise` — Promise untuk memastikan device settings sudah dimuat sebelum chart/render
- **BARU**: `window.getDeviceSettings(deviceKey)` — ambil per-device settings, return `null` jika device tidak punya row
- **BARU**: `window.getDeviceSetting(deviceKey, field, defaultValue)` — ambil single field dengan fallback
- `fetchDeviceSettings()`: Load dari `/api/Api/device-settings`, simpan ke `window._deviceSettings`
- **FIX**: `.catch()` → `.fail()` (jQuery jqXHR tidak punya `.catch()`)

### `Views/Monitoring/Index.cshtml`
- **3 lokasi divide-by-zero fix**: `Math.min((dayaWatt / maxCap) * 100, 100)` → `maxCap > 0 ? Math.min(...) : 0`
- `applyPanelFilter()` (~line 690): Guard status filter saat maxCap=0
- `syncProgressBars()` (~line 1175): Load gauge tampilkan "-" dengan `bg-secondary` saat maxCap=0
- `updatePanelCards()` (~line 1462): Sama seperti syncProgressBars
- Semua `getDeviceSettings()` fallback diubah dari `{ maxCapacity: 30000 }` ke `{ maxCapacity: 0 }`
- **Timing fix**: `Promise.resolve(window._deviceSettingsPromise)` ditambahkan ke `Promise.all()`

### `Views/Monitoring/Charts.cshtml`
- `loadChartData()`: EMA threshold dan Max Capacity line menggunakan per-device settings
- Garis EMA Upper: `borderColor: 'transparent'` + data kosong jika `upperValue <= 0`
- Garis EMA Lower: Sama, plus cek downtime
- Garis Max Capacity: Tampilkan hanya jika `mcValue > 0`, label "Max Capacity (-)" jika 0
- `loadEmaSettings()`: Global EMA defaults diubah ke 0
- Form save: EMA defaults diubah ke 0
- **Timing fix**: `Promise.resolve(window._deviceSettingsPromise)` ditambahkan ke `Promise.all()`

### `Views/Monitoring/Details.cshtml`
- `loadDetailChartData()`: Sama seperti Charts.cshtml untuk EMA dan Max Capacity
- `updatePanelMonitoring()`: Fallback `{ maxCapacity: 0 }`, load gauge "-" saat maxCap=0
- `systemSettings`: Default `maxCapacity: 0`
- Form save: EMA defaults diubah ke 0
- **FIX**: `detailAnomalyGrid.getDataSource()` → `$('#detailAnomalyGrid').dxDataGrid('instance').getDataSource()`
- **Timing fix**: `Promise.resolve(window._deviceSettingsPromise)` ditambahkan

### `Views/Shared/_AnomalyService.cshtml`
- `processAnomalyCheck()`: Early return jika `upperValue <= 0 && lowerValue <= 0` (skip anomaly detection untuk device yang tidak dikonfigurasi)
- Fallback `{ emaUpperThreshold: 30, emaLowerThreshold: 50 }` diubah ke `{}`
- Global EMA defaults diubah ke 0
- **Timing fix**: `Promise.resolve(window._deviceSettingsPromise)` ditambahkan

### `Views/Monitoring/_DeviceSettingsTab.cshtml` (BARU)
- Tab baru untuk manajemen pengaturan per device
- `collectDeviceSettings()`: Semua defaults diubah ke 0
- Simpan per device dan bulk

### `Views/Monitoring/Settings.cshtml`
- Diringkas dari ~959 baris menjadi lebih sederhana
- Fokus pada global settings (EMA period, mode, refresh interval) dan Device Settings tab

### `Views/Monitoring/_PanelCard.cshtml`
- Penyesuaian minor untuk konsistensi

### `wwwroot/css/site.css` & `wwwroot/js/site.js`
- Penyesuaian minor

---

## Bug Fix

### 1. Timing Race Condition
**Masalah**: Charts/Details/Index/AnomalyService diinisialisasi sebelum `window._deviceSettingsPromise` selesai → `getDeviceSettings()` return null → fallback ke hardcoded defaults (30000W, Lower 50%).

**Fix**: `Promise.resolve(window._deviceSettingsPromise)` ditambahkan ke semua `Promise.all()` chain di:
- `Index.cshtml` `$(document).ready()`
- `Charts.cshtml` init
- `Details.cshtml` init
- `_AnomalyService.cshtml` init

### 2. jQuery `.catch()` Error
**Masalah**: `$.ajax().then().catch()` → `catch is not a function` di semua halaman.

**Fix**: `.catch()` diganti `.fail()` di `fetchDeviceSettings()`.

### 3. DevExtreme Instance Error
**Masalah**: `detailAnomalyGrid.getDataSource()` → `getDataSource is not a function` karena `$('#detailAnomalyGrid').dxDataGrid()` return jQuery object, bukan widget instance.

**Fix**: Gunakan `$('#detailAnomalyGrid').dxDataGrid('instance').getDataSource().reload()`.

### 4. Divide-by-Zero (3 lokasi di Index.cshtml)
**Masalah**: `Math.min((dayaWatt / maxCap) * 100, 100)` → `Infinity` saat `maxCap = 0`.

**Fix**: `maxCap > 0 ? Math.min((dayaWatt / maxCap) * 100, 100) : 0`

### 5. Backend Override Nilai 0
**Masalah**: `DeviceSettingsService.GetEffectiveAsync()` dan `GetAllEffectiveAsync()` meng-override `MaxCapacity = 30000m` saat nilai 0, sehingga frontend tidak bisa membedakan "tidak dikonfigurasi" vs "default".

**Fix**: Override dihapus. Nilai 0 dipertahankan sebagai "tidak dikonfigurasi".

### 6. Status Panel Hardcoded
**Masalah**: API `/api/Api/panels` menggunakan `KWHData.GetStatus()` dengan hardcoded `maxCapacity = 30000m`.

**Fix**: API endpoint kini menggunakan `GetDeviceStatus()` helper yang menghitung status berdasarkan per-device settings.

---

## Data Flow

### Load Gauge (Index.cshtml, Details.cshtml)
```
DB DeviceSettings
    ↓
GET /api/Api/device-settings
    ↓
window._deviceSettings[deviceKey]
    ↓
window.getDeviceSettings(deviceKey)
    ↓
maxCap = ds.maxCapacity || 0
    ↓
if maxCap > 0: loadPercent = Math.min((dayaWatt / maxCap) * 100, 100)
if maxCap = 0: tampilkan "-" dengan bg-secondary
```

### Chart EMA Lines (Charts.cshtml, Details.cshtml)
```
DB DeviceSettings
    ↓
GET /api/Api/device-settings
    ↓
window.getDeviceSettings(deviceKey)
    ↓
upperValue = ds.emaUpperThreshold || 0
lowerValue = ds.emaLowerThreshold || 0
    ↓
if upperValue > 0: tampilkan garis upper
if lowerValue > 0 AND not downtime: tampilkan garis lower
if keduanya 0: sembunyikan garis threshold
```

### Chart Max Capacity Line (Charts.cshtml, Details.cshtml)
```
DB DeviceSettings
    ↓
GET /api/Api/device-settings
    ↓
window.getDeviceSettings(deviceKey)
    ↓
mcValue = ds.maxCapacity || 0
    ↓
if mcValue > 0: tampilkan garis horizontal "Max Capacity (X W)"
if mcValue = 0: label "Max Capacity (-)", borderColor transparent
```

### Anomaly Detection (_AnomalyService.cshtml)
```
DB DeviceSettings
    ↓
GET /api/Api/device-settings
    ↓
window.getDeviceSettings(deviceKey)
    ↓
upperValue = ds.emaUpperThreshold || 0
lowerValue = ds.emaLowerThreshold || 0
    ↓
if upperValue <= 0 AND lowerValue <= 0: SKIP anomaly detection
if salah satu > 0: jalankan anomaly detection dengan nilai per-device
```

---

## Breaking Changes

| Item | Sebelum | Sesudah |
|------|---------|---------|
| MaxCapacity default | 30000W | 0 (not configured) |
| EMA Upper default | 30% | 0 (hidden) |
| EMA Lower default | 50% | 0 (hidden) |
| EMA Fib Upper default | 1.618 | 0 (hidden) |
| EMA Fib Lower default | 0.618 | 0 (hidden) |
| Load Gauge (no config) | Tampilkan 30000W | Tampilkan "-" |
| Panel Status (no config) | Hitung vs 30000W | "NORMAL" |
| Anomaly Detection (no config) | Fallback 30%/50% | Skip sepenuhnya |
| Chart Max Capacity | Selalu tampil | Hidden jika 0 |
| Chart EMA Lines | Selalu tampil | Hidden jika 0 |

---

## Catatan Deployment

1. **Database Migration**: Jalankan migrasi `20260921120000_AddDeviceSettings` untuk membuat tabel `DeviceSettings`
2. **Existing Data**: Device yang sudah memiliki row di `DeviceSettings` dengan nilai `MaxCapacity=30000` akan tetap menggunakan nilai tersebut sampai diubah manual. Untuk menerapkan "not configured" (0), ubah nilai MaxCapacity ke 0 di halaman Settings
3. **New Devices**: Device baru akan di-seed dengan MaxCapacity=0 dan EMA thresholds=0 secara otomatis
4. **Restart Required**: Aplikasi harus di-restart agar semua perubahan berlaku (build artifacts dan cache)
5. **Browser Cache**: Clear browser cache (Ctrl+Shift+R) untuk memastikan JS/CSS terbaru dimuat

---

## File yang Diubah (19 modified + 5 new)

### Modified
| File | Perubahan |
|------|-----------|
| `Controllers/ApiController.cs` | +device-settings endpoints, GetDeviceStatus(), EMA defaults |
| `Controllers/MonitoringController.cs` | Per-device MaxCapacity, EMA defaults |
| `Migrations/ApplicationDbContextModelSnapshot.cs` | DeviceSettings table |
| `Models/AppSettings.cs` | EMA defaults → 0 |
| `Models/ApplicationDbContext.cs` | DeviceSettings DbSet |
| `Models/KWHData.cs` | Status fallback → NORMAL/success |
| `Models/PanelViewModel.cs` | MaxCapacity=0, divide-by-zero guard |
| `Services/DbInitializer.cs` | Seed defaults → 0 |
| `Startup.cs` | DeviceSettingsService DI registration |
| `Views/Account/Login.cshtml` | Minor adjustment |
| `Views/Monitoring/Charts.cshtml` | Per-device EMA/MaxCapacity lines |
| `Views/Monitoring/Details.cshtml` | Per-device EMA/MaxCapacity, grid fix |
| `Views/Monitoring/Index.cshtml` | Divide-by-zero fix, timing fix |
| `Views/Monitoring/Settings.cshtml` | Simplified, Device Settings tab |
| `Views/Monitoring/_PanelCard.cshtml` | Minor adjustment |
| `Views/Shared/_AnomalyService.cshtml` | Per-device thresholds, timing fix |
| `Views/Shared/_Layout.cshtml` | fetchDeviceSettings(), getDeviceSettings() |
| `wwwroot/css/site.css` | Minor styles |
| `wwwroot/js/site.js` | Minor scripts |

### New
| File | Deskripsi |
|------|-----------|
| `Migrations/20260921120000_AddDeviceSettings.cs` | Migration untuk tabel DeviceSettings |
| `Migrations/20260921120000_AddDeviceSettings.Designer.cs` | Migration designer |
| `Models/DeviceSettings.cs` | Entity model per-device settings |
| `Services/DeviceSettingsService.cs` | Service layer per-device settings |
| `Views/Monitoring/_DeviceSettingsTab.cshtml` | UI tab pengaturan per device |
