# Rencana Implementasi Anomali Perbandingan Estimasi dan Aktual Tagihan

> Status diperbarui pada 9 Oktober 2026. Implementasi source code, konfigurasi, migration, integrasi Usage Statistics, dan threshold per-device sudah ditambahkan. Migration belum diterapkan ke database production.

## 1. Ruang Lingkup

Fitur ini menambahkan deteksi anomali pada halaman **Usage Statistics**, khususnya perbandingan antara estimasi tagihan sistem dan tagihan aktual dari ERP.

Anomali dibuat apabila:

1. Estimasi sistem untuk suatu periode sudah tersedia atau selesai dibuat.
2. Tagihan sudah di-input ke ERP. Tagihan yang sudah di-input ke ERP dianggap sudah payment dan langsung valid untuk dibandingkan.
3. Selisih antara estimasi dan aktual melebihi nilai ambang yang telah ditentukan.

Contoh:

```text
Periode konsumsi : September 2026
Estimasi sistem  : Rp10.000.000
Tagihan ERP      : Rp13.500.000
Ambang selisih   : Rp2.000.000
Selisih          : Rp3.500.000
Hasil            : ANOMALI
```

Jika kondisi tersebut terpenuhi, sistem harus:

- Menampilkan penanda anomali pada perbandingan Usage Statistics.
- Menyimpan log ke Anomaly Center.
- Mengirim notifikasi sesuai konfigurasi notifikasi aplikasi.
- Mencegah log dan notifikasi ganda untuk tagihan yang sama.

## 2. Kondisi Sistem Saat Ini

Repository aplikasi:

```text
D:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\KWHMonitoring -  .NET 2.1 - V7\KWHMonitoring
```

Teknologi utama:

- ASP.NET Core `netcoreapp2.1`.
- Entity Framework Core SQL Server.
- Model anomaly berada pada `Models\AnomalyLog.cs`.
- DbContext berada pada `Models\ApplicationDbContext.cs`.
- Endpoint API utama berada pada `Controllers\ApiController.cs`.
- Notifikasi ditangani oleh `Services\NotificationService.cs`.
- Tampilan Usage Statistics berada pada `Views\Monitoring\UsageStatistics.cshtml`.
- Anomaly Center ditampilkan pada `Views\Monitoring\AnomalyLogs.cshtml`.

Pada Usage Statistics saat ini sudah tersedia kolom:

- Perangkat.
- Periode.
- Energi terukur.
- Perkiraan tagihan.
- Tagihan aktual.
- Keterangan.
- Tanggal pembayaran.
- Selisih tagihan.

Model ERP yang ditemukan pada `Models\ErpBillingComparison.cs` memiliki data:

```text
DeviceKey
KodeLokasi
Periode
JumlahTagihan
TagihanListrikID
```

Pencocokan periode yang sudah digunakan sistem menggeser periode konsumsi satu bulan untuk mencari periode pembayaran:

```text
expectedPaymentPeriod = consumptionPeriod + 1 bulan
```

Jika terdapat lebih dari satu record pada periode yang sama, record dengan `TagihanListrikID` terbesar digunakan sebagai record terbaru.

## 3. Aturan Bisnis

### 3.1 Syarat evaluasi anomali

Anomali hanya dievaluasi apabila data berikut tersedia:

- Device atau lokasi dapat diidentifikasi.
- Periode estimasi tersedia.
- Nilai estimasi tersedia.
- Data aktual dari ERP tersedia.
- `TagihanListrikID` valid.
- Periode ERP cocok dengan periode estimasi berdasarkan aturan periode pembayaran.

Apabila tagihan belum tersedia di ERP, sistem tidak membuat anomaly selisih.

Status yang disarankan:

```text
Estimasi saja
Tersedia di ERP
Sudah dibayar
Normal
Anomali
```

### 3.2 Definisi tagihan valid

Aturan bisnis final:

> Tagihan yang sudah di-input ke ERP berarti sudah payment dan langsung valid untuk dibandingkan dengan estimasi sistem.

Indikator bahwa tagihan sudah di-input dan valid adalah:

```text
TagihanListrikID > 0
```

Tidak diperlukan pemeriksaan tambahan terhadap field `Paid`, `IsPaid`, `PaymentStatus`, atau `TanggalPembayaran` untuk menentukan apakah data boleh dibandingkan. Jika record ERP sudah ditemukan dan memiliki `TagihanListrikID` valid, record tersebut langsung diproses sebagai tagihan aktual.

Status yang digunakan:

```text
Tagihan ERP belum ditemukan : Belum tersedia
Tagihan ERP ditemukan       : Valid / sudah payment
```

Dengan aturan ini, istilah `Sudah dibayar` pada tampilan dapat digunakan sebagai representasi bisnis bahwa tagihan ERP sudah valid, meskipun sistem tidak perlu melakukan validasi status pembayaran terpisah.

### 3.3 Rumus selisih

Rumus yang disarankan:

```text
difference = actualBilling - estimatedBilling
absoluteDifference = abs(difference)
```

Kondisi anomali:

```text
absoluteDifference > configuredThreshold
```

Nilai absolut digunakan supaya selisih yang terlalu besar dapat terdeteksi baik ketika tagihan aktual lebih tinggi maupun lebih rendah daripada estimasi.

Contoh tagihan aktual lebih tinggi:

```text
Estimasi : Rp10.000.000
Aktual   : Rp13.500.000
Selisih  : Rp3.500.000
```

Contoh tagihan aktual lebih rendah:

```text
Estimasi : Rp10.000.000
Aktual   : Rp6.000.000
Selisih  : Rp4.000.000
```

### 3.4 Estimasi nol

Jika estimasi sama dengan nol:

```text
Estimasi = Rp0
Aktual   = Rp2.000.000
```

Sistem tetap dapat membuat anomaly apabila nilai aktual lebih besar dari threshold. Persentase selisih tidak dihitung karena pembagian dengan nol dan ditampilkan sebagai `Tidak tersedia`.

### 4. Konfigurasi Nilai Ambang

Nilai threshold tidak ditanam langsung sebagai satu nilai tetap di kode. Threshold dapat diatur per device melalui menu **Settings → Pengaturan Perangkat**.

Urutan prioritas konfigurasi:

1. `DeviceSettings.BillingAnomalyThreshold` untuk device terkait jika nilainya diisi.
2. `AppSettingsRecords` dengan key `UsageStatistics.BillingAnomalyThreshold`.
3. `appsettings.json` pada `UsageStatistics:BillingAnomalyThreshold`.
4. Fallback kode Rp2.000.000 jika seluruh konfigurasi tidak tersedia.

Nilai kosong pada Settings per device berarti mengikuti threshold global, bukan bernilai nol.

Nama konfigurasi yang disarankan:

```text
UsageStatistics.BillingAnomalyThreshold
```

Karena aplikasi telah menggunakan `AppSettingsRecords` dan `AppSettingsCache`, konfigurasi utama sebaiknya disimpan di database agar dapat diubah tanpa deployment.

Fallback pada `appsettings.json` dapat digunakan apabila konfigurasi database belum tersedia:

```json
{
  "UsageStatistics": {
    "BillingAnomalyThreshold": 2000000
  }
}
```

Nilai awal contoh di atas adalah Rp2.000.000 dan perlu dikonfirmasi sebelum implementasi.

## 5. Tipe Anomaly Baru

Tipe anomaly yang direkomendasikan:

```text
BILLING_VARIANCE
```

Nama alternatif:

```text
ERP_BILLING_VARIANCE
ESTIMATE_ACTUAL_BILLING
```

`BILLING_VARIANCE` direkomendasikan karena singkat dan mudah digunakan oleh API, UI, filter, serta laporan.

## 6. Data yang Disimpan di Anomaly Center

### 6.1 Pilihan minimal: menggunakan field yang ada

Tanpa perubahan schema, field lama dapat dipetakan sementara sebagai berikut:

```text
PowerValue     = nilai tagihan aktual
ThresholdValue = nilai threshold selisih
Deviation      = nilai selisih
Notes          = estimasi, aktual, periode, status, dan ID ERP
```

Kelebihan:

- Tidak membutuhkan migration baru.
- Risiko perubahan database lebih kecil.
- Implementasi lebih cepat.

Kekurangan:

- Nama field tidak sesuai konteks billing.
- Filtering dan laporan billing menjadi kurang rapi.
- Data perlu ditafsirkan berdasarkan `AnomalyType`.

### 6.2 Pilihan yang direkomendasikan: field khusus billing

Jika fitur ini akan menjadi bagian permanen dari Anomaly Center, tambahkan field khusus pada `AnomalyLog`:

```text
EstimatedBilling
ActualBilling
BillingDifference
BillingDifferencePercent
BillingPeriod
ErpBillingId
PaymentStatus
```

Kelebihan:

- Data lebih jelas.
- Mudah ditampilkan dan difilter.
- Mendukung audit dan laporan bulanan.
- Tidak perlu mem-parsing isi `Notes`.

Kekurangan:

- Membutuhkan perubahan model dan migration.
- Membutuhkan penyesuaian API dan Anomaly Center.

Rekomendasi akhir: gunakan field khusus billing apabila tidak ada batasan perubahan database.

## 7. Alur Proses

### Tahap 1 — Mengambil data Usage Statistics

Endpoint Usage Statistics mengambil:

- Data energi.
- Tarif.
- Estimasi tagihan.
- Data aktual dari ERP.
- Status atau keterangan tagihan.

### Tahap 2 — Mencocokkan periode

Untuk setiap estimasi:

```text
expectedPaymentPeriod = consumptionPeriod + 1 bulan
```

Tagihan ERP dicari berdasarkan:

- `DeviceKey` atau `KodeLokasi`.
- Tahun periode.
- Bulan periode.

Jika terdapat beberapa record yang cocok, gunakan `TagihanListrikID` terbesar.

### Tahap 3 — Memvalidasi ketersediaan tagihan

Jika tidak ditemukan tagihan ERP:

```text
status = Belum tersedia
```

Tidak ada anomaly dan tidak ada notifikasi.

Jika ditemukan tagihan ERP:

```text
status = Valid / sudah payment
```

Tidak diperlukan pengecekan status pembayaran tambahan. Keberadaan record ERP dengan `TagihanListrikID` valid sudah cukup untuk menjalankan perbandingan.

### Tahap 4 — Membandingkan nilai

```text
difference = actualBilling - estimatedBilling
absoluteDifference = abs(difference)
```

Jika:

```text
absoluteDifference > threshold
```

maka buat anomaly `BILLING_VARIANCE`.

Jika tidak melewati threshold, statusnya `Normal`.

### Tahap 5 — Deduplication

Sebelum membuat anomaly baru, lakukan pengecekan menggunakan identitas:

```text
AnomalyType
DeviceKey
BillingPeriod
TagihanListrikID
```

Tujuannya agar pembukaan halaman atau pemanggilan endpoint berulang tidak menghasilkan log dan notifikasi ganda.

Jika menggunakan field khusus, sebaiknya tambahkan unique index atau mekanisme idempotency untuk anomaly billing.

## 8. Lokasi Logika Deteksi

### Opsi A — Di endpoint Usage Statistics

Logika anomaly dijalankan ketika endpoint menyusun response perbandingan.

Kelebihan:

- Data estimasi dan ERP sudah tersedia di lokasi yang sama.
- Implementasi awal lebih sederhana.

Kekurangan:

- Endpoint baca memiliki efek samping insert database.
- Pemanggilan halaman dapat mengirim notifikasi.
- Risiko duplicate process lebih tinggi.

### Opsi B — Service khusus

Buat service:

```text
IBillingAnomalyDetectionService
BillingAnomalyDetectionService
```

Service menangani:

- Validasi data.
- Perhitungan selisih.
- Pembacaan threshold.
- Deduplication.
- Penyimpanan anomaly.
- Pemanggilan notifikasi.

Service dapat dipanggil oleh background service, proses sinkronisasi ERP, atau endpoint dengan mekanisme idempotent.

### Rekomendasi

Gunakan service khusus. Endpoint Usage Statistics sebaiknya menampilkan status dan detail anomaly, sedangkan proses penyimpanan anomaly dilakukan oleh service yang idempotent.

## 9. Notifikasi

Notifikasi menggunakan service yang sudah ada:

```text
Services\NotificationService.cs
```

Contoh isi notifikasi:

```text
Anomali Selisih Tagihan Terdeteksi

Perangkat       : Panel A
Periode         : September 2026
Estimasi        : Rp10.000.000
Tagihan ERP     : Rp13.500.000
Selisih         : Rp3.500.000
Ambang          : Rp2.000.000
Persentase      : 35%
Tagihan ERP ID  : 123456
Status          : Tersedia di ERP / Sudah dibayar

Rekomendasi:
Verifikasi pembacaan energi, tarif, periode pencatatan, dan komponen tambahan pada tagihan ERP.
```

Notifikasi hanya dikirim apabila anomaly baru berhasil dibuat. Jika anomaly yang sama sudah ada, notifikasi tidak dikirim ulang.

## 10. Detail di Anomaly Center

Anomaly Center perlu menampilkan:

- Tipe anomaly `BILLING_VARIANCE`.
- Perangkat atau lokasi.
- Periode tagihan.
- Nilai estimasi.
- Nilai aktual.
- Selisih nominal.
- Persentase selisih.
- Nilai threshold.
- ID tagihan ERP.
- Status ERP atau pembayaran.
- Waktu anomaly dibuat.
- Root cause.
- Rekomendasi tindakan.
- Status acknowledged atau resolved.

Filter tambahan yang disarankan:

```text
Billing Variance
```

## 11. Root Cause dan Rekomendasi

Contoh root cause:

```text
Nilai tagihan aktual berbeda signifikan dari estimasi sistem pada periode yang sama.
Kemungkinan penyebab meliputi perubahan tarif, perbedaan periode pencatatan, konsumsi energi yang tidak normal, komponen biaya tambahan, atau ketidaksesuaian data ERP.
```

Contoh rekomendasi:

```text
Verifikasi periode tagihan, nilai meter, tarif WBP/LWBP, komponen biaya tambahan, dan kecocokan lokasi antara sistem monitoring dengan ERP.
```

## 12. File yang Kemungkinan Berubah

Perubahan belum dilakukan. File yang kemungkinan terdampak:

```text
D:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\KWHMonitoring -  .NET 2.1 - V7\KWHMonitoring\Models\AnomalyLog.cs
D:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\KWHMonitoring -  .NET 2.1 - V7\KWHMonitoring\Models\ErpBillingComparison.cs
D:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\KWHMonitoring -  .NET 2.1 - V7\KWHMonitoring\Models\ApplicationDbContext.cs
D:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\KWHMonitoring -  .NET 2.1 - V7\KWHMonitoring\Controllers\ApiController.cs
D:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\KWHMonitoring -  .NET 2.1 - V7\KWHMonitoring\Services\NotificationService.cs
D:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\KWHMonitoring -  .NET 2.1 - V7\KWHMonitoring\Views\Monitoring\UsageStatistics.cshtml
D:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\KWHMonitoring -  .NET 2.1 - V7\KWHMonitoring\Views\Monitoring\AnomalyLogs.cshtml
D:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\KWHMonitoring -  .NET 2.1 - V7\KWHMonitoring\appsettings.json
```

File service baru yang direkomendasikan:

```text
D:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\KWHMonitoring -  .NET 2.1 - V7\KWHMonitoring\Services\IBillingAnomalyDetectionService.cs
D:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\KWHMonitoring -  .NET 2.1 - V7\KWHMonitoring\Services\BillingAnomalyDetectionService.cs
```

Jika field khusus ditambahkan ke `AnomalyLog`, migration EF Core baru juga diperlukan pada:

```text
D:\File Project\Projects\KWHMonitoring -  .NET 2.1 - Final 5\KWHMonitoring -  .NET 2.1 - V7\KWHMonitoring\Migrations
```

## 13. Skenario Pengujian

### 13.1 Kondisi normal

```text
Estimasi tersedia
Tagihan ERP tersedia
Selisih <= threshold
```

Hasil:

- Tidak ada anomaly.
- Status normal.
- Tidak ada notifikasi.

### 13.2 Selisih melewati threshold

```text
Estimasi = Rp10.000.000
Aktual   = Rp13.000.000
Ambang   = Rp2.000.000
```

Hasil:

- Anomaly dibuat.
- Anomaly muncul di Anomaly Center.
- Notifikasi dikirim.
- Baris Usage Statistics diberi penanda anomaly.

### 13.3 Tagihan belum tersedia

```text
Estimasi tersedia
Tagihan ERP tidak ditemukan
```

Hasil:

- Status `Belum tersedia`.
- Tidak ada anomaly.
- Tidak ada notifikasi.

### 13.4 Tagihan baru tersedia setelah sebelumnya belum ada

Pemanggilan pertama:

```text
Estimasi tersedia
ERP belum tersedia
```

Pemanggilan berikutnya:

```text
Estimasi tersedia
ERP sudah tersedia
Selisih melewati threshold
```

Hasil:

- Anomaly dibuat pada pemanggilan kedua.
- Notifikasi dikirim satu kali.

### 13.5 Pemanggilan berulang

Endpoint dipanggil berkali-kali dengan data yang sama.

Hasil:

- Hanya satu anomaly untuk kombinasi periode, perangkat, dan `TagihanListrikID`.
- Notifikasi tidak dikirim berulang.

### 13.6 Aktual lebih kecil dari estimasi

```text
Estimasi = Rp10.000.000
Aktual   = Rp6.000.000
Ambang   = Rp2.000.000
```

Hasil:

- Anomaly dibuat karena selisih absolut Rp4.000.000 melebihi threshold.

### 13.7 Estimasi nol

```text
Estimasi = Rp0
Aktual   = Rp2.000.000
```

Hasil:

- Anomaly dibuat jika aktual melebihi threshold.
- Persentase selisih ditampilkan sebagai `Tidak tersedia`.

## 14. Keputusan yang Sudah Ditetapkan

Aturan berikut menjadi dasar implementasi:

1. Nilai threshold awal, misalnya Rp2.000.000.
2. Tagihan yang sudah di-input ke ERP dianggap sudah payment dan langsung valid untuk dibandingkan.
3. `TagihanListrikID > 0` digunakan sebagai indikator validitas tagihan ERP.
4. Tidak perlu menambahkan validasi status pembayaran terpisah.
5. Field lama atau field khusus pada `AnomalyLog` dipilih berdasarkan hasil pemeriksaan dampak database, dengan preferensi field khusus billing.
6. Deteksi dilakukan melalui service khusus yang idempotent.

## 15. TODO List Penerapan Sistem Anomaly Tagihan

Checklist berikut harus dikerjakan secara berurutan. Setiap tahap wajib divalidasi sebelum melanjutkan ke tahap berikutnya.

### A. Persiapan dan baseline

- [ ] Simpan commit atau buat branch khusus untuk fitur anomaly tagihan.
- [ ] Pastikan working tree bersih sebelum implementasi.
- [ ] Dokumentasikan perilaku endpoint Usage Statistics sebelum perubahan.
- [ ] Dokumentasikan perilaku anomaly perangkat yang sudah berjalan, khususnya `OVERLOAD`, `DROP`, `DEVICE_DROP`, dan `DEVICE_OFFLINE`.
- [ ] Catat response API Usage Statistics saat kondisi normal, ERP belum tersedia, dan ERP sudah tersedia.
- [ ] Catat struktur tabel `AnomalyLogs` dan migration terakhir.
- [x] Pastikan perubahan baru hanya menggunakan `AnomalyType = BILLING_VARIANCE`.
- [x] Pastikan tidak ada perubahan terhadap rumus anomaly daya, threshold EMA, cooldown, confirmation count, dan deduplication anomaly perangkat.

### B. Finalisasi aturan bisnis

- [x] Tetapkan `UsageStatistics.BillingAnomalyThreshold` sebagai nama konfigurasi threshold global.
- [x] Tetapkan nilai default threshold global Rp2.000.000.
- [x] Tambahkan `DeviceSettings.BillingAnomalyThreshold` nullable untuk override per device dari menu Settings.
- [x] Tetapkan urutan fallback: per-device → AppSettingsRecords → appsettings.json → Rp2.000.000.
- [x] Dokumentasikan bahwa `TagihanListrikID > 0` berarti tagihan sudah di-input ke ERP, sudah payment, dan langsung valid.
- [x] Dokumentasikan bahwa ERP yang belum memiliki record tagihan tidak boleh diproses sebagai anomaly selisih.
- [x] Tetapkan bahwa perbandingan menggunakan `absoluteDifference > threshold`.
- [x] Tetapkan bahwa perbandingan periode tetap mengikuti aturan existing: periode pembayaran ERP adalah periode konsumsi ditambah satu bulan.
- [x] Tetapkan bahwa record ERP dengan `TagihanListrikID` terbesar digunakan jika terdapat lebih dari satu record pada periode dan lokasi yang sama.
- [x] Tetapkan kunci deduplication: `AnomalyType + DeviceKey + BillingPeriod + TagihanListrikID`.

### C. Pemeriksaan sumber data ERP

- [ ] Verifikasi query atau service yang mengambil `ErpBillingRecord`.
- [ ] Pastikan pemetaan `DeviceKey` dan `KodeLokasi` tidak berubah untuk kebutuhan fitur baru.
- [ ] Pastikan `Periode`, `JumlahTagihan`, dan `TagihanListrikID` diambil tanpa mengubah query yang dipakai fitur lain.
- [ ] Pastikan record dengan `TagihanListrikID <= 0` diperlakukan sebagai tidak valid.
- [ ] Pastikan nilai `JumlahTagihan` yang null, negatif, atau tidak wajar memiliki aturan penanganan yang jelas.
- [ ] Pastikan data ERP yang sudah valid tetap dapat dipakai oleh tampilan Usage Statistics tanpa menunggu field status pembayaran tambahan.
- [ ] Tambahkan logging diagnostik hanya pada alur billing anomaly dan jangan mengubah logging ERP existing.

### D. Desain service terisolasi

- [x] Buat interface `IBillingAnomalyDetectionService`.
- [x] Buat implementasi `BillingAnomalyDetectionService`.
- [x] Service hanya menerima data perbandingan estimasi dan ERP yang sudah tersedia.
- [x] Service hanya memproses `BILLING_VARIANCE`.
- [x] Pisahkan fungsi validasi, perhitungan, deduplication, penyimpanan log, dan notifikasi.
- [x] Pastikan service tidak memanggil atau mengubah algoritma `AnomalyAnalysisService` untuk anomaly perangkat secara sembarangan.
- [x] Pastikan kegagalan notifikasi tidak membatalkan data perbandingan Usage Statistics.
- [x] Pastikan kegagalan pencatatan anomaly tidak membuat endpoint Usage Statistics gagal menampilkan data utama.
- [x] Tambahkan error handling sesuai pola service yang sudah digunakan aplikasi.

### E. Perubahan model dan database

- [x] Tentukan bahwa `AnomalyLog` memakai field khusus billing.
- [x] Tambahkan field `EstimatedBilling`, `ActualBilling`, `BillingDifference`, `BillingDifferencePercent`, `BillingPeriod`, dan `ErpBillingId`.
- [x] Tidak menggunakan pemetaan field existing sebagai pengganti field billing khusus.
- [x] Tambahkan konfigurasi kolom di `ApplicationDbContext` hanya untuk field baru yang diperlukan.
- [x] Tambahkan field threshold nullable dan mapping pada `DeviceSettings`.
- [x] Tambahkan migration `20261009150000_AddBillingAnomalyThresholdToDeviceSettings`.
- [x] Perbarui `ApplicationDbContextModelSnapshot.cs` untuk field threshold per-device.
- [x] Tambahkan migration EF Core baru jika ada perubahan schema.
- [x] Pastikan migration bersifat additive dan tidak menghapus atau mengubah data anomaly lama.
- [x] Tambahkan index untuk pencarian deduplication billing.
- [x] Hindari unique index global yang dapat memengaruhi anomaly perangkat lama.
- [ ] Uji migration pada database salinan sebelum diterapkan ke database utama.
- [ ] Siapkan script rollback migration jika deployment gagal.

### F. Integrasi ke API Usage Statistics

- [x] Integrasikan endpoint batch dan single-device Usage Statistics.
- [x] Tambahkan data status anomaly billing ke response tanpa menghapus field response existing.
- [x] Pertahankan nama dan tipe field existing agar frontend lain tidak rusak.
- [x] Jalankan deteksi hanya ketika estimasi dan tagihan ERP valid.
- [x] Jangan membuat anomaly hanya karena tagihan ERP belum ditemukan.
- [x] Pastikan record ERP valid langsung dianggap `sudah payment` dan dibandingkan.
- [x] Pastikan pemanggilan API berulang tidak membuat log ganda.
- [x] Naikkan cache key Usage Statistics ke versi billing anomaly.
- [x] Pertahankan response dan performa endpoint untuk perangkat yang tidak memiliki data billing.

### G. Deduplication dan konsistensi transaksi

- [x] Buat pengecekan anomaly existing berdasarkan device, periode, tipe anomaly, dan `TagihanListrikID`.
- [x] Pastikan dua request bersamaan tidak membuat dua log untuk tagihan yang sama.
- [x] Gunakan mekanisme idempotency database yang sesuai dengan pola database aplikasi.
- [x] Jika unique constraint digunakan, batasi constraint hanya untuk data `BILLING_VARIANCE`.
- [x] Tangani duplicate key sebagai kondisi aman, bukan sebagai error fatal ke pengguna.
- [ ] Pastikan perubahan nilai tagihan ERP pada ID yang sama memiliki aturan update atau re-evaluation yang jelas.
- [ ] Jangan mengubah atau menghapus anomaly perangkat lama saat proses deduplication billing.

### H. Integrasi Anomaly Center

- [ ] Tambahkan `BILLING_VARIANCE` sebagai tipe anomaly yang dapat ditampilkan.
- [ ] Tambahkan label yang ramah pengguna, misalnya `Selisih Tagihan`.
- [ ] Tampilkan periode, estimasi, aktual, selisih, threshold, dan `TagihanListrikID`.
- [ ] Tampilkan status `Belum tersedia` jika record ERP belum ditemukan.
- [ ] Tampilkan status `Valid / sudah payment` jika record ERP memiliki `TagihanListrikID > 0`.
- [ ] Pastikan filter anomaly perangkat lama tetap bekerja seperti sebelumnya.
- [ ] Pastikan sorting dan summary anomaly lama tidak berubah akibat tipe baru.
- [ ] Tambahkan root cause khusus billing.
- [ ] Tambahkan recommended action khusus billing.
- [ ] Pastikan acknowledgement dan resolve tetap dapat digunakan tanpa mengubah alur anomaly lama.

### I. Integrasi notifikasi

- [x] Tambahkan template email atau WhatsApp khusus `BILLING_VARIANCE`.
- [x] Sertakan device, periode, estimasi, aktual, selisih, threshold, dan ID ERP.
- [x] Gunakan `NotificationService` existing tanpa mengubah format notifikasi anomaly perangkat yang sudah ada.
- [x] Kirim notifikasi hanya setelah anomaly baru berhasil tersimpan.
- [x] Jangan mengirim notifikasi untuk tagihan ERP yang belum ditemukan.
- [x] Jangan mengirim notifikasi ulang untuk anomaly dengan kunci deduplication yang sama.
- [x] Pastikan konfigurasi email/WhatsApp yang disabled tetap dihormati.
- [x] Jika notifikasi gagal, catat warning dan jangan menggagalkan response Usage Statistics.
- [ ] Uji notifikasi menggunakan mode test atau recipient terbatas sebelum production.

### J. Perubahan frontend Usage Statistics

- [x] Tambahkan badge atau label anomaly pada baris perbandingan yang melewati threshold.
- [ ] Gunakan warna dan class CSS baru yang tidak menimpa style anomaly existing.
- [ ] Tampilkan keterangan ERP belum tersedia secara terpisah dari anomaly.
- [x] Tampilkan status valid atau sudah payment ketika record ERP ditemukan.
- [x] Tampilkan nominal selisih dan persentase secara konsisten dengan format currency existing.
- [x] Tambahkan input threshold billing per device pada menu Settings.
- [x] Simpan nilai threshold per device melalui endpoint single-save dan bulk-save.
- [ ] Pastikan data null tidak menyebabkan error JavaScript.
- [ ] Pastikan filter device, kategori, periode, dan mode single/all device tetap berfungsi.
- [ ] Pastikan tampilan mobile tetap terbaca.
- [ ] Jangan mengubah rumus biaya dan grafik finansial lain yang tidak berkaitan langsung dengan anomaly billing.

### K. Pengujian unit dan integrasi

- [ ] Uji estimasi dan aktual sama: tidak anomaly.
- [ ] Uji selisih tepat sama dengan threshold: tidak anomaly karena operator yang digunakan adalah `>`.
- [ ] Uji selisih lebih besar dari threshold global: anomaly dibuat.
- [ ] Uji threshold per-device lebih kecil dari global: device memakai nilai per-device.
- [ ] Uji threshold per-device lebih besar dari global: device memakai nilai per-device.
- [ ] Uji threshold per-device dikosongkan: device kembali memakai nilai global.
- [ ] Uji aktual lebih kecil dari estimasi dengan selisih absolut melewati threshold.
- [ ] Uji estimasi nol.
- [ ] Uji tagihan ERP tidak ditemukan.
- [ ] Uji `TagihanListrikID <= 0`.
- [ ] Uji tagihan ERP dengan `TagihanListrikID > 0`: langsung valid dan dianggap sudah payment.
- [ ] Uji periode konsumsi September 2026 dengan periode pembayaran Oktober 2026 sesuai aturan existing.
- [ ] Uji beberapa record ERP pada periode sama dan pastikan ID terbesar dipilih.
- [ ] Uji request berulang.
- [ ] Uji dua request bersamaan.
- [ ] Uji notifikasi hanya dikirim satu kali.
- [ ] Uji kegagalan notifikasi tidak merusak response utama.
- [ ] Uji anomaly `OVERLOAD`, `DROP`, `DEVICE_DROP`, dan `DEVICE_OFFLINE` sebelum dan sesudah perubahan.
- [ ] Uji halaman Usage Statistics tanpa data ERP.
- [ ] Uji halaman Usage Statistics dengan data ERP valid.
- [ ] Uji Anomaly Center dengan data anomaly lama dan anomaly billing secara bersamaan.

### L. Validasi regresi sistem lain

- [ ] Jalankan build project setelah proses aplikasi/debugger yang mengunci DLL dihentikan.
- [ ] Jalankan seluruh test yang tersedia.
- [ ] Verifikasi endpoint panel, chart, history, dashboard, dan anomaly perangkat.
- [ ] Verifikasi background service anomaly dan notification existing.
- [ ] Verifikasi migration database pada database uji.
- [ ] Verifikasi query Usage Statistics tidak mengalami peningkatan query yang tidak terkendali.
- [ ] Verifikasi cache hit dan cache miss tetap aman.
- [ ] Verifikasi tidak ada perubahan pada autentikasi dan authorization.
- [ ] Verifikasi tidak ada perubahan pada MQTT, relay control, device monitoring, atau kalkulasi energi.
- [ ] Verifikasi response API existing tetap backward compatible.

### M. Deployment dan rollback

- [ ] Backup database sebelum migration.
- [ ] Deploy migration terlebih dahulu jika diperlukan.
- [ ] Deploy aplikasi setelah migration berhasil.
- [x] Sediakan threshold global melalui konfigurasi resmi dan dokumentasikan nilainya.
- [ ] Atur dan verifikasi threshold per-device pada environment deployment.
- [ ] Aktifkan notifikasi secara bertahap atau menggunakan recipient terbatas.
- [ ] Pantau error log, jumlah anomaly billing, dan jumlah notifikasi setelah deployment.
- [ ] Bandingkan jumlah anomaly perangkat sebelum dan sesudah deployment.
- [ ] Siapkan rollback aplikasi tanpa menghapus log anomaly yang sudah tersimpan.
- [ ] Jika fitur harus dinonaktifkan, sediakan feature flag atau threshold disable yang tidak memengaruhi anomaly perangkat.
- [ ] Dokumentasikan hasil deployment dan keputusan perubahan threshold.

### N. Kriteria penerimaan

- [ ] Tagihan yang sudah di-input ke ERP dengan `TagihanListrikID > 0` langsung dianggap sudah payment dan valid.
- [ ] Tagihan ERP yang belum ada tidak menghasilkan anomaly.
- [ ] Selisih di atas threshold menghasilkan satu log anomaly.
- [ ] Log anomaly tampil di Anomaly Center.
- [ ] Notifikasi dikirim satu kali untuk anomaly baru.
- [ ] Request berulang tidak membuat duplicate log atau duplicate notification.
- [ ] Perhitungan Usage Statistics existing tetap sama.
- [ ] Anomaly perangkat existing tetap bekerja sama seperti sebelum perubahan.
- [ ] Migration dapat diterapkan dan di-rollback dengan aman.
- [ ] Build dan seluruh pengujian regresi berhasil.

## 16. Rekomendasi Implementasi Akhir

Rekomendasi desain yang paling aman:

1. Buat `BillingAnomalyDetectionService`.
2. Gunakan rumus `absoluteDifference > threshold`.
3. Simpan threshold pada `AppSettingsRecords` dengan key `UsageStatistics.BillingAnomalyThreshold`.
4. Gunakan tipe anomaly `BILLING_VARIANCE`.
5. Tambahkan field billing khusus pada `AnomalyLog` jika database boleh diubah.
6. Gunakan `TagihanListrikID` sebagai identitas deduplication.
7. Jangan membuat anomaly jika tagihan ERP belum tersedia.
8. Kirim notifikasi hanya ketika anomaly baru berhasil dibuat.
9. Tambahkan badge dan detail anomaly pada Usage Statistics.
10. Tambahkan tipe billing variance dan detail finansial pada Anomaly Center.
11. Buat migration jika field baru ditambahkan.
12. Uji seluruh skenario normal, anomaly, ERP belum tersedia, tagihan ERP valid, duplicate request, aktual lebih kecil, estimasi nol, dan regresi anomaly perangkat.

## 17. Status Dokumen

```text
Status: Implementasi selesai, validasi deployment tersisa
Implementasi kode: Selesai
Perubahan database: Migration additive tersedia, belum diterapkan ke production
Perubahan konfigurasi: Selesai; global fallback Rp2.000.000 dan threshold per-device tersedia
```