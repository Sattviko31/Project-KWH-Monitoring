# TODO Implementasi Usage Statistics Real-time

Checklist ini mengikuti [rencana perbaikan](RENCANA_PERBAIKAN_USAGE_STATISTICS_REALTIME.md). Item dikerjakan berurutan; jangan menghapus jalur lama sampai kesetaraan hasil dan keselamatan perubahan sudah dipastikan.

## Tahap 0 — Baseline dan ruang lingkup

- [x] Tinjau alur filter, refresh, endpoint, dan aturan perhitungan yang ada.
- [x] Catat durasi request batch di log aplikasi tanpa mencatat kunci perangkat.
- [ ] Lengkapi baseline runtime UI: p50/p95 siklus lengkap, query count/duration, timeout, dan umur data yang benar-benar tampil.
- [x] Ambil sampel awal endpoint energi lama serta snapshot freshness tabel agregat dari runtime lokal.
- [x] Ambil snapshot jumlah perangkat/baris tabel agregat pada database terkonfigurasi.
- [x] Daftarkan kontrak hasil yang harus dibandingkan untuk tanggal hari ini/historis, semua perangkat/perangkat tunggal, tarif/WBP, metrik finansial, data kosong, dan ranking.

## Tahap 1 — Konsistensi filter dan siklus request UI

- [x] Jadikan filter tanggal + perangkat terbaru sebagai pemicu request; perubahan filter tetap diproses meski request sebelumnya aktif.
- [x] Batalkan request lama yang tidak relevan dan lindungi tampilan dari hasil lama yang selesai belakangan.
- [x] Coalesce event refresh realtime selama siklus data dasar dan finansial aktif.
- [x] Pertahankan tampilan data terakhir selama refresh dan tampilkan status/waktu ketika memuat atau request data dasar gagal.
- [x] Pencarian teks chip tidak memicu query statistik; filter kategori dan pilihan perangkat tetap mengikuti perilaku sebelumnya.

## Tahap 2 — Jalur batch server

- [x] Rancang response contract batch dengan versi/waktu agregasi dan status data agregat per perangkat.
- [x] Implementasikan endpoint batch dengan query kelompok per periode/perangkat serta filter perangkat opsional.
- [x] Pertahankan tarif dan hitung WBP/LWBP, pemborosan, anggaran, pembanding, proyeksi, anomali, load factor, dan unit economics per perangkat sebelum agregasi lintas perangkat.
- [x] Isolasi exception pada kalkulasi finansial per perangkat; hasil perangkat lain tetap dikembalikan dan respons menandai baris perangkat yang gagal.
- [ ] Pulihkan kegagalan query database berkelompok secara parsial. Query bersama yang gagal masih menggagalkan seluruh batch; fallback endpoint lama berusaha memulihkan grafik dan finansial, tetapi belum mengisolasi sumber data/tabel mana yang gagal.
- [x] Pertahankan endpoint lama sebagai fallback selama perbandingan dan pulihkan grafik serta finansial per perangkat saat batch mengembalikan error non-timeout/response tidak valid (maksimum enam request fallback bersamaan; hasil parsial ditampilkan).
- [x] Alihkan mode semua perangkat ke batch dan kirim `deviceKey` untuk filter satu perangkat; endpoint lama tetap menjadi fallback dan kill switch.

## Tahap 3 — Query dan kesegaran data

- [x] Gunakan tabel agregat yang ada untuk data per jam/hari/bulan/tahun dengan batas periode yang sama.
- [x] Ambil waktu pembaruan agregat dan versi sumber data; jam berjalan membaca agregat jam terbaru yang sudah di-flush listener.
- [x] Periksa indeks fisik dan estimated execution plan query batch pada database terkonfigurasi sebelum mengubah indeks.
- [ ] Validasi calon indeks pada volume representatif dan ukur dampak tulis sebelum menambahkannya; tabel aktif saat pemeriksaan masih kecil.
- [x] Ambil pengaturan/tarif dan top anomali secara berkelompok; hitung metrik finansial memakai pengaturan masing-masing perangkat.

## Tahap 4 — Cache dan update realtime

- [x] Tambahkan cache sangat pendek (1 detik) per tanggal/cakupan, hanya jika token `/data-version` tersedia.
- [x] Ikat cache ke versi energi, versi anomali, dan timestamp setiap pengaturan perangkat/tarif agar perubahan satu perangkat membentuk cache key baru.
- [x] Coalesce perubahan realtime dan pastikan refresh terbaru yang datang selama request aktif dijalankan setelah request selesai.
- [x] Tampilkan waktu agregasi terbaru dan status saat pembaruan gagal atau perangkat belum memiliki agregat.

## Tahap 5 — Pembuktian dan rollout

- [ ] Bandingkan hasil lama dan batch untuk seluruh kasus kontrak; toleransi pembulatan harus sama.
- [ ] Uji filter cepat, response out-of-order, kegagalan parsial, perangkat offline, dan update realtime saat request aktif.
- [ ] Ukur kriteria p95, timeout, query/load database, serta umur data terhadap baseline.
- [x] Sediakan sakelar rollback konfigurasi `UsageStatistics:UseBatchEndpoint=false`; nilai default tetap mengaktifkan batch, dan mode lama memakai endpoint grafik + finansial perangkat dengan batas enam request bersamaan.
- [ ] Aktifkan bertahap dan pertahankan rollback ke endpoint lama sampai stabil.
- [ ] Hapus jalur lama hanya setelah semua kriteria keberhasilan terpenuhi.

## Catatan pelaksanaan

- Tahap 1–4 memiliki implementasi awal pada `Views/Monitoring/UsageStatistics.cshtml`, `Controllers/ApiController.cs`, dan dokumentasi halaman Usage Statistics. Mode Semua Panel dan filter satu perangkat menggunakan endpoint batch (satu `deviceKey` untuk filter tunggal); endpoint lama dipertahankan untuk rollback/pemulihan. Filter tunggal memetakan hasil finansial per perangkat langsung, tidak melewati agregator Semua Panel, untuk menjaga pembulatan lama.
- Build .NET berhasil setelah kompilasi ke output sementara. Endpoint lama telah diukur read-only pada runtime, tetapi belum ada pembandingan kesetaraan angka atau pengukuran endpoint batch dari build workspace saat ini.
- Snapshot runtime 2026-10-02: 22 DeviceSettings, 10 panel pada endpoint `/panels`, 11 device key di tabel energi. `HourlyEnergy` memiliki 3.555 baris, `DailyEnergy` 158, `MonthlyEnergy` 21, dan `AnomalyLogs` 153. Timestamp terbaru saat sampel: HourlyEnergy berumur 3 detik; DailyEnergy dan MonthlyEnergy masing-masing sekitar 24.173 detik. Umur tabel berbeda karena tabel harian/bulanan merepresentasikan agregat periode yang tidak berubah sesering agregat jam; freshness UI seharusnya dinilai dari sumber yang digunakan untuk metrik aktif. Indeks unik `(DeviceKey, Hour/Date/Year,Month)` pada tabel agregat ditemukan. `AnomalyLogs` memiliki indeks terpisah `DeviceKey` dan `DetectedTime`, bukan indeks komposit.
- Baseline awal endpoint energi lama, 10 request serial pada tanggal yang sama: sukses 10/10; p50 194 ms, p95 832.2 ms, mean 269.2 ms, rentang 72.1–832.2 ms. Sampel kecil ini hanya endpoint energi, bukan siklus UI lengkap atau batch baru. Tidak ada bukti timeout pada sampel ini.
- Baseline awal endpoint finansial lama, satu request serial untuk masing-masing dari 10 panel: sukses 10/10; p50 154.2 ms, p95 420.8 ms, maksimum 420.8 ms, total serial 2.044 s. Siklus lama memerlukan 1 request statistik energi + 10 request finansial per refresh (di luar request daftar panel); sampel ini serial, bukan simulasi fan-out browser konkuren. Tidak ada timeout pada sampel ini.
- Proses web lokal dimulai sebelum build workspace saat ini. Request ke path `/usage-statistics/batch` pada proses tersebut menghasilkan kontrak endpoint dinamis lama (`deviceKey=batch`, tanpa `summary`), sehingga hasil itu tidak dapat dipakai untuk mengukur handler batch terbaru; UI memicu fallback karena response contract tidak valid.
- Baseline p50/p95 siklus UI lengkap, query count/duration, audit kesetaraan semua rumus dan kasus batas, validasi indeks pada volume besar/dampak tulis, dan rollout masih tertunda.
- Kontrak pembanding yang sudah didaftarkan: hari berjalan dan tanggal historis; semua panel dan satu panel; tarif berbeda/default serta konfigurasi WBP/LWBP; energi dan biaya hari/bulan/tahun; pembanding/proyeksi/load factor/unit economics; top-20 anomali dan ranking; perangkat tanpa agregat/offline; perubahan filter dan versi realtime. Hasil numerik belum dinyatakan setara sampai dibandingkan memakai data runtime.
- Batas toleransi kegagalan yang teridentifikasi: exception kalkulasi per perangkat kini dikembalikan sebagai status `calculation-error` tanpa menggugurkan perangkat lain. Exception saat pembacaan kelompok (termasuk tabel anomali) belum dapat dipulihkan secara parsial; jangan mengklaim seluruh kriteria partial failure sebelum jalur itu ditangani.
- Estimated plan query batch tanggal/periode pada ukuran itu memilih clustered index scan pada HourlyEnergy, DailyEnergy, MonthlyEnergy, dan AnomalyLogs; query hourly/daily juga melakukan sort sebelum agregasi. Estimasi biaya SQL rendah pada ukuran saat ini, sehingga ini bukti bentuk plan saat ini, bukan urgensi indeks untuk deployment sekarang. Tambah indeks hanya jika manfaat di volume representatif terbukti melebihi dampak tulis dan storage.
- Kandidat untuk benchmark volume besar dicatat di rencana: index covering berawalan `Hour`, `Date`, serta `(Year, Month)` untuk filter tabel agregat; filtered index OVERLOAD berawalan `DetectedTime` untuk query anomali. Belum dibuat/applied karena tabel saat ini kecil dan dampak write belum terukur.
- Cache settings menggunakan daftar timestamp efektif per perangkat yang diurutkan; tidak lagi bergantung pada timestamp maksimum global yang dapat melewatkan perubahan perangkat lain.
- Log endpoint batch kini mencatat durasi total serta durasi pembacaan settings, hourly, daily, monthly, dan anomaly, beserta jumlah group/row. Cache hit dan exception juga mencatat durasi; log tidak merekam device key. Angka ini dapat dipakai menganalisis query dan p50/p95 setelah build baru dijalankan.
- Endpoint agregat lama kini mengembalikan `latestAggregateAt`/`dataVersion` dari `CalculatedAt`; UI mode rollback memakai timestamp sumber ini dan tidak memberi kesan waktu browser sebagai waktu data.
- Fallback batch error non-timeout/response tidak valid mempertahankan metrik finansial lama dengan batas enam request aktif dan ikut dibatalkan saat identitas filter berubah. Timeout batch masih mempertahankan data lama yang sedang tampil dan belum memulai fan-out pemulihan. Dokumentasi endpoint dibersihkan dari dua rute `device-financial` yang tidak ditemukan di controller.
- Batch memiliki kill switch runtime melalui konfigurasi `UsageStatistics:UseBatchEndpoint`; setel `false` per environment dan restart aplikasi untuk kembali ke endpoint lama. Sakelar tersedia, tetapi rollout bertahap tetap menunggu kesetaraan dan baseline baru.
- Jangan mengubah fitur selain Usage Statistics.
- Jangan menghapus atau merapikan perubahan lokal yang sudah ada di workspace.
- Setelah tiap tahap, catat file yang berubah, dampak perilaku, dan bukti verifikasi yang dijalankan.
