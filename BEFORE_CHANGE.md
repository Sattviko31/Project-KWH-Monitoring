# Laporan Kondisi Sebelum Perubahan (Task #5 - #9)

## Ringkasan
Dokumen ini merekam kondisi codebase sebelum implementasi Task #5 sampai #9 dari rencana keamanan berbasis email. Task #1 - #4 sudah selesai dan stabil.

## Status Task #1 - #4

| Task | Status | Catatan |
|------|--------|---------|
| #1 Database models & migration | ✅ Selesai | `ApplicationUser`, `EmailVerificationToken`, `SecurityAuditLog`, tabel dibuat manual via SQL |
| #2 EmailService | ✅ Selesai | `NotificationService` + `EmailService` + `IEmailService` |
| #3 Autentikasi | ✅ Selesai | Register, Login, Logout, Verify Email, Forgot/Reset Password |
| #4 Approval workflow | ✅ Selesai | New user → master admin email → approval link → role assignment |

## Kondisi Keamanan Saat Ini

### 1. Autentikasi & Otorisasi
- Cookie authentication sudah aktif dengan `HttpOnly`, `SecurePolicy=SameAsRequest`, `SameSite=Lax`.
- Authorization policies di `Startup.cs`: `RequireAdmin`, `RequireOperator`, `RequireViewer`.
- Controller `MonitoringController.Settings()`, `UpdateSettings()`, `GetSettings()` sudah memiliki `[Authorize(Roles = "Admin")]`.
- Controller `UserManagementController` sudah memiliki `[Authorize(Roles = "Admin")]` dan proteksi master admin.

### 2. Audit Logging
- Tabel `SecurityAuditLogs` tersedia.
- Audit log saat ini tercatat untuk: `Register`, `Login`, `LoginFailed`, `Lockout`, `Logout`, `EmailVerificationSent`, `EmailVerified`, `AccessRequestSent`, `PasswordResetRequested`, `PasswordReset`.
- **Belum tercatat**: `SettingsViewed`, `SettingsUpdated`, `RelayOn`, `RelayOff`, `RelayPulse`, `RoleChanged`.

### 3. Kontrol Relay / Tombol ON/OFF
- Endpoint: `POST /api/Api/publish-relay` di `ApiController.cs`.
- **Tidak memiliki atribut `[Authorize]`** — siapa saja yang login dapat mengakses.
- **Tidak ada audit log** untuk aksi ON/OFF.
- **Tidak ada rate limiting** — dapat dipanggil berulang kali tanpa batas.
- Tombol ON/OFF di `_PanelCard.cshtml` selalu ditampilkan tanpa memeriksa role pengguna.

### 4. Email Notifikasi Kritis
- Email saat ini h digunakan untuk: verifikasi email, approval request, approval confirmation, rejection, reset password, dan laporan/anomali otomatis.
- **Belum ada email notifikasi untuk**: aksi ON/OFF perangkat, perubahan settings, perubahan role user.

### 5. UI/UX
- Halaman `Settings.cshtml` menampilkan tab "User Management" hanya untuk master admin (`ViewBag.IsMasterAdmin`).
- Tombol kontrol relay di `_PanelCard.cshtml` tidak disembunyikan berdasarkan role.
- Tidak ada modal konfirmasi sebelum aksi ON/OFF.

## File yang Akan Dimodifikasi

| File | Alasan Perubahan |
|------|------------------|
| `Controllers/ApiController.cs` | Proteksi `publish-relay`, audit log, rate limiting, email notifikasi kritis |
| `Controllers/MonitoringController.cs` | Audit log `SettingsViewed` dan `SettingsUpdated` |
| `Controllers/UserManagementController.cs` | Audit log `RoleChanged`, email notifikasi perubahan role |
| `Views/Monitoring/_PanelCard.cshtml` | Sembunyikan tombol ON/OFF sesuai role |
| `Views/Monitoring/Index.cshtml` | Tambahkan modal konfirmasi aksi relay |
| `Views/Monitoring/Settings.cshtml` | Tambahkan modal konfirmasi perubahan settings |
| `Services/NotificationService.cs` atau `Services/EmailService.cs` | Tambahkan helper email notifikasi kritis |
| `Models/SecurityAuditLog.cs` | (jika diperlukan) tambahkan nilai enum |

## Risiko Utama yang Perlu Diatasi

1. **Endpoint relay tanpa otorisasi** — user Viewer dapat mematikan/menyalakan perangkat.
2. **Tidak ada audit trail** untuk aksi kritis (ON/OFF, settings, role).
3. **Tidak ada rate limiting** pada tombol ON/OFF — risiko abuse/flood.
4. **UI tidak menyembunyikan tombol** sesuai role — UX dan keamanan lemah.
5. **Tidak ada notifikasi email** saat aksi kritis dilakukan.

---

*Dokumen ini dibuat sebelum implementasi Task #5 - #9.*
