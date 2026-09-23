# Dokumentasi Fitur dan Fungsi - KWH Monitoring System

## Ringkasan Proyek
**Nama Proyek:** KWH Monitoring - .NET 2.1 - V7  
**Teknologi:** ASP.NET Core MVC 2.1, Entity Framework Core, MQTT, SQL Server  
**Tujuan:** Monitoring konsumsi daya listrik (KWH) secara real-time dengan fitur deteksi anomali, notifikasi, dan kontrol device

---

## 1. FITUR UTAMA

### 1.1 Monitoring Real-Time
- **Dashboard Panel Monitoring** - Menampilkan semua device panel dalam bentuk kartu dengan informasi voltage, current, power, dan energy
- **Grafik Real-Time** - Visualisasi data voltage, current, dan power dengan 20 data points terakhir
- **Statistics Dashboard** - Total daya, total energy, jumlah panel aktif, average power factor
- **Filter dan Pencarian** - Filter berdasarkan status (HIGH/MEDIUM/NORMAL), phase (1-phase/3-phase), dan search text

### 1.2 Manajemen Device
- **Device Registry** - Registrasi dan tracking device MQTT yang terhubung
- **Device Categorization** - Kategorisasi device (Billboard, Videotron, dll) via AppSettings
- **Device Control** - Kontrol ON/OFF device dengan mode OTP (One-Time Password) untuk keamanan
- **Relay Control** - Kontrol relay via MQTT dengan rate limiting (10 request/menit per user)

### 1.3 Anomaly Detection System
- **EMA (Exponential Moving Average) Analysis** - Deteksi anomali berbasis trend analisis
- **Threshold Mode** - Mode threshold manual dan Fibonacci untuk deteksi OVERLOAD dan DROP
- **Anomaly Information Center** - Dashboard khusus untuk monitoring anomali dengan filter dan statistik
- **Auto Acknowledge & Resolve** - Anomali otomatis di-acknowledge saat kondisi kembali normal
- **Anomaly Chart Snapshot** - Menyimpan snapshot grafik saat anomali terdeteksi untuk analisis
- **Monthly Anomaly Report** - Laporan bulanan otomatis dengan summary dan rekomendasi
- **Root Cause Analysis** - Analisis penyebab anomali (downtime, beban berlebih, device offline)
- **Severity Classification** - Klasifikasi severity: Low, Medium, High, Critical
- **Cooldown System** - Mencegah notifikasi berulang untuk anomali yang sama
- **Server-side Deduplication** - Mencegah duplikasi anomali yang sama dalam waktu dekat

### 1.4 Energy Aggregation
- **Hourly Energy Calculation** - Perhitungan energy per jam (kWh)
- **Daily Energy Calculation** - Perhitungan energy per hari
- **Monthly Energy Calculation** - Perhitungan energy per bulan
- **Yearly Energy Calculation** - Perhitungan energy per tahun
- **Usage Statistics Dashboard** - Visualisasi konsumsi energi dengan grafik hourly, daily, monthly
- **Real-time kWh Tracking** - Tracking kWh real-time dengan update per 30 detik
- **Estimated Cost Calculation** - Perhitungan estimasi biaya berdasarkan tarif per kWh

### 1.5 Authentication & Authorization
- **Email-based Registration** - Registrasi dengan verifikasi email
- **Email Verification** - Token-based email verification (24 jam expiry)
- **Login System** - Login dengan remember me (7 hari) dan session-based (30 menit)
- **Password Reset** - Forgot password dengan token reset (1 jam expiry)
- **Account Lockout** - Lockout otomatis setelah 5 percobaan login gagal (15 menit)
- **Role-based Access Control (RBAC)** - 4 role: None (pending), Viewer, Operator, Admin
- **Admin Approval Flow** - Approval request dikirim ke Master Admin setelah email verified
- **Master Admin Protection** - Master admin tidak dapat dihapus atau diubah role-nya
- **User Management** - CRUD user, toggle active/inactive, change role (Master Admin only)

### 1.6 Notification System
- **Email Notifications** - SMTP-based email notification
- **WhatsApp Notifications** - WhatsApp integration via Wablas API
- **Instant Anomaly Alerts** - Alert real-time saat anomali terdeteksi
- **Downtime Power Alert** - Alert khusus saat device masih menyala di jam mati
- **Hourly Reports** - Laporan berkala per jam (opsional)
- **Daily Reports** - Laporan harian pada waktu yang ditentukan
- **Monthly Reports** - Laporan bulanan pada hari yang ditentukan
- **Test Notification** - Fitur test koneksi email dan WhatsApp
- **Configurable Notification** - Enable/disable per channel dan per jenis laporan

### 1.7 Security Features
- **Security Audit Logging** - Log semua aksi security (login, logout, settings change, relay control, dll)
- **IP Address & User Agent Tracking** - Tracking IP dan user agent untuk setiap aksi
- **Rate Limiting** - Rate limiting untuk relay control (10 request/60 detik)
- **AES Encryption** - AES encryption untuk sensitive data
- **Password Hashing** - PBKDF2-based password hashing
- **Secure Token Generation** - Cryptographically secure token untuk email verification
- **Token Hashing** - Token di-hash sebelum disimpan di database
- **OTP for Device Control** - OTP untuk kontrol device OFF (keamanan tambahan)

### 1.8 Data Management
- **Data Archiving** - Archive data lama ke KWHData_History saat melebihi maxCapacity
- **CSV Export** - Export data monitoring ke CSV (max 50,000 records)
- **Data Retention** - Retensi data berdasarkan kapasitas maksimum (default 30,000)
- **Failed Message Queue** - Queue untuk pesan MQTT yang gagal diproses
- **AppLog** - Logging aplikasi untuk debugging dan monitoring

### 1.9 Configuration Management
- **Dynamic Settings** - Pengaturan dapat diubah tanpa restart aplikasi
- **AppSettingsCache** - Singleton cache dengan TTL 5 menit dan auto-refresh
- **MQTT Configuration** - Broker, port, TLS/SSL, certificates, credentials
- **Load Thresholds** - Threshold untuk status HIGH (>20kW), MEDIUM (10-20kW), NORMAL (<10kW)
- **EMA Configuration** - Period, mode (manual/Fibonacci), thresholds, Fib levels
- **Tariff Configuration** - Tarif per kWh untuk perhitungan biaya
- **Device Category Mapping** - Mapping device key ke kategori
- **Dark Mode Toggle** - Mode gelap untuk tampilan UI

### 1.10 Health & Monitoring
- **Health Check Endpoint** - API endpoint untuk monitoring kesehatan sistem
- **Database Connection Resilience** - Retry policy (3x) dan MARS untuk concurrent queries
- **MQTT Connection Management** - Auto-reconnect dengan validasi settings
- **Background Services** - AnomalyNotificationBackgroundService untuk monitoring berkelanjutan

### 1.11 UI/UX Features
- **Responsive Design** - UI responsive dengan Bootstrap 5
- **Dark Mode** - Mode gelap untuk kenyamanan visual
- **Real-time Updates** - Auto-refresh data setiap interval yang ditentukan
- **Interactive Charts** - Chart.js untuk visualisasi interaktif
- **Modal Dialogs** - Modal untuk detail device, settings, dan konfirmasi aksi
- **Toast Notifications** - Notifikasi toast untuk feedback user
- **Loading Indicators** - Spinner loading untuk operasi async
- **Pagination** - Pagination untuk tabel data besar
- **Sorting** - Sorting kolom untuk tabel data

---

## 2. ARSITEKTUR SISTEM

### 2.1 Tech Stack
- **Framework:** ASP.NET Core MVC 2.1
- **Database:** SQL Server
- **ORM:** Entity Framework Core 2.1
- **MQTT Client:** MQTTnet
- **Frontend:** Bootstrap 5, Chart.js, jQuery
- **Authentication:** Cookie-based authentication dengan Claims
- **Email:** System.Net.Mail.SmtpClient
- **WhatsApp:** Wablas API (HTTP REST)
- **Password Hashing:** PBKDF2 (Rfc2898DeriveBytes)
- **Encryption:** AES-256

### 2.2 Database Schema

#### Core Tables
1. **KWHData** - Data monitoring real-time dari device
2. **KWHData_History** - Data monitoring yang di-archive
3. **DeviceRegistry** - Registry device MQTT
4. **HourlyEnergy** - Agregasi energi per jam
5. **DailyEnergy** - Agregasi energi per hari
6. **MonthlyEnergy** - Agregasi energi per bulan
7. **YearlyEnergy** - Agregasi energi per tahun

#### Anomaly Tables
8. **AnomalyLogs** - Log deteksi anomali
9. **AnomalyChartSnapshots** - Snapshot grafik saat anomali
10. **AnomalyMonthlyReports** - Laporan bulanan anomali

#### Security Tables
11. **ApplicationUsers** - User accounts
12. **EmailVerificationTokens** - Token verifikasi email
13. **SecurityAuditLogs** - Audit log aksi security

#### Configuration Tables
14. **AppSettings** - Dynamic application settings
15. **ColumnMapping** - Mapping kolom legacy
16. **ColumnScaleConfig** - Konfigurasi scale faktor kolom
17. **FailedMessages** - Queue pesan gagal
18. **AppLog** - Application logs
19. **RelayControls** - Log kontrol relay

### 2.3 Services Layer

| Service | Fungsi |
|---------|--------|
| **MqttService** | Koneksi MQTT, publish/subscribe, TLS support |
| **AnomalyAnalysisService** | Analisis anomali, severity assessment, root cause |
| **NotificationService** | Email dan WhatsApp notifications |
| **EmailService** | SMTP email sending |
| **AesEncryptionService** | AES encryption/decryption |
| **PasswordHasher** | Password hashing dan verifikasi |
| **AppSettingsCache** | Cache singleton untuk settings |
| **DbInitializer** | Database initialization |
| **AnomalyNotificationBackgroundService** | Background service untuk anomaly monitoring |

### 2.4 Controllers

| Controller | Fungsi |
|------------|--------|
| **MonitoringController** | Dashboard monitoring, charts, history, settings |
| **ApiController** | REST API untuk panels, statistics, relay control |
| **AccountController** | Login, register, forgot password, email verification |
| **UserManagementController** | User management (Admin only) |
| **AdminApprovalController** | Approval request user baru |
| **HomeController** | Home dan error pages |
| **HealthController** | Health check endpoint |
| **QwenChatController** | (Eksperimental) Chat interface |

---

## 3. FLOW SISTEM

### 3.1 Data Flow - MQTT to Dashboard

```
Device (ESP32/Modbus) 
    ↓ (MQTT Publish: data/KWHAPP/{deviceId}/12345678)
MQTT Broker
    ↓ (MQTT Subscribe - MqttService)
ASP.NET Core App
    ↓ (Parse JSON payload)
    ↓ (Validasi device di DeviceRegistry)
    ↓ (Simpan ke KWHData)
    ↓ (Check threshold → update Status: HIGH/MEDIUM/NORMAL)
    ↓ (Check anomaly → AnomalyAnalysisService)
    ↓ (Jika anomali → NotificationService)
    ↓ (Aggregasi → HourlyEnergy/DailyEnergy/MonthlyEnergy)
Database (SQL Server)
    ↓ (EF Core Query)
MonitoringController / ApiController
    ↓ (Return JSON / View)
Dashboard UI (Chart.js + Real-time polling)
```

### 3.2 Flow - Anomaly Detection

```
Data Baru Masuk (KWHData)
    ↓
Ambil EMA Settings dari AppSettingsCache
    ↓
Hitung EMA berdasarkan mode (manual / Fibonacci)
    ↓
Bandingkan daya aktual dengan threshold (EMA ± threshold%)
    ↓
[OVERLOAD] Jika daya > upper threshold
[DROP] Jika daya < lower threshold
[NORMAL] Jika daya dalam threshold
    ↓
[Jika OVERLOAD/DROP]
    ↓
Check cooldown period (mencegah spam)
    ↓
Check server deduplication (cegah duplikasi dalam 5 menit)
    ↓
Simpan AnomalyLog
    ↓
Buat AnomalyChartSnapshot
    ↓
Kirim notifikasi (Email + WhatsApp)
    ↓
Update AnomalyMonthlyReport
    ↓
[Jika kembali NORMAL]
    ↓
Auto-acknowledge anomali aktif
    ↓
Reset counter dan cooldown
```

### 3.3 Flow - Authentication & Authorization

```
[Register]
    ↓
Input email, password, display name
    ↓
Validasi input
    ↓
Hash password (PBKDF2)
    ↓
Simpan user dengan role=None (pending approval)
    ↓
Generate secure token (64 bytes random)
    ↓
Hash token (SHA256)
    ↓
Simpan EmailVerificationToken (expiry 24 jam)
    ↓
Kirim email verifikasi
    ↓
User klik link verifikasi
    ↓
Validasi token hash dan expiry
    ↓
Set EmailConfirmed=true
    ↓
Generate approval token
    ↓
Kirim email approval request ke Master Admin
    ↓
[Admin Approval]
    ↓
Admin klik link approval
    ↓
Validasi token
    ↓
Set role=Viewer (atau Operator/Admin)
    ↓
User dapat login
    ↓
[Login]
    ↓
Input email + password
    ↓
Lookup user by normalized email
    ↓
Check lockout status
    ↓
Check email confirmed
    ↓
Check active status
    ↓
Check role != None
    ↓
Verify password
    ↓
Increment AccessFailedCount / reset on success
    ↓
[Jika gagal ≥5] → Lockout 15 menit
    ↓
[Sukses] → SignIn dengan Claims (Name, Email, Role)
    ↓
Redirect ke Dashboard
```

### 3.4 Flow - Relay Control

```
User klik tombol ON/OFF di UI
    ↓
[Jika OFF] → Input OTP (validasi via API)
    ↓
Check rate limit (10 request/60 detik per user)
    ↓
[Rate limited] → Return error
    ↓
[Not limited] → Log security action
    ↓
Build MQTT topic: data/KWHAPP/{deviceId}/12345678
    ↓
Build payload JSON: { _terminalTime, _groupName, RC: "1"/"0" }
    ↓
Publish via MQTTnet (QoS 1)
    ↓
Device terima dan eksekusi relay
    ↓
Log relay control ke RelayControls table
    ↓
Update UI status
```

### 3.5 Flow - Data Archiving

```
Data baru masuk (KWHData)
    ↓
Hitung total records di KWHData
    ↓
Check maxCapacity (default 30,000)
    ↓
[Jika exceed]
    ↓
Ambil data tertua (ORDER BY Waktu_Server ASC, TOP N)
    ↓
Insert ke KWHData_History
    ↓
Delete dari KWHData
    ↓
Log archiving ke AppLog
```

### 3.6 Flow - Energy Aggregation

```
[Hourly Calculation - Trigger setiap 30 detik]
    ↓
Ambil data KWHData device dalam 1 jam terakhir
    ↓
Hitung delta Total_Energy_Wh (first vs last)
    ↓
Konversi ke kWh (÷ 1000)
    ↓
Simpan ke HourlyEnergy
    ↓
[Daily Calculation - Trigger per jam]
    ↓
Sum HourlyEnergy dalam 1 hari
    ↓
Simpan ke DailyEnergy
    ↓
[Monthly Calculation - Trigger per hari]
    ↓
Sum DailyEnergy dalam 1 bulan
    ↓
Simpan ke MonthlyEnergy
    ↓
[Yearly Calculation - Trigger per bulan]
    ↓
Sum MonthlyEnergy dalam 1 tahun
    ↓
Simpan ke YearlyEnergy
```

### 3.7 Flow - Notification

```
[Anomaly Detected]
    ↓
Load NotificationSettings dari AppSettingsCache
    ↓
Check EnableEmail / EnableWhatsApp
    ↓
Check SendInstantAlert
    ↓
[Email]
    ↓
Build HTML email dengan styling gradient
    ↓
Include: alert detail, device status, anomaly summary (24h)
    ↓
Send via SMTP
    ↓
[WhatsApp]
    ↓
Build markdown message
    ↓
Include: alert detail, device status, anomaly summary
    ↓
Send via Wablas API (HTTP POST)
    ↓
Log notification ke AppLog
```

---

## 4. FITUR DETAIL PER MODUL

### 4.1 Monitoring Module

**Views:**
- `Index.cshtml` - Dashboard utama dengan panel cards
- `Charts.cshtml` - Halaman grafik semua device
- `Details.cshtml` - Detail单个 device
- `History.cshtml` - Tabel history dengan pagination
- `UsageStatistics.cshtml` - Statistik penggunaan energi
- `AnomalyLogs.cshtml` - Anomaly Information Center
- `Settings.cshtml` - Halaman pengaturan sistem

**API Endpoints:**
- `GET /api/panels` - List semua panels dengan filter
- `GET /api/panels/{deviceKey}/chart` - Data chart device
- `GET /api/statistics` - Statistik global
- `POST /api/usage-statistics` - Usage statistics (date filter)
- `POST /api/usage-statistics/{deviceKey}` - Per-device usage
- `POST /api/relay-control` - Kontrol relay ON/OFF
- `POST /api/relay-control/otp` - Generate/validasi OTP
- `GET /api/export-csv/{deviceKey}` - Export CSV

### 4.2 Anomaly Module

**Deteksi:**
- EMA-based anomaly detection
- Threshold mode: Manual (±30%) atau Fibonacci (±0.618/1.618)
- Downtime period detection (jam mati otomatis)
- Cooldown period (5 menit)
- Server deduplication (5 menit)

**Severity Levels:**
- **Low:** Deviation <10%
- **Medium:** Deviation 10-30%
- **High:** Deviation 30-50% atau DROP non-downtime
- **Critical:** Deviation >50%, repeated pattern, atau downtime overload

**Operator Actions:**
- Acknowledge anomali
- Resolve dengan root cause
- Add notes
- View chart snapshot

**Reports:**
- Monthly report auto-generated
- Summary: total anomalies, overload/drop count, affected devices
- Top affected device
- Average deviation
- Recommendations

### 4.3 User Management Module

**Roles:**
- **None:** Pending approval (tidak bisa login)
- **Viewer:** Read-only access
- **Operator:** Can control devices, acknowledge anomalies
- **Admin:** Full access except user management
- **Master Admin:** Full access + user management

**Features:**
- User list (excludes Master Admin)
- Change role dropdown
- Toggle active/inactive
- Delete user (with confirmation)
- Protected: Master Admin cannot be modified

### 4.4 Notification Module

**Channels:**
- **Email:** SMTP (Gmail, Office365, custom)
- **WhatsApp:** Wablas API

**Notification Types:**
1. **Instant Anomaly Alert** - Real-time saat anomali terdeteksi
2. **Downtime Power Alert** - Device masih nyala saat jam mati
3. **Hourly Report** - Ringkasan per jam (opsional)
4. **Daily Report** - Ringkasan harian (08:00 default)
5. **Monthly Report** - Ringkasan bulanan (hari 1, 08:00)

**Email Template:**
- Gradient header dengan alert icon
- Alert detail cards (power, threshold, deviation)
- Device status table
- Anomaly summary (24h)
- Footer dengan timestamp

**WhatsApp Message:**
- Markdown formatting
- Emoji untuk visual cue
- Structured sections
- Summary statistics

### 4.5 Security Module

**Audit Actions:**
- Login / LoginFailed
- Logout
- Register
- EmailVerified
- EmailVerificationSent
- PasswordResetRequested
- PasswordReset
- Lockout
- SettingsViewed
- SettingsUpdated
- RelayControl
- OTPGenerated
- OTPValidated
- RoleChanged
- AccessRequestSent
- AnomalyAcknowledged
- AnomalyResolved
- ExportData

**Logged Fields:**
- UserId (nullable)
- Email
- Action (enum)
- TargetDevice (nullable)
- Details
- Success (bool)
- IpAddress
- UserAgent
- Timestamp (UTC)

---

## 5. KONFIGURASI SISTEM

### 5.1 MQTT Settings
- `MQTT.Broker` - Broker hostname/IP
- `MQTT.Port` - Port (default 1883, TLS 8883)
- `MQTT.UseTls` - Enable TLS/SSL
- `MQTT.ClientId` - Client identifier
- `MQTT.Username` / `MQTT.Password` - Credentials
- `MQTT.TlsCaCertFile` - CA certificate filename
- `MQTT.TlsClientCertFile` - Client certificate filename
- `MQTT.TlsClientCertPassword` - Client cert password
- `MQTT.TlsSkipCertValidation` - Skip validation (dev only)

### 5.2 Notification Settings
- `Notification.SmtpServer` - SMTP server
- `Notification.SmtpPort` - SMTP port (587/465)
- `Notification.SenderEmail` - From email
- `Notification.SenderPassword` - Email password
- `Notification.EnableEmail` - Enable email notifications
- `Notification.WablasServerUrl` - Wablas API URL
- `Notification.WablasToken` - Wablas API token
- `Notification.WablasSecretKey` - Wablas secret key
- `Notification.WablasPhoneNumbers` - Comma-separated phone numbers
- `Notification.EnableWhatsApp` - Enable WhatsApp notifications
- `Notification.SendInstantAlert` - Enable instant alerts
- `Notification.SendHourlyReport` - Enable hourly reports
- `Notification.SendDailyReport` - Enable daily reports
- `Notification.SendMonthlyReport` - Enable monthly reports
- `Notification.DailyReportTime` - Daily report time (HH:MM)
- `Notification.MonthlyReportDay` - Day of month (1-31)
- `Notification.MonthlyReportTime` - Monthly report time (HH:MM)
- `Notification.MasterAdminEmail` - Master admin email

### 5.3 Load Thresholds
- `Load.MaxCapacity` - Max capacity (default 30000 Watt)
- `Load.MediumThreshold` - Medium load % (default 70%)
- `Load.NormalThreshold` - Normal load % (default 30%)

### 5.4 EMA Settings
- `emaPeriod` - EMA period (default 20)
- `emaMode` - "manual" atau "fibonacci"
- `emaUpperThreshold` - Upper threshold % (manual mode, default 30)
- `emaLowerThreshold` - Lower threshold % (manual mode, default 50)
- `emaFibUpper` - Fibonacci upper multiplier (default 1.618)
- `emaFibLower` - Fibonacci lower multiplier (default 0.618)
- `emaShowLine` - Show EMA line on chart
- `emaShowThresholds` - Show threshold bands on chart
- `useInitial100ForEma` - Use 100 initial points for EMA init

### 5.5 UI Settings
- `RefreshInterval` - Dashboard refresh interval (seconds, default 10)
- `ChartDataPoints` - Number of chart data points (default 20)
- `Tariff.PerKWh` - Electricity tariff per kWh (default 1500)

### 5.6 Data Retention
- `Data.MaxCapacity` - Max records in KWHData (default 30000)

---

## 6. TABEL DATABASE - FIELD DETAIL

### 6.1 KWHData
| Field | Type | Description |
|-------|------|-------------|
| Id | int | Primary key, identity |
| DeviceKey | varchar(20) | Unique device identifier |
| DeviceId | nvarchar(50) | Device ID dari payload |
| GroupName | nvarchar(100) | Nama group/panel |
| Waktu_Device | datetime | Terminal time dari device |
| Waktu_Server | datetime | Received time di server |
| PHASE_R | decimal(18,2) | Voltage phase R |
| PHASE_S | decimal(18,2) | Voltage phase S (3-phase) |
| PHASE_T | decimal(18,2) | Voltage phase T (3-phase) |
| AMPERE_R | decimal(18,3) | Current phase R |
| AMPERE_S | decimal(18,3) | Current phase S (3-phase) |
| AMPERE_T | decimal(18,3) | Current phase T (3-phase) |
| W | decimal(18,1) | Power in Watt |
| CosPhi | decimal(18,3) | Power factor |
| F | decimal(18,2) | Frequency in Hz |
| Aktif_Power | decimal(18,2) | Active energy Wh |
| TotalW | decimal(18,2) | Total energy Wh |
| TotalW1M | decimal(18,2) | Total energy 1-minute Wh |

### 6.2 AnomalyLog
| Field | Type | Description |
|-------|------|-------------|
| Id | int | Primary key |
| DeviceKey | nvarchar(50) | Device identifier |
| DeviceId | nvarchar(50) | Device ID |
| AnomalyType | nvarchar(20) | OVERLOAD / DROP / DEVICE_DROP |
| PowerValue | decimal(18,2) | Power saat deteksi |
| ThresholdValue | decimal(18,2) | Threshold yang dilanggar |
| Deviation | decimal(5,2) | Deviasi persentase |
| DetectedTime | datetime | Waktu deteksi |
| EMAValue | decimal(18,2) | EMA value saat deteksi |
| ThresholdMode | nvarchar(20) | manual / fibonacci |
| Acknowledged | bit | Sudah di-acknowledge |
| AcknowledgedTime | datetime | Waktu acknowledge |
| AcknowledgedBy | nvarchar(256) | Email yang acknowledge |
| ResolvedBy | nvarchar(256) | Email yang resolve |
| ResolvedTime | datetime | Waktu resolve |
| IsResolved | bit | Sudah di-resolve |
| OperatorAction | nvarchar(100) | Tindakan operator |
| OperatorNotes | nvarchar(1000) | Catatan operator |
| Severity | nvarchar(20) | low/medium/high/critical |
| RootCause | nvarchar(500) | Penyebab anomali |
| RecommendedAction | nvarchar(1000) | Rekomendasi tindakan |
| Notes | nvarchar(500) | Catatan tambahan |

### 6.3 ApplicationUser
| Field | Type | Description |
|-------|------|-------------|
| Id | int | Primary key |
| Email | nvarchar(256) | Email user |
| NormalizedEmail | nvarchar(256) | Email lowercase trimmed |
| DisplayName | nvarchar(256) | Nama tampilan |
| PasswordHash | nvarchar(500) | PBKDF2 hash |
| Role | nvarchar(50) | None/Viewer/Operator/Admin |
| CreatedAt | datetime | Waktu pembuatan |
| LastLoginAt | datetime | Login terakhir |
| LockoutEnd | datetime | End of lockout period |
| EmailConfirmed | bit | Email terverifikasi |
| IsActive | bit | Akun aktif |
| AccessFailedCount | int | Failed login count |

### 6.4 SecurityAuditLog
| Field | Type | Description |
|-------|------|-------------|
| Id | int | Primary key |
| UserId | int? | FK to ApplicationUser |
| Email | nvarchar(256) | Email user |
| Action | nvarchar | Security action enum |
| TargetDevice | nvarchar(100) | Device target (jika ada) |
| Details | nvarchar(500) | Detail aksi |
| Success | bit | Aksi berhasil |
| IpAddress | nvarchar(50) | IP address |
| UserAgent | nvarchar(500) | Browser user agent |
| Timestamp | datetime | Waktu aksi (UTC) |

---

## 7. API REFERENCE LENGKAP

### Monitoring APIs

#### GET /api/panels
**Query Parameters:**
- `search` - Filter by groupName/deviceKey/deviceId
- `status` - HIGH/MEDIUM/NORMAL/all
- `phase` - 3phase/1phase/all

**Response:**
```json
{
  "deviceKey": "PANEL_001",
  "deviceId": "DEV001",
  "groupName": "Videotron Lobby",
  "deviceCategory": "Videotron",
  "isThreePhase": true,
  "r": 220.5,
  "s": 219.8,
  "t": 221.2,
  "ampR": 15.2,
  "ampS": 14.8,
  "ampT": 15.5,
  "cosPhi": 0.95,
  "dayaWatt": 10250,
  "totalW1M": 150000,
  "energiAktif": 145000,
  "totalEnergy": 150000,
  "frekuensi": 50.0,
  "avgVoltage": 220.5,
  "avgAmpere": 15.17,
  "phaseRColor": "#28a745",
  "phaseSColor": "#28a745",
  "phaseTColor": "#28a745",
  "status": "MEDIUM"
}
```

#### GET /api/panels/{deviceKey}/chart
**Query Parameters:**
- `points` - Number of data points (default 20)

**Response:**
```json
{
  "deviceKey": "PANEL_001",
  "labels": ["10:00:00", "10:01:00", ...],
  "isThreePhase": true,
  "voltage": { "r": [220, 221, ...], "s": [...], "t": [...] },
  "current": { "r": [15, 15.2, ...], "s": [...], "t": [...] },
  "power": [10000, 10200, ...]
}
```

#### GET /api/statistics
**Response:**
```json
{
  "totalDaya": 125000,
  "totalEnergy": 1500000,
  "totalW1M": 1450000,
  "totalEnergiAktif": 1480000,
  "activePanels": 12,
  "avgPowerFactor": 0.94,
  "timestamp": "18/09/2026, 14:30:00"
}
```

#### POST /api/usage-statistics
**Body:**
```json
{
  "startDate": "2026-09-18"
}
```

**Response:**
```json
{
  "totalToday": 125.5,
  "avgPerHour": 5.23,
  "peakHour": 15.8,
  "peakHourTime": "14:00",
  "totalThisMonth": 3500.2,
  "avgPerDay": 116.67,
  "peakDay": 145.3,
  "peakDayDate": "15/09",
  "totalThisYear": 42000.5,
  "avgPerMonth": 3500.04,
  "peakMonth": "September",
  "hourlyData": [{ "timeLabel": "00:00", "energy": 3.5, "sortKey": 0 }, ...],
  "dailyData": [{ "dateLabel": "1/9/2026", "energy": 115.2, "sortKey": 1 }, ...],
  "monthlyData": [{ "monthName": "Januari", "energy": 3200.5, "sortKey": 1 }, ...],
  "tariffPerKWh": 1500,
  "estimatedCost": 5250300,
  "realtimeKWh": 5.23,
  "currentHourLabel": "14:00",
  "secondsToNextHour": 1800,
  "isToday": true,
  "serverDate": "2026-09-18",
  "serverHour": 14,
  "serverDay": 18,
  "serverMonth": 9
}
```

#### POST /api/relay-control
**Body:**
```json
{
  "deviceId": "DEV001",
  "value": "1",
  "mode": "OnOff"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Relay control published"
}
```

#### POST /api/relay-control/otp
**Body:**
```json
{
  "deviceKey": "PANEL_001",
  "otpCode": "123456"
}
```

**Response:**
```json
{
  "success": true,
  "message": "OTP validated",
  "isValid": true
}
```

---

## 8. BACKGROUND SERVICES

### AnomalyNotificationBackgroundService
**Fungsi:** Background service yang berjalan terus-menerus untuk monitoring anomali

**Loop:**
1. Wait 30 detik
2. Load semua device aktif dari DeviceRegistry
3. Untuk setiap device:
   - Ambil data terakhir dari KWHData
   - Load anomaly settings (EMA period, mode, thresholds)
   - Hitung EMA
   - Tentukan anomaly type (OVERLOAD/DROP/NORMAL)
   - Handle cooldown dan deduplication
   - Jika anomali baru:
     - Simpan AnomalyLog
     - Buat AnomalyChartSnapshot
     - Kirim notifikasi
     - Update monthly report
   - Jika kembali normal:
     - Auto-acknowledge anomali aktif
     - Reset counters
4. Log error jika ada exception

---

## 9. FITUR KHUSUS

### 9.1 Downtime Detection
- Deteksi otomatis periode jam mati (22:00 - 06:00 default)
- Jika device masih ada daya saat downtime → kirim POWER ALERT
- DROP saat downtime dianggap normal (tidak kirim alert)
- Override downtime hours via AppSettings

### 9.2 Cooldown System
- Cooldown period: 5 menit setelah anomali terdeteksi
- Mencegah notifikasi spam untuk anomali berkelanjutan
- Reset saat tipe anomali berubah (DROP ↔ OVERLOAD)
- Counter per device per anomaly type

### 9.3 Server Deduplication
- Cek anomali serupa dalam 5 menit terakhir
- Cegah duplikasi dari multiple server instances
- Key: DeviceKey + AnomalyType + rounded power value

### 9.4 Auto-Acknowledge
- Saat daya kembali ke zona normal
- Semua anomali aktif otomatis di-acknowledge
- Timestamp acknowledge = waktu kembali normal
- Tidak perlu intervensi operator

### 9.5 Repeated Pattern Detection
- Deteksi anomali berulang (≥3 kali dalam 24 jam)
- Upgrade severity ke "critical"
- Tambahkan ke impact assessment
- Rekomendasi tindakan lebih agresif

### 9.6 Rate Limiting
- Relay control: 10 request per 60 detik per user
- sliding window implementation
- Return error 429 jika exceed
- Log attempt ke security audit

### 9.7 OTP for Device OFF
- Generate OTP 6 digit (random)
- Kirim OTP ke email user
- Validasi OTP sebelum eksekusi OFF
- Expire OTP dalam 5 menit
- Log ke security audit

---

## 10. TROUBLESHOOTING

### Database Connection Issues
**Error:** "MARS TDS header contained errors"  
**Fix:** 
- Enable MARS di connection string: `MultipleActiveResultSets=true`
- Add retry policy: `EnableRetryOnFailure(3, 10s)`
- Use AppSettingsCache singleton

### MQTT Connection Failed
**Error:** "Cannot connect to broker"  
**Check:**
- Broker hostname dan port
- Credentials (username/password)
- TLS settings (certificates, port 8883)
- Firewall rules

### Email Not Sending
**Error:** "SMTP error"  
**Check:**
- SMTP server dan port (587/465)
- Sender email dan password
- Enable less secure apps (Gmail)
- App password (2FA enabled)

### Anomaly Not Detected
**Check:**
- EMA settings (period, mode, thresholds)
- Data masuk ke KWHData
- Background service running
- Cooldown period aktif

### High Memory Usage
**Fix:**
- Reduce maxCapacity (default 30000)
- Reduce ChartDataPoints (default 20)
- Check memory leak di BackgroundService

---

## 11. DEPLOYMENT

### Prerequisites
- .NET Core 2.1 SDK
- SQL Server 2016+
- MQTT Broker (Mosquitto, EMQX, dll)
- SMTP server account
- Wablas API account (optional)

### Steps
1. Clone repository
2. Update `appsettings.json` dengan connection string
3. Run `dotnet ef database update`
4. Setup MQTT broker dan test connection
5. Configure notification settings di database
6. Set MasterAdminEmail di AppSettings
7. Run aplikasi: `dotnet run`
8. Akses `http://localhost:5000`

### Production Considerations
- Use reverse proxy (Nginx/IIS)
- Enable HTTPS
- Setup monitoring (Application Insights, Serilog)
- Configure backup database
- Setup log rotation
- Enable rate limiting di reverse proxy
- Monitor MQTT connection health

---

## 12. FILE STRUKTUR

```
KWHMonitoring - .NET 2.1 - V7/
├── KWHMonitoring/
│   ├── Controllers/
│   │   ├── ApiController.cs              # REST API endpoints
│   │   ├── AccountController.cs          # Auth: login, register, reset password
│   │   ├── AdminApprovalController.cs    # User approval
│   │   ├── HomeController.cs             # Home, error pages
│   │   ├── HealthController.cs           # Health check
│   │   ├── MonitoringController.cs       # Dashboard, charts, settings
│   │   ├── QwenChatController.cs         # (Experimental) chat
│   │   └── UserManagementController.cs   # User CRUD (Admin)
│   ├── Models/
│   │   ├── ApplicationDbContext.cs       # EF Core DbContext
│   │   ├── ApplicationUser.cs            # User entity
│   │   ├── KWHData.cs                    # Monitoring data entity
│   │   ├── AnomalyLog.cs                 # Anomaly log entity
│   │   ├── DeviceRegistry.cs             # Device registry entity
│   │   ├── EmailVerificationToken.cs     # Email token entity
│   │   ├── SecurityAuditLog.cs           # Audit log entity
│   │   ├── AppSettings.cs                # Settings model
│   │   ├── AppSettingsRecord.cs          # Settings DB entity
│   │   ├── DailyEnergy.cs                # Daily aggregation
│   │   ├── HourlyEnergy.cs               # Hourly aggregation
│   │   ├── MonthlyEnergy.cs              # Monthly aggregation
│   │   ├── YearlyEnergy.cs               # Yearly aggregation
│   │   ├── NotificationSettings.cs       # Notification config
│   │   ├── RelayControl.cs               # Relay control log
│   │   └── ... (20+ model files)
│   ├── Services/
│   │   ├── MqttService.cs                # MQTT client wrapper
│   │   ├── AnomalyAnalysisService.cs     # Anomaly analysis logic
│   │   ├── NotificationService.cs        # Email/WhatsApp notifications
│   │   ├── EmailService.cs               # SMTP email sender
│   │   ├── AesEncryptionService.cs       # AES encryption
│   │   ├── PasswordHasher.cs             # PBKDF2 hashing
│   │   ├── AppSettingsCache.cs           # Settings singleton cache
│   │   ├── DbInitializer.cs              # DB initialization
│   │   └── AnomalyNotificationBackgroundService.cs
│   ├── Views/
│   │   ├── Monitoring/
│   │   │   ├── Index.cshtml              # Main dashboard
│   │   │   ├── Charts.cshtml             # All charts page
│   │   │   ├── Details.cshtml            # Device detail
│   │   │   ├── History.cshtml            # Data history
│   │   │   ├── UsageStatistics.cshtml    # Energy stats
│   │   │   ├── AnomalyLogs.cshtml        # Anomaly center
│   │   │   ├── Settings.cshtml           # System settings
│   │   │   └── _PanelCard.cshtml         # Panel card partial
│   │   ├── Account/
│   │   │   ├── Login.cshtml
│   │   │   ├── Register.cshtml
│   │   │   ├── ForgotPassword.cshtml
│   │   │   └── ... (8 views)
│   │   ├── UserManagement/
│   │   │   └── _UserManagementTab.cshtml
│   │   ├── AdminApproval/
│   │   │   ├── Approve.cshtml
│   │   │   └── Reject.cshtml
│   │   └── Shared/
│   │       ├── _Layout.cshtml
│   │       ├── _DashboardHeader.cshtml
│   │       ├── _ValidationScriptsPartial.cshtml
│   │       └── ... (partials)
│   ├── Migrations/
│   │   ├── ApplicationDbContextModelSnapshot.cs
│   │   └── [timestamp]_*.cs             # EF migrations
│   ├── wwwroot/
│   │   ├── css/                          # Stylesheets
│   │   ├── js/                           # JavaScript files
│   │   └── lib/                          # Third-party libs
│   ├── appsettings.json                  # Configuration
│   ├── Startup.cs                        # App startup
│   └── Program.cs                        # Entry point
└── KWHMonitoring.slnx                    # Solution file
```

---

## 13. KONVENSI DAN BEST PRACTICES

### Code Style
- Async/await untuk semua I/O operations
- CancellationToken untuk long-running operations
- Try-catch di controller dengan TempData["Error"]
- Using statement untuk IDisposable resources
- Dependency injection untuk semua services

### Database
- Indexes on frequently queried columns (DeviceKey, Waktu_Server)
- Foreign keys dengan Restrict delete behavior
- Decimal types untuk numeric data (presisi tinggi)
- Datetime2 untuk timestamp (presisi lebih tinggi)
- Soft delete pattern (IsActive flags)

### Security
- Password hashing dengan PBKDF2 (256-bit)
- Token hashing dengan SHA256
- AES encryption untuk sensitive data
- Rate limiting untuk critical endpoints
- Audit logging untuk semua security actions
- OTP untuk device control OFF

### Performance
- Memory caching untuk settings (5-min TTL)
- Pagination untuk large datasets
- Async database operations
- Connection pooling dengan MARS
- Background services untuk heavy operations

---

Dokumentasi ini dibuat pada: **18 September 2026**  
Versi Project: **.NET 2.1 - V7**  
Total Fitur: **11 modul utama, 50+ fitur detail**  
Total Tabel Database: **19 tabel**  
Total API Endpoints: **15+ endpoints**  
Total Views: **20+ views**
