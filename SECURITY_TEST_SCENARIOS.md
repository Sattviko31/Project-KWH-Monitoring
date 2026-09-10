# Skenario Testing End-to-End Security (Task #5 - #9)

Dokumen ini berisi skenario test manual untuk memverifikasi fitur keamanan yang baru diimplementasikan.

## Prasyarat

- Aplikasi berjalan dan dapat diakses.
- SMTP sudah dikonfigurasi di halaman Settings > Notifications.
- Tersedia 3 akun test:
  - Admin: `sattvikoramdhani@gmail.com`
  - Operator: (dengan role Operator)
  - Viewer: (dengan role Viewer)

---

## Skenario 1: Akses Halaman Settings

### 1.1 Admin mengakses Settings
1. Login sebagai Admin.
2. Buka `/Monitoring/Settings`.
3. **Expected**: Halaman Settings terbuka dan audit log `SettingsViewed` tercatat di tabel `SecurityAuditLogs`.

### 1.2 Viewer mengakses Settings
1. Login sebagai Viewer.
2. Coba buka `/Monitoring/Settings`.
3. **Expected**: Redirect ke `/Account/AccessDenied` atau halaman login.

---

## Skenario 2: Tombol ON/OFF Berdasarkan Role

### 2.1 Admin/Operator melihat tombol ON/OFF
1. Login sebagai Admin atau Operator.
2. Buka halaman Dashboard (`/Monitoring/Index`).
3. Expand panel device.
4. **Expected**: Tombol ON/OFF/Pulse/Toggle terlihat.

### 2.2 Viewer tidak melihat tombol ON/OFF
1. Login sebagai Viewer.
2. Buka halaman Dashboard.
3. Expand panel device.
4. **Expected**: Tombol kontrol tidak terlihat, muncul pesan: "Kontrol perangkat memerlukan role Operator atau Admin."

---

## Skenario 3: API publish-relay Terproteksi

### 3.1 Viewer tidak bisa memanggil publish-relay
1. Login sebagai Viewer.
2. Buka browser console.
3. Jalankan:
```javascript
fetch('/api/Api/publish-relay', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ DeviceId: 'TEST-001', RCValue: '1', Pulse: false })
});
```
4. **Expected**: HTTP 401 atau 403.

### 3.2 Operator dapat memanggil publish-relay
1. Login sebagai Operator.
2. Klik tombol ON pada panel device.
3. Klik "Ya, Lanjutkan" di modal konfirmasi.
4. **Expected**: Perintah terkirim, audit log `RelayOn` tercatat, email notifikasi dikirim ke master admin.

---

## Skenario 4: Rate Limiting Relay

1. Login sebagai Operator.
2. Klik tombol ON/OFF lebih dari 10 kali dalam 60 detik.
3. **Expected**: Setelah 10 kali, muncul pesan error "Terlalu banyak perintah relay. Silakan tunggu 60 detik." dan audit log `UnauthorizedAttempt` tercatat.

---

## Skenario 5: Audit Log

1. Lakukan berbagai aksi: login, logout, ON/OFF device, ubah settings, ubah role user.
2. Jalankan query SQL:
```sql
SELECT Action, Email, TargetDevice, Details, Success, Timestamp
FROM SecurityAuditLogs
ORDER BY Timestamp DESC;
```
3. **Expected**: Semua aksi kritis tercatat lengkap dengan email, device target, dan status sukses/gagal.

---

## Skenario 6: Email Notifikasi Kritis

1. Pastikan SMTP terkonfigurasi dan `Notification.MasterAdminEmail` terisi.
2. Lakukan aksi berikut:
   - ON/OFF device
   - Simpan System Settings
   - Ubah role user melalui User Management
3. **Expected**: Master admin menerima email notifikasi untuk setiap aksi di atas.

---

## Skenario 7: Modal Konfirmasi

### 7.1 Konfirmasi Relay
1. Login sebagai Operator.
2. Klik tombol ON pada panel device.
3. **Expected**: Muncul modal konfirmasi dengan detail device dan aksi.
4. Klik "Batal" → tidak terjadi apa-apa.
5. Klik "Ya, Lanjutkan" → perintah dikirim.

### 7.2 Konfirmasi Settings
1. Login sebagai Admin.
2. Buka Settings > System.
3. Ubah nilai setting.
4. Klik "Save Settings".
5. **Expected**: Muncul modal konfirmasi sebelum data dikirim ke server.

---

## Skenario 8: API save-system-settings dan save-notification-settings

### 8.1 Admin dapat menyimpan settings
1. Login sebagai Admin.
2. Jalankan dari console:
```javascript
fetch('/api/Api/save-system-settings', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ 'Test.Key': 'TestValue' })
});
```
3. **Expected**: HTTP 200 OK.

### 8.2 Viewer tidak dapat menyimpan settings
1. Login sebagai Viewer.
2. Jalankan request yang sama seperti 8.1.
3. **Expected**: HTTP 401 atau 403.

---

## Catatan Hasil Test

| Skenario | Tester | Tanggal | Hasil | Catatan |
|----------|--------|---------|-------|---------|
| 1.1 | | | | |
| 1.2 | | | | |
| 2.1 | | | | |
| 2.2 | | | | |
| 3.1 | | | | |
| 3.2 | | | | |
| 4 | | | | |
| 5 | | | | |
| 6 | | | | |
| 7.1 | | | | |
| 7.2 | | | | |
| 8.1 | | | | |
| 8.2 | | | | |
