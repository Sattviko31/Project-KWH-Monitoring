# Rencana Penyesuaian Estimasi Biaya Berdasarkan Cutoff Meter per Device

**Status:** Implementasi inti selesai; verifikasi environment dan data PLN masih terbuka  
**Tanggal penyusunan:** 12 Oktober 2026  
**Project:** KWHMonitoring v7  

## 1. Latar Belakang

Saat ini estimasi biaya pada fitur finansial menggunakan periode bulan kalender dan proyeksi berdasarkan jumlah hari yang telah berlalu pada bulan berjalan. Sementara itu, pencatatan meter PLN dilakukan pada rentang tanggal **25–31** setiap bulan. Akibatnya, periode konsumsi aplikasi belum selalu sama dengan periode pencatatan meter PLN.

Perbedaan tersebut dapat menyebabkan:

- konsumsi dan biaya masuk ke bulan yang berbeda dari periode tagihan PLN;
- estimasi biaya terlalu tinggi atau terlalu rendah ketika device belum melewati tanggal pembacaan meter;
- perbandingan bulan-ke-bulan tidak sepenuhnya sebanding;
- dashboard gabungan memakai asumsi periode yang sama, padahal tanggal pencatatan dapat berbeda antar-device.

## 2. Tujuan

1. Menyediakan tanggal cutoff meter yang dapat dikonfigurasi **per device**.
2. Menghitung periode billing dari cutoff device, bukan hanya dari tanggal 1 sampai hari terakhir bulan kalender.
3. Membuat estimasi kWh dan biaya lebih mendekati siklus pencatatan PLN.
4. Mempertahankan perilaku existing untuk device lama dan device yang belum dikonfigurasi, yaitu periode bulan kalender penuh dari tanggal 1 sampai hari terakhir bulan.
5. Menjamin bahwa agregasi “Semua Panel” tetap benar walaupun setiap device memiliki cutoff berbeda.
6. Menampilkan informasi periode yang dipakai agar hasil estimasi dapat dijelaskan dan diaudit.

## 3. Konteks Implementasi Saat Ini

Komponen yang berhubungan dengan perubahan:

| Komponen | Kondisi saat ini | Rencana dampak |
|---|---|---|
| `Models/DeviceSettings.cs` | Menyimpan tarif dan parameter finansial per device | Menambah konfigurasi cutoff per device |
| `Services/DeviceSettingsService.cs` | Menyediakan effective settings dan menyimpan settings | Menambah default, validasi, dan persistensi cutoff |
| `Models/ApplicationDbContext.cs` | Mapping entity EF Core | Menambahkan mapping field baru bila diperlukan |
| `Controllers/ApiController.cs` | Menyediakan endpoint usage statistics dan device settings | Menggunakan periode billing per device saat menghitung proyeksi |
| `Views/Monitoring/_DeviceSettingsTab.cshtml` | Form settings per device | Menambahkan input cutoff |
| `Views/Monitoring/UsageStatistics.cshtml` dan `Details.cshtml` | Menampilkan metrik finansial | Menampilkan cutoff dan periode aktif |
| `MonthlyEnergy` / `HourlyEnergy` | Menyediakan data agregasi energi | Dipakai sesuai batas periode billing yang ditentukan |
| Migration EF Core | Menyimpan perubahan skema | Menambah kolom cutoff dengan default aman |

Referensi perilaku saat ini:

- `DeviceSettings` sudah menjadi sumber konfigurasi per-device untuk tarif dan budget.
- `billProjection` saat ini menggunakan konsep `currentMonthKWh / daysElapsed * totalDaysInMonth`.
- Agregasi dashboard utama menjumlahkan hasil per-device; pendekatan ini perlu dipertahankan untuk cutoff yang berbeda.

## 4. Definisi Bisnis

### 4.1 Cutoff meter

Cutoff meter adalah tanggal nominal dalam bulan ketika pencatatan meter PLN untuk suatu device dianggap dilakukan.

Contoh:

- Device A: cutoff tanggal 25.
- Device B: cutoff tanggal 28.
- Device C: cutoff tanggal 31.

Nilai cutoff disimpan sebagai **angka hari dalam bulan**, bukan tanggal lengkap, karena tanggal tersebut berulang setiap bulan.

### 4.2 Mode periode billing

Sistem memiliki dua mode periode:

1. **Mode default/legacy — bulan kalender penuh**
   - Digunakan jika device belum memiliki cutoff yang dikonfigurasi.
   - `periodStart` adalah tanggal 1 pukul 00:00.
   - `periodEnd` adalah tanggal 1 bulan berikutnya pukul 00:00, dengan interval `[periodStart, periodEnd)`.
   - Perilaku ini mempertahankan perhitungan existing dari awal sampai akhir bulan.

2. **Mode cutoff per-device**
   - Digunakan hanya jika cutoff sudah dikonfigurasi dan diaktifkan untuk device tersebut.
   - Periode dimulai setelah cutoff bulan sebelumnya dan berakhir pada cutoff bulan berjalan.

Default sistem **bukan tanggal 25**. Tanggal 25 hanya menjadi contoh nilai konfigurasi cutoff untuk device tertentu.

### 4.3 Periode billing pada mode cutoff

Untuk cutoff `C`, periode billing berjalan didefinisikan sebagai:

- **Tanggal mulai:** hari setelah cutoff bulan sebelumnya, yaitu `C + 1` pada bulan sebelumnya.
- **Tanggal akhir:** cutoff pada bulan berjalan.

Contoh cutoff tanggal 25 yang dikonfigurasi:

```text
Periode 25 September 2026 – 25 Oktober 2026
atau, jika memakai interval tanpa titik akhir ganda:
26 September 2026 00:00 sampai 25 Oktober 2026 00:00
```

Implementasi harus memilih satu konvensi interval secara konsisten. Rekomendasi: gunakan interval **[periodStart, periodEnd)** pada level timestamp, sehingga satu pembacaan tidak dihitung dua kali.

Contoh hasil resolusi fase pertama:

| `asOfDate` | Cutoff | `periodStart` | `periodEnd` | Mode |
|---|---:|---|---|---|
| 12 Okt 2026 | nonaktif | 1 Okt 2026 | 1 Nov 2026 | `calendar_month` |
| 12 Okt 2026 | 25 | 26 Sep 2026 | 25 Okt 2026 | `device_cutoff` |
| 12 Okt 2026 | 28 | 29 Sep 2026 | 28 Okt 2026 | `device_cutoff` |
| 12 Okt 2026 | 30 | 1 Okt 2026 | 30 Okt 2026 | `device_cutoff` |
| 12 Okt 2026 | 31 | 1 Okt 2026 | 31 Okt 2026 | `device_cutoff` |
| 26 Okt 2026 | 31 | 1 Okt 2026 | 31 Okt 2026 | `device_cutoff` |
| 29 Feb 2024 | 31 | 1 Mar 2024 | 31 Mar 2024 | `device_cutoff` |

### 4.4 Tanggal cutoff aktual

Tanggal konfigurasi adalah cutoff nominal. Data aktual dapat tidak tersedia tepat pada tanggal tersebut karena:

- device offline;
- pencatatan PLN dilakukan pada tanggal lain dalam rentang 25–31;
- tidak ada pembacaan pada jam yang sama;
- keterlambatan ingest data.

Karena itu, desain perlu memisahkan:

- `ConfiguredCutoffDay`: cutoff yang diatur admin;
- `EffectiveCutoffDate`: tanggal batas yang benar-benar dipakai pada perhitungan tertentu;
- `CutoffResolutionStatus`: status apakah cutoff tepat, fallback, atau belum dapat ditentukan.

## 5. Rekomendasi Data dan Konfigurasi

### 5.1 Field minimum pada `DeviceSettings`

Nama final dapat mengikuti konvensi project, tetapi rekomendasi awal:

| Field | Tipe | Default | Keterangan |
|---|---:|---:|---|
| `BillingCutoffDay` | `int?` | `null` | Hari cutoff nominal, rentang 25–31; null berarti mode bulan kalender |
| `BillingCutoffToleranceDays` | `int` | `0` atau `1` | Toleransi pencarian pembacaan aktual di sekitar cutoff |
| `BillingCutoffEnabled` | `bool` | `false` | Mengaktifkan cutoff per-device; false berarti mode bulan kalender |

Rekomendasi minimal untuk fase pertama adalah `BillingCutoffDay` nullable dan/atau `BillingCutoffEnabled`, dengan default **mode bulan kalender penuh**. Toleransi dan status aktual dapat ditambahkan jika kebutuhan audit mengharuskannya.

### 5.2 Aturan validasi

- `BillingCutoffDay` wajib berada pada rentang 25–31 sesuai kebijakan pencatatan PLN.
- Nilai kosong pada data lama berarti mode bulan kalender penuh, bukan cutoff tanggal 25.
- Nilai invalid tidak boleh mengaktifkan cutoff secara diam-diam; sistem harus menolak nilai tersebut atau kembali ke mode bulan kalender dan mencatat peringatannya.
- Bulan yang tidak memiliki tanggal 29, 30, atau 31 harus memiliki aturan eksplisit. Rekomendasi: gunakan hari terakhir bulan tersebut sebagai tanggal cutoff efektif.
- Perubahan cutoff berlaku untuk perhitungan baru; histori lama tidak boleh diam-diam dihitung ulang tanpa mekanisme rekalkulasi yang disetujui.
- Device yang belum memiliki row `DeviceSettings` tetap dihitung menggunakan periode bulan kalender penuh dan tarif fallback yang sudah berlaku.

## 6. Rancangan Perhitungan

### 6.1 Menentukan periode device

Untuk device pada mode default bulan kalender penuh:

```text
periodStart = tanggal 1 bulan berjalan 00:00
periodEnd   = tanggal 1 bulan berikutnya 00:00
```

Untuk device pada mode cutoff, gunakan aturan berikut.

Untuk tanggal referensi `asOfDate` dan cutoff device `C`:

1. Tentukan cutoff bulan berjalan: `min(C, daysInMonth(year, month))`.
2. Jika `asOfDate` sudah mencapai/melewati cutoff bulan berjalan, gunakan cutoff bulan berjalan sebagai `periodEnd`.
3. Jika belum mencapai cutoff, gunakan cutoff bulan sebelumnya sebagai `periodEnd` untuk periode yang sedang berjalan.
4. Tentukan `periodStart` sebagai cutoff periode sebelumnya, dengan interval akhir eksklusif.
5. Hitung durasi periode dari `periodStart` ke `periodEnd`.

Untuk device cutoff 25 pada 12 Oktober 2026, contoh periode berjalan adalah:

```text
periodStart = 26 September 2026 00:00
periodEnd   = 25 Oktober 2026 00:00
```

Jika tanggal referensi sudah 26 Oktober 2026, periode berikutnya dimulai pada 26 Oktober 2026.

### 6.2 Konsumsi berjalan

Konsumsi berjalan harus dihitung dari data energi device yang berada dalam interval:

```text
[periodStart, min(asOfTimestamp, periodEnd))
```

Prioritas sumber data:

1. Gunakan agregasi energi yang resolusinya memadai dan konsisten dengan timestamp.
2. Jika memakai `MonthlyEnergy`, jangan langsung menggunakannya untuk periode yang memotong dua bulan kalender tanpa penyesuaian; gunakan `DailyEnergy`/`HourlyEnergy` atau agregasi timestamp yang setara.
3. Filter wajib berdasarkan `DeviceKey` dan batas waktu periode.
4. Hindari double counting ketika data agregasi dan data mentah dicampur.

### 6.3 Proyeksi sampai cutoff

Jika konsumsi berjalan `actualKWh`, durasi terpakai `elapsedDays`, dan durasi penuh periode `periodDays`:

```text
averageKWhPerDay = actualKWh / elapsedDays
projectedKWh     = averageKWhPerDay × periodDays
projectedCost    = projectedKWh × effectiveTariff
```

Aturan khusus:

- Jika `elapsedDays <= 0`, proyeksi tidak boleh membagi dengan nol; kembalikan status `insufficient_data`.
- Jika sudah melewati `periodEnd`, `projectedKWh` menjadi konsumsi aktual periode yang telah selesai.
- Pembulatan kWh dan biaya harus mengikuti konvensi finansial existing dan dilakukan pada tahap yang konsisten.
- Tarif harus diambil dari settings device, bukan dari settings device pertama atau tarif global ketika menghitung hasil per-device.

### 6.4 Estimasi biaya anomali

Perubahan cutoff tidak mengubah durasi asumsi anomali per kejadian. Namun, total dampak biaya yang ditampilkan pada periode harus menggunakan periode billing device yang sama agar daftar anomali dan proyeksi dapat dibandingkan secara konsisten.

## 7. Kontrak API yang Direncanakan

Endpoint existing tetap dipertahankan dan field baru ditambahkan secara additive.

Contoh objek `billProjection` mode cutoff yang direncanakan:

```json
{
  "projectedMonthKWh": 520.3,
  "projectedCost": 780450,
  "actualKWh": 260.15,
  "periodStart": "2026-09-26T00:00:00",
  "periodEnd": "2026-10-25T00:00:00",
  "asOfDate": "2026-10-12",
  "configuredCutoffDay": 25,
  "billingPeriodMode": "device_cutoff",
  "effectiveCutoffDate": "2026-10-25",
  "periodDays": 29,
  "elapsedDays": 16,
  "daysRemaining": 13,
  "cutoffResolutionStatus": "configured"
}
```

Catatan kompatibilitas:

- Pertahankan field lama selama masa transisi jika frontend existing masih menggunakannya.
- `projectedMonthKWh` tetap dapat digunakan pada mode bulan kalender. Untuk mode cutoff, tambahkan `projectedPeriodKWh` dan `projectedPeriodCost`; tandai field lama sebagai legacy hanya bila dipakai untuk periode non-kalender.
- Endpoint aggregate harus menjumlahkan `projectedPeriodKWh` dan `projectedCost` dari setiap device, bukan menghitung satu proyeksi global menggunakan cutoff device pertama.

## 8. Perubahan UI yang Direncanakan

### 8.1 Settings per device

Tambahkan field:

- Label: `Tanggal Cutoff Meter PLN`.
- Input: angka/select tanggal 25–31.
- Nilai default: cutoff tidak aktif/kosong, sehingga device memakai periode bulan kalender penuh.
- Jika diaktifkan, nilai cutoff dapat dipilih pada rentang 25–31.
- Help text: “Tanggal pencatatan meter PLN untuk device ini.”
- Validasi client dan server.

### 8.2 Usage Statistics dan Details

Pada bagian bill projection tampilkan:

- periode aktif, misalnya `26 Sep 2026 – 25 Okt 2026`;
- cutoff device;
- konsumsi aktual sampai tanggal referensi;
- estimasi sampai cutoff;
- sisa hari menuju cutoff;
- indikator apabila cutoff aktual memakai fallback karena data pembacaan tidak tersedia.

### 8.3 Dashboard aggregate

Untuk “Semua Panel”:

- total biaya tetap merupakan jumlah biaya per-device;
- jangan menampilkan satu cutoff sebagai representasi semua device;
- tampilkan label seperti “Agregasi periode per device”;
- bila diperlukan, sediakan ringkasan jumlah device per cutoff.

## 9. Rencana Perubahan File pada Tahap Implementasi

Perubahan berikut sudah dilakukan pada implementasi fase pertama:

1. `Models/DeviceSettings.cs` — property cutoff.
2. `Models/ApplicationDbContext.cs` — mapping kolom.
3. `Services/DeviceSettingsService.cs` — default, validasi, load/save, dan sanitasi nilai.
4. `Services/BillingPeriodCalculator.cs` dan `Controllers/ApiController.cs` — helper periode dan proyeksi per-device.
5. `Views/Monitoring/_DeviceSettingsTab.cshtml` — input konfigurasi cutoff.
6. `Views/Monitoring/UsageStatistics.cshtml` dan `Details.cshtml` — tampilan periode/cutoff.
7. Migration EF Core — kolom nullable/flag nonaktif dengan default aman.
8. `FINANCIAL_CALCULATIONS.md` — formula mode kalender dan cutoff.
9. Smoke test helper kalender — tervalidasi sebagai langkah verifikasi lokal.

Item berikut masih memerlukan environment/data eksternal dan tidak dapat diklaim selesai hanya dari source repository:

- apply migration pada database staging;
- verifikasi backup database dan rollback aktual;
- perbandingan dengan pencatatan/tagihan PLN nyata;
- smoke test browser pada staging/production;
- rollout bertahap dan pemantauan log production.

Rekomendasi arsitektur: letakkan logika periode pada helper/service khusus, bukan menambah formula tersebar di controller dan view. Dengan begitu, endpoint per-device dan endpoint aggregate memakai sumber kebenaran yang sama.

## 10. Strategi Migrasi dan Backward Compatibility

1. Tambahkan kolom baru nullable atau flag nonaktif; nilai kosong/nonaktif berarti periode bulan kalender penuh.
2. Jangan backfill device existing menjadi tanggal 25 karena akan mengubah perilaku existing.
3. Device baru menggunakan periode bulan kalender penuh sampai admin mengaktifkan cutoff per-device.
4. API lama tetap merespons field existing selama frontend belum selesai dimigrasikan.
5. Jangan mengubah nilai historis `MonthlyEnergy` secara otomatis.
6. Tambahkan logging ketika nilai invalid dikoreksi ke default.
7. Sediakan cara untuk menguji hasil sebelum dan sesudah cutoff di environment non-production.

## 11. Skenario Pengujian

### 11.1 Validasi konfigurasi

- cutoff 25, 26, 27, 28, 29, 30, dan 31 diterima;
- cutoff 1, 24, 32, negatif, dan kosong ditolak atau dikembalikan ke default;
- device lama tanpa settings memakai periode bulan kalender penuh;
- perubahan cutoff tersimpan dan terbaca kembali oleh endpoint settings.

### 11.2 Kalender

- cutoff 25 pada bulan 30 hari;
- cutoff 31 pada bulan 31 hari;
- cutoff 31 ketika bulan berikutnya hanya 30 hari;
- cutoff 31 pada Februari tahun biasa;
- cutoff 31 pada Februari tahun kabisat;
- pergantian periode tepat pada tengah malam cutoff;
- timestamp sebelum, tepat pada, dan sesudah batas periode.

### 11.3 Akurasi konsumsi dan biaya

- data hanya pada satu bulan kalender tetapi periode billing melintasi dua bulan;
- data kosong pada awal periode;
- data berhenti sebelum cutoff;
- data melewati cutoff;
- tarif flat dan tarif WBP/LWBP per device;
- dua device dengan cutoff dan tarif berbeda.

### 11.4 Agregasi

- hasil aggregate sama dengan penjumlahan hasil per-device;
- aggregate tidak menggunakan cutoff, tarif, atau budget device pertama;
- satu device tanpa settings tidak menggagalkan device lain;
- device dengan periodDays berbeda tetap menghasilkan total biaya yang benar.

### 11.5 Regresi

- endpoint existing tetap dapat dipanggil;
- UI device yang belum dikonfigurasi tetap menampilkan nilai yang masuk akal;
- perhitungan budget, waste, WBP/LWBP, anomaly cost impact, MoM, dan YoY tidak mengalami perubahan tidak sengaja;
- build dan migration berjalan pada target framework project.

## 12. Observability dan Audit

Setiap response proyeksi sebaiknya menyediakan atau mencatat:

- `DeviceKey`;
- cutoff configured;
- period start/end;
- as-of timestamp;
- sumber data dan jumlah data yang digunakan;
- status fallback;
- tarif yang digunakan;
- waktu perhitungan.

Log tidak perlu mencatat data mentah berlebihan, tetapi harus cukup untuk menjawab pertanyaan: “Mengapa estimasi device ini memakai periode dan biaya tersebut?”

## 13. Risiko dan Mitigasi

| Risiko | Dampak | Mitigasi |
|---|---|---|
| Data tidak tersedia tepat pada cutoff | Periode tidak memiliki batas aktual | Gunakan cutoff nominal dan status fallback yang eksplisit |
| Data agregasi hanya berbasis bulan kalender | Periode lintas bulan salah hitung | Gunakan daily/hourly atau agregasi timestamp |
| Semantics field lama masih “month” | Salah interpretasi frontend | Tambahkan field `projectedPeriod*`, dokumentasikan legacy |
| Cutoff berbeda antar-device | Aggregate sulit dijelaskan | Sum hasil per-device dan tampilkan “periode per device” |
| Perubahan cutoff mengubah histori | Laporan lama berubah | Terapkan perubahan untuk perhitungan baru atau simpan snapshot periode |
| Tanggal 31 tidak ada pada semua bulan | Off-by-one/error tanggal | Clamp ke hari terakhir bulan dan uji kalender lengkap |
| Cache settings stale | Estimasi belum memakai cutoff terbaru | Invalidasi/refresh cache setelah save settings |

## 14. Urutan Implementasi yang Disarankan

### Fase 1 — Fondasi

1. Finalisasi definisi interval dan aturan tanggal tidak valid.
2. Tambah field cutoff pada model, mapping, migration, service, dan settings API.
3. Tambah validasi cutoff dan pertahankan default mode bulan kalender penuh.

### Fase 2 — Kalkulasi

1. Buat helper/service periode billing per device.
2. Ubah proyeksi per-device untuk memakai periode tersebut.
3. Pastikan tarif tetap berasal dari device terkait.
4. Tambahkan status dan metadata periode ke response.

### Fase 3 — UI dan Agregasi

1. Tambah input cutoff pada settings.
2. Tampilkan periode/cutoff pada detail dan usage statistics.
3. Sesuaikan aggregate agar menjumlahkan hasil per-device.
4. Tambah tooltip/help text dan status fallback.

### Fase 4 — Verifikasi dan Rilis

1. Jalankan unit test kalender dan formula.
2. Jalankan integration test endpoint.
3. Bandingkan hasil dengan contoh tagihan PLN pada beberapa device.
4. Deploy migration pada staging.
5. Lakukan dual-check antara estimasi lama dan estimasi berbasis cutoff.
6. Rilis bertahap setelah hasil disetujui pemilik bisnis.

## 15. Keputusan yang Perlu Disetujui Sebelum Coding

1. Apakah cutoff hanya tanggal nominal per device, atau harus mencari tanggal pembacaan aktual dari data meter?
2. Apakah rentang valid selalu 25–31, atau boleh tanggal lain untuk kebutuhan khusus?
3. Saat tanggal cutoff tidak tersedia, apakah sistem memakai cutoff nominal, pembacaan terdekat, atau menandai estimasi tidak tersedia?
4. Apakah perubahan cutoff berlaku langsung untuk periode berjalan atau mulai periode berikutnya?
5. Apakah API akan mempertahankan `projectedMonthKWh` sementara, atau langsung menggantinya dengan nama berbasis period?
6. Apakah diperlukan penyimpanan snapshot periode/tagihan agar histori tidak berubah ketika settings diubah?

## 16. Kriteria Selesai

Implementasi dapat dianggap selesai apabila:

- setiap device memiliki cutoff efektif yang dapat ditelusuri;
- default device lama tetap menggunakan periode bulan kalender penuh;
- proyeksi menggunakan periode cutoff hanya untuk device yang mengaktifkannya; device lain tetap memakai bulan kalender penuh;
- aggregate memakai hasil per-device dan benar untuk cutoff berbeda;
- UI menampilkan periode dan status fallback;
- validasi kalender lengkap lulus;
- migration, build, test, dan regression check lulus;
- hasil minimal beberapa device telah dibandingkan dengan pencatatan PLN nyata.
