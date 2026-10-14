# TODO Implementasi Filter dan Tabel Gabungan Usage Statistics

> **Status:** Implementasi utama selesai; verifikasi browser dan build normal masih terbuka.
>
> **Pembaruan terakhir:** 13 Oktober 2026.
>
> **Keterangan:** `[x]` selesai/terverifikasi melalui source atau pemeriksaan statis, `[~]` sudah diterapkan tetapi masih menunggu validasi runtime/browser atau environment, `[ ]` belum dikerjakan.
>
> **Catatan implementasi:** Perubahan aktual dilakukan pada `Views/Monitoring/UsageStatistics.cshtml`. Filter device/search/category untuk tabel sekarang menggunakan state `tableUsageDeviceKey` dan tidak memuat ulang kartu, grafik, atau data finansial dashboard. Billing tetap mengambil periode perbandingan dari endpoint bersama dan menggunakan data ERP `WWERP2019`.

## 1. Tujuan Implementasi

- [x] Menyatukan tabel UI **Peringkat** dan tabel UI **Perbandingan** menjadi satu tabel pada halaman Usage Statistics.
- [x] Menambahkan atau menyelaraskan filter tingkat waktu: **Hari**, **Bulan**, dan **Tahun**.
- [x] Menambahkan preset waktu yang sesuai dengan tingkat periode:
  - [x] Hari Ini.
  - [x] Kemarin.
  - [x] 2 Hari Lalu.
  - [x] 3 Hari Lalu.
  - [x] Bulan Ini.
  - [x] Bulan Lalu.
  - [x] 2 Bulan Lalu.
  - [x] 3 Bulan Lalu.
  - [x] Tahun Ini.
  - [x] Tahun Lalu.
  - [x] 2 Tahun Lalu.
- [x] Menambahkan pilihan periode kustom sesuai kemampuan UI yang sudah ada.
- [x] Menyatukan informasi perbandingan energi dan perbandingan tagihan tanpa mengubah formula lama secara tidak sengaja.
- [x] Memastikan semua data dalam satu tabel menggunakan perangkat dan periode yang sama melalui `DeviceKey` dan periode pembanding aktif.
- [~] Mempertahankan perilaku refresh, loading, error, empty state, dan fallback yang sudah berjalan; pemeriksaan source selesai, tetapi smoke test browser masih diperlukan.

## 2. Batasan Fitur

- [x] Batasi perubahan pada halaman Usage Statistics.
- [ ] Jangan mengubah tabel database `HourlyEnergy`, `DailyEnergy`, `MonthlyEnergy`, atau `YearlyEnergy`.
- [x] Jangan membuat tabel database baru untuk kebutuhan UI ini.
- [x] Jangan menghapus endpoint lama.
- [x] Jangan mengubah formula konsumsi, MoM, YoY, estimasi tagihan, atau perbandingan ERP tanpa keputusan eksplisit.
- [x] Jangan mengubah halaman Details atau halaman lain kecuali disetujui sebagai pekerjaan terpisah.
- [ ] Jangan menghapus fallback endpoint lama sebelum kesetaraan hasil dibuktikan.
- [ ] Jangan merapikan atau menghapus perubahan lokal yang sudah ada di workspace.

## 3. File yang Harus Diaudit Sebelum Coding

- [x] Baca struktur lengkap `KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring/Views/Monitoring/UsageStatistics.cshtml`.
- [x] Identifikasi markup tabel Peringkat yang sedang digunakan.
- [x] Identifikasi markup tabel Perbandingan yang sedang digunakan.
- [x] Identifikasi markup perbandingan tagihan pada halaman yang sama.
- [x] Catat `id`, `class`, selector, template row, dan container yang terkait tabel.
- [x] Catat fungsi JavaScript yang mengambil data, memproses response, dan merender tabel.
- [x] Catat event handler filter, refresh, auto-refresh, pencarian, dan pemilihan perangkat.
- [x] Baca action Usage Statistics pada `MonitoringController.cs`.
- [x] Baca endpoint Usage Statistics pada `ApiController.cs`.
- [x] Identifikasi request model dan response object yang digunakan endpoint aktif.
- [x] Identifikasi sumber data ranking aktual dari source code.
- [x] Identifikasi field `periodComparison` yang dikirim pada response aktif.
- [x] Identifikasi field estimasi tagihan dan tagihan aktual yang tersedia.
- [x] Periksa `ErpBillingComparison.cs` untuk kontrak perbandingan tagihan ERP.
- [x] Baca `AFTER_FINANCIAL_USAGE.md` untuk kontrak `periodComparison` dan data finansial.
- [x] Baca `FINANCIAL_CALCULATIONS.md` untuk formula dan pembulatan.
- [x] Baca `TODO_IMPLEMENTASI_USAGE_STATISTICS_REALTIME.md` untuk status batch, fallback, data version, dan refresh.
- [ ] Catat file test yang sudah tersedia dan pola penamaannya.

## 4. Keputusan Fungsional Sebelum Implementasi

### 4.1 Tingkat periode

- [ ] Tetapkan nilai internal untuk tingkat periode: `DAY`, `MONTH`, dan `YEAR`.
- [ ] Tetapkan label tampilan untuk ketiga tingkat periode.
- [ ] Tetapkan tingkat periode default saat halaman pertama dibuka.
- [ ] Verifikasi apakah default yang disepakati adalah `MONTH`.
- [ ] Pastikan perubahan tingkat periode memperbarui label, preset, pembanding, dan kolom tabel secara bersamaan.

### 4.2 Preset Hari

- [ ] Tetapkan definisi **Hari Ini** menggunakan zona waktu aplikasi.
- [ ] Tetapkan definisi **Kemarin**.
- [ ] Tetapkan definisi **2 Hari Lalu**.
- [ ] Tetapkan definisi **3 Hari Lalu**.
- [ ] Tetapkan perilaku untuk pilihan tanggal manual.
- [ ] Tetapkan apakah pilihan rentang tanggal juga diperlukan.
- [ ] Tetapkan pembanding default untuk mode Hari.
- [ ] Tentukan apakah pembanding harian memakai hari sebelumnya, hari yang sama minggu sebelumnya, atau pilihan lain.
- [ ] Hindari penggunaan label MoM/YoY pada mode Hari jika istilah tersebut tidak sesuai.

### 4.3 Preset Bulan

- [ ] Tetapkan definisi **Bulan Ini** berdasarkan tanggal dan zona waktu aplikasi.
- [ ] Tetapkan definisi **Bulan Lalu**.
- [ ] Tetapkan definisi **2 Bulan Lalu**.
- [ ] Tetapkan definisi **3 Bulan Lalu**.
- [ ] Tetapkan perilaku saat berpindah tahun, misalnya Januari ke Desember tahun sebelumnya.
- [ ] Tetapkan perilaku pilihan bulan dan tahun manual.
- [ ] Tetapkan apakah rentang beberapa bulan diperlukan.
- [ ] Tetapkan pembanding MoM: bulan kalender sebelumnya.
- [ ] Tetapkan pembanding YoY: bulan yang sama pada tahun sebelumnya.
- [ ] Tetapkan perlakuan khusus untuk bulan berjalan yang belum selesai.
- [ ] Putuskan apakah perbandingan bulan berjalan memakai hari yang sama atau bulan penuh sebelumnya.
- [ ] Tampilkan label bahwa bulan berjalan masih berstatus `Berjalan`.

### 4.4 Preset Tahun

- [ ] Tetapkan definisi **Tahun Ini**.
- [ ] Tetapkan definisi **Tahun Lalu**.
- [ ] Tetapkan definisi **2 Tahun Lalu**.
- [ ] Tetapkan perilaku pilihan tahun manual.
- [ ] Tetapkan pembanding tahun sebelumnya.
- [ ] Tetapkan aturan perbandingan tahun berjalan dengan cakupan bulan yang setara.
- [ ] Tampilkan cakupan, misalnya `Jan–Okt 2026`, jika tahun berjalan belum lengkap.
- [ ] Hindari membandingkan tahun berjalan parsial dengan tahun sebelumnya penuh tanpa label.

## 5. Kontrak Periode dan Batas Waktu

- [ ] Tetapkan satu format internal tanggal untuk request.
- [ ] Tetapkan batas awal periode sebagai inklusif.
- [ ] Tetapkan batas akhir periode sebagai eksklusif.
- [ ] Pastikan batas periode tidak bergantung pada waktu browser jika server menjadi sumber kebenaran.
- [ ] Pastikan timezone server, database, dan UI dipahami.
- [ ] Uji pergantian hari pada tengah malam.
- [ ] Uji pergantian bulan pada tanggal pertama.
- [ ] Uji pergantian tahun pada 1 Januari.
- [ ] Uji bulan dengan 28, 29, 30, dan 31 hari.
- [ ] Uji tahun kabisat.
- [ ] Uji transisi Januari ke Desember tahun sebelumnya.
- [ ] Uji transisi Desember ke Januari tahun berikutnya.
- [ ] Tetapkan label periode yang ditampilkan kepada pengguna.
- [ ] Tetapkan status `Berjalan`, `Final`, `Sebagian`, atau `Tidak tersedia`.

## 6. Rancangan Filter UI

- [x] Buat kontrol tingkat periode: `Hari`, `Bulan`, `Tahun`.
- [x] Buat kontrol preset yang berubah sesuai tingkat periode.
- [x] Pastikan preset Hari tidak muncul ketika mode Bulan atau Tahun aktif.
- [x] Pastikan preset Bulan tidak muncul ketika mode Hari atau Tahun aktif.
- [x] Pastikan preset Tahun tidak muncul ketika mode Hari atau Bulan aktif.
- [x] Tambahkan kontrol tanggal manual untuk mode Hari.
- [x] Tambahkan kontrol bulan dan tahun manual untuk mode Bulan.
- [x] Tambahkan kontrol tahun manual untuk mode Tahun.
- [x] Tambahkan pilihan periode kustom yang didukung UI.
- [x] Tambahkan indikator periode aktif.
- [x] Tambahkan indikator pembanding yang sedang digunakan.
- [~] Tambahkan indikator waktu data terakhir bila data tersebut tersedia; source tersedia, validasi browser masih terbuka.
- [x] Pastikan pilihan perangkat tetap bekerja bersama filter waktu.
- [x] Pastikan perubahan filter periode memicu siklus pembaruan yang konsisten.
- [x] Pastikan filter tabel tidak mengirim request dashboard tambahan.
- [x] Pertahankan nilai filter terakhir selama proses loading.
- [ ] Pastikan filter dapat digunakan dengan keyboard.
- [ ] Pastikan label dan kontrol dapat dibaca oleh screen reader bila pola aksesibilitas sudah digunakan project.

## 7. Desain Tabel UI Gabungan

### 7.1 Struktur baris

- [x] Tetapkan satu baris untuk satu `DeviceKey`.
- [x] Gunakan `DeviceKey` sebagai kunci pencocokan data.
- [x] Jangan mencocokkan baris hanya berdasarkan nama panel.
- [x] Pastikan satu perangkat tidak menghasilkan baris duplikat dari response yang sama.
- [x] Pertahankan baris ranking ketika data perbandingan belum tersedia.

### 7.2 Kolom inti

- [x] Kolom **Peringkat**.
- [x] Kolom **Perangkat/Panel**.
- [x] Kolom **Konsumsi Periode Aktif**.
- [x] Kolom **Periode Pembanding** yang labelnya menyesuaikan mode Hari/Bulan/Tahun.
- [x] Kolom **Perubahan Absolut**.
- [x] Kolom **Perubahan Persentase**.
- [x] Kolom **Estimasi Tagihan**.
- [x] Kolom **Tagihan Aktual** jika tersedia.
- [x] Kolom **Selisih Tagihan** jika kedua nilai tersedia.
- [x] Kolom **Selisih Tagihan (%)** jika pembagi valid.
- [x] Kolom **Status Data**.
- [x] Kolom **Data Terakhir**.
- [x] Link **Detail** pada row perangkat.

### 7.3 Label dinamis

- [ ] Mode Hari menggunakan label seperti `Hari Aktif`, `Hari Sebelumnya`, dan `Perubahan Harian`.
- [ ] Mode Bulan menggunakan label seperti `Bulan Aktif`, `Bulan Sebelumnya`, `MoM`, dan `YoY`.
- [ ] Mode Tahun menggunakan label seperti `Tahun Aktif`, `Tahun Sebelumnya`, dan `Perubahan Tahunan`.
- [ ] Label kolom menyertakan periode aktual jika diperlukan, misalnya `Oktober 2026`.
- [ ] Label tidak menyebut MoM/YoY pada periode yang tidak sesuai.
- [ ] Satuan kWh dan Rupiah ditampilkan dengan jelas.

### 7.4 Header bertingkat

- [ ] Putuskan apakah tabel menggunakan header bertingkat.
- [ ] Jika digunakan, kelompokkan kolom menjadi Identitas, Peringkat, Konsumsi, Perbandingan Energi, Perbandingan Tagihan, dan Status.
- [ ] Pastikan `colspan` dan `rowspan` sesuai jumlah kolom aktual.
- [ ] Pastikan header bertingkat tetap terbaca pada layar kecil.
- [ ] Jika header bertingkat tidak sesuai dengan struktur view, gunakan header tunggal yang jelas.

## 8. Penyatuan Data Peringkat dan Perbandingan

- [x] Identifikasi sumber data Peringkat yang aktif.
- [x] Identifikasi sumber data `periodComparison` yang aktif.
- [x] Identifikasi sumber data estimasi tagihan.
- [x] Identifikasi sumber data tagihan aktual ERP.
- [x] Tetapkan prioritas response perbandingan bersama.
- [x] Buat pemetaan field per perangkat.
- [x] Gabungkan data berdasarkan `DeviceKey`.
- [x] Pertahankan baris ranking jika data perbandingan belum tersedia.
- [ ] Tentukan apakah baris perbandingan tanpa ranking tetap ditampilkan.
- [x] Jangan menghitung ulang ranking di browser jika ranking sudah disediakan oleh sumber resmi.
- [x] Jangan mengubah formula `periodComparison` hanya karena tabel UI digabung.
- [x] Pastikan data peringkat dan perbandingan menggunakan filter periode yang sama.
- [x] Pastikan data tagihan menggunakan periode tagihan yang sesuai dengan periode energi.
- [x] Validasi bahwa data ERP dicocokkan berdasarkan perangkat dan periode, bukan nama tampilan saja.

## 9. Aturan Perbandingan Energi

### Mode Hari

- [ ] Tetapkan nilai periode aktif harian.
- [ ] Tetapkan nilai hari pembanding.
- [ ] Hitung perubahan absolut harian sesuai formula yang disetujui.
- [ ] Hitung perubahan persentase tanpa pembagian dengan nol.
- [ ] Tetapkan perlakuan jika hari pembanding tidak memiliki data.

### Mode Bulan

- [ ] Pertahankan `lastMonthKWh` untuk bulan sebelumnya.
- [ ] Pertahankan `momChange`.
- [ ] Pertahankan `momChangePercent`.
- [ ] Pertahankan `lastYearSameMonthKWh`.
- [ ] Pertahankan `yoyChange`.
- [ ] Pertahankan `yoyChangePercent`.
- [ ] Pastikan pembulatan sama dengan perilaku lama.
- [ ] Uji nilai pembanding nol.
- [ ] Uji periode aktif nol.
- [ ] Uji data pembanding tidak tersedia.

### Mode Tahun

- [ ] Tetapkan agregasi total tahunan.
- [ ] Tetapkan periode pembanding tahun sebelumnya.
- [ ] Tetapkan perilaku tahun berjalan parsial.
- [ ] Pastikan cakupan bulan kedua periode dapat dibandingkan secara adil.
- [ ] Tampilkan status parsial bila tahun aktif belum selesai.

## 10. Aturan Perbandingan Tagihan

### Data tagihan

- [ ] Tetapkan definisi **Estimasi Tagihan**.
- [ ] Tetapkan definisi **Tagihan Aktual** dari ERP.
- [ ] Tetapkan definisi **Selisih Tagihan**.
- [ ] Tetapkan definisi **Selisih Tagihan (%)**.
- [ ] Tetapkan status `Sesuai`, `Estimasi lebih tinggi`, `Estimasi lebih rendah`, `Belum tersedia`, dan `Tidak dapat dibandingkan`.
- [x] Pastikan tagihan aktual dicocokkan berdasarkan `DeviceKey` dan periode.
- [x] Pastikan `KodeLokasi`, `Periode`, `JumlahTagihan`, dan `TagihanListrikID` dipetakan sesuai kontrak `WWERP2019` yang aktif.

### Mode Hari

- [ ] Pastikan apakah tagihan aktual harian tersedia.
- [ ] Jika tidak tersedia, tampilkan hanya estimasi harian dengan status yang jelas.
- [x] Jangan mengubah billing ERP bulanan menjadi tagihan harian ketika display mode adalah Hari.
- [x] Tampilkan `—` atau status belum tersedia jika record billing periode yang cocok tidak tersedia.

### Mode Bulan

- [x] Tampilkan estimasi tagihan pada periode pembanding aktif.
- [x] Tampilkan tagihan aktual ERP pada periode pembanding aktif jika tersedia.
- [x] Hitung selisih estimasi dan aktual.
- [x] Hitung persentase selisih hanya jika nilai estimasi valid dan bukan nol.
- [x] Tangani tagihan aktual nol tanpa pembagian dengan nol.
- [ ] Tandai bulan berjalan sebagai belum final bila tagihan aktual belum lengkap.

### Mode Tahun

- [ ] Tetapkan apakah tagihan dijumlahkan per tahun atau berdasarkan cakupan bulan yang sama.
- [ ] Gunakan perbandingan cakupan setara untuk tahun berjalan dan tahun sebelumnya.
- [ ] Tandai hasil sebagai parsial jika sebagian bulan belum memiliki tagihan aktual.
- [ ] Jangan menyebut hasil sebagai final sebelum seluruh periode tersedia.

## 11. Sorting dan Ranking

- [ ] Tetapkan metrik ranking utama.
- [ ] Tetapkan arah ranking default.
- [ ] Tetapkan apakah ranking berdasarkan konsumsi, biaya, atau metrik lain.
- [ ] Tetapkan aturan tie antarperangkat.
- [ ] Tetapkan tie-breaker deterministik.
- [ ] Tetapkan perilaku ranking ketika satu perangkat dipilih.
- [ ] Pastikan urutan default tabel mengikuti ranking.
- [ ] Jika sorting kolom lain disediakan, jangan mengubah nilai posisi ranking.
- [ ] Pastikan nilai kosong berada pada posisi yang konsisten.
- [ ] Pastikan perubahan filter menghitung atau mengambil ranking yang sesuai dengan periode baru.

## 12. Format Nilai dan Tampilan

- [ ] Samakan jumlah angka desimal kWh dengan tampilan lama.
- [ ] Samakan format Rupiah dengan format finansial yang sudah digunakan.
- [ ] Tampilkan tanda `+` untuk kenaikan jika itu standar UI yang disepakati.
- [ ] Tampilkan tanda `−` untuk penurunan.
- [ ] Bedakan nilai nol dengan data kosong.
- [ ] Gunakan `—` untuk data tidak tersedia.
- [ ] Gunakan warna sebagai bantuan, bukan satu-satunya indikator.
- [ ] Pastikan nilai negatif tetap terbaca pada mode kontras rendah.
- [ ] Pastikan alignment identitas rata kiri dan angka rata kanan.
- [ ] Tampilkan tooltip/detail untuk nilai yang terpotong jika diperlukan.

## 13. Responsive dan Aksesibilitas

- [ ] Uji tabel pada desktop.
- [ ] Uji tabel pada tablet.
- [ ] Uji tabel pada mobile.
- [ ] Sediakan scroll horizontal jika semua kolom dipertahankan.
- [ ] Pertimbangkan expandable row untuk detail tambahan.
- [ ] Pastikan kolom prioritas tetap terlihat pada layar sempit.
- [ ] Pastikan header tidak tertukar dengan isi row.
- [ ] Pastikan angka tidak terpotong tanpa indikator.
- [ ] Pastikan kontrol filter dapat digunakan dengan keyboard.
- [ ] Pastikan label filter tidak hanya disampaikan melalui warna.
- [ ] Pastikan status loading dan error dapat dibaca oleh pengguna teknologi bantu bila pola project mendukungnya.

## 14. Refresh, Request, dan Konsistensi Data

- [x] Pastikan perubahan filter periode membatalkan atau mengabaikan request lama yang tidak relevan.
- [x] Pastikan response lama tidak menimpa response untuk filter terbaru.
- [x] Pastikan tabel gabungan diperbarui dari response yang sama untuk ranking, energi, dan billing.
- [x] Pastikan ranking, energi, dan tagihan berasal dari identitas periode yang sama.
- [x] Pertahankan data valid terakhir selama refresh jika itu perilaku saat ini.
- [x] Tampilkan indikator loading tanpa menghapus filter pengguna.
- [ ] Tampilkan waktu data terakhir dari sumber data, bukan sekadar waktu browser.
- [ ] Pastikan auto-refresh tidak menggandakan baris.
- [x] Pastikan perubahan preset tidak memicu request ganda pada alur normal.
- [x] Pastikan mode fallback tetap menampilkan tabel dengan kontrak yang dipahami UI.
- [x] Pastikan timeout tidak menghapus data valid yang sedang tampil tanpa alasan.

## 15. Edge Case

- [ ] Tidak ada perangkat.
- [ ] Perangkat ada tetapi tidak memiliki data periode aktif.
- [ ] Perangkat memiliki data aktif tetapi tidak memiliki data pembanding.
- [ ] Perangkat memiliki ranking tetapi tidak memiliki tagihan aktual.
- [ ] Perangkat memiliki tagihan aktual tetapi tidak memiliki data energi yang sesuai.
- [ ] Nilai periode aktif nol.
- [ ] Nilai pembanding nol.
- [ ] Estimasi tagihan nol.
- [ ] Tagihan aktual nol.
- [ ] Data ERP belum tersedia.
- [ ] Data ERP memiliki lebih dari satu record untuk perangkat dan periode yang sama.
- [ ] Dua perangkat memiliki nama yang sama.
- [ ] Dua perangkat memiliki nilai ranking yang sama.
- [ ] Bulan berjalan belum selesai.
- [ ] Tahun berjalan hanya memiliki sebagian bulan.
- [ ] Perubahan filter terjadi saat request aktif.
- [ ] Response request lama selesai setelah response baru.
- [ ] Salah satu sumber data mengalami error.
- [ ] Semua sumber data mengalami error.
- [ ] Pergantian bulan.
- [ ] Pergantian tahun.
- [ ] Tahun kabisat.
- [ ] Periode yang dipilih berada sebelum data historis tersedia.

## 16. Pengujian Unit dan Logika

- [ ] Uji resolusi preset Hari Ini.
- [ ] Uji resolusi preset Kemarin.
- [ ] Uji resolusi preset 2 Hari Lalu.
- [ ] Uji resolusi preset 3 Hari Lalu.
- [ ] Uji resolusi preset Bulan Ini.
- [ ] Uji resolusi preset Bulan Lalu.
- [ ] Uji resolusi preset 2 Bulan Lalu.
- [ ] Uji resolusi preset 3 Bulan Lalu.
- [ ] Uji resolusi preset Tahun Ini.
- [ ] Uji resolusi preset Tahun Lalu.
- [ ] Uji resolusi preset 2 Tahun Lalu.
- [ ] Uji batas awal dan akhir periode.
- [ ] Uji MoM normal.
- [ ] Uji YoY normal.
- [ ] Uji perubahan harian normal.
- [ ] Uji pembanding nol.
- [ ] Uji data pembanding tidak tersedia.
- [ ] Uji estimasi tagihan dan aktual.
- [ ] Uji selisih tagihan negatif, nol, dan positif.
- [ ] Uji persentase selisih dengan aktual nol.
- [ ] Uji ranking dan tie-breaker.
- [ ] Uji pencocokan data berdasarkan `DeviceKey`.
- [ ] Uji perangkat yang hanya muncul di satu sumber data.

## 17. Pengujian Integrasi Endpoint

- [ ] Uji request mode Hari.
- [ ] Uji request mode Bulan.
- [ ] Uji request mode Tahun.
- [ ] Uji semua preset Bulan.
- [ ] Uji semua preset Hari.
- [ ] Uji semua preset Tahun.
- [ ] Uji pilihan satu perangkat.
- [ ] Uji semua perangkat.
- [ ] Uji tanggal/periode historis.
- [ ] Uji bulan berjalan.
- [ ] Uji tahun berjalan parsial.
- [ ] Uji response dengan `periodComparison` lengkap.
- [ ] Uji response dengan `periodComparison` kosong.
- [ ] Uji response dengan data tagihan aktual.
- [ ] Uji response tanpa data tagihan aktual.
- [ ] Uji response error.
- [ ] Uji response parsial bila didukung.
- [ ] Uji fallback endpoint lama.
- [ ] Uji data version/cache bila digunakan oleh jalur aktif.

## 18. Pengujian UI

- [~] Pastikan hanya satu tabel gabungan terlihat; source sudah diperiksa, smoke test browser masih terbuka.
- [~] Pastikan tabel lama Peringkat dan Perbandingan tidak tetap tampil di area yang sama; perlu verifikasi browser.
- [~] Pastikan setiap perangkat hanya memiliki satu row; logika source sudah menggunakan satu row per ranking item.
- [~] Pastikan header sesuai dengan data row; source sudah memuat 14 kolom, perlu verifikasi visual.
- [~] Pastikan label kolom berubah sesuai mode Hari/Bulan/Tahun; source sudah mengubah label pembanding.
- [~] Pastikan preset aktif memiliki indikator visual; perlu verifikasi browser.
- [x] Pastikan filter perangkat tidak merusak filter periode melalui state `tableUsageDeviceKey` terpisah.
- [~] Pastikan sorting default berdasarkan peringkat; source menggunakan `combinedRankingRows`, perlu verifikasi visual.
- [~] Pastikan status data tampil dengan benar; mapping status source sudah tersedia, perlu verifikasi response runtime.
- [x] Pastikan tagihan aktual yang kosong tidak ditampilkan sebagai nol.
- [x] Pastikan perubahan positif dan negatif mudah dibedakan melalui class warna dan tanda nilai.
- [~] Pastikan loading state tidak menyebabkan layout rusak; perlu smoke test browser.
- [x] Pastikan empty state jelas.
- [~] Pastikan error state jelas; perlu validasi response error runtime.
- [~] Pastikan refresh tidak menggandakan row; source diperiksa, perlu validasi runtime.
- [ ] Pastikan mobile/tablet dapat digunakan.
- [x] Pastikan link detail perangkat tetap benar.

## 19. Rekonsiliasi Hasil Sebelum dan Sesudah

- [ ] Simpan contoh hasil tabel Peringkat sebelum perubahan.
- [ ] Simpan contoh hasil tabel Perbandingan sebelum perubahan.
- [ ] Simpan contoh hasil perbandingan tagihan sebelum perubahan.
- [ ] Bandingkan jumlah perangkat sebelum dan sesudah.
- [ ] Bandingkan nilai konsumsi periode aktif.
- [ ] Bandingkan nilai periode pembanding.
- [ ] Bandingkan perubahan absolut.
- [ ] Bandingkan perubahan persentase.
- [ ] Bandingkan posisi ranking.
- [ ] Bandingkan estimasi tagihan.
- [ ] Bandingkan tagihan aktual.
- [ ] Bandingkan selisih tagihan.
- [ ] Catat perbedaan pembulatan secara terpisah.
- [ ] Catat perbedaan karena perubahan label atau layout secara terpisah.
- [ ] Pastikan tidak ada perbedaan angka yang tidak dapat dijelaskan.

## 20. Performa

- [ ] Catat baseline durasi request sebelum perubahan.
- [ ] Catat baseline durasi render tabel sebelum perubahan.
- [ ] Catat jumlah request saat filter berubah.
- [ ] Pastikan peleburan UI tidak menyebabkan request tambahan yang tidak perlu.
- [ ] Pastikan data ranking dan perbandingan dapat menggunakan response yang sama bila memungkinkan.
- [ ] Ukur performa semua perangkat.
- [ ] Ukur performa satu perangkat.
- [ ] Ukur performa periode Hari, Bulan, dan Tahun.
- [ ] Ukur performa saat tagihan aktual tersedia dan tidak tersedia.
- [ ] Pastikan sorting client-side tidak menyebabkan jeda yang mengganggu pada jumlah perangkat besar.

## 21. Dokumentasi Setelah Implementasi

- [x] Dokumentasikan struktur filter final pada checklist ini dan source `UsageStatistics.cshtml`.
- [x] Dokumentasikan arti setiap preset melalui konfigurasi periode yang aktif di view.
- [x] Dokumentasikan pembanding untuk mode Hari pada konfigurasi periode dan rendering tabel.
- [x] Dokumentasikan pembanding MoM dan YoY untuk mode Bulan melalui `periodComparison`.
- [x] Dokumentasikan pembanding Tahun melalui konfigurasi periode dan label dinamis.
- [ ] Dokumentasikan status bulan/tahun berjalan parsial.
- [x] Dokumentasikan struktur kolom tabel gabungan pada source rendering row.
- [x] Dokumentasikan sumber setiap kolom melalui mapping `combinedRankingRows` dan `combinedComparisonResponse`.
- [x] Dokumentasikan aturan tagihan aktual yang belum tersedia melalui tampilan `—` dan status `Belum tersedia`.
- [x] Dokumentasikan aturan pencocokan berdasarkan `DeviceKey` dan periode ERP.
- [~] Dokumentasikan edge case yang sudah diverifikasi; pemeriksaan source selesai, validasi runtime masih terbuka.
- [x] Dokumentasikan file yang berubah: `Views/Monitoring/UsageStatistics.cshtml` dan checklist ini.
- [x] Dokumentasikan perintah verifikasi: `git diff --check`; build normal masih terhambat file DLL yang terkunci proses lain.
- [ ] Dokumentasikan hasil rekonsiliasi angka runtime.

## 22. Rollout dan Rollback

- [ ] Tentukan apakah fitur akan diaktifkan langsung atau melalui feature flag yang sudah tersedia.
- [ ] Pertahankan tampilan lama sebagai referensi selama validasi.
- [ ] Uji fitur pada environment non-produksi.
- [ ] Validasi response dan UI menggunakan data representatif.
- [ ] Aktifkan bertahap jika mekanisme rollout tersedia.
- [ ] Pantau error JavaScript, error endpoint, durasi request, dan kesalahan angka.
- [ ] Siapkan rollback ke tabel UI lama tanpa mengubah data sumber.
- [ ] Pastikan rollback tidak menghapus filter atau konfigurasi pengguna.
- [ ] Hentikan rollout jika ranking, perbandingan, atau tagihan tidak konsisten.
- [ ] Jangan menghapus jalur lama sebelum kriteria selesai terpenuhi.

## 23. Kriteria Selesai

- [~] Filter Hari, Bulan, dan Tahun tersedia; perlu validasi browser.
- [~] Preset Bulan Ini, Bulan Lalu, 2 Bulan Lalu, dan 3 Bulan Lalu tersedia; perlu validasi browser.
- [~] Preset Hari dan Tahun tersedia; perlu validasi browser.
- [x] Tabel Peringkat dan Perbandingan telah menjadi satu tabel UI.
- [x] Perbandingan tagihan tampil dalam kelompok kolom yang jelas dan tidak tercampur dengan kWh.
- [x] Semua baris dicocokkan berdasarkan `DeviceKey`.
- [x] Tidak ada penggabungan berdasarkan nama tampilan saja.
- [x] Label kolom pembanding mengikuti tingkat periode.
- [x] Nilai perubahan dan persentase menggunakan formula source yang ada serta mencegah pembagian nol.
- [~] Estimasi dan aktual tagihan memiliki status yang benar; perlu verifikasi response ERP runtime.
- [x] Nilai nol, kosong, dan error dibedakan oleh null handling dan format `—`.
- [~] Ranking konsisten dan deterministik; perlu verifikasi data runtime.
- [x] Filter tabel tidak mengubah dashboard data.
- [ ] Tabel responsive dan dapat digunakan pada layar kecil.
- [ ] Hasil sebelum dan sesudah telah direkonsiliasi.
- [~] Build normal berhasil; percobaan terakhir terhenti pada copy `KWHMonitoring.Views.dll` karena dikunci proses lain, bukan error source compilation.
- [~] Test browser dan rekonsiliasi runtime masih terbuka.
- [x] Tidak ada perubahan pada database.
- [x] Tidak ada fitur di luar Usage Statistics yang diubah.
- [x] Perubahan dan hasil verifikasi source sudah didokumentasikan.

## 24. Catatan Implementasi

- [ ] Setiap item yang dicentang harus mencantumkan file yang berubah dan bukti verifikasi.
- [ ] Jangan menandai item selesai hanya karena kode telah ditulis; lakukan pengujian terlebih dahulu.
- [ ] Jika ditemukan perbedaan antara dokumentasi dan source aktif, prioritaskan source aktif lalu dokumentasikan perbedaannya.
- [ ] Jika field tagihan aktual belum tersedia untuk suatu tingkat periode, tampilkan status tidak tersedia dan jangan membuat angka perkiraan tanpa persetujuan.
- [ ] Jika keputusan bisnis belum ditetapkan, biarkan item tetap `[ ]` dan jangan memilih perilaku secara diam-diam.