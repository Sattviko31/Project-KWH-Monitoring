# Rencana Perbaikan Usage Statistics untuk Banyak Perangkat

## Tujuan

Mempercepat halaman Usage Statistics ketika jumlah perangkat bertambah, mempertahankan kesegaran data real-time, dan menjaga perilaku filter yang sudah ada. Implementasi dicatat bertahap di [TODO implementasi](TODO_IMPLEMENTASI_USAGE_STATISTICS_REALTIME.md).

## Persyaratan kesegaran real-time

Data yang ditampilkan harus mencerminkan data terbaru yang sudah diterima dan diagregasi server. Optimasi tidak boleh mengubah halaman menjadi laporan berkala yang tertinggal.

- Respons server menyertakan waktu data/agregasi terakhir dan versi data untuk cakupan filter yang diminta.
- Cache hanya boleh menjadi tampilan sementara saat request terbaru berjalan. Cache tidak boleh dianggap sebagai hasil terbaru setelah versi data berubah atau batas kesegaran terlampaui.
- Setiap perubahan versi data yang relevan memicu pembaruan untuk tanggal dan perangkat aktif. Data jam berjalan mengikuti frekuensi flush listener yang berlaku; versi baru diproses sesegera mungkin setelah agregasi tersedia.
- Bila request terbaru gagal, pertahankan nilai valid terakhir agar layar tidak kosong, tetapi tandai waktu pembaruannya dan status bahwa data terbaru belum berhasil dimuat. Jangan menyamarkan data lama sebagai data real-time.
- Respons dengan versi lebih lama atau filter yang sudah tidak aktif tidak boleh menimpa tampilan terbaru.
- Refresh harus dikoaleskan agar perubahan yang datang beruntun tidak membuat request bertumpuk. Setelah request aktif selesai, proses versi terbaru yang belum sempat dimuat.
- Target keterlambatan maksimum dari data diterima listener sampai tampil di halaman harus ditetapkan dan dipantau. Target tersebut harus sesuai dengan interval flush nyata; jangan menetapkan TTL cache yang melebihi target ini.

## Perilaku yang wajib dipertahankan

- Filter tanggal tetap mengubah tanggal grafik per jam, grafik harian dalam bulan terpilih, grafik bulanan dalam tahun terpilih, ringkasan energi, puncak, dan biaya.
- Filter perangkat tetap menampilkan nilai perangkat terpilih. Pilihan “Semua Panel” tetap menjumlahkan seluruh perangkat yang masuk cakupan.
- Pilihan tanggal dan perangkat harus dapat dipakai bersamaan. Mengganti salah satu filter tidak boleh mengembalikan filter yang lain ke nilai default.
- Perhitungan tarif per perangkat, WBP/LWBP, anggaran, anomali, proyeksi, dan nilai kosong/tidak terkonfigurasi harus tetap mengikuti rumus yang berlaku sekarang.
- Tombol periode peringkat (hari ini/bulan ini/tahun ini) tetap memiliki cakupan waktunya sendiri dan tidak diam-diam berubah mengikuti filter tanggal grafik.
- Pembaruan real-time tidak boleh menimpa hasil untuk filter terbaru dengan respons lama.

## Permasalahan yang ditemukan

1. Mode semua perangkat memanggil endpoint statistik finansial satu kali per perangkat dari browser. Jumlah request dan beban query tumbuh seiring jumlah perangkat.
2. Semua request perangkat dikirim serentak dengan timeout 15 detik. Endpoint menjalankan beberapa query database berurutan; lonjakan konkurensi dapat membuat sebagian request terlambat atau gagal.
3. Agregasi finansial bersifat all-or-nothing: satu perangkat gagal menyebabkan seluruh hasil finansial dianggap tidak tersedia.
4. Penguncian hanya melindungi request statistik utama, bukan rangkaian request finansial per perangkat. Refresh real-time baru dapat memulai pekerjaan lain sementara fan-out sebelumnya masih berjalan.
5. Daftar panel dan statistik utama dimuat terpisah. Jika daftar perangkat belum tersedia, pemuatan finansial menunggu dan mencoba lagi; kegagalan daftar kurang terlihat.
6. Statistik dasar menunggu endpoint agregat, lalu analisis finansial baru dimulai. Karena itu bagian halaman dapat terlihat selesai pada waktu yang berbeda.
7. Endpoint daftar panel dan peringkat mencari rekaman terbaru dari tabel telemetry. Pada tabel besar, pencarian ini berpotensi ikut membebani halaman awal.

## Rencana solusi

### Tahap 1 — Respons UI dan konsistensi filter

- Perlakukan tanggal dan kunci perangkat sebagai satu identitas permintaan. Setiap permintaan dan hasil cache harus memakai pasangan `(tanggal, deviceKey atau semua)`.
- Saat filter berubah, perbarui label/tanggal yang dipilih langsung. Pertahankan data terakhir yang valid sambil menandainya sedang diperbarui; jangan kosongkan seluruh grafik dan kartu lebih dulu.
- Batalkan request lama bila memungkinkan dan selalu gunakan nomor urut/generasi permintaan untuk menolak respons yang bukan milik filter terbaru.
- Terapkan debounce singkat hanya pada input yang dapat berubah cepat, seperti pencarian teks perangkat. Klik chip, tombol tanggal, dan pemilihan tanggal tetap terasa langsung.
- Satukan pengendalian refresh real-time dan filter: satu alur pemuatan aktif per kombinasi filter, dengan refresh terbaru dijadwalkan setelah alur aktif selesai. Hindari tumpukan request.
- Jika kombinasi filter yang sama baru saja dimuat, tampilkan hasil cache langsung lalu perbarui di latar belakang sesuai aturan kesegaran data.

### Tahap 2 — Endpoint batch sebagai jalur utama

- Tambahkan endpoint batch yang menerima tanggal mulai/akhir serta kunci perangkat opsional. Tanpa kunci, endpoint menghasilkan data seluruh perangkat; dengan kunci, endpoint hanya mengembalikan perangkat tersebut.
- Lakukan agregasi di server dengan query yang dikelompokkan per perangkat dan periode, bukan satu rangkaian query finansial per perangkat dari browser.
- Pertahankan response contract atau buat pemetaan eksplisit di sisi browser agar nama properti dan satuan yang dipakai grafik/kartu tidak berubah.
- Hitung jumlah energi, biaya, dan metrik finansial per perangkat sebelum menjumlahkannya. Ini penting karena tarif dan pengaturan WBP/LWBP dapat berbeda untuk tiap perangkat.
- Untuk permintaan satu perangkat, gunakan filter perangkat pada query batch agar tidak memproses semua perangkat.
- Satu kegagalan data perangkat tidak boleh menggagalkan seluruh batch. Kembalikan hasil valid beserta daftar/status perangkat yang gagal atau belum memiliki data.

### Tahap 3 — Query dan sumber data real-time

- Jadikan tabel agregat per jam/hari/bulan/tahun sebagai sumber grafik dan total historis, dengan cakupan tanggal dan batas waktu yang sama seperti sekarang.
- Untuk jam/hari berjalan, gabungkan agregat terakhir dari listener sesuai semantik saat ini; jangan memakai cache yang membuat pembacaan real-time basi tanpa batas.
- Periksa execution plan dan indeks untuk filter perangkat/tanggal pada tabel agregat serta pencarian rekaman telemetry terbaru. Jangan menambah indeks sebelum memeriksa indeks yang sudah ada dan dampaknya pada proses tulis.
- Jika volume representatif menunjukkan clustered scan menjadi mahal, uji indeks covering dengan leading key waktu: `HourlyEnergy(Hour) INCLUDE (DeviceKey, EnergyKWh, CalculatedAt)`, `DailyEnergy(Date) INCLUDE (DeviceKey, EnergyKWh, CalculatedAt)`, dan `MonthlyEnergy(Year, Month) INCLUDE (DeviceKey, EnergyKWh, CalculatedAt)`. Untuk anomali, uji filtered index khusus `AnomalyType='OVERLOAD'` dengan `DetectedTime` sebagai key awal dan kolom ranking/proyeksi sebagai key/include sesuai actual plan. Jangan menerapkan kandidat ini hanya berdasarkan estimated plan pada tabel kecil.
- Pindahkan pembacaan pengaturan/tarif bersama ke satu pembacaan batch per request, bukan pembacaan berulang per perangkat.
- Kelompokkan pembacaan data anomali per periode/perangkat, lalu hitung dampak biaya di server dari hasil terbatas yang relevan.

### Tahap 4 — Pembaruan dan cache yang aman

- Cache respons berdasarkan tanggal dan cakupan perangkat, dengan masa berlaku pendek yang sesuai SLA kesegaran real-time. Cache hanya mempercepat tampilan awal dan tidak menggantikan validasi versi terbaru.
- Invalidasi atau perbarui cache ketika versi data agregat berubah; perubahan pengaturan tarif/perangkat juga harus menginvalidasi nilai finansial terkait.
- Jangan cache error sebagai hasil kosong. Kegagalan sementara harus dapat dicoba ulang tanpa menghapus data valid yang sedang terlihat.
- Gunakan versi/timestamp agregasi untuk mengetahui apakah ada data baru. Jika versi berubah, kirim pembaruan segera; jika belum berubah, hindari payload besar berulang. Push (misalnya SignalR) dapat dipertimbangkan kemudian jika pengukuran menunjukkan polling tetap membebani sistem, dengan syarat tidak memperlambat tampilan versi terbaru.

## Cara UI tetap responsif tanpa hasil salah

1. Pengguna memilih tanggal/perangkat; label dan state filter langsung berubah.
2. UI segera menampilkan hasil cache untuk kombinasi filter itu jika tersedia, disertai indikator pembaruan ringan dan waktu data terakhir.
3. Browser meminta satu batch untuk kombinasi filter terbaru dan versi data terbaru. Request lama dibatalkan atau hasilnya diabaikan.
4. Hasil batch yang sukses mengganti tampilan secara atomik hanya jika filter dan versinya masih terbaru. Perangkat gagal ditandai parsial tanpa menghapus hasil perangkat lain.
5. Peristiwa real-time yang datang selama request aktif dikoaleskan; setelah request selesai, versi paling baru yang belum tampil langsung diminta.

Dengan pola ini, respons interaksi tidak menunggu seluruh perangkat untuk memberikan umpan balik visual, sementara angka yang ditampilkan tetap terkait dengan filter yang dipilih.

## Tahapan implementasi dan pengamanan

1. Catat baseline waktu respons, jumlah perangkat, jumlah query, timeout, dan freshness sebelum perubahan.
2. Implementasikan endpoint batch di balik jalur baru atau feature flag; biarkan endpoint lama tersedia selama perbandingan.
3. Bandingkan hasil lama dan batch untuk tanggal historis, hari berjalan, semua perangkat, satu perangkat, tarif berbeda, WBP/LWBP, data kosong, dan anomali.
4. Uji pergantian filter cepat, respons datang tidak berurutan, refresh saat request aktif, dan kegagalan satu perangkat.
5. Aktifkan bertahap dan pantau latensi, error per perangkat, jumlah query, penggunaan koneksi database, dan keterlambatan data.
6. Hentikan jalur lama hanya setelah kesetaraan hasil dan kestabilan batch terbukti.

Sakelar rollback tersedia melalui konfigurasi `UsageStatistics:UseBatchEndpoint=false` (atau environment variable `UsageStatistics__UseBatchEndpoint=false`). Nilai default mengaktifkan batch; perubahan environment diterapkan setelah aplikasi dimulai ulang. Ini menyediakan rollback operasional, tetapi bukan pengganti aktivasi bertahap dan pembuktian hasil.

## Kriteria keberhasilan

- Jumlah request finansial browser tidak bertambah mengikuti jumlah perangkat; mode semua perangkat memakai satu request batch per pembaruan.
- Filter tanggal dan perangkat tunggal menghasilkan metrik yang sama dengan perhitungan saat ini.
- Kegagalan satu perangkat tidak menghilangkan data perangkat lain.
- Respons lama tidak dapat menimpa hasil filter terbaru.
- Refresh real-time tidak membuat rangkaian request finansial bertumpuk.
- UI menampilkan waktu/versi data terakhir dan menandai secara jelas ketika refresh terbaru gagal.
- Setiap versi agregasi baru untuk filter aktif akhirnya tampil; tidak ada cache yang menahan versi terbaru melewati batas kesegaran.
- Latensi p95 dan tingkat timeout membaik terhadap baseline, sementara usia data tetap dalam target real-time yang disepakati.

## Batasan dan keputusan sebelum implementasi

- Tetapkan target kesegaran data yang eksplisit: interval flush listener, batas waktu agregasi, dan batas maksimum dari data diterima hingga tampil di UI. Pantau keterlambatan pada tiap tahap.
- Definisikan bagaimana UI menampilkan perangkat yang offline, belum memiliki agregat, atau gagal dihitung agar tidak disamakan dengan konsumsi nol.
- Pastikan pembulatan dan aturan periode historis/tahun kabisat identik dengan sistem sekarang sebelum mengganti endpoint.
- Jalur filter/request lifecycle, endpoint batch, timestamp sumber, dan cache 1 detik berbasis versi telah diimplementasikan. Kesetaraan angka/runtime, baseline performa, pemeriksaan indeks, dan rollout masih perlu diselesaikan sesuai checklist.
