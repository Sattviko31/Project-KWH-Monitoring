# 🐛 BUG HUNTING & AUDIT REPORT — KWH Monitoring (.NET Core 2.1)

**Tanggal audit:** 30 September 2026
**Branch/Commit:** `main` @ `c4c5b6c2babf0a1dd2bc98872207c3bdaad91a0e`
**Scope:** Seluruh project `KWHMonitoring -  .NET 2.1 - Final 5` (Controllers, Services, Models, Views, Filters, Migrations, konfigurasi, dependensi).
**Metode:** Static code review menyeluruh (seluruh source utama dibaca), secret scanning, pola bug umum (injection, otorisasi, race condition, logika terbalik, resource leak), pemeriksaan rantai migrasi EF, dan **compile test (Rebuild penuh)**.
**Status:** ⚠️ **TIDAK ADA source code yang diubah.** File ini satu-satunya tambahan. `git status` bersih (tidak ada file tracked yang berubah).

---

## Hasil Testing / Build

| Test | Hasil |
|---|---|
| `dotnet build` full Rebuild (SDK 2.1.818) | ✅ **SUKSES — 0 Error, 0 Warning** |
| Artefak compile (KWHMonitoring.dll 1,49 MB + Views.dll 2,56 MB) | ✅ Ter-generate bersih |
| `git status --short` setelah audit | ✅ Tidak ada file berubah (hanya laporan ini yang baru) |
| Secret scanning `git show HEAD:...appsettings.json` | ❌ **Secret ter-commit ke Git** |
| Pemeriksaan rantai migrasi EF (urutan `Migrate()`) | ❌ **Konflik 2 migration awal** |
| Scan `@Html.Raw` di seluruh Razor views | ⚠️ 3 lokasi berisiko XSS |
| Endpoint API tanpa `[Authorize]` (ApiController 7.111 baris) | ❌ **2 endpoint sensitif terbuka** |

**Ringkasan: 3 KRITIS, 8 TINGGI, 15 SEDANG, 20 RENDAH.**

---

## 🔴 KRITIS (CRITICAL)

### C1. Secret sensitif ter-commit ke repository Git (publik)
**File:** `KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring/appsettings.json` (baris 3, 4, 23, 37) — **file TRACKED di git** (remote `github.com/Sattviko31/Project-KWH-Monitoring`).

| Baris | Secret | Dampak |
|---|---|---|
| 3 | DB aplikasi: `kwhapp/kwhapp1234` @ `192.168.168.38:1433` | Kunci penuh ke database monitoring |
| 4 | DB ERP: `sa/123456` @ `192.168.168.16\SQLEXPRESS` | Akun **superadmin `sa`** + password lemah |
| 23 | `Encryption:Key = "KWH-Monitoring-2024-AES256-SecureKey!"` | Semua nilai `ENC:` (password SMTP, API key) bisa didekripsi siapa pun |
| 37 | API Key Qwen: `sk-ws-H.DMRLYXX...` | Penyalahgunaan API AI (biaya) |

**Bukti:** `git show HEAD:...appsettings.json` menampilkan semua secret di atas — ada di commit, bukan hanya working tree.
**Rekomendasi:** Rotasi semua secret sekarang (password DB, API key, ganti kunci AES + re-encrypt data `ENC:`), pindah ke env var/user-secrets/Key Vault, tambahkan `appsettings.json` ke `.gitignore`, bersihkan history Git.

### C2. Endpoint publik `GET /api/Api/get-system-settings` membocorkan SELURUH settings termasuk kredensial
**File:** `Controllers/ApiController.cs:2894-2912` — `[HttpGet("get-system-settings")]` **TANPA `[Authorize]`**, mengembalikan semua baris `AppSettingsRecords` (key + value) apa adalah, termasuk:
- `MQTT.Username` / `MQTT.Password` (plaintext — `MqttService.cs:85-86`) → penyerang anonim dapat kontrol broker & data IoT
- `Notification.WhatsAppToken`, `WablasToken`, `WablasSecretKey` (plaintext — `NotificationService.cs:2137-2138`)
- `Notification.SenderPassword` (`ENC:...`; terdekripsi karena kunci AES ada di repo, lihat C1)
- `Chatbot.ApiKey` (ter-encrypt tetapi ikut bocor)

Dokumentasi `SECURITY_SETTINGS_SYNC_FIX_RESULT.md` mengklaim endpoint ini sudah `[Authorize(Admin)]` — **klaim itu tidak terimplementasi di kode**.

### C3. Endpoint debug publik `GET /api/Api/debug/notification-settings-db` (tanpa auth)
**File:** `Controllers/ApiController.cs:5557-5574` — tanpa `[Authorize]`, mengembalikan **nilai mentah** semua `Notification.*` (password SMTP ter-encrypt + token WhatsApp/Wablas plaintext). Endpoint debug harus dihapus atau dikunci Admin + dinonaktifkan di produksi.

---

## 🟠 TINGGI (HIGH)

### H1. Kredensial disimpan plaintext & perlindungan enkripsi tidak konsisten
Password SMTP di-encrypt (`ENC:`) di `SaveNotificationSettings` (`ApiController.cs:5701-5706`), tetapi `Notification.WhatsAppToken` (**plaintext**, `ApiController.cs:5715`), `Notification.WablasToken` & `WablasSecretKey` (**plaintext**, `NotificationService.cs:2137-2138`), serta `MQTT.Password` & `MQTT.TlsClientCertPassword` (**plaintext**, `MqttService.cs:85-91`) tidak. Ketiganya ikut bocor lewat C2/C3.

### H2. Bug fungsi: logika dekripsi SMTP TERBALIK di API admin
**File:** `Controllers/ApiController.cs:5379-5381` (`GetNotificationSettingsFull`):
```csharp
if (!string.IsNullOrEmpty(senderPassword) && !senderPassword.StartsWith("ENC:"))
    senderPassword = _encryption.Decrypt(senderPassword) ?? senderPassword;
```
Syarat dibalik: nilai yang benar (`ENC:...`) **tidak** didekripsi, nilai legacy dipaksa didekripsi (gagal → mentah). Halaman Settings Admin menampilkan string `ENC:<base64>` alih-alih password asli. Logika benar ada di `NotificationService.LoadSettings` (`NotificationService.cs:67-72`, pakai `StartsWith("ENC:")` + `Substring(4)`).

### H3. Rantai migrasi EF rusak — 2 "migration awal" saling menimpa tabel
- `Migrations/20260826064232_initialMigration.cs` membuat 13 tabel (termasuk `AnomalyLogs`, `AppLog`).
- `Migrations/20260916092238_InitialCreate.cs` **membuat ulang tabel yang sama** (`AnomalyLogs`, `AppLog`, `KWHData`, `ApplicationUsers`, ...).

Pada database **baru**, `context.Database.Migrate()` (`Startup.cs:138-148`) gagal di migration kedua: *"There is already an object named ... in the database"*. `Startup` fallback ke `EnsureCreated()` yang **no-op bila database sudah ada** → skema setengah jangan (kolom dari migration berikutnya tak pernah dibuat) dan `__EFMigrationsHistory` tidak sinkron. Kehadiran `fix_efmigrationshistory.sql` + `apply_anomaly_center_phase1.sql` di root membuktikan masalah ini terjadi di lapangan dan diperbaiki manual.

### H4. Data publik tanpa batas → DoS & eksploitasi ekspor
- `GET /api/Api/history` (`ApiController.cs:1283`): `page`/`pageSize` **tanpa validasi** — `pageSize=1000000` memaksa muat jutaan baris; `page<=0` → `Skip(negatif)` → error SQL OFFSET; `pageSize=0` → `DivideByZeroException`. Publik (tanpa `[Authorize]`).
- `GET /api/Api/history-grid` (`ApiController.cs:1378-1381`): `request.Take` tanpa batas.
- `Monitoring/ExportCSV` (`MonitoringController.cs:542-554`): publik, ekspor hingga **50.000 baris** tanpa autentikasi.
- `GET /api/Api/history-export` & `history-archive-export` (`ApiController.cs:1432`, `1719`): publik.

### H5. CSV Formula Injection + XML Injection pada ekspor
**File:** `ApiController.cs:1519-1524` (history-export), `1814-1820` (archive-export), `MonitoringController.cs:556-580`.
- Kolom teks (`DeviceKey`, `DeviceId`, `GroupName`) ditulis **tanpa kutip/escape** di CSV → field berkoma merusak kolom; karakter awal `=` `+` `-` `@` dari data perangkat (MQTT) dieksekusi sebagai formula oleh Excel (CWE-1236).
- Ekspor "Excel" (SpreadsheetML XML) menyisipkan `GroupName`/`DeviceId` **tanpa XML-encoding** (`ApiController.cs:1489`, `1783`) → file rusak / injeksi markup.
- `MonitoringController.CsvEscape` (baris 738-742) hanya melipat ganda kutip, tidak menetralkan karakter formula.

### H6. Potensi Stored XSS via `@Html.Raw(Json.Serialize(...))` di dalam `<script>`
**Lokasi:** `Views/Monitoring/Index.cshtml:596`, `Index.cshtml:925`, `Charts.cshtml:193`.
`Json.Serialize` (Newtonsoft) **tidak meng-escape `</script>`** maupun `<`/`>`. Nilai `deviceKey`/`groupName` berasal dari data perangkat (MQTT/DB). `GroupName` = `</script><script>alert(1)</script>` (muat di `nvarchar(50)`) akan dieksekusi di browser semua pengunjung dashboard publik. Attack surface: siapa pun yang bisa publish topic MQTT / menulis DB.

### H7. Framework End-of-Life tanpa patch keamanan
`KWHMonitoring.csproj:4` — `netcoreapp2.1` (EOL sejak **21 Agustus 2021**). Dependensi: `EF Core 2.1.14`, `MQTTnet 3.1.2` (2020), `Microsoft.AspNetCore.App` 2.1.x — tidak akan pernah menerima perbaikan CVE lagi.

### H8. Lockout akun bermasalah & reset password tidak mencabut sesi
**File:** `Controllers/AccountController.cs`
- **Lockout permanen-per-sesi:** saat lockout berakhir, `AccessFailedCount` **tidak pernah di-reset** (hanya di-reset pada login sukses, baris 119). Karena masih ≥ 5, **1 kali salah password saja** langsung mengunci lagi 15 menit → terkunci berulang tanpa henti (baris 104-117).
- **Reset password tidak mengakhiri sesi lama** (baris 306-345): cookie yang beredar tetap valid (tidak ada security stamp) → penyerang yang sudah login tetap masuk meski korban ganti password; `LockoutEnd`/`AccessFailedCount` juga tidak dibersihkan.
- **Tidak ada rate-limit per-IP** pada `Login`, `Register`, `ResendVerification` (baris 239-251) → brute-force multi-IP dan **email bombing** (siapa pun bisa POST `ResendVerification` berkali-kali untuk alamat korban).

---

## 🟡 SEDANG (MEDIUM)

### M1. Exception detail bocor ke pengguna (bertentangan dengan pola `SafeError`)
`SafeError` (`ApiController.cs:115-121`) sudah bagus (pesan generik + log server), tetapi masih ada sisa:
- `MonitoringController.cs:153, 224, 301, 486, 534` → `TempData["Error"] = "Error: " + ex.Message;` (detail SQL/internal tampil ke user).
- `MonitoringController.cs:646, 682, 704` → `Json(new { ..., message = ex.Message })`.
- `QwenChatController.cs:357` → `detail = ex.Message`; `:341, 349, 933` → memantulkan `responseString` upstream; `:939` → `message = ex.Message`.
Klaim dokumentasi "83+ ex.Message diganti" belum tuntas.

### M2. `AdminApproval/Reject` mengubah state lewat GET
**File:** `Controllers/AdminApprovalController.cs:131-173`. `Reject` GET langsung menonaktifkan akun (`user.IsActive = false`) + memakai token. Link yang diklik otomatis oleh **email security scanner / link prefetcher** dapat menonaktifkan akun tanpa niat admin. `Approve` GET hanya menampilkan konfirmasi (benar) — `Reject` seharusnya juga POST-only.

### M3. Tabel agregat energi TANPA index & tanpa unique constraint
`ApplicationDbContext.OnModelCreating` (baris 332-396) tidak mendeklarasikan index untuk `DailyEnergy`, `HourlyEnergy`, `MonthlyEnergy`, `YearlyEnergy`; grep seluruh `Migrations/` juga **tidak menemukan** `IX_DailyEnergy`/`IX_HourlyEnergy`/dst., padahal `Migrations/README.md` **mengklaim** unique composite index ada (dokumen vs kenyataan tidak cocok). Akibat:
- Query rentang (`WHERE Date >= ...`, `WHERE Hour >= ...`) dijalankan di **hampir setiap request dashboard** (`MonitoringController.cs:330-353`, `ApiController.cs:351-384`) → full scan yang makin lambat.
- Tanpa pengaman DB terhadap **baris duplikat** → job agregat dobel/konkuren membuat angka kWh (tagihan) terdouble-count.

### M4. Kinerja buruk: memuat tabel penuh ke memori
- `QwenChatController.cs:427` → `_context.KWH_Monitoring.ToListAsync()` memuat **SELURUH tabel riwayat** hanya untuk mengambil baris terakhir per device (harusnya GroupBy di SQL).
- `QwenChatController.cs:492` → memuat seluruh `AppSettingsRecords` (termasuk kredensial) hanya untuk membaca tarif.
- Pola dashboard di-**copy-paste 3×** di `Index`/`Charts`/`AnomalyLogs` (`MonitoringController.cs:56-59`, `171-174`, `238-241`) → duplikasi ±250 baris.

### M5. Race condition pada rate-limit & OTP (IMemoryCache tidak atomik)
- `ApiController.cs:115-141` `IsRelayControlRateLimited`: baca-modify-tulis list tanpa lock → request paralel menembus limit 10/60 dtk.
- `ApiController.cs:2586-2598` & `2679-2720`: penghitung gagal-OTP & limit OTP juga non-atomik.
- `DeviceSettingsService.SaveAsync` (`DeviceSettingsService.cs:146-187`): baca-lalu-tulis tanpa transaksi → update konkuren saling menimpa.

### M6. Logika parsing jam downtime salah (pakai `Contains`)
**File:** `Services/AnomalyAnalysisService.cs:66-69` — `log.Notes.Contains("22") ? 22 : 0, log.Notes.Contains("6") ? 6 : 0`. "Jam mati" disimpulkan dari substring bebas pada `Notes`. Contoh salah: notes `"downtime period 16:00-23:00"` mengandung `"6"` → menampilkan jam **6**; notes `"2200 W"` dianggap jam 22. Harusnya parse dari kolom terstruktur `DowntimeStart`/`DowntimeEnd`.

### M7. Filter `OR` pada DevExtreme grid diabaikan diam-diam
**File:** `Controllers/ApiController.cs:1872-1873` — komentar `// For "or", we'd need more complex expression trees` → hanya `and` yang diterapkan. Filter OR dari UI **diproses seolah tidak ada filter** → hasil grid salah tanpa error.

### M8. Cookie "Remember Me" 7 hari vs `ExpireTimeSpan` 30 menit
`Startup.cs:76-77` menetapkan `ExpireTimeSpan = 30 menit` + sliding, sedangkan `SignInUserAsync` (`AccountController.cs:481-485`) menyetel `ExpiresUtc = +7 hari`. Pada ASP.NET Core 2.1 sliding renewal memakai `ExpireTimeSpan` → cookie persistent bisa diterbitkan ulang dengan masa berlaku 30 menit (**"Ingat saya" tidak berfungsi**). Perlu verifikasi runtime; idealnya `ExpireTimeSpan` diatur per-signin.

### M9. CSP memuat `unsafe-inline` + `unsafe-eval`
`Startup.cs:243` — meski header CSP dipasang, `script-src 'self' 'unsafe-inline' 'unsafe-eval' ...` membuat perlindungan XSS dari CSP praktis **tidak berarti**.

### M10. CSRF tidak konsisten
Hanya sebagian POST yang memakai `[ValidateAntiForgeryToken]` (Login/Logout/relay). `[HttpPost] UpdateSettings` (`MonitoringController.cs:586-588`), `save-notification-settings`, `save-ema-settings`, `save-system-settings`, `device-settings`, dst. **tidak**. `SameSite=Lax` memberi mitigasi parsial, tetapi tetap rentan pada skenario tertentu (subdomain, redirect).

### M11. Status HTTP 200 untuk error database di halaman browser
`Filters/DatabaseExceptionFilter.cs:46-51` — untuk request non-API dikembalikan `ViewResult "DatabaseError"` **tanpa men-set status 500/503** → uptime checker melihat HTTP 200 padahal DB mati.

### M12. Seed admin gagal hanya tercatat di `Debug.WriteLine`
`Services/DbInitializer.cs:31, 37, 51, 57, 95` — kegagalan konfigurasi admin awal tidak pernah masuk log aplikasi (hilang di Release). `IsStrongPassword` (baris 113-118) juga hanya cek panjang ≥ 12 tanpa kompleksitas.

### M13. Log berlebihan berisi pesan user & payload besar (QwenChat)
`QwenChatController.cs:155` (pesan user mentah di-log), `:170-171` (seluruh payload `RealTimeData` di-serialize ke log) → risiko PII di log + **log flooding** (payload tanpa batas ukuran).

### M14. Chatbot: biaya API & SSRF
`QwenChatController.cs:147-359` — `[HttpPost("send")]` tidak membatasi panjang pesan/`History` → token (biaya) & memori terkuras. `Chatbot.ApiUrl` diambil dari DB (baris 83-84) lalu server kirim `Bearer <apikey>` ke URL itu → **SSRF** bila admin terkompromi. `[HttpGet("test")]` (baris 877) bisa dipanggil Viewer → memicu panggilan API berbayar.

### M15. Campur `DateTime.Now` vs `DateTime.UtcNow` di seluruh codebase
Audit log `UtcNow` (`AccountController.cs:500`), tetapi `AppSettingsRecord.UpdatedAt`, statistik (`MonitoringController.cs:323`), `HealthController`, background service memakai lokal/campur → batas laporan harian/bulanan bisa meleset dan timestamp audit tidak konsisten untuk investigasi.

---

## 🔵 RENDAH (LOW) / INFO

| # | Temuan | Lokasi |
|---|---|---|
| L1 | `Math.Abs(BitConverter.ToInt32(...))` untuk OTP bisa melempar `OverflowException` bila `int.MinValue` (sangat jarang); sedikit modulo bias | `ApiController.cs:2709` |
| L2 | `Skip((page-1)*pageSize)` tanpa validasi `page` (≤0) → exception | `MonitoringController.cs:514`, `ApiController.cs:1312` |
| L3 | `toDate.AddDays(1)` pada filter tanggal (input berjam → rentang kelebihan s.d. 24 jam; `<=` vs `<` tidak konsisten antar endpoint) | `MonitoringController.cs:507` vs `ApiController.cs:1305` (`<`) |
| L4 | `ValidateServerCertificate` mengabaikan return value `chain.Build(...)` — validasi rantai tidak dipastikan | `MqttService.cs:318` |
| L5 | Upload sertifikat: file temp `GetTempFileName()` bocor bila validasi melempar exception; allowlist ekstensi memuat `""` (file tanpa ekstensi diterima) | `ApiController.cs:2352-2420` |
| L6 | `GetRelayStates` catch mengembalikan `success: true` (dengan field error) → kegagalan ditandai sukses | `ApiController.cs:2826` |
| L7 | `deviceId` pada `publish-relay` tidak divalidasi bentuknya → potensi injeksi karakter topic MQTT (`/`, `#`) oleh user Operator+ | `MqttService.cs:360`, `ApiController.cs:2563` |
| L8 | Audit log pada exception relay selalu mencatat action `RelayOn` meski perintahnya OFF/Pulse | `ApiController.cs:2652` |
| L9 | `SendEmailAsync` men-set `mailMessage.Body` **dan** `AlternateView` berisi body sama → duplikasi konten di sebagian client email | `NotificationService.cs:1616-1621` |
| L10 | `lock (this)` pada service (lock objek publik — code smell); `_appSettings` static mutable pada controller | `NotificationService.cs:51`, `MonitoringController.cs:24` |
| L11 | `ApplicationDbContext.AppSettingsChanged` berupa field `static Action` (bukan event) yang di-assign dari konstructor singleton | `ApplicationDbContext.cs:15`, `AppSettingsCache.cs:33` |
| L12 | `UserManagementController.Delete` menghapus user tetapi `SecurityAuditLogs` miliknya dibiarkan jadi orphan (tanpa FK) | `UserManagementController.cs:126-159` |
| L13 | `Register` tanpa CAPTCHA/rate-limit → pendaftaran massal; `NormalizeEmail` membuang karakter aneh sehingga normalisasi bisa beda dari alamat asli | `AccountController.cs:145-179, 506-514` |
| L14 | `HealthController.Db` `[AllowAnonymous]` mengungkap status DB (wajar untuk health-check, tetap info disclosure) | `HealthController.cs:22` |
| L15 | Legacy AES decrypt memakai **IV deterministik** (16 byte pertama key) dan heuristic `Length > 32` untuk pilih format — data legacy > 32 byte selalu dicoba sebagai format baru dulu | `AesEncryptionService.cs:65, 89-99` |
| L16 | Komentar mengandung karakter asing (`maksimal на 1000 data` — Cyrillic) — indikasi copy-paste/generate | `ApiController.cs:1739` |
| L17 | Repo hygiene: `obj/hash_password.csproj.*` sisa project terhapus; `bin/` kosong di root; `.qwen/build-temp*` | root project |
| L18 | Duplikasi kode dashboard ±250 baris di `Index`/`Charts`/`AnomalyLogs` (copy-paste) → rawan perbaikan tidak konsisten | `MonitoringController.cs` |
| L19 | `GetHistory` tidak memvalidasi input tanggal (`DateTime.TryParse` gagal → diabaikan diam-diam, filter bocor) | `ApiController.cs:1290-1294` |
| L20 | Tidak ada `AutoValidateAntiforgeryToken` global; keamanan antiforgery mengandalkan atribut per-endpoint yang belum lengkap | `Startup.cs:104-107` |

---

## ✅ YANG SUDAH BAIK (konteks positif)

- **Build bersih**: 0 error/0 warning pada SDK 2.1.818.
- `PasswordHasher`: PBKDF2-HMAC-SHA256, 100k iterasi, salt acak, perbandingan constant-time.
- `SafeError()` generik + logging server-side untuk mayoritas endpoint API.
- Token email di-hash SHA-256, sekali pakai + kadaluarsa; `returnUrl` divalidasi `Url.IsLocalUrl` (anti open-redirect).
- Endpoint relay: `[Authorize(Roles="Operator,Admin")]` + antiforgery + rate-limit 10/60 dtk + OTP email untuk perintah OFF (3 percobaan, 5 menit).
- Header keamanan (X-Frame-Options, nosniff, dll.) dipasang; cookie `HttpOnly` + `SameSite=Lax`; response compression aktif.
- `TitikLokasiService` memvalidasi nama DB/tabel via regex (anti SQL-injection) + cache + fail-cooldown ke DB ERP.
- **Tidak ditemukan SQL injection** (semua query lewat EF LINQ/parameterized; SQL statis di `GetRelayStates`).
- Filter DevExtreme memakai **whitelist** kolom (bukan reflection bebas) → aman dari injeksi ekspresi.
- Background service notifikasi ditulis hati-hati (guard `async void`, anti-reentrancy, persist jadwal kirim agar tidak dobel setelah restart).
- Tidak ditemukan `@Html.Raw` pada data user-input selain pola `Json.Serialize` (lihat H6).

---

## 🎯 Prioritas Perbaikan yang Disarankan

1. **Segera (hari ini):** Rotasi semua secret (C1) → tutup/otentikasi `get-system-settings` & hapus endpoint `debug/*` (C2, C3).
2. **Minggu ini:** Perbaiki logika dekripsi ENC terbalik (H2); bungkus sisa `ex.Message` dengan `SafeError` (M1); validasi `page/pageSize/Take` (H4); netralisasi CSV formula + XML-escape (H5); pindahkan `AdminApproval/Reject` ke POST (M2).
3. **Sprint berikutnya:** Perbaiki rantai migrasi (H3 — hilangkan migration duplikat + transaksi); reset `AccessFailedCount` saat lockout berakhir & tambah rate-limit per-IP (H8); tambah unique index + index rentang pada tabel agregat (M3); ganti `@Html.Raw(Json.Serialize)` dengan escape `</script>` aman (H6).
4. **Rencana jangka panjang:** Upgrade ke .NET LTS yang masih didukung (H7); konsolidasi semua kredensial ke penyimpanan ter-encrypt (H1); optimasi query dashboard (M4).

---

*Catatan: Audit ini berupa static analysis + build verification. Pengujian runtime terhadap database produksi (migration end-to-end, perilaku cookie sliding, eksekusi SQL aktual) belum dilakukan karena memerlukan akses environment — poin tersebut ditandai "perlu verifikasi runtime". Tidak ada source code yang diubah selama audit ini.*





