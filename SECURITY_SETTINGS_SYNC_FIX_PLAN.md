# Rencana Perbaikan: Sinkronisasi Settings Per Device & Keamanan

## Tanggal: 2026-09-23

## Status: 🔴 PLANNING — Menunggu Persetujuan

---

## Daftar Isi
1. [Kondisi Saat Ini (BEFORE)](#1-kondisi-saat-ini-before)
2. [Rencana Pengerjaan](#2-rencana-pengerjaan)
3. [Detail Per Phase](#3-detail-per-phase)
4. [File yang Akan Diubah](#4-file-yang-akan-diubah)
5. [Risiko & Mitigasi](#5-risiko--mitigasi)
6. [Target Kondisi (AFTER)](#6-target-kondisi-after)

---

## 1. Kondisi Saat Ini (BEFORE)

### A. Sinkronisasi Settings Per Device

| Masalah | Dampak |
|---------|--------|
| `NotificationService` baca threshold & tarif dari `AppSettingsRecord` (global), bukan `DeviceSettings` (per-device) | Notifikasi anomali pakai threshold lama — bisa false positive/negative |
| Field overlapping (MaxCapacity, LoadThresholds, EMA, Tariff, ControlMode) tidak disinkronkan antara `AppSettingsRecord` ↔ `DeviceSettings` | Perubahan di System Settings tab tidak tercermin di per-device, dan sebaliknya |
| `window._deviceSettings` di-load sekali saat page load, tidak di-refresh | Perubahan EMA threshold di Settings tab tidak langsung efek ke anomaly detection — perlu reload page |
| EMA threshold default = 0 → anomaly detection di-skip untuk device baru | Device baru tidak punya anomaly detection sampai admin konfigurasi manual |
| EMA mode Fibonacci di-hardcode `'manual'` di client — `EmaFibUpper/EmaFibLower` di model tidak digunakan | Field Fibonacci di DeviceSettings menjadi dead code |
| `BulkSaveDeviceSettings` tanpa transaction scope — concurrent bulk-save bisa last-write-wins | Race condition saat multiple admin save bersamaan |
| `NotificationService._settings` di-cache di memory tanpa auto-refresh | Perubahan SMTP/WhatsApp/toggle notifikasi via Settings page tidak langsung efek |

### B. Keamanan

| Masalah | Severity | Dampak |
|---------|----------|--------|
| `GET /api/get-notification-settings` — kembalikan SMTP password tanpa auth | 🔴 KRITIS | Kredensial SMTP bocor ke anonymous |
| `GET /api/chatbot-settings` — kembalikan API key ter-decrypt tanpa auth | 🔴 KRITIS | API key AI bocor ke anonymous |
| `GET /api/get-system-settings` — kembalikan semua settings tanpa auth | 🔴 KRITIS | Config sensitif terekspos |
| `POST /api/test-database-connection` — arbitrary SQL Server connection tanpa auth | 🔴 KRITIS | Network recon, brute-force, data exfiltration |
| ~15 write endpoint tanpa `[Authorize]` (save-tariff, save-ema, save-downtime, dll) | 🟠 TINGGI | Anonymous bisa manipulasi konfigurasi sistem |
| `QwenChatController` tanpa `[Authorize]` | 🟠 TINGGI | Anonymous bisa pakai AI quota, ekstrak data |
| SMTP password disimpan plaintext di DB & ter-log | 🟡 SEDANG | Kredensial terekspos di log & DB |
| Exception messages (`ex.Message`) dikembalikan ke client (~50+ instance) | 🟡 SEDANG | Information disclosure — reveal internal detail |
| `CookiePolicyOptions.MinimumSameSitePolicy = SameSiteMode.None` | 🟡 SEDANG | Melemahkan CSRF protection untuk non-auth cookies |
| AesEncryptionService IV deterministik | 🟢 RENDAH | Identik plaintext → identik ciphertext |
| Mayoritas POST/PUT/DELETE API tanpa `[ValidateAntiForgeryToken]` | 🟢 RENDAH | CSRF risk |

### C. Logika Anomaly & Monitoring

| Masalah | Dampak |
|---------|--------|
| Anomaly detection 100% client-side — jika tidak ada browser terbuka, anomali tidak terdeteksi | Gap arsitektural — tidak ada server-side detection |
| Multi-tab menjalankan deteksi independen — state counter/cooldown bisa drift antar tab | Duplikasi effort, state inconsistency |
| `AnomalyAlert.Active.{deviceKey}` dedup flag tidak auto-expire | Jika tidak ada browser setelah anomali, flag tetap aktif selamanya |
| State anomali (confirmationCounts, cooldownState) disimpan sebagai JSON string di AppSettings — concurrent write = lost-update | State bisa corrupt/overwrite |
| Downtime check client vs server baca dari sumber berbeda — bisa tidak sepakat | Client anggap bukan downtime, server anggap downtime (atau sebaliknya) |

---

## 2. Rencana Pengerjaan

Dibagi menjadi **5 Phase** berdasarkan prioritas dan ketergantungan:

```
Phase 1: KEAMANAN KRITIS (est. 1-2 jam)
  ├── Task 1.1  Tambah [Authorize] endpoint kredensial
  ├── Task 1.2  Tambah [Authorize] test-database-connection
  ├── Task 1.3  Tambah [Authorize] write endpoints
  └── Task 1.4  Tambah [Authorize] QwenChatController

Phase 2: KEAMANAN SEDANG (est. 1-2 jam)
  ├── Task 2.1  Encrypt SMTP password di DB
  ├── Task 2.2  Hapus password dari log output
  ├── Task 2.3  Generic error message (bukan ex.Message)
  └── Task 2.4  Fix Cookie SameSite policy

Phase 3: SINKRONISASI SETTINGS PER DEVICE (est. 2-3 jam)
  ├── Task 3.1  NotificationService baca threshold dari DeviceSettings
  ├── Task 3.2  NotificationService baca tarif dari DeviceSettings
  ├── Task 3.3  Sync field overlapping AppSettingsRecord ↔ DeviceSettings
  ├── Task 3.4  Refresh window._deviceSettings secara periodik
  └── Task 3.5  NotificationService auto-reload settings

Phase 4: LOGIKA ANOMALY & MONITORING (est. 2-3 jam)
  ├── Task 4.1  Auto-expire AnomalyAlert.Active dedup flag
  ├── Task 4.2  Fix downtime check consistency client vs server
  ├── Task 4.3  Bulk save dengan transaction scope
  ├── Task 4.4  Handle EMA threshold 0 — fallback ke global settings
  └── Task 4.5  Dead code cleanup — Fibonacci fields / mode

Phase 5: PENGUATAN TAMBAHAN (est. 1-2 jam)
  ├── Task 5.1  AesEncryptionService random IV
  ├── Task 5.2  Tambah ValidateAntiForgeryToken pada write endpoints
  ├── Task 5.3  OTP comparison constant-time
  └── Task 5.4  Password hasher constant-time compare
```

**Total estimasi: 7-12 jam kerja**

---

## 3. Detail Per Phase

### Phase 1: KEAMANAN KRITIS 🔴
> Tujuan: Menutup semua endpoint yang mengekspos kredensial atau mengizinkan write tanpa auth

#### Task 1.1 — Tambah `[Authorize]` pada Endpoint Kredensial
**File:** `Controllers/ApiController.cs`

| Endpoint | Aksi | Attribute |
|----------|------|-----------|
| `GET /api/get-notification-settings` | Tambah auth, mask password di response | `[Authorize(Roles = "Admin")]` + filter `senderPassword` dari response |
| `GET /api/chatbot-settings` | Tambah auth | `[Authorize(Roles = "Admin")]` |
| `GET /api/get-system-settings` | Tambah auth | `[Authorize(Roles = "Admin")]` |
| `GET /api/debug/notification-settings-db` | Sudah ada auth? Verifikasi | Verifikasi `[Authorize(Policy = "RequireAdmin")]` |

**Catatan:** Endpoint `get-notification-settings` dipanggil oleh `_AnomalyService.cshtml` (client-side) saat load settings anomaly. Jika ditambah auth, client-side perlu adjustment — anomaly service harus handle 401 gracefully. Alternatif: buat endpoint baru `/api/notification-settings-public` yang hanya return field non-sensitif (`checkInterval`, `maxConfirmations`, `cooldownTime`, dll) tanpa `senderPassword`.

#### Task 1.2 — Tambah `[Authorize]` pada test-database-connection
**File:** `Controllers/ApiController.cs`
- Tambah `[Authorize(Roles = "Admin")]` pada `TestDatabaseConnection`
- Pertimbangkan juga rate limiting (mis. 3 request per menit)

#### Task 1.3 — Tambah `[Authorize]` pada Write Endpoints
**File:** `Controllers/ApiController.cs`

| Endpoint | Attribute |
|----------|-----------|
| `POST /api/save-tariff` | `[Authorize(Roles = "Admin")]` |
| `POST /api/save-ema-settings` | `[Authorize(Roles = "Admin")]` |
| `POST /api/save-downtime-settings` | `[Authorize(Roles = "Admin")]` |
| `POST /api/save-anomaly-settings` | `[Authorize(Roles = "Admin")]` |
| `POST /api/save-anomaly-state` | `[Authorize(Roles = "Operator,Admin")]` |
| `POST /api/log-anomaly` | `[Authorize(Roles = "Operator,Admin")]` |
| `POST /api/reset-anomaly-alert` | `[Authorize(Roles = "Operator,Admin")]` |
| `POST /api/anomaly-logs/{id}/chart-snapshot` | `[Authorize(Roles = "Operator,Admin")]` |
| `POST /api/device-category` | `[Authorize(Roles = "Admin")]` |
| `POST /api/categories` | `[Authorize(Roles = "Admin")]` |
| `PUT /api/categories/{name}` | `[Authorize(Roles = "Admin")]` |
| `DELETE /api/categories/{name}` | `[Authorize(Roles = "Admin")]` |

**Catatan:** `log-anomaly`, `save-anomaly-state`, `reset-anomaly-alert` dipanggil oleh client-side anomaly service. Perlu pastikan user login dengan role Operator/Admin. Jika ada skenario anonymous monitoring, perlu diskusi.

#### Task 1.4 — Tambah `[Authorize]` pada QwenChatController
**File:** `Controllers/QwenChatController.cs`
- Tambah `[Authorize]` class-level
- `POST /api/qwenchat/send` → `[Authorize(Roles = "Operator,Admin")]`
- `GET /api/qwenchat/current-dashboard-data` → `[Authorize(Roles = "Operator,Admin")]`

---

### Phase 2: KEAMANAN SEDANG 🟡
> Tujuan: Menghentikan kebocoran kredensial di storage & log, menutup information disclosure

#### Task 2.1 — Encrypt SMTP Password di DB
**File:** `Services/NotificationService.cs`, `Controllers/ApiController.cs`, `Services/AesEncryptionService.cs`
- Saat save notification settings: encrypt `senderPassword` via `AesEncryptionService.Encrypt()`
- Saat read notification settings: decrypt via `AesEncryptionService.Decrypt()`
- Simpan encrypted value di `AppSettingsRecord` dengan prefix marker (mis. `ENC:`) untuk backward compat
- Handle migrasi: jika value tidak diawali `ENC:`, anggap plaintext (baca langsung), lalu re-encrypt saat next save

#### Task 2.2 — Hapus Password dari Log Output
**File:** `Controllers/ApiController.cs`
- Cari semua `_logger.LogInformation` yang log value settings
- Filter key sensitif (`SenderPassword`, `ApiKey`, dll) — ganti value dengan `***`
- Contoh: `_logger.LogInformation("[SAVE-NOTIF] Updated key: {0} = {1}", key, IsSensitiveKey(key) ? "***" : value)`

#### Task 2.3 — Generic Error Message ke Client
**File:** `Controllers/ApiController.cs`
- Buat helper method `SafeErrorMessage(ex)` yang return "Terjadi kesalahan internal. Silakan hubungi administrator."
- Log exception detail ke server (`_logger.LogError(ex, ...)`)
- Ganti semua `return StatusCode(500, new { error = ex.Message })` → `return StatusCode(500, new { error = SafeErrorMessage() })`
- Untuk development mode, bisa tetap tampilkan detail via `IHostingEnvironment.IsDevelopment()`

#### Task 2.4 — Fix Cookie SameSite Policy
**File:** `Startup.cs`
- Ubah `MinimumSameSitePolicy = SameSiteMode.None` → `SameSiteMode.Lax`
- Verifikasi tidak ada breakage pada cross-site flows (seharusnya tidak ada karena auth cookie sudah Lax)

---

### Phase 3: SINKRONISASI SETTINGS PER DEVICE 🟠
> Tujuan: Memastikan perubahan settings per device tercermin ke seluruh komponen sistem

#### Task 3.1 — NotificationService Baca Threshold dari DeviceSettings
**File:** `Services/NotificationService.cs`
- Ubah `LoadThresholdsAsync()` untuk membaca per-device threshold dari `DeviceSettingsService`
- Jika device punya `MaxCapacity > 0`, gunakan itu; fallback ke global `Load.MaxCapacity`
- Jika device punya `LoadNormalThreshold > 0` / `LoadMediumThreshold > 0`, gunakan itu; fallback ke global
- Signature mungkin perlu berubah dari global → per-device: `LoadThresholdsAsync(string deviceKey)`

#### Task 3.2 — NotificationService Baca Tarif dari DeviceSettings
**File:** `Services/NotificationService.cs`
- Ubah `GetTariffPerKWhAsync()` untuk menerima `deviceKey` parameter
- Jika device punya `TariffPerKWh > 0`, gunakan itu; fallback ke global `Tariff.PerKWh`

#### Task 3.3 — Sync Field Overlapping AppSettingsRecord ↔ DeviceSettings
**File:** `Controllers/ApiController.cs` (SaveDeviceSettings), `Services/DeviceSettingsService.cs`
- Saat save per-device settings, sync bidirectional untuk field overlapping:
  - `MaxCapacity` → sync ke `Load.MaxCapacity` (global fallback)
  - `LoadNormalThreshold` → sync ke `Load.NormalThreshold`
  - `LoadMediumThreshold` → sync ke `Load.MediumThreshold`
  - `EmaUpperThreshold` → sync ke `EMA.UpperThreshold`
  - `EmaLowerThreshold` → sync ke `EMA.LowerThreshold`
  - `TariffPerKWh` → sync ke `Tariff.PerKWh`
  - `ControlMode` → sync ke `Control.Mode`
- **Atau alternatif:** deprecate field global, semua baca dari DeviceSettings. Pilih salah satu pendekatan.
- Perlu diskusi: mana yang menjadi source of truth?

#### Task 3.4 — Refresh `window._deviceSettings` Secara Periodik
**File:** `Views/Shared/_Layout.cshtml`, `Views/Shared/_AnomalyService.cshtml`
- Di `_Layout.cshtml`, tambah interval refresh `window._deviceSettings` setiap 60 detik (sama dengan settingsReloadTimer)
- Di `_AnomalyService.cshtml`, pastikan `processAnomalyCheck()` selalu baca dari `window.getDeviceSettings()` yang sudah di-refresh
- Handle race condition: jika refresh sedang berjalan, jangan overwrite sampai selesai

#### Task 3.5 — NotificationService Auto-Reload Settings
**File:** `Services/NotificationService.cs`
- Subscribe ke `ApplicationDbContext.AppSettingsChanged` hook
- Atau inject `IOptionsMonitor<AppSettings>` pattern
- Atau sederhana: cek timestamp terakhir load, reload jika > 5 menit (sama seperti AppSettingsCache TTL)
- Prioritas: SMTP, WhatsApp, toggle notifikasi harus langsung efek setelah admin save

---

### Phase 4: LOGIKA ANOMALY & MONITORING 🔵
> Tujuan: Memperkuat konsistensi dan reliability anomaly detection

#### Task 4.1 — Auto-Expire AnomalyAlert.Active Dedup Flag
**File:** `Controllers/ApiController.cs` (LogAnomaly, ResetAnomalyAlert)
- Tambah timestamp saat set `AnomalyAlert.Active.{deviceKey}`
- Saat cek dedup, jika flag > 30 menit (atau configurable), anggap expired → clear & lanjutkan
- Atau: di `AppSettingsCache` refresh, cek dan bersihkan flag stale
- Simpan sebagai JSON: `{ "type": "OVERLOAD", "loggedAt": "2026-09-23T10:00:00" }`

#### Task 4.2 — Fix Downtime Check Consistency Client vs Server
**File:** `Views/Shared/_AnomalyService.cshtml`, `Controllers/ApiController.cs`
- Client: refresh downtime settings bersama dengan `window._deviceSettings` refresh (Task 3.4)
- Server: `CheckDowntimePeriodAsync` sudah baca dari DB — ini benar
- Tambahkan: jika server mendeteksi inconsistency (client kirim anomali yang menurut server harus suppressed), return info supaya client bisa update state

#### Task 4.3 — Bulk Save dengan Transaction Scope
**File:** `Controllers/ApiController.cs` (BulkSaveDeviceSettings)
- Wrap iterasi save dalam `using var transaction = await _context.Database.BeginTransactionAsync()`
- Commit di akhir jika semua berhasil, rollback jika ada error
- Tangkap `DbUpdateConcurrencyException` untuk handle concurrent modification

#### Task 4.4 — Handle EMA Threshold 0 — Fallback ke Global Settings
**File:** `Views/Shared/_AnomalyService.cshtml`
- Saat ini: jika `emaUpperThreshold == 0 && emaLowerThreshold == 0`, skip anomaly detection
- Ubah: fallback ke global EMA settings (dari `EMA.UpperThreshold` / `EMA.LowerThreshold` di AppSettings)
- Logika: per-device override jika > 0, global default jika 0
- Ini memastikan device baru tetap punya anomaly detection pakai global threshold

#### Task 4.5 — Dead Code Cleanup — Fibonacci Fields / Mode
**File:** `Models/DeviceSettings.cs`, `Views/Monitoring/_DeviceSettingsTab.cshtml`
- Opsi A: Hapus `EmaFibUpper`, `EmaFibLower` dari DeviceSettings model jika tidak digunakan
- Opsi B: Implementasikan Fibonacci mode di client-side anomaly service (connect ke per-device FibUpper/FibLower)
- **Rekomendasi:** Opsi A — hapus dead code, simplify. Fibonacci mode bisa di-add later jika dibutuhkan.

---

### Phase 5: PENGUATAN TAMBAHAN 🟢
> Tujuan: Hardening security & crypto best practices

#### Task 5.1 — AesEncryptionService Random IV
**File:** `Services/AesEncryptionService.cs`
- Generate random IV per encryption menggunakan `RandomNumberGenerator.GetBytes(16)`
- Simpan IV bersama ciphertext (prepend atau separate field)
- Update decrypt logic untuk extract IV dari ciphertext
- Handle backward compat: jika ciphertext length == tanpa IV, gunakan IV lama (deterministic)

#### Task 5.2 — Tambah `[ValidateAntiForgeryToken]` pada Write Endpoints
**File:** `Controllers/ApiController.cs`
- Tambah pada semua POST/PUT/DELETE yang menerima form data
- Untuk JSON API endpoints, pertimbangkan: custom header `X-Requested-With` check sebagai alternatif
- Atau: tambah anti-forgery token di AJAX request header secara global di `_Layout.cshtml`

#### Task 5.3 — OTP Comparison Constant-Time
**File:** `Controllers/ApiController.cs` (PublishRelay)
- Ganti `request.OtpCode.Trim() != storedCode.Trim()` dengan constant-time string compare
- Implementasi sederhana: loop semua karakter, XOR accumulator, return false hanya di akhir
- Atau pakai `CryptographicOperations.FixedTimeEquals` dari `Microsoft.AspNetCore.Cryptography.Internal`

#### Task 5.4 — Password Hasher Constant-Time Compare
**File:** `Services/PasswordHasher.cs`
- Ganti early-exit byte comparison dengan `CryptographicOperations.FixedTimeEquals`
- Minor: timing leak saat password salah, tapi PBKDF2 cost membuat eksploitasi tidak praktis

---

## 4. File yang Akan Diubah

| File | Phase | Perubahan |
|------|-------|-----------|
| `Controllers/ApiController.cs` | 1, 2, 3, 4 | Tambah `[Authorize]`, mask kredensial, generic error, transaction scope, dedup expiry |
| `Controllers/QwenChatController.cs` | 1 | Tambah `[Authorize]` class-level |
| `Controllers/MonitoringController.cs` | 3 | Inject `DeviceSettingsService` untuk settings sync |
| `Services/NotificationService.cs` | 3, 4 | Baca threshold/tarif dari DeviceSettings, auto-reload |
| `Services/AesEncryptionService.cs` | 2, 5 | Random IV, encrypt SMTP password |
| `Services/PasswordHasher.cs` | 5 | Constant-time compare |
| `Services/DeviceSettingsService.cs` | 3 | Sync logic bidirectional |
| `Startup.cs` | 2 | Fix SameSite policy |
| `Views/Shared/_Layout.cshtml` | 3 | Periodic refresh `_deviceSettings` |
| `Views/Shared/_AnomalyService.cshtml` | 3, 4 | EMA fallback, downtime consistency |
| `Views/Monitoring/_DeviceSettingsTab.cshtml` | 4 | Cleanup Fibonacci fields |
| `Models/DeviceSettings.cs` | 4 | Cleanup Fibonacci properties |

---

## 5. Risiko & Mitigasi

| Risiko | Mitigasi |
|--------|----------|
| Tambah `[Authorize]` di endpoint anomaly → client-side anomaly service 401 | Buat public endpoint untuk non-sensitif fields, atau pastikan semua user login sebagai Operator minimum |
| Encrypt SMTP password → breaking change baca existing data | Prefix marker `ENC:` + backward compat read (plaintext → encrypt on next save) |
| Sync bidirectional AppSettingsRecord ↔ DeviceSettings → infinite loop | Satu arah sync: DeviceSettings sebagai source of truth → push ke AppSettingsRecord |
| Generic error message → debugging production sulit | Log full exception di server, return generic ke client. Dev mode tetap verbose |
| Refresh `_deviceSettings` periodik → extra DB load | Interval 60s per browser, cached di client. Bisa ditambah ETag/If-Modified-Since |
| Random IV AES → tidak bisa decrypt data lama | Backward compat: deteksi panjang ciphertext, gunakan path lama jika tanpa IV prepended |
| Transaction scope bulk save → deadlock risk | Keep transaction short, retry logic untuk `DbUpdateConcurrencyException` |

---

## 6. Target Kondisi (AFTER)

### A. Sinkronisasi Settings Per Device
- ✅ NotificationService membaca threshold & tarif per-device (fallback ke global jika 0)
- ✅ Field overlapping tersinkronisasi — DeviceSettings sebagai source of truth
- ✅ `window._deviceSettings` di-refresh setiap 60 detik
- ✅ Device baru otomatis menggunakan global EMA threshold sebagai fallback
- ✅ NotificationService auto-reload saat settings berubah
- ✅ Bulk save aman dengan transaction scope

### B. Keamanan
- ✅ Semua endpoint kredensial memerlukan `[Authorize(Roles = "Admin")]`
- ✅ SMTP password ter-encrypt di DB (AES, random IV)
- ✅ Password/API key tidak muncul di log
- ✅ Client menerima generic error, detail hanya di server log
- ✅ Cookie SameSite policy konsisten (Lax)
- ✅ Semua write endpoint memerlukan auth yang sesuai
- ✅ CSRF protection pada form-based endpoints
- ✅ Constant-time comparison untuk OTP & password hash

### C. Logika Anomaly & Monitoring
- ✅ Dedup flag auto-expire (TTL 30 menit)
- ✅ Downtime check konsisten client vs server
- ✅ Dead code Fibonacci dihapus
- ✅ Multi-tab aware (server-side dedup tetap bekerja)

### D. Yang TIDAK Diubah (Out of Scope)
- ❌ Arsitektur anomaly detection tetap client-side (server-side detection = proyek terpisah)
- ❌ Multi-tab state drift (mitigated oleh server-side dedup, full sync = proyek terpisah)
- ❌ Migration state JSON ke tabel terpisah (low priority, bisa nanti)

---

## Checklist Pengerjaan

### Phase 1: Keamanan Kritis
- [ ] Task 1.1 — `[Authorize]` endpoint kredensial + mask password
- [ ] Task 1.2 — `[Authorize]` test-database-connection
- [ ] Task 1.3 — `[Authorize]` write endpoints
- [ ] Task 1.4 — `[Authorize]` QwenChatController
- [ ] Build & verify

### Phase 2: Keamanan Sedang
- [ ] Task 2.1 — Encrypt SMTP password di DB
- [ ] Task 2.2 — Hapus password dari log
- [ ] Task 2.3 — Generic error message
- [ ] Task 2.4 — Fix SameSite policy
- [ ] Build & verify

### Phase 3: Sinkronisasi Settings
- [ ] Task 3.1 — NotificationService threshold per-device
- [ ] Task 3.2 — NotificationService tarif per-device
- [ ] Task 3.3 — Sync bidirectional overlapping fields
- [ ] Task 3.4 — Refresh window._deviceSettings periodik
- [ ] Task 3.5 — NotificationService auto-reload
- [ ] Build & verify

### Phase 4: Logika Anomaly & Monitoring
- [ ] Task 4.1 — Auto-expire dedup flag
- [ ] Task 4.2 — Downtime consistency client vs server
- [ ] Task 4.3 — Bulk save transaction scope
- [ ] Task 4.4 — EMA fallback ke global
- [ ] Task 4.5 — Dead code cleanup Fibonacci
- [ ] Build & verify

### Phase 5: Penguatan Tambahan
- [ ] Task 5.1 — Random IV AES
- [ ] Task 5.2 — ValidateAntiForgeryToken
- [ ] Task 5.3 — OTP constant-time
- [ ] Task 5.4 — Password constant-time
- [ ] Build & verify
