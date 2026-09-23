# Implementasi Keamanan Berbasis Email - Task #1 s/d #4

## Ringkasan
Implementasi fitur keamanan email-based authentication, email verification, dan approval workflow untuk aplikasi KWH Monitoring ASP.NET Core 2.1.

## Task Selesai
- ✅ Task #1: Setup database models (`ApplicationUser`, `EmailVerificationToken`, `SecurityAuditLog`) dan migration
- ✅ Task #2: Implement `EmailService`
- ✅ Task #3: Implementasi autentikasi (Register, Login, Logout, Email Verification, Forgot Password)
- ✅ Task #4: Implementasi approval workflow (new user → master admin email → approval link → role assignment)

## Perubahan File

### 1. Models
- `Models/ApplicationUser.cs` - Model user dengan email, password hash, role
- `Models/EmailVerificationToken.cs` - Token verifikasi dan approval
- `Models/SecurityAuditLog.cs` - Log audit keamanan
- `Models/ApplicationDbContext.cs` - Update DbContext untuk tabel baru
- `Models/AccountViewModels.cs` - ViewModels untuk Login, Register, Forgot/Reset Password

### 2. Services
- `Services/EmailService.cs` - Layanan pengirim email SMTP
- `Services/IEmailService.cs` - Interface email service
- `Services/PasswordHasher.cs` - Utility hashing password dengan PBKDF2
- `Services/DbInitializer.cs` - Seed admin default saat startup

### 3. Controllers
- `Controllers/AccountController.cs` - Register, login, logout, verify email, forgot password
- `Controllers/AdminApprovalController.cs` - Handle approval link dari email master admin

### 4. Views
- `Views/Account/Login.cshtml`
- `Views/Account/Register.cshtml`
- `Views/Account/RegistrationSuccess.cshtml`
- `Views/Account/VerificationSuccess.cshtml`
- `Views/Account/ForgotPassword.cshtml`
- `Views/Account/ForgotPasswordConfirmation.cshtml`
- `Views/Account/ResetPassword.cshtml`
- `Views/Account/ResetPasswordSuccess.cshtml`
- `Views/Account/AccessDenied.cshtml`
- `Views/AdminApproval/Approve.cshtml`
- `Views/AdminApproval/Reject.cshtml`

### 5. Configuration & Startup
- `Startup.cs` - Registrasi authentication cookie dan authorization policy, registrasi `NotificationService` untuk digunakan oleh `EmailService`
- `appsettings.json` - Konfigurasi umum aplikasi (tanpa hardcoded SMTP)
- `global.json` (root dan project) - Update SDK ke 2.1.818 agar kompatibel dengan .NET Core 2.1

### 6. UI Shared
- `Views/Shared/_Layout.cshtml` - Menampilkan status login/logout di topbar

### 7. Database Migration & Deployment Note
- Migration pertama (`20260909032743_AddEmailAuthentication`) yang dihasilkan dengan `--no-build` ternyata kosong karena masih mereferensi assembly lama. Oleh karena itu tabel dibuat langsung via SQL script manual ke database `HaiwellElectrical`.
- Tabel yang dibuat: `ApplicationUsers`, `EmailVerificationTokens`, `SecurityAuditLogs`
- Admin default dimasukkan langsung ke tabel `ApplicationUsers`.

## Alur Kerja
1. User register dengan email dan password
2. Sistem kirim email verifikasi dengan token unik
3. User klik link verifikasi → akun aktif, role masih "None"
4. Sistem kirim email request akses ke master admin email
5. Master admin klik link approval dan pilih role (Viewer/Operator)
6. User mendapat email konfirmasi persetujuan
7. User dapat login sesuai role

## Akun Default
- Email: `sattvikoramdhani@gmail.com`
- Password: `MasterAdmin2024!`
- Role: `Admin`

**Note:** Password harus segera diganti setelah login pertama kali.

## Kenapa Email Verifikasi Belum Terkirim?
Email belum terkirim karena konfigurasi SMTP belum tersimpan di tabel `AppSettingsRecords`. Sistem akan tetap mencoba mengirim email, tapi akan gagal karena tidak ada SMTP host yang dikonfigurasi. Silakan isi konfigurasi SMTP melalui halaman Settings > Notifications.

## Konfigurasi yang Perlu Diisi
Buka halaman **Settings > Notifications** dan isi:

- **SMTP Server**: `smtp.gmail.com`
- **SMTP Port**: `587`
- **Sender Email**: email pengirim (contoh: `your-email@gmail.com`)
- **App Password**: App Password dari email pengirim
- **Master Admin Email**: `sattvikoramdhani@gmail.com` (atau email admin master lainnya)
- **Recipient Email**: email penerima notifikasi anomali (bisa dipisah koma)

Konfigurasi akan tersimpan di tabel `AppSettingsRecords` dengan key berikut:
- `Notification.SmtpServer`
- `Notification.SmtpPort`
- `Notification.SenderEmail`
- `Notification.SenderPassword`
- `Notification.MasterAdminEmail`
- `Notification.RecipientEmail`
- `Notification.EnableEmail`

## Catatan Keamanan
- Token menggunakan RandomNumberGenerator (bukan Guid.NewGuid)
- Password hash menggunakan PBKDF2 (Rfc2898DeriveBytes) dengan 100.000 iterasi
- Cookie authentication dengan HttpOnly, Secure, SameSite=Lax
- Role-based authorization policy sudah didefinisikan di Startup
- Audit logging untuk setiap aksi autentikasi dan perubahan role
- Lockout account setelah 5 kali percobaan gagal
- Token verifikasi dan approval expired dalam 24 jam
- Password reset token expired dalam 1 jam

## Arsitektur SMTP Refactor
SMTP untuk verifikasi email dan notifikasi anomali sekarang menggunakan satu sumber konfigurasi: tabel `AppSettingsRecords`.

### Alur
1. User/admin mengisi konfigurasi SMTP di halaman **Settings > Notifications**
2. `ApiController.SaveNotificationSettings()` menyimpan ke `AppSettingsRecords`
3. `NotificationService` membaca konfigurasi dari `AppSettingsRecords` saat mengirim email notifikasi anomali
4. `EmailService` menerima `NotificationService` melalui constructor dan menggunakan overload `SendEmailAsync(string to, string subject, string body)` untuk mengirim email verifikasi/approval
5. `AccountController.GetMasterAdminEmailAsync()` membaca `Notification.MasterAdminEmail` dari `AppSettingsRecords`

### Keuntungan
- Tidak ada lagi hardcoded SMTP di `appsettings.json` atau kode
- Satu tempat konfigurasi untuk semua kebutuhan email (anomali dan autentikasi)
- Mudah diubah melalui UI tanpa redeploy aplikasi

## Bug Fix
- `_AnomalyService.cshtml` dipindahkan dari `Views/Monitoring/` ke `Views/Shared/` karena `_Layout.cshtml` memanggil partial tersebut, tetapi partial hanya tersedia di folder Monitoring. Akibatnya halaman Account (Login, Register, dll) yang menggunakan `_Layout` gagal render. Sekarang partial bisa ditemukan dari semua view.

## Next Steps (Task #5 - #9)
- Task #5: Proteksi halaman Settings dengan `[Authorize(Roles = "Admin")]` dan audit log
- Task #6: Proteksi tombol ON/OFF dengan `[Authorize(Roles = "Operator/Admin")]` + audit log + rate limiting
- Task #7: Email notifikasi untuk aksi kritis (ON/OFF, Settings change, role change)
- Task #8: Update UI/UX: sembunyikan tombol sesuai role, modal konfirmasi
- Task #9: Testing end-to-end
