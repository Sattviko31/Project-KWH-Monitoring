# Panduan Setup Database KWHMonitoring

## Tujuan
Dokumen ini menjelaskan cara setup database untuk project KWHMonitoring di device baru. Database dan semua tabel akan tergenerate otomatis.

## Opsi Setup

### Opsi 1: Menggunakan Entity Framework Migrations (RECOMMENDED)

#### Prerequisites
- .NET Core SDK 2.1+ sudah terinstall
- SQL Server sudah running
- Connection string sudah dikonfigurasi

#### Langkah-langkah

1. **Buka Command Prompt/PowerShell di folder project**
   ```bash
   cd "KWHMonitoring"
   ```

2. **Restore NuGet packages**
   ```bash
   dotnet restore
   ```

3. **Install EF Core tools** (jika belum)
   ```bash
   dotnet tool install --global dotnet-ef --version 2.1.1
   ```

4. **Verifikasi connection string**
   
   Pastikan file `appsettings.json` sudah benar:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=NAMA_SERVER;Database=KWHMonitoring;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
     }
   }
   ```

5. **Jalankan migration**
   ```bash
   dotnet ef database update
   ```

   Perintah ini akan:
   - ✅ Membuat database `KWHMonitoring` jika belum ada
   - ✅ Membuat 7 tabel dengan struktur lengkap
   - ✅ Membuat semua indexes untuk performa
   - ✅ Seed default data di AppSettings

6. **Verify database created**
   ```bash
   # Check tables yang sudah dibuat
   dotnet ef database list
   ```

### Opsi 2: Menggunakan SQL Script Manual

Jika tidak menggunakan EF Core, bisa run script SQL langsung.

#### Langkah-langkah

1. **Buka SQL Server Management Studio (SSMS)** atau Azure Data Studio

2. **Connect ke SQL Server instance** Anda

3. **Buka file** `scripts/quickstart.sql` dari project

4. **Execute script** (Ctrl+Shift+E)

   Script ini akan:
   - ✅ Create database `KWHMonitoring`
   - ✅ Create semua tabel
   - ✅ Create semua indexes
   - ✅ Insert default settings

5. **Verifikasi**
   ```sql
   SELECT TABLE_NAME 
   FROM INFORMATION_SCHEMA.TABLES 
   WHERE TABLE_TYPE = 'BASE TABLE' 
   ORDER BY TABLE_NAME;
   ```

   Harus muncul 7 tabel:
   - AnomalyLogs
   - AppSettings
   - DailyEnergy
   - HourlyEnergy
   - KWHData
   - MonthlyEnergy
   - YearlyEnergy

## Struktur Database

### 1. KWHData
Tabel utama untuk menyimpan data mentah dari monitoring device.
- Menyimpan reading voltase, current, power, energy per waktu
- Berisi 18 kolom termasuk ID dan timestamp

### 2. AnomalyLogs
Menyimpan log anomali/alarm yang terdeteksi sistem.
- Tipe anomali, nilai threshold, deviasi
- Status acknowledgment dan catatan

### 3. AppSettings
Konfigurasi aplikasi yang bisa diubah runtime.
- MaxCapacity (default: 100000 Watt)
- NotificationEnabled (default: false)
- CheckIntervalSeconds (default: 30 detik)

### 4. HourlyEnergy
Data agregat energi per jam (kWh).
- Unique constraint per DeviceKey + Hour

### 5. DailyEnergy
Data agregat energi per hari (kWh).
- Unique constraint per DeviceKey + Date

### 6. MonthlyEnergy
Data agregat energi per bulan (kWh).
- Unique constraint per DeviceKey + Year + Month

### 7. YearlyEnergy
Data agregat energi per tahun (kWh).
- Unique constraint per DeviceKey + Year

## Troubleshooting

### Error: "Database cannot be opened"
**Solusi:** Pastikan SQL Server service sedang running dan port 1433 terbuka di firewall.

### Error: "Login failed for user"
**Solusi:** Cek username/password di appsettings.json, pastikan user SQL Server sudah punya akses ke database.

### Error: "Table already exists"
**Solusi:** Tidak perlu khawatir, script sudah handle conditional create. Tabel akan skip jika sudah ada.

### Migration berulang kali error
**Solusi:** Reset migration dengan cara:
```bash
dotnet ef database drop
dotnet ef database update
```

⚠️ **PERINGATAN:** Command di atas akan menghapus semua data!

## Deploy ke Production

Untuk deployment di environment production:

1. **Backup database sebelum deploy**
   ```sql
   BACKUP DATABASE [KWHMonitoring] TO DISK = 'C:\Backups\KWHMonitoring_PreDeploy.bak'
   ```

2. **Update connection string** sesuai production server

3. **Run migration**
   ```bash
   dotnet ef database update
   ```

4. **Verify application** berjalan normal

## Performance Tips

1. **Index sudah optimal** untuk query time-series
2. **Partition table** KWHData jika volume > 10 juta records
3. **Regular cleanup** data lama di KWHData (archive ke tabel aggregated)
4. **Monitor disk space** untuk database yang besar

## Version Info

- **EF Core Version:** 2.1.14
- **SQL Server:** Compatible dengan SQL Server 2012+
- **Migration Version:** InitialCreate v1.0

## Contacts

Jika ada pertanyaan atau issue, hubungi developer atau buat issue di repository.
