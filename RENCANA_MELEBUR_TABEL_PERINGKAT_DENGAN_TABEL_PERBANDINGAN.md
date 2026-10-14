# Rencana Melebur Tabel Peringkat dengan Tabel Perbandingan pada Usage Statistics

> **Status:** Rencana saja — belum ada implementasi.
>
> **Batasan:** Rencana ini hanya membahas peleburan **tabel UI Peringkat** dan **tabel UI Perbandingan** pada halaman Usage Statistics. Tidak membahas penggabungan tabel database dan tidak mengubah kode, database, konfigurasi, endpoint, view, atau file lain.

## 1. Tujuan

Melebur tabel **Peringkat** dan tabel **Perbandingan** menjadi satu tabel UI pada halaman Usage Statistics agar informasi setiap perangkat dapat dibaca dalam satu baris yang utuh.

Setiap baris tabel gabungan harus menampilkan:

- identitas perangkat;
- nilai konsumsi periode yang sedang dipilih;
- nilai pembanding periode sebelumnya dan/atau periode tahun sebelumnya;
- perubahan konsumsi dalam nilai absolut;
- perubahan konsumsi dalam persentase;
- posisi peringkat perangkat;
- nilai yang digunakan sebagai dasar peringkat.

Peleburan ini hanya menyatukan tampilan dan sumber data pada UI. Tidak ada rencana membuat atau menggabungkan tabel fisik database.

## 2. Ruang Lingkup

### Termasuk

- Tabel UI Peringkat pada halaman Usage Statistics.
- Tabel UI Perbandingan pada halaman Usage Statistics.
- Struktur kolom tabel gabungan.
- Urutan baris dan sorting.
- Penyamaan filter periode dan perangkat.
- Penyatuan data dari response Usage Statistics.
- State loading, kosong, error, dan data tidak lengkap.
- Tampilan desktop dan responsive/mobile.
- Validasi angka dan kesetaraan dengan tampilan lama.

### Tidak termasuk

- Mengubah struktur tabel `HourlyEnergy`, `DailyEnergy`, `MonthlyEnergy`, atau `YearlyEnergy`.
- Membuat tabel database baru.
- Menghapus endpoint lama.
- Mengubah formula konsumsi yang sudah berjalan.
- Mengubah fitur di luar halaman Usage Statistics.
- Menggabungkan tabel UI pada halaman Details kecuali diminta secara terpisah.
- Mengubah desain global situs di luar kebutuhan tabel gabungan.

## 3. Lokasi UI dan Data yang Relevan

| Komponen | Lokasi | Peran |
|---|---|---|
| Halaman Usage Statistics | `KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring/Views/Monitoring/UsageStatistics.cshtml` | Tempat tabel Peringkat dan Perbandingan ditampilkan |
| API Usage Statistics | `KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring/Controllers/ApiController.cs` | Penyedia data statistik per perangkat/semua perangkat |
| Controller halaman | `KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring/Controllers/MonitoringController.cs` | Penyedia halaman Usage Statistics bila action halaman memerlukannya |
| Dokumentasi formula | `AFTER_FINANCIAL_USAGE.md` | Definisi `periodComparison`, MoM, dan YoY |
| Dokumentasi perhitungan | `FINANCIAL_CALCULATIONS.md` | Formula perubahan periode dan aturan edge case |

File-file di atas hanya merupakan kandidat yang perlu diperiksa atau mungkin diubah saat implementasi. Pada tahap rencana ini tidak ada perubahan terhadap file-file tersebut.

## 4. Kondisi Data yang Harus Dipahami

### 4.1 Data tabel Perbandingan

Data perbandingan yang sudah terdokumentasi menggunakan objek `periodComparison`, dengan informasi utama:

- `lastMonthKWh`;
- `momChange`;
- `momChangePercent`;
- `lastYearSameMonthKWh`;
- `yoyChange`;
- `yoyChangePercent`.

Perbandingan MoM membandingkan periode aktif dengan bulan sebelumnya. Perbandingan YoY membandingkan periode aktif dengan bulan yang sama pada tahun sebelumnya.

### 4.2 Data tabel Peringkat

Sebelum implementasi, data yang mengisi tabel Peringkat harus dipetakan secara pasti dari source aktif. Hal yang harus diketahui:

- nama field perangkat;
- nama field konsumsi yang digunakan untuk ranking;
- urutan ranking, terbesar atau terkecil;
- apakah ranking berdasarkan kWh, biaya, perubahan, atau metrik lain;
- apakah ranking bersifat global untuk semua perangkat;
- perilaku saat pengguna memilih satu perangkat;
- aturan jika dua perangkat memiliki nilai yang sama;
- perilaku perangkat tanpa data.

Dokumen ini tidak mengasumsikan nama field ranking tertentu sebelum implementasi memeriksa `UsageStatistics.cshtml` dan response API aktual.

## 5. Desain Tabel UI Gabungan

### 5.1 Prinsip tampilan

Tabel gabungan harus memiliki satu baris untuk satu perangkat. Informasi peringkat dan perbandingan ditempatkan dalam baris yang sama sehingga pengguna tidak perlu mencocokkan dua tabel secara manual.

Urutan informasi yang direkomendasikan:

1. Peringkat.
2. Nama perangkat/panel.
3. Konsumsi periode aktif.
4. Konsumsi periode sebelumnya.
5. Perubahan MoM.
6. Perubahan YoY.
7. Nilai dasar ranking.
8. Status atau waktu data terakhir.

### 5.2 Rancangan kolom

| No. | Kolom UI | Sumber data konseptual | Keterangan |
|---:|---|---|---|
| 1 | Peringkat | Data ranking | Posisi perangkat pada cakupan ranking yang disepakati |
| 2 | Perangkat | `DeviceKey`, `DeviceId`, atau `GroupName` | Identitas yang ditampilkan kepada pengguna |
| 3 | Periode Aktif | Nilai konsumsi periode terpilih | Nilai dasar yang sedang dianalisis |
| 4 | Bulan Sebelumnya | `periodComparison.lastMonthKWh` | Nilai pembanding MoM |
| 5 | Perubahan MoM | `momChange` | Selisih periode aktif dengan bulan sebelumnya |
| 6 | Persentase MoM | `momChangePercent` | Perubahan MoM dalam persen |
| 7 | Bulan Sama Tahun Lalu | `lastYearSameMonthKWh` | Nilai pembanding YoY |
| 8 | Perubahan YoY | `yoyChange` | Selisih periode aktif dengan tahun lalu |
| 9 | Persentase YoY | `yoyChangePercent` | Perubahan YoY dalam persen |
| 10 | Dasar Peringkat | Metrik ranking | Nilai yang digunakan untuk menentukan peringkat |
| 11 | Data Terakhir | `CalculatedAt`/`SourceDataAt` bila tersedia | Menjelaskan kesegaran data |

Kolom dapat disederhanakan pada layar kecil menggunakan tampilan responsif, tetapi informasi inti tidak boleh hilang tanpa alternatif seperti detail baris, tooltip, atau scroll horizontal.

### 5.3 Pengelompokan header yang disarankan

Jika desain tabel menggunakan header bertingkat, kelompok kolom dapat disusun sebagai berikut:

```text
┌──────────────┬──────────────────┬──────────────────────────────┬────────────────┐
│ Identitas    │ Peringkat        │ Perbandingan                 │ Data           │
│ Perangkat    │                  │ Aktif | MoM | YoY            │ Pendukung      │
└──────────────┴──────────────────┴──────────────────────────────┴────────────────┘
```

Jika header bertingkat tidak sesuai dengan struktur view yang sudah ada, gunakan header tunggal dengan label yang jelas. Jangan memaksakan perubahan besar pada layout.

## 6. Penyatuan Sumber Data di UI

### 6.1 Pencocokan baris

Data Peringkat dan Perbandingan harus digabung berdasarkan kunci perangkat yang konsisten, dengan prioritas:

1. `DeviceKey` sebagai kunci utama.
2. `DeviceId` atau identifier lain hanya sebagai fallback jika memang dipakai oleh source aktif.
3. Jangan mencocokkan baris hanya berdasarkan nama tampilan karena nama dapat sama atau berubah.

### 6.2 Proses penggabungan data

Urutan proses yang direncanakan:

1. Ambil response Usage Statistics sesuai filter aktif.
2. Ambil daftar data peringkat dari response atau proses yang saat ini digunakan UI.
3. Ambil `periodComparison` untuk perangkat yang sama.
4. Buat lookup berdasarkan `DeviceKey`.
5. Gabungkan data peringkat dan data perbandingan menjadi satu koleksi baris tabel.
6. Pertahankan baris perangkat yang tidak memiliki data pembanding dengan status data tidak tersedia.
7. Urutkan hasil berdasarkan peringkat yang sudah ditentukan.
8. Render satu tabel gabungan.

### 6.3 Perhitungan server atau browser

Keputusan yang direkomendasikan:

- Ranking final ditetapkan oleh sumber data yang saat ini bertanggung jawab atas ranking.
- UI tidak menghitung ulang posisi ranking dengan aturan yang berbeda.
- UI hanya menggabungkan dan menampilkan data.
- Formula MoM/YoY tetap mengikuti nilai dari `periodComparison`.
- Jika ada perhitungan tambahan di browser, perhitungan tersebut harus identik dengan formula lama dan diuji terhadap response server.

## 7. Filter, Sorting, dan Perilaku Interaksi

### 7.1 Filter periode

- Tabel gabungan harus mengikuti filter tanggal/periode yang sama dengan Usage Statistics.
- Periode aktif, bulan sebelumnya, dan tahun sebelumnya harus berasal dari konteks filter yang sama.
- Saat filter berubah, seluruh baris tabel harus diperbarui sebagai satu kesatuan.
- Tidak boleh terjadi kondisi ranking berasal dari periode baru sementara perbandingan masih berasal dari periode lama.

### 7.2 Filter perangkat

- Jika semua perangkat dipilih, tampilkan seluruh baris sesuai cakupan ranking.
- Jika satu perangkat dipilih, tampilkan perilaku yang sudah disepakati:
  - mempertahankan peringkat global; atau
  - menghitung peringkat ulang pada subset.
- Pilihan tersebut harus konsisten dengan perilaku tabel Peringkat sebelum peleburan.

### 7.3 Sorting

- Urutan default tabel mengikuti peringkat.
- Jika sorting berdasarkan kolom lain diperbolehkan, tampilkan indikator kolom aktif.
- Sorting berdasarkan perubahan MoM/YoY tidak boleh mengubah nilai `RankingPosition`; hanya mengubah urutan tampilan.
- Jika sorting client-side digunakan, nilai kosong harus diletakkan secara konsisten di bagian bawah atau mengikuti aturan UI lama.

### 7.4 Refresh otomatis

- Refresh otomatis harus memperbarui tabel gabungan secara atomik.
- Response lama tidak boleh menimpa hasil baru ketika request selesai tidak berurutan.
- Saat refresh berlangsung, pertahankan data terakhir yang valid bila itu merupakan perilaku UI saat ini.
- Tampilkan status loading tanpa menghilangkan konteks filter pengguna.

## 8. Format Nilai dan Status Data

### 8.1 Nilai energi

- Gunakan satuan dan jumlah angka desimal yang konsisten dengan tabel Usage Statistics saat ini.
- Jangan mencampur Wh dan kWh dalam satu kolom tanpa label jelas.
- Nilai total dan nilai perbandingan harus menggunakan pembulatan yang sama dengan tampilan lama.

### 8.2 Nilai persentase

- Tampilkan tanda positif/negatif secara konsisten.
- Gunakan warna hanya sebagai bantuan visual; angka dan tanda harus tetap tersedia.
- Nilai 0% harus dibedakan dari data tidak tersedia.
- Persentase 100% akibat pembanding nol harus mengikuti aturan yang sudah berjalan.

### 8.3 Data kosong

Gunakan status yang jelas, misalnya:

- `—` untuk data tidak tersedia;
- `0` hanya jika sumber memang menyatakan nilai nol;
- `Belum ada data` untuk perangkat tanpa data periode terkait.

Jangan mengubah data kosong menjadi nol secara otomatis karena dapat memengaruhi interpretasi peringkat dan perbandingan.

## 9. Rencana Perubahan Saat Implementasi

### Tahap 1 — Audit UI saat ini

- Baca struktur markup tabel Peringkat pada `UsageStatistics.cshtml`.
- Baca struktur markup tabel Perbandingan pada file yang sama.
- Catat ID elemen, class CSS, template row, fungsi render, dan event listener.
- Catat sumber response yang dipakai masing-masing tabel.
- Catat apakah ada dua request terpisah atau satu response yang diproses dua kali.

### Tahap 2 — Pemetaan data

- Cocokkan field ranking dan field `periodComparison` berdasarkan `DeviceKey`.
- Buat tabel pemetaan field lama ke kolom tabel gabungan.
- Identifikasi field yang namanya berbeda tetapi memiliki arti sama.
- Identifikasi field yang hanya tersedia pada salah satu tabel.
- Tentukan perilaku jika pasangan data tidak ditemukan.

### Tahap 3 — Rancangan markup

- Tentukan satu elemen tabel utama.
- Tentukan struktur `<thead>` dan `<tbody>`.
- Tentukan apakah memakai header bertingkat atau header tunggal.
- Pertahankan class CSS lama selama masih dapat digunakan.
- Tambahkan class baru hanya bila diperlukan untuk kolom gabungan atau responsive layout.
- Pastikan label dan satuan mudah dipahami.

### Tahap 4 — Rancangan rendering

- Buat satu fungsi pembentukan row gabungan.
- Pastikan fungsi tersebut menerima data ranking dan perbandingan untuk perangkat yang sama.
- Hindari dua fungsi render yang menulis ke tabel yang sama.
- Pastikan row memiliki key unik berbasis `DeviceKey`.
- Pastikan render menghapus atau mengganti hasil lama secara aman tanpa menggandakan baris.

### Tahap 5 — Penyatuan interaksi

- Hubungkan filter yang sudah ada ke tabel gabungan.
- Pertahankan perilaku refresh, loading, error, dan empty state.
- Pastikan pagination, sorting, pencarian, atau filter kategori—bila ada—berlaku pada satu tabel gabungan.
- Pastikan tombol detail/link perangkat tetap mengarah ke perangkat yang benar.

### Tahap 6 — Penyelarasan visual

- Samakan tinggi baris, alignment angka, dan format header.
- Gunakan alignment kiri untuk identitas dan kanan untuk angka.
- Gunakan warna perubahan secara konsisten dengan UI lama.
- Tambahkan scroll horizontal pada layar kecil bila seluruh kolom harus dipertahankan.
- Jangan membuat tabel terlalu padat sehingga angka tidak terbaca.

## 10. Edge Case yang Wajib Ditangani

| Kasus | Perilaku tabel gabungan |
|---|---|
| Perangkat ada di ranking tetapi tidak ada data perbandingan | Baris tetap tampil; kolom perbandingan berstatus tidak tersedia |
| Perangkat ada di perbandingan tetapi tidak ada ranking | Baris tetap tampil dengan peringkat `—`, atau dikeluarkan sesuai aturan ranking lama |
| Periode pembanding bernilai nol | Tampilkan aturan persentase lama tanpa membagi dengan nol |
| Nilai periode aktif nol | Tampilkan nol sebagai nilai valid bila sumber menyatakan nol |
| Nilai kosong/null | Jangan otomatis mengubah menjadi nol |
| Dua perangkat memiliki nilai ranking sama | Ikuti aturan tie yang berlaku dan gunakan urutan stabil |
| Nama perangkat sama | Gunakan `DeviceKey` untuk pencocokan dan tampilkan identifier tambahan bila perlu |
| Response datang tidak berurutan | Hanya response untuk filter terbaru yang boleh ditampilkan |
| Semua perangkat tidak memiliki data | Tampilkan empty state yang jelas dan bukan tabel kosong tanpa keterangan |
| Layar sempit | Gunakan scroll horizontal, kolom prioritas, atau detail baris tanpa menghilangkan konteks |
| Error salah satu data | Tampilkan status parsial bila mekanisme yang ada mendukung; jangan merusak seluruh tabel tanpa alasan |

## 11. Pengujian yang Direncanakan

### 11.1 Pengujian struktur UI

- Hanya ada satu tabel gabungan yang dirender.
- Tidak ada tabel Peringkat dan Perbandingan terpisah yang masih tampil pada area yang sama.
- Header kolom sesuai dengan isi baris.
- Setiap `DeviceKey` hanya menghasilkan satu baris.
- Tidak ada baris duplikat setelah refresh.

### 11.2 Pengujian data

- Nilai peringkat cocok dengan tabel Peringkat lama.
- Nilai MoM cocok dengan tabel Perbandingan lama.
- Nilai YoY cocok dengan tabel Perbandingan lama.
- Nama perangkat cocok dengan data perangkat.
- Baris tetap benar ketika sebagian data pembanding kosong.
- Urutan default sama dengan aturan peringkat yang disepakati.

### 11.3 Pengujian filter dan refresh

- Filter periode aktif.
- Filter periode historis.
- Filter semua perangkat.
- Filter satu perangkat.
- Perubahan filter cepat berturut-turut.
- Refresh otomatis.
- Response lama selesai setelah response baru.
- Error request dan pemulihan setelah refresh berikutnya.

### 11.4 Pengujian responsive

- Desktop dengan seluruh kolom.
- Tablet dengan lebar terbatas.
- Mobile dengan scroll horizontal atau tampilan alternatif.
- Header tidak tertukar dengan baris.
- Angka dan persentase tidak terpotong.
- Link/detail perangkat tetap dapat digunakan.

## 12. Kriteria Selesai

Rencana implementasi dianggap selesai apabila:

- Tabel Peringkat dan tabel Perbandingan telah menjadi satu tabel UI pada Usage Statistics.
- Satu perangkat hanya memiliki satu baris pada tabel gabungan.
- Semua kolom utama peringkat, MoM, dan YoY tersedia serta memiliki label jelas.
- Filter periode dan perangkat berlaku konsisten pada seluruh tabel.
- Nilai yang ditampilkan setara dengan tabel lama sebelum dilebur.
- Sorting default mengikuti peringkat yang telah ditentukan.
- Nilai kosong, nol, dan error ditampilkan secara berbeda dan benar.
- Refresh otomatis tidak menggandakan baris atau menampilkan response lama.
- Tabel dapat digunakan pada desktop dan layar kecil.
- Tidak ada perubahan pada database atau fitur di luar Usage Statistics.
- Tidak ada file selain file yang memang disetujui untuk implementasi yang berubah.

## 13. Risiko dan Pencegahan

| Risiko | Pencegahan |
|---|---|
| Data dua tabel tidak memiliki kunci perangkat yang sama | Gunakan `DeviceKey` dan lakukan pemetaan field terlebih dahulu |
| Ranking dan perbandingan memakai periode berbeda | Gunakan filter periode dan request identity yang sama |
| Nilai ranking dihitung ulang berbeda di browser | Pertahankan posisi ranking dari sumber resmi dan jangan menghitung ulang tanpa aturan tertulis |
| Baris menjadi duplikat | Gunakan satu row per `DeviceKey` dan validasi jumlah baris |
| Tabel terlalu lebar | Gunakan grouping header, responsive scroll, atau detail row |
| Data lama menimpa data baru | Validasi token/request identity sebelum render |
| Perubahan UI memengaruhi halaman lain | Batasi selector, ID, dan CSS pada scope Usage Statistics |
| Nilai nol dianggap sebagai data kosong | Gunakan status ketersediaan data terpisah dari nilai numerik |

## 14. Rekomendasi Akhir

Peleburan sebaiknya dilakukan pada **lapisan UI Usage Statistics** dengan prinsip berikut:

1. Pertahankan sumber data dan formula yang sudah ada.
2. Satukan baris berdasarkan `DeviceKey`.
3. Tampilkan peringkat dan metrik perbandingan dalam satu baris.
4. Jadikan peringkat sebagai urutan default tabel.
5. Pertahankan MoM dan YoY tanpa mengubah rumus.
6. Pertahankan perilaku filter, refresh, loading, error, dan empty state.
7. Jangan mengubah struktur database atau fitur di luar Usage Statistics.
8. Verifikasi angka sebelum dan sesudah peleburan dengan kasus data yang sama.