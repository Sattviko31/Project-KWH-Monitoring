# Catatan Sesi: Perbaikan Freshness Data ERP pada Card Monitoring

**Tanggal:** 2026-10-05  
**Status:** ✅ Perbaikan kode selesai; validasi runtime ERP masih diperlukan

## Tujuan

Memastikan data berikut yang ditampilkan pada card monitoring berasal dari record ERP paling baru untuk setiap lokasi:

- ID pelanggan (`IDPelanggan`)
- Catatan (`Catatan`)
- Daya terpasang (`Daya` / `InstalledCapacityVA`)

Perubahan ini tidak menghapus atau mengubah nilai konfigurasi `MaxCapacity` yang tersimpan pada database monitoring.

## Masalah yang Ditemukan

Query lama menggunakan `SELECT DISTINCT`, kemudian mengambil baris pertama yang terbaca untuk setiap `TitikLokasiID`. Karena SQL Server tidak menjamin urutan hasil query tanpa `ORDER BY`, baris yang dipilih tidak selalu merupakan data terbaru.

Selain itu, hasil lookup ERP sebelumnya disimpan dalam memory cache selama 15 menit. Akibatnya, perubahan data ERP dapat terlambat tampil pada dashboard.

## Perbaikan yang Diterapkan

### 1. Pemilihan record terbaru secara deterministik

File:

`Services/TitikLokasiService.cs`

Query sekarang menggunakan `ROW_NUMBER()`:

```sql
ROW_NUMBER() OVER (
    PARTITION BY l.[TitikLokasiID]
    ORDER BY t.[Periode] DESC, t.[TagihanListrikID] DESC
)
```

Aturan pemilihannya:

1. Untuk setiap `TitikLokasiID`, pilih `TagihanListrik` dengan `Periode` paling baru.
2. Jika terdapat beberapa record dengan periode yang sama, pilih `TagihanListrikID` paling besar sebagai tie-breaker.
3. Hanya record dengan `RowNumber = 1` yang dikembalikan ke aplikasi.

Dengan demikian, field berikut diambil dari record tagihan terbaru yang terpilih:

```csharp
IDPelanggan = Normalize(Convert.ToString(reader["IDPelanggan"])),
Catatan = Normalize(Convert.ToString(reader["Catatan"])),
DayaVA = ReadDecimal(reader["Daya"])
```

### 2. Cache ERP dinonaktifkan secara default

File:

`appsettings.json`

Konfigurasi diubah menjadi:

```json
"CacheMinutes": 0
```

Saat nilainya `0`:

- service tidak membaca cache ERP lama;
- service tidak menyimpan hasil lookup ERP baru ke cache;
- setiap pemuatan dashboard mencoba membaca data terbaru dari ERP;
- cache kegagalan (`FailCooldownSeconds`) tetap digunakan untuk mencegah percobaan koneksi berulang ketika ERP sedang bermasalah.

Cache positif masih dapat diaktifkan kembali bila diperlukan dengan mengubah `WwmErp:CacheMinutes` menjadi nilai lebih besar dari `0`.

### 3. Alur data ke card monitoring

`MonitoringController` mengambil hasil dari `TitikLokasiService`, kemudian meneruskan:

```csharp
InstalledCapacityVA = installedCapacityVA
```

Nilai tersebut digunakan untuk menghitung `MaxCapacity` tampilan berdasarkan daya terpasang dan `Cos_Phi`. Nilai konfigurasi `MaxCapacity` perangkat tidak dihapus dan tidak ditimpa oleh perubahan ini.

## File yang Berubah dalam Perbaikan Ini

- `Services/TitikLokasiService.cs`
- `appsettings.json`
- `Views/Monitoring/_PanelCard.cshtml` hanya dirapikan pada akhir file saat validasi whitespace; tidak ada perubahan kontrak data.

## Validasi yang Dilakukan

- Struktur query dan relasi `TagihanListrik -> RekListrik -> TitikLokasi` diperiksa.
- Pemetaan `IDPelanggan`, `Catatan`, dan `Daya` diperiksa.
- Jalur penerusan `DayaVA` ke `InstalledCapacityVA` pada controller diperiksa.
- Konfigurasi `CacheMinutes = 0` diperiksa.
- Pemeriksaan whitespace/diff dilakukan selama verifikasi.

## Keterbatasan Validasi

Query langsung ke database ERP produksi belum dapat dijalankan dari lingkungan pengembangan ini karena tool `sqlcmd` tidak tersedia dan koneksi runtime ERP belum dapat diverifikasi secara langsung.

Oleh karena itu, perbaikan ini telah memvalidasi logika pemilihan record terbaru pada kode, tetapi tetap perlu diuji di lingkungan yang memiliki akses ke SQL Server ERP dengan skenario berikut:

1. Satu lokasi dengan beberapa record `TagihanListrik` berbeda periode.
2. Dua record dengan `Periode` sama tetapi `TagihanListrikID` berbeda.
3. Perubahan `IDPelanggan` pada record terbaru.
4. Perubahan `Catatan` pada record terbaru, termasuk catatan kosong dan multiline.
5. Perubahan `Daya` pada record terbaru.
6. Reload dashboard setelah perubahan data ERP dan memastikan nilai terbaru tampil.

## Catatan Asumsi

Pemilihan data terbaru menggunakan `TagihanListrik.Periode` sebagai kolom waktu utama dan `TagihanListrikID` sebagai tie-breaker. Jika database ERP memiliki kolom audit yang lebih akurat, misalnya `UpdatedAt` atau `TanggalUpdate`, kolom tersebut sebaiknya diprioritaskan setelah struktur database dikonfirmasi.