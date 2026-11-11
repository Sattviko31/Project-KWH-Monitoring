# Rencana Proyeksi Data Energi Saat MQTT Terputus

**Status:** Rencana implementasi  
**Tanggal:** 9 November 2026  
**Ruang lingkup:** Melengkapi interval energi yang hilang ketika device berhenti mengirim telemetry MQTT, tanpa mengubah data aktual.

## 1. Tujuan

Sistem harus mendeteksi kapan device berhenti mengirim data, menghitung interval yang hilang, membuat proyeksi berdasarkan pola historis berulang, dan menampilkan hasil proyeksi dengan **bar merah**. Data yang telah tersimpan aktual tetap berwarna seperti saat ini:

- Biru: per jam.
- Hijau: per hari/bulan sesuai panel.
- Biru muda: per bulan/tahun sesuai panel.
- Merah: interval hasil proyeksi karena MQTT gap.

Proyeksi bukan pembacaan meter aktual. Status, metode, confidence, sumber histori, dan rentang ketidakpastian wajib dapat diaudit.

## 2. Dasar arsitektur saat ini

Rencana mengikuti struktur repository saat ini:

- ASP.NET Core `netcoreapp2.1` dan Entity Framework Core SQL Server.
- `KWHData` menyimpan data mentah.
- `HourlyEnergy`, `DailyEnergy`, `MonthlyEnergy`, dan `YearlyEnergy` menyimpan agregat.
- Listener menyimpan data MQTT, melakukan tracking real-time, flush berkala, finalisasi jam, dan backfill historis.
- Halaman dashboard berada pada `Views\Monitoring\UsageStatistics.cshtml`.
- Grafik menggunakan Chart.js 4.4.2.
- Trapezoidal rule tetap dipakai untuk energi aktual; proyeksi hanya mengisi interval tanpa telemetry.

## 3. Prinsip wajib

### 3.1 Aktual tidak boleh ditimpa

Gunakan status berikut:

```text
ACTUAL       = dihitung dari telemetry valid yang diterima
PROJECTED    = dihitung karena interval telemetry hilang
MIXED        = agregat berisi aktual dan projected
RECONCILED   = sebelumnya projected, kemudian diganti oleh data aktual terlambat
INVALID      = kandidat ditolak oleh validasi
```

### 3.2 Nilai nol bukan otomatis downtime

Interval tanpa reading harus berstatus `MISSING` lebih dahulu. Nilai `0 kWh` hanya aktual jika ada telemetry valid yang memang menyatakan konsumsi nol.

### 3.3 Perangkat adalah unit perhitungan

Pola setiap `DeviceKey` berbeda. Semua baseline utama harus berasal dari device yang sama. Rata-rata global hanya fallback dengan confidence rendah.

### 3.4 Waktu konsisten

Simpan timestamp dalam UTC atau timezone bisnis yang konsisten. Boundary jam, hari, bulan, dan tahun harus dihitung di server memakai timezone aplikasi, bukan timezone browser.

### 3.5 Seluruh perhitungan wajib dinamis

Tidak boleh ada nilai energi, biaya, jumlah device, jumlah bucket, confidence, warna status, atau label grafik yang di-hardcode berdasarkan contoh screenshot. Semua nilai harus dihitung dari data runtime dan parameter periode yang sedang dipilih.

Perhitungan harus dinamis terhadap:

- `DeviceKey` atau daftar device aktif.
- Tanggal, periode, dan timezone aplikasi.
- Bucket jam, hari, bulan, dan tahun yang benar-benar tersedia.
- Data aktual MQTT terbaru.
- Gap MQTT yang baru terdeteksi atau berubah status.
- Tarif yang berlaku pada bucket waktu terkait.
- Konfigurasi interval MQTT dan threshold gap.
- Histori aktual yang tersedia untuk device tersebut.
- Mode `actual-only` atau `complete`.

Angka pada dokumen, tooltip, laporan, dan screenshot hanya contoh format. Nilai implementasi wajib berasal dari query/API dan kalkulasi runtime.

### 3.6 Tidak boleh ada asumsi jumlah device atau bucket

Sistem tidak boleh mengasumsikan jumlah device, 24 bucket jam, jumlah hari dalam bulan, atau jumlah bulan dalam tahun. Bucket dibuat dari boundary periode yang dipilih dan timezone bisnis. Device tanpa data harus berstatus `NO_DATA` atau mengikuti filter, bukan diberi nilai nol otomatis.

### 3.7 Perubahan data harus memicu refresh dinamis

Ketika reading MQTT baru masuk, gap ditutup, proyeksi direkalkulasi, tarif berubah, atau filter berubah, data berikut harus diperbarui dari sumber runtime:

```text
seri grafik
kartu total
peak hour/day/month
estimasi biaya
confidence dan status data
tooltip
laporan
export
```

Cache tidak boleh menjadi sumber kebenaran frontend. Cache hanya boleh digunakan jika key memuat seluruh parameter perhitungan dan memiliki invalidation/version yang jelas.

## 4. Deteksi MQTT gap

### 4.1 Interval normal

Untuk reading berurutan:

```text
gapSeconds = current.TerminalTime - previous.TerminalTime
```

Gap menjadi kandidat kehilangan data jika:

```text
gapSeconds > max(
    ExpectedIntervalSeconds * GapMultiplier,
    MinimumGapSeconds
)
```

Nilai awal yang disarankan:

```text
ExpectedIntervalSeconds = median interval valid 24 jam terakhir
GapMultiplier            = 2.5
MinimumGapSeconds        = 300
```

Interval normal harus dihitung per device. Jitter jaringan singkat tidak boleh langsung membuat proyeksi.

### 4.2 Boundary gap

```text
gapStart     = reading valid terakhir
gapEnd       = reading valid pertama setelah device kembali
missingStart = boundary bucket pertama setelah gapStart
missingEnd   = boundary bucket terakhir sebelum gapEnd
```

Untuk grafik per jam, proyeksi dibentuk pada bucket jam. Daily/monthly/yearly kemudian dijumlahkan dari bucket jam tersebut agar total selalu konsisten.

### 4.3 Device masih offline

Background worker memeriksa:

```text
now - LastValidReadingTime > OfflineThreshold
```

Jika benar, buat/update gap terbuka. Gap ditutup ketika reading valid berikutnya masuk atau operator menandainya sebagai downtime terencana.

## 5. Model data yang disarankan

### 5.1 `EnergyProjectionGaps`

Tambahkan migration/tabel baru sehingga data aktual lama tetap aman:

```text
Id
DeviceKey
GapStartUtc
GapEndUtc                         nullable untuk gap terbuka
IntervalStartUtc
IntervalEndUtc
ExpectedIntervalSeconds
LastActualReadingUtc
FirstActualReadingAfterGapUtc
Status                            OPEN, PROJECTED, RECONCILED, REJECTED
ProjectionMethod
ConfidenceScore
HistoricalSamples
Reason
CalculationVersion
CreatedAtUtc
CalculatedAtUtc
ReconciledAtUtc                   nullable
```

### 5.2 `EnergyProjectionValues`

Simpan hasil pada granularitas jam sebagai sumber proyeksi:

```text
Id
ProjectionGapId
DeviceKey
BucketStartUtc
BucketEndUtc
ProjectedEnergyKWh
LowerBoundKWh
UpperBoundKWh
ConfidenceScore
ProjectionMethod
SourcePeriodKey
Status                            PROJECTED, RECONCILED, INVALID
CalculationVersion
CreatedAtUtc
```

Unique key yang disarankan:

```text
(DeviceKey, BucketStartUtc, CalculationVersion)
```

### 5.3 Sumber agregasi

Opsi utama adalah read-time union:

```text
HourlyEnergy aktual
UNION ALL
EnergyProjectionValues projected
```

Daily/monthly/yearly dihitung dari hasil tersebut. Jangan menimpa record agregat aktual. Jika materialisasi diperlukan untuk performa, tambahkan:

```text
DataQuality       ACTUAL, MIXED, PROJECTED
ActualEnergyKWh
ProjectedEnergyKWh
CalculationVersion
```

## 6. Metode proyeksi utama: per jam dan per device

Perhitungan proyeksi **wajib dimulai dari grafik per jam untuk setiap device secara terpisah**. Baseline utama adalah data aktual historis dari:

```text
DeviceKey yang sama
Jam yang sama
Pola hari yang sama atau paling mirip
```

Contoh aturan utama:

```text
Device A - 12:00 yang hilang
  dibandingkan dengan data aktual Device A pada pukul 12:00
  dari hari-hari atau minggu-minggu sebelumnya.

Device A - 03:00 yang hilang
  dibandingkan dengan data aktual Device A pada pukul 03:00
  dari hari-hari atau minggu-minggu sebelumnya.

Device B - 12:00 yang hilang
  hanya dibandingkan dengan Device B pada pukul 12:00,
  bukan dengan Device A dan bukan dengan jam 11:00 atau 13:00.
```

Dengan demikian, setiap bucket proyeksi memiliki kunci pola minimal:

```text
(DeviceKey, HourOfDay, DayClass)
```

`DayClass` minimal terdiri dari `WORKDAY`, `WEEKEND`, dan `HOLIDAY` jika kalender tersedia. Jam yang sama tetap menjadi syarat utama; `DayClass` dipakai untuk memilih histori yang paling sebanding.

Tidak boleh menggunakan rata-rata seluruh device sebagai metode utama. Rata-rata device lain hanya boleh menjadi fallback ketika device target tidak mempunyai histori yang cukup, dengan confidence rendah dan alasan yang jelas.

Tidak ada metode yang menjamin akurasi sempurna tanpa data aktual. Karena itu histori jam yang sama diuji dengan backtesting sebelum hasilnya digunakan.

### 6.1 Normalisasi histori

Definisikan:

```text
E[d,h] = energi aktual device pada tanggal d dan jam h
```

Syarat data histori:

- Jangan memakai bucket `PROJECTED`, `MIXED`, atau `INVALID` sebagai baseline.
- Buang hari dengan coverage aktual di bawah 80%.
- Tandai weekday, weekend, holiday, dan event khusus jika tersedia.
- Gunakan median atau winsorization untuk outlier; jangan menghapus puncak valid tanpa alasan.

Profile dasar:

```text
Profile(h, class) = weightedMedian(E[d,h])
```

`class` minimal adalah `WORKDAY`, `WEEKEND`, dan `HOLIDAY`.

### 6.2 Kandidat metode

#### A. Data aktual device yang sama pada jam yang sama — metode utama

Untuk target `DeviceKey = D`, tanggal target `d`, dan jam target `h`:

```text
Candidate(D,d,h) = {
    E[x,h]
    | x adalah tanggal historis device D,
      HourOfDay(x) = h,
      DayClass(x) sesuai atau paling mirip dengan DayClass(d),
      bucket x berstatus ACTUAL,
      bucket x valid dan tidak mengalami gap
}
```

Nilai dasar proyeksi:

```text
P_same_device_same_hour(D,d,h)
    = weightedMedian(Candidate(D,d,h))
```

Bobot histori harus lebih besar untuk data yang lebih baru dan pola hari yang lebih sama. Contoh bobot:

```text
weight(x) = recencyWeight(x) * dayClassWeight(x)
```

Aturan pemilihan histori:

1. Prioritas pertama: device sama, jam sama, dan `DayClass` sama.
2. Prioritas kedua: device sama, jam sama, dan `DayClass` berdekatan/serupa.
3. Prioritas ketiga: device sama dan jam sama dari histori yang lebih lama.
4. Fallback terakhir: baseline device lain atau baseline global, hanya jika dikonfigurasi dan confidence diturunkan.

#### B. Seasonal mingguan pada jam yang sama

```text
P_week(D,d,h) = median energi aktual Device D
                pada weekday dan jam h yang sama
                dari 4-12 minggu terakhir
```

#### C. Hari sejenis pada jam yang sama

```text
P_day(D,d,h) = median energi aktual Device D
               pada jam h yang sama
               dari 14-30 hari dengan class yang sama
```

#### D. Pola tanggal historis pada jam yang sama

Gunakan tanggal sama atau hari terdekat dari bulan/minggu sebelumnya pada **jam yang sama** hanya jika pembanding lengkap, berstatus aktual, dan tidak sedang mengalami gap.

#### E. Seasonal dengan faktor skala per device dan jam

Jika bentuk pola berulang tetapi level berubah:

```text
scale       = median(
                 actualRecentSameHour / historicalSameHour
               )
scale       = clamp(scale, 0.70, 1.40)
P_scaled(D,d,h) = scale * P_week(D,d,h)
```

#### F. Hari analog terbaik, tetap mengambil jam target yang sama

Cari hari lengkap dengan bentuk profil paling mirip:

```text
distance(d) = sqrt(
    sum_h ((E[current,h] - E[d,h]) / max(E[d,h], epsilon))^2
)
```

Ambil nilai pada **jam target h** dari 3-5 analog terbaik, bukan nilai dari jam berbeda. Metode ini berguna untuk device dengan pola operasi khusus.

### 6.3 Backtesting dan ensemble

Untuk setiap metode `m`, sembunyikan blok aktual historis sepanjang gap target, lalu hitung proyeksi. Ukur:

```text
MAE  = mean(abs(actual - predicted))
RMSE = sqrt(mean((actual - predicted)^2))
WAPE = sum(abs(actual - predicted)) / max(sum(abs(actual)), epsilon)
```

Bobot metode:

```text
rawWeight(m) = 1 / (WAPE(m) + epsilon)
weight(m)    = rawWeight(m) / sum(rawWeight(all m))
```

Proyeksi final per device dan per jam:

```text
ProjectedKWh(D,d,h) = sum(weight(m) * P_m(D,d,h))
```

Jika histori kurang atau seluruh metode gagal, gunakan median jam yang sama dari device sebagai fallback, beri confidence rendah, dan simpan alasan `INSUFFICIENT_BASELINE`.

## 7. Confidence dan interval ketidakpastian

Skor confidence adalah kualitas bukti, bukan jaminan angka pasti benar.

```text
sampleScore   = min(1, historicalSamples / 56)
coverageScore = actualHistoricalBuckets / expectedHistoricalBuckets
errorScore    = max(0, 1 - WAPE / WapeLimit)
repeatScore   = min(1, repeatedPatterns / 8)

confidence = 100 * (
    0.30 * sampleScore +
    0.25 * coverageScore +
    0.30 * errorScore +
    0.15 * repeatScore
)
```

Level:

```text
80-100 HIGH, 60-79 MEDIUM, 40-59 LOW, 0-39 VERY_LOW
```

Interval prediksi:

```text
lowerBound = max(0, projected - 1.96 * residualStdDev)
upperBound = projected + 1.96 * residualStdDev
```

Jika residual tidak normal, gunakan quantile residual P10 dan P90 dari backtesting.

## 8. Agregasi harian, bulanan, dan tahunan

**Hanya grafik per jam yang menjalankan perhitungan proyeksi.** Harian, bulanan, dan tahunan tidak boleh memiliki algoritma proyeksi terpisah. Setelah setiap bucket jam per device memiliki nilai aktual atau projected, level yang lebih tinggi hanya melakukan agregasi normal seperti saat data lengkap:

```text
DailyEnergy(D,d)   = sum(HourlyEnergy(D,d,h))
MonthlyEnergy(D,m) = sum(DailyEnergy(D,d) dalam bulan m)
YearlyEnergy(D,y)  = sum(MonthlyEnergy(D,m) dalam tahun y)
```

Aturan penting:

1. Tidak ada pencarian pola baru pada level harian.
2. Tidak ada pencarian pola baru pada level bulanan.
3. Tidak ada pencarian pola baru pada level tahunan.
4. Tidak boleh memproyeksikan total harian atau tahunan secara langsung.
5. Semua nilai projected pada harian/bulanan/tahunan harus berasal dari penjumlahan bucket jam projected.
6. Proses agregasi menggunakan jalur normal yang sudah dipakai sistem.

Status agregat dihitung dari komposisi bucket jam:

```text
ACTUAL    = semua bucket aktual
PROJECTED = semua bucket projected
MIXED     = campuran aktual dan projected
```

Kartu dashboard menampilkan minimal:

```text
Aktual: xxx kWh
Proyeksi: yyy kWh
Total termasuk proyeksi: zzz kWh
Confidence: MEDIUM (72)
```

## 9. Target tampilan chart dan cakupan halaman

Target visual mengikuti tampilan dashboard pada referensi: kartu ringkasan di bagian atas, grafik energi per jam/per hari/per bulan, kartu puncak konsumsi, estimasi biaya, dan bagian Usage Statistics. Perubahan hanya menambahkan pembeda proyeksi; struktur visual existing tetap dipertahankan.

### 9.1 Halaman Usage Statistics per device di Details

Pada halaman Details untuk satu device:

1. Grafik per jam memakai data aktual device tersebut.
2. Jika terdapat MQTT gap, hanya bucket jam yang hilang yang diisi dengan hasil proyeksi device yang sama.
3. Bar aktual tetap menggunakan warna existing.
4. Bar projected menggunakan warna merah.
5. Tooltip menampilkan status, sumber histori, confidence, rentang prediksi, dan dampak biaya.
6. Kartu total menampilkan pemisahan actual, projected, dan total complete.
7. Peak hour harus memiliki dua mode yang jelas:
   - `Actual peak`: hanya berdasarkan data aktual.
   - `Complete peak`: berdasarkan aktual + projected.

Contoh seri per-device:

```text
Device A - 03:00 = aktual  -> warna biru
Device A - 04:00 = projected -> warna merah
Device A - 12:00 = aktual  -> warna biru
```

Perhitungan pada Details tidak boleh mengambil baseline dari device lain.

### 9.2 Tab utama Usage Statistics

Pada tab utama Usage Statistics:

1. Perhitungan awal tetap dilakukan per device dan per jam.
2. Setelah semua device memiliki seri per jam masing-masing, seri tersebut dijumlahkan untuk tampilan gabungan.
3. Data aktual gabungan tetap memakai warna existing.
4. Jika satu atau beberapa device memiliki proyeksi pada jam yang sama, bagian projected pada total gabungan diberi warna merah.
5. Tooltip gabungan harus dapat menjelaskan kontribusi projected per device.
6. Filter device/panel harus memakai metode yang sama dengan halaman Details.
7. Mengubah filter device tidak boleh mengubah baseline histori atau mencampur histori antar device.

Struktur perhitungan tab utama:

```text
Untuk setiap DeviceKey:
    hitung HourlyActual(DeviceKey, HourOfDay)
    hitung HourlyProjected(DeviceKey, HourOfDay) jika gap

TotalActual(hour)    = SUM(HourlyActual semua device)
TotalProjected(hour) = SUM(HourlyProjected semua device)
TotalComplete(hour)  = TotalActual(hour) + TotalProjected(hour)
```

### 9.3 Target visual grafik

Grafik harus mendukung seri actual dan projected secara terpisah atau memakai `backgroundColor` array berdasarkan status bucket.

```text
Per jam aktual       = biru existing
Per jam projected    = merah
Per hari aktual      = hijau existing
Per hari projected   = merah
Per bulan/tahun      = biru muda existing
Projected aggregate  = merah atau stacked merah
```

Untuk grafik stacked:

```text
bar actual    = bagian konsumsi aktual
bar projected = bagian konsumsi hasil proyeksi
```

Jika seluruh nilai dalam satu bucket adalah projected, bar tersebut sepenuhnya merah. Jika hanya sebagian device projected, bar dapat berupa stacked actual + projected agar kontribusi proyeksi tidak disamarkan.

### 9.4 Kartu ringkasan dan indikator

Kartu ringkasan harus tetap memakai layout existing, tetapi menambahkan metadata jika terdapat proyeksi:

```text
Hari ini: 208,37 kWh
Aktual: 160,20 kWh
Proyeksi: 48,17 kWh
Status: MIXED
Confidence terendah: 68/100
```

Jika tidak ada proyeksi, tampilan harus tetap sama seperti sebelumnya tanpa badge tambahan.

Indikator yang disarankan:

```text
AKTUAL
PROYEKSI MQTT GAP
CAMPURAN AKTUAL + PROYEKSI
```

### 9.5 Tampilan responsif dan dinamis

Layout chart harus menyesuaikan data dan viewport tanpa mengubah arti warna/status:

- Jumlah label sumbu mengikuti bucket yang dikembalikan API.
- Label yang terlalu banyak boleh dipadatkan, tetapi tooltip tetap menyediakan detail setiap bucket.
- Ukuran chart mengikuti container, bukan ukuran tetap dari screenshot.
- Legend hanya menampilkan seri yang tersedia.
- Dataset actual/projected dibuat atau disembunyikan sesuai status data.
- Kartu energi, biaya, dan confidence menyesuaikan panjang angka, jumlah device, dan format bahasa.
- Grafik harus bekerja untuk satu device maupun banyak device.
- State `loading`, kosong, error, `NO_DATA`, dan `NO_BASELINE` harus berbeda.

### 9.6 Filter dan mode tampilan

Filter harus mengubah hasil perhitungan secara dinamis, bukan hanya menyembunyikan bar di frontend:

```text
deviceKey atau semua device
tanggal/periode
hourly/daily/monthly/yearly
actual-only atau actual + projected
include/exclude planned downtime
```

Saat filter berubah, backend/API harus mengembalikan seri, total, biaya, peak, confidence, dan metadata untuk filter baru. Frontend merender response tersebut tanpa memakai sisa data dari filter sebelumnya.

## 10. Estimasi biaya per jam, per hari, bulan, dan tahun

Estimasi biaya harus mengikuti seri energi yang sama. Jangan menghitung biaya dengan rata-rata biaya terpisah yang tidak berasal dari bucket energi.

### 10.1 Tarif

Gunakan tarif yang berlaku pada waktu bucket. Jika tarif sederhana satu nilai per kWh:

```text
costKWh = energyKWh * tariffPerKWh
```

Jika terdapat tarif berdasarkan waktu, gunakan tarif bucket:

```text
hourCost(D,d,h) = energyKWh(D,d,h) * tariff(D,d,h)
```

Energi aktual dan projected harus dihitung terpisah:

```text
actualCost(D,d,h)    = actualEnergyKWh(D,d,h) * tariff(D,d,h)
projectedCost(D,d,h) = projectedEnergyKWh(D,d,h) * tariff(D,d,h)
completeCost(D,d,h)  = actualCost(D,d,h) + projectedCost(D,d,h)
```

### 10.2 Biaya per jam

Pada grafik per jam atau detail bucket:

```text
Actual hourly cost    = energi aktual pada jam tersebut * tarif jam tersebut
Projected hourly cost = energi projected pada jam tersebut * tarif jam tersebut
Complete hourly cost  = actual hourly cost + projected hourly cost
```

Tooltip wajib menunjukkan:

```text
Energi: 105,55 kWh
Status: PROYEKSI
Tarif: Rp1.449/kWh
Biaya projected: Rp152.942
Rentang biaya: Rp132.000 - Rp174.000
```

### 10.3 Biaya per hari

Biaya harian hanya agregasi dari seluruh bucket jam:

```text
DailyActualCost(D,d)    = SUM(actualCost(D,d,h))
DailyProjectedCost(D,d) = SUM(projectedCost(D,d,h))
DailyCompleteCost(D,d)  = DailyActualCost(D,d) + DailyProjectedCost(D,d)
```

Untuk tab utama:

```text
DailyActualCost(d)    = SUM(DailyActualCost(D,d) semua device)
DailyProjectedCost(d) = SUM(DailyProjectedCost(D,d) semua device)
DailyCompleteCost(d)  = DailyActualCost(d) + DailyProjectedCost(d)
```

Tidak boleh menghitung biaya harian dengan cara `rata-rata jam * 24` jika seri jam tersedia.

### 10.4 Biaya bulanan dan tahunan

```text
MonthlyActualCost(m)    = SUM(DailyActualCost dalam bulan m)
MonthlyProjectedCost(m) = SUM(DailyProjectedCost dalam bulan m)
MonthlyCompleteCost(m)  = MonthlyActualCost(m) + MonthlyProjectedCost(m)

YearlyActualCost(y)     = SUM(MonthlyActualCost dalam tahun y)
YearlyProjectedCost(y)  = SUM(MonthlyProjectedCost dalam tahun y)
YearlyCompleteCost(y)   = YearlyActualCost(y) + YearlyProjectedCost(y)
```

Kartu `Estimasi Biaya` pada dashboard harus menampilkan mode yang dipakai:

```text
Estimasi biaya - aktual saja
Estimasi biaya - termasuk proyeksi
```

Untuk transparansi, tambahkan rincian:

```text
Aktual: RpX
Proyeksi: RpY
Total termasuk proyeksi: RpZ
```

Jika laporan resmi tidak boleh menggunakan estimasi, gunakan `actual-only`. Jika pengguna ingin melihat estimasi lengkap akibat gap, gunakan `complete`.

### 10.5 Kartu Estimasi Biaya dengan nilai proyeksi

Kartu **Estimasi Biaya** harus menampilkan biaya total termasuk proyeksi sebagai nilai utama. Nilai utama tidak boleh hanya menampilkan biaya aktual ketika mode yang dipilih adalah `complete`.

Rumus kartu:

```text
ActualBillCost    = biaya aktual pada periode terpilih
ProjectedBillCost = biaya dari bucket projected pada periode terpilih
EstimatedCost     = ActualBillCost + ProjectedBillCost
```

Contoh dinamis:

```text
ActualBillCost    = Rp15.000.000
ProjectedBillCost = Rp726.000
EstimatedCost     = Rp15.726.000
```

Target tampilan kartu:

```text
ESTIMASI BIAYA - NOVEMBER 2026
Rp 15.726.000
Tarif: Rp 1.449/kWh | Proyeksi: Rp 726.000
```

Aturan tampilan:

1. Angka utama kartu adalah `EstimatedCost` ketika mode `complete` aktif.
2. Nilai `ProjectedBillCost` ditampilkan di sebelah informasi tarif pada baris metadata kartu.
3. Label `Proyeksi` harus berwarna atau memiliki indikator merah agar berbeda dari tarif.
4. Nilai tarif dan biaya proyeksi berasal dari response/API runtime, bukan teks tetap pada view.
5. Jika tidak ada bucket projected:

   ```text
   Tarif: Rp 1.449/kWh | Proyeksi: Rp 0
   ```

   atau indikator proyeksi dapat disembunyikan sesuai desain UI, tetapi nilai total harus tetap sama dengan biaya aktual.

6. Jika terdapat proyeksi sebagian, kartu menampilkan total aktual + proyeksi.
7. Jika seluruh data periode masih projected, kartu tetap menampilkan total projected dengan status `PROJECTED` dan confidence yang sesuai.
8. Jika proyeksi berstatus `NO_BASELINE`, biaya tersebut tidak boleh dimasukkan ke total; kartu harus menampilkan status bahwa estimasi belum lengkap.
9. Tooltip/detail kartu harus memuat rincian:

   ```text
   Biaya aktual: Rp15.000.000
   Biaya proyeksi: Rp726.000
   Total estimasi: Rp15.726.000
   Confidence: dinamis dari hasil per-device
   ```

10. Saat filter device, periode, tarif, atau mode berubah, ketiga nilai biaya tersebut harus dihitung ulang:

    ```text
    actualCost
    projectedCost
    completeCost
    ```

11. Pada mode `actual-only`, angka utama kartu adalah `ActualBillCost` dan metadata menampilkan `Proyeksi tidak dihitung` atau nilai proyeksi secara terpisah tanpa menjumlahkannya.

12. Pada mode `complete`, response API minimal menyediakan:

    ```json
    {
      "actualCost": 15000000,
      "projectedCost": 726000,
      "completeCost": 15726000,
      "tariffPerKWh": 1449,
      "costStatus": "MIXED",
      "projectionUsed": true
    }
    ```

13. Format rupiah harus mengikuti formatter dinamis aplikasi dan tidak boleh mengandalkan string nominal hardcode. Nilai numerik tetap disimpan/dikirim sebagai angka agar dapat dihitung ulang dan diekspor.

### 10.6 Perhitungan biaya harus dinamis

Tarif tidak boleh disisipkan langsung di JavaScript atau template tampilan. Tarif harus diambil dari konfigurasi/service tarif yang berlaku untuk:

```text
DeviceKey
tanggal
jam
mode tarif
periode efektif tarif
```

Jika tarif berubah di tengah hari, bucket sebelum dan sesudah perubahan dihitung dengan tarif masing-masing. Jika tarif tidak tersedia atau tidak valid, API mengembalikan `TARIFF_UNAVAILABLE` dan tidak membuat angka biaya palsu.

Perubahan tarif, device, periode, atau mode proyeksi harus menghitung ulang seluruh biaya terkait dan tidak boleh hanya mengganti label kartu.

## 11. Bar merah pada Chart.js

### 11.1 Warna

```text
Hourly actual  #1976D2
Daily actual   #198754
Monthly/yearly #18B6D9
Projected gap  #DC3545
Projected hover #FF6B6B
```

Merah hanya diterapkan pada bucket yang berasal dari `EnergyProjectionValues`.

### 11.2 Kontrak seri API

Setiap label grafik mengirim status dan metadata:

```json
{
  "label": "09:00",
  "energyKWh": 105.55,
  "actualEnergyKWh": 0.0,
  "projectedEnergyKWh": 105.55,
  "actualCost": 0.0,
  "projectedCost": 152942.0,
  "completeCost": 152942.0,
  "dataStatus": "PROJECTED",
  "deviceKey": "Device-A",
  "hourOfDay": 9,
  "confidenceScore": 72,
  "projectionMethod": "SEASONAL_ENSEMBLE",
  "gapId": 123
}
```

Di Chart.js, gunakan dataset actual dan projected. Jika tetap memakai satu dataset, `backgroundColor` harus berupa array yang ditentukan dari `dataStatus`.

Tooltip wajib memuat:

```text
09:00 - 105,55 kWh
Status: PROYEKSI
Metode: Seasonal Ensemble
Confidence: 72/100
Sumber: 8 minggu terakhir
Rentang: 91,20 - 119,80 kWh
Biaya projected: Rp152.942
```

Legend: `Aktual` dan `Proyeksi - MQTT Gap`. Tambahkan filter `Tampilkan proyeksi`.

### 11.3 Level grafik

- Per jam: bar merah tepat pada jam yang hilang dan dihitung per device.
- Per hari: bar/segmen merah hanya menunjukkan jumlah jam projected yang dijumlahkan normal; biaya harian mengikuti jumlah biaya per jam.
- Per bulan: hanya agregasi normal dari nilai harian/jam, bukan proyeksi bulanan baru; biaya bulanan mengikuti agregasi harian.
- Per tahun: hanya agregasi normal dari nilai bulanan, bukan proyeksi tahunan baru; biaya tahunan mengikuti agregasi bulanan.

## 12. Laporan harian dan penjelasan proyeksi per device

Laporan harian harus menyatakan secara eksplisit apakah angka dan biaya menggunakan proyeksi. Jika tidak ada proyeksi, laporan tetap menggunakan format normal.

### 12.1 Ringkasan laporan harian

Contoh jika terdapat proyeksi:

```text
Tanggal: 9 November 2026
Status data: CAMPURAN AKTUAL + PROYEKSI

Energi aktual: 160,20 kWh
Energi proyeksi: 48,17 kWh
Energi total termasuk proyeksi: 208,37 kWh

Biaya aktual: Rp232.000
Biaya proyeksi: Rp69.820
Biaya total termasuk proyeksi: Rp301.820
```

### 12.2 Rincian per device

Laporan harus menulis device yang memakai proyeksi:

```text
Device-A
  Jam projected: 03:00, 04:00, 12:00
  Energi projected: 48,17 kWh
  Biaya projected: Rp69.820
  Metode: Device yang sama + jam yang sama
  Historis: 8 minggu sebelumnya
  Confidence: 72/100 - MEDIUM
  Rentang energi: 42,10 - 55,30 kWh

Device-B
  Status: AKTUAL PENUH
  Jam projected: tidak ada
```

Jika beberapa device memiliki gap, setiap device harus memiliki rincian sendiri. Laporan tidak boleh hanya menulis `data menggunakan proyeksi` tanpa daftar device dan bucket jam.

### 12.3 Catatan metode pada laporan

Tambahkan catatan standar:

```text
Nilai proyeksi dihitung per device menggunakan data aktual historis
pada jam yang sama dan pola hari yang sama atau paling mirip.
Data harian, bulanan, dan tahunan hanya merupakan agregasi dari
bucket per jam. Nilai projected bukan pembacaan meter aktual.
```

### 12.4 Export dan laporan resmi

Export CSV/PDF/Excel harus memiliki kolom:

```text
DeviceKey
Tanggal
Jam
ActualEnergyKWh
ProjectedEnergyKWh
CompleteEnergyKWh
ActualCost
ProjectedCost
CompleteCost
DataStatus
ProjectionMethod
ConfidenceScore
GapId
```

Tambahkan pilihan export:

```text
Aktual saja
Aktual + proyeksi
```

### 12.5 Laporan dinamis berdasarkan hasil runtime

Laporan harian tidak boleh menggunakan nama device atau jam tetap. Daftar device dan bucket harus dibentuk dari hasil query periode tersebut.

Jika tidak ada device yang memakai proyeksi:

```text
Status data: AKTUAL PENUH
Proyeksi: tidak digunakan
```

Jika ada proyeksi, laporan hanya membuat rincian untuk device dan bucket yang benar-benar berstatus `PROJECTED` atau `MIXED`. Jika jumlah device besar, detail dapat disajikan sebagai tabel yang dapat diurutkan/difilter tanpa menghilangkan metadata proyeksi.

Setiap laporan menyimpan parameter runtime:

```text
Device filter
Period start/end
Timezone
Projection mode
Tariff version
Calculation version
GeneratedAtUtc
```

## 13. Alur backend

### Tahap A - Ingest MQTT

1. Validasi payload dan timestamp.
2. Simpan reading aktual ke `KWHData`.
3. Update `LastValidReading` per device.
4. Hitung interval terhadap reading sebelumnya.
5. Buat/update `EnergyProjectionGaps` jika threshold terlampaui.

### Tahap B - Projection worker

Worker berjalan setiap 1-5 menit:

1. Ambil gap terbuka/baru ditutup.
2. Ambil histori aktual device.
3. Bangun kandidat seasonal profile.
4. Jalankan backtesting.
5. Hitung ensemble, confidence, dan bounds.
6. Simpan bucket projected secara idempotent.
7. Tandai gap `PROJECTED` dan simpan `CalculationVersion`.

### Tahap C - Rekonsiliasi

Jika telemetry terlambat masuk:

1. Tandai bucket dengan telemetry sebagai `RECONCILED`.
2. Abaikan projected pada bucket tersebut.
3. Hitung ulang hanya bucket yang masih hilang.
4. Rebuild daily/monthly/yearly dari seri jam.
5. Catat waktu dan hasil rekonsiliasi.

## 14. Kontrak API

Tambahkan metadata tanpa menghapus field lama:

```json
{
  "energy": 1240.50,
  "actualEnergy": 900.25,
  "projectedEnergy": 340.25,
  "dataStatus": "MIXED",
  "projection": {
    "enabled": true,
    "confidenceScore": 72,
    "confidenceLevel": "MEDIUM",
    "method": "SEASONAL_ENSEMBLE",
    "historicalSamples": 56,
    "gapCount": 1,
    "lowerBound": 1180.10,
    "upperBound": 1305.70,
    "calculationVersion": 1
  },
  "series": []
}
```

Parameter:

```text
includeProjection=true|false
projectionMode=actual|complete
```

Mode `actual` harus tetap tersedia untuk laporan resmi yang tidak boleh memasukkan estimasi.

## 15. Validasi akurasi

### 15.1 Simulasi gap

Pada histori aktual yang lengkap, sembunyikan blok:

```text
1 jam, 3 jam, 6 jam, 12 jam, 24 jam, 48 jam, 7 hari
```

Ukur MAE, RMSE, WAPE, bias, dan coverage interval prediksi per device serta kelas hari.

### 15.2 Target awal

```text
WAPE median <= 15% untuk gap <= 24 jam
WAPE median <= 25% untuk gap 24-72 jam
Bias absolut <= 5% pada agregat harian
Tidak ada projected negatif
Tidak ada bucket duplikat
Total level jam = total level hari/bulan/tahun
```

Jika target gagal, tetap tampilkan bar merah tetapi turunkan confidence. Jangan menyamarkan proyeksi sebagai aktual.

### 15.3 Uji UI dan regresi

- Grafik tanpa gap harus sama seperti sebelumnya.
- Satu jam hilang menghasilkan satu bar merah.
- Gap melewati tengah malam, bulan, dan tahun harus memakai boundary benar.
- Tooltip, legend, export, ranking, peak hour, biaya, dan comparison harus membedakan actual/projected.
- Late reading harus menghapus pengaruh projected pada bucket yang sudah menjadi aktual.

## 16. Edge case

| Kondisi | Perlakuan |
|---|---|
| Device baru, histori < 7 hari | Fallback confidence rendah atau `NO_BASELINE` |
| Tidak ada histori aktual | Jangan membuat angka proyeksi |
| Gap lebih panjang dari histori | Batasi confidence dan gunakan baseline konservatif |
| Device sengaja dimatikan | Tandai `PLANNED_DOWNTIME`; jangan proyeksikan konsumsi normal |
| Reading terlambat | Rekonsiliasi bucket dan hitung ulang sisa gap |
| Timestamp mundur/ekstrem | Karantina reading, jangan membuat gap palsu |
| Meter reset | Pisahkan dari MQTT gap dan validasi counter |
| Pola operasi berubah | Beri bobot lebih besar pada histori terbaru dan turunkan confidence |
| Banyak device offline | Tetap hitung per device, jangan menyalin pola device lain tanpa fallback eksplisit |

## 17. Tahapan implementasi

### Fase 1 - Observability dan schema

- Buat tabel gap dan projection values.
- Tambah status data dan calculation version.
- Catat last received, expected interval, coverage, dan alasan gap.

### Fase 2 - Mesin proyeksi

- Implementasikan bucket jam.
- Implementasikan pencarian aktual `DeviceKey + HourOfDay + DayClass`.
- Implementasikan seasonal weekday/weekend pada jam yang sama.
- Implementasikan backtesting MAE/RMSE/WAPE.
- Implementasikan ensemble, confidence, dan bounds.
- Implementasikan worker idempotent.

### Fase 3 - Agregasi dan API

- Gabungkan actual + projected pada query hourly.
- Jalankan agregasi daily/monthly/yearly normal dari bucket jam.
- Pertahankan response lama, tambahkan metadata.
- Pastikan biaya per jam dan per hari berasal dari energi per jam.
- Pastikan biaya bulanan/tahunan hanya agregasi biaya level bawah.
- Pastikan mode biaya actual-only atau complete tersedia.
- Pastikan response selalu mengikuti filter dan periode runtime.
- Pastikan cache memakai key lengkap dan invalidation saat data, konfigurasi, atau calculation version berubah.

### Fase 4 - Dashboard

- Tambahkan dataset/bar merah Chart.js.
- Tambahkan legend, tooltip, filter, badge, dan ringkasan.
- Bedakan ranking/peak berbasis actual dan complete.
- Terapkan tampilan yang sama pada Details per device dan tab utama Usage Statistics.
- Tampilkan biaya aktual, projected, dan complete pada tooltip/kartu.
- Pastikan jumlah dataset, label, device, kartu, dan state tampilan dibentuk dinamis dari response.
- Pastikan perubahan filter melakukan refresh data dan kalkulasi, bukan hanya menyembunyikan elemen visual.

### Fase 5 - Rekonsiliasi dan audit

- Uji late MQTT delivery.
- Uji restart worker dan duplikasi.
- Uji calculation version.
- Tambahkan detail sumber pola dan alasan proyeksi.
- Jalankan backtesting production sample sebelum default diaktifkan.

## 18. Keputusan yang direkomendasikan

1. Granularitas dan titik awal proyeksi adalah grafik per jam.
2. Setiap perhitungan memakai `DeviceKey` yang sama dan `HourOfDay` yang sama.
3. Baseline utama adalah data aktual historis dengan pola hari yang sama atau paling mirip.
4. Bobot metode ditentukan dari backtesting aktual.
5. Hasil projected disimpan terpisah dan tidak overwrite aktual.
6. Bar merah muncul per bucket dengan tooltip dan confidence.
7. Daily/monthly/yearly hanya agregasi normal dari hasil per jam.
8. Telemetry terlambat memicu rekonsiliasi otomatis.
9. Tanpa baseline, tampilkan `NO_BASELINE`, bukan angka palsu.
10. Halaman Details dan tab utama Usage Statistics wajib menggunakan metode perhitungan yang sama.
11. Biaya per jam dan per hari mengikuti energi per jam; biaya bulanan/tahunan hanya agregasi normal.
12. Laporan harian wajib menyebutkan device, jam, metode, confidence, energi, dan biaya yang diproyeksikan.
13. Tidak ada angka, device, jam, tarif, label, atau jumlah bucket yang di-hardcode.
14. Semua grafik, kartu, laporan, export, dan biaya mengikuti filter serta data runtime terbaru.

## 19. Kriteria selesai

- Gap MQTT terdeteksi dan tersimpan idempotent.
- Actual dan projected dapat difilter serta diaudit terpisah.
- Bar merah muncul tepat pada interval hilang.
- Total antar level konsisten.
- Late reading dapat mengganti projected tanpa duplikasi.
- Backtesting dan confidence tersedia per device.
- Grafik existing tanpa gap tidak berubah.
- Grafik Details per device dan tab utama menampilkan hasil yang konsisten dari metode yang sama.
- Estimasi biaya per jam, per hari, bulanan, dan tahunan konsisten dengan energi masing-masing.
- Laporan harian mencantumkan device dan jam yang menggunakan proyeksi.
- Perubahan device, periode, tarif, mode proyeksi, dan data MQTT menghasilkan tampilan serta perhitungan baru secara dinamis.
- State loading, empty, error, `NO_DATA`, `NO_BASELINE`, actual-only, dan complete tampil sesuai kondisi runtime.
- Build dan seluruh test tersedia berhasil.
- Migration diverifikasi dan database dibackup sebelum production.