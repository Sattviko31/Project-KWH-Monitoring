# Audit Mekanisme Deteksi Anomali KWHMonitoring

**Tanggal audit:** 1 Oktober 2026  
**Ruang lingkup:** kode aplikasi pada subfolder KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring. Catatan ini menggambarkan implementasi yang ditelusuri; aplikasi dan database produksi tidak dijalankan/diverifikasi.

> **Pembaruan implementasi:** bagian “Tindak lanjut yang sudah diterapkan” merangkum perubahan setelah audit. Risiko yang belum tertutup dicatat terpisah.

## Ringkasan

Deteksi operasional terutama dijalankan oleh JavaScript pada Views/Shared/_AnomalyService.cshtml, yang dimuat dari _Layout.cshtml. Browser berkala mengambil daftar panel dan sampel grafik, menghitung EMA serta batas atas/bawah, lalu menunggu sejumlah sampel valid berturut-turut sebelum mengirim temuan ke POST /api/Api/log-anomaly. Server menyimpan temuan di AnomalyLogs, mengisi analisis tekstual/severity, dapat menyimpan snapshot grafik, mengirim notifikasi, dan menandai alert aktif untuk deduplikasi.

Dengan demikian, deteksi bukan worker server mandiri: ia bergantung pada halaman/layout yang aktif, browser yang berjalan, akses API, data chart, pengaturan per device, serta state konfirmasi yang dipersistenkan. AnomalyNotificationBackgroundService menangani laporan/notifikasi terjadwal; ia bukan loop deteksi sampel.

## Komponen

| Komponen | Peran |
|---|---|
| Views/Shared/_Layout.cshtml | Memuat _AnomalyService pada layout umum. |
| Views/Shared/_AnomalyService.cshtml | Loop client-side, sampling, EMA, threshold, downtime, konfirmasi, cooldown, pengiriman log/snapshot. |
| Controllers/ApiController.cs | Endpoint pengaturan, data chart, pencatatan, deduplikasi, downtime, state, analitik dan snapshot. |
| Services/AnomalyAnalysisService.cs | Menentukan severity, dugaan root cause, tindakan yang disarankan, dan pola berulang setelah log dibuat. |
| Models/AnomalyLog.cs, Models/AnomalyChartSnapshot.cs | Model persistensi temuan dan snapshot sebelum/sesudah. |
| Services/NotificationService.cs | Mengirim notifikasi realtime/downtime setelah log dibuat. |
| Services/AnomalyNotificationBackgroundService.cs | Menjalankan laporan berkala; bukan mesin deteksi telemetry. |
| wwwroot/js/realtime-refresh.js | Memicu refresh konsumen UI; bukan algoritme pendeteksi. |

## Alur deteksi

1. Partial mengambil pengaturan EMA, interval/anomaly, pengaturan sistem, status downtime kategori, daftar device, serta state konfirmasi/cooldown yang disimpan server.
2. Setelah dependensi awal selesai, pemeriksaan pertama dijadwalkan sekitar 2 detik kemudian. Berikutnya memakai Anomaly.CheckInterval (default 30 detik). Pengaturan dimuat ulang setiap 60 detik.
3. GET /api/Api/panels memberikan daftar device. Untuk setiap deviceKey, proses meminta /api/Api/panels/{deviceKey}/chart?points=N, dengan N = max(chartDataPoints, 50).
4. Sampel dilewati bila seri daya kosong, sampel terakhir invalid (powerValid[last] !== true), timestamp hilang, atau timestamp sama dengan sampel terakhir yang diproses pada browser ini. Flag in-flight mencegah request bersamaan per device.
5. Nilai EMA dihitung dari seri chart. Jika useInitial100ForEma aktif, baseline diambil dari get-initial-ema/{deviceKey}: rata-rata daya 100 baris tertua (atau semua baris bila kurang dari 100). Client memakai baseline konstan untuk semua titik, bukan EMA bergerak. Jika baseline gagal didapat, dipakai EMA biasa.
6. Ambang dihitung hanya bila sedikitnya satu emaUpperThreshold/emaLowerThreshold per device positif. Angka upper/lower berasal dari window.getDeviceSettings(deviceKey); mode dan nilai persentase/multiplier global dari pengaturan EMA. Jika keduanya nol/tidak tersedia, device dilewati.
7. Saat kondisi terdeteksi, penghitung per deviceKey + anomalyType dinaikkan. Sampel normal mereset counter device; perpindahan tipe membersihkan counter lawan. Ketika batas konfirmasi terpenuhi (default 3), client mengirim payload anomaly dan snapshot awal.
8. Bila server menerima log, client mulai mengumpulkan titik setelah kejadian lalu mengirimkannya bertahap melalui chart-snapshot. Buffer dibatasi 50 titik.
9. Setelah log berhasil, state cooldown client menunggu kondisi normal sebelum anomaly yang sama dicoba lagi. Server juga memiliki key alert aktif per device untuk menekan log bertipe sama.

## Rumus dan klasifikasi

### EMA

Mode biasa memakai:

- k = 2 / (period + 1)
- EMA[0] = power[0]
- EMA[i] = power[i] * k + EMA[i-1] * (1-k)

Nilai null di tengah seri diganti EMA sebelumnya. Periode default 20. Mode baseline awal berbeda: server menghitung SMA dari 100 data paling awal berdasarkan waktu naik, lalu client memakai nilai tetap itu untuk setiap titik chart. Ini bukan EMA yang bergerak.

### Ambang

Mode manual:

- upper = EMA * (1 + upperPercent / 100)
- lower = EMA * (1 - lowerPercent / 100)

Mode fibonacci:

- upper = EMA * fibUpper
- lower = EMA * fibLower

Implementasi mengalikan nilai langsung; konfigurasi harus memastikan makna multiplier (fraksi atau faktor) konsisten. Batas untuk chart dan deteksi dihitung dengan fungsi yang sama.

### Aturan deteksi

| Kondisi | Aturan | Threshold/deviation yang dicatat |
|---|---|---|
| Operasi normal, overload | power > upper | threshold = upper; (power - upper) / upper * 100 |
| Operasi normal, drop | power < lower | threshold = lower; (lower - power) / lower * 100 |
| Downtime, overload | power > EMA | threshold = EMA; (power - EMA) / EMA * 100 |
| Downtime, drop | Tidak dideteksi client; jika request masuk, server menekan DROP saat downtime. | — |

Perbandingan ketat berarti nilai tepat sama dengan batas tidak terdeteksi. Pada implementasi awal, status telemetry senyap belum menjadi keluaran client. `DROP` berarti daya terukur di bawah lower threshold, bukan timeout/offline akibat hilangnya sampel; sampel invalid/tanpa timestamp dilewati.

## Konfirmasi, deduplikasi, dan state

- Konfirmasi default 3 sampel, configurable lewat Anomaly.MaxConfirmations. Dengan loop default 30 detik, nominalnya perlu tiga sampel berbeda; waktu aktual tergantung ketersediaan sampel dan browser. maxConfirmations <= 0 menghentikan pemeriksaan.
- Counter/cooldown disimpan sebagai JSON per device dan per tab di AppSettingsRecords; scope tab bertahan di sessionStorage saat navigasi/reload. State yang tidak diperbarui 30 hari dibersihkan.
- Cooldown utama berbasis kondisi, bukan timer. Operasi normal menunggu daya kembali di antara lower dan upper. Downtime menunggu daya <= EMA. State yang direstore dibuang jika loggedAt lebih tua 24 jam.
- Server memiliki AppSettingsRecords key AnomalyAlert.Active.{deviceKey}, berisi tipe dan waktu. Tipe sama yang masih aktif disupresi; setelah 30 menit entri kedaluwarsa otomatis. Tipe berbeda tidak ditekan oleh pemeriksaan tipe sama dan dapat mengganti state aktif saat disimpan.
- Reset server dipanggil client ketika kondisi normal terdeteksi. Server menyimpan state aktif per device, bukan per tipe; client cooldown juga menyimpan satu tipe per device.
- Anomaly.CooldownTime tetap dibaca/dikirim ke client, tetapi jalur aktif memakai cooldown berbasis kondisi. Timer cooldown lama tidak digunakan, sehingga setting ini berpotensi menyesatkan.

## Perbedaan client dan server

Client menghitung kandidat anomaly untuk tampilan. Sebelum menyimpan, server memverifikasi telemetry terbaru, menghitung ulang EMA/threshold/jenis anomaly dari telemetry dan pengaturan tersimpan, serta memakai waktu sampel sebagai referensi downtime. Nilai pengukuran dalam payload client tidak lagi menjadi sumber log. Status tampilan client masih dapat berbeda dari hasil server bila jam atau zona waktu berbeda.

Counter/cooldown kini disimpan per tab dan per device di AppSettings; lastProcessedSampleByDevice dan in-flight tetap berada di memori browser. Penghentian browser mendadak masih dapat menghilangkan counter debounce yang belum tersimpan.

## Payload dan data tersimpan

AnomalyLogs menyimpan device key/id, jenis, watt, threshold, deviation (%), waktu sampel yang diverifikasi, EMA, mode, acknowledgement, status resolve, severity, root cause, rekomendasi, dan notes. Server menyalin DetectedTime dari timestamp telemetry terbaru yang diverifikasi.

Snapshot menyimpan hingga 50 titik sebelum kejadian, upper/lower threshold dan EMA saat log, kemudian hingga 50 titik sesudahnya. Data sesudah dikirim bertahap; status snapshot menjadi complete bila 50 titik terkumpul. Snapshot tidak menentukan anomaly.

## Analisis otomatis setelah log

AnomalyAnalysisService memakai log baru dan riwayat device 24 jam untuk menentukan pola berulang, severity dan teks bantuan:

- Pola berulang bertipe sama pada device dalam 24 jam mendapat critical bila hitungan riwayat mencapai tiga. Riwayat API mencakup log baru yang sudah disimpan.
- OVERLOAD downtime selalu critical.
- OVERLOAD normal: deviation >50 critical, >30 high, >10 medium, selebihnya low.
- DROP normal selalu high. Cabang severity DROP downtime tidak dicapai oleh alur normal karena DROP downtime disupresi.
- Root cause/rekomendasi adalah template berbasis tipe/deviation, bukan diagnosis dari model statistik atau pemeriksaan fisik.
- Untuk OVERLOAD downtime, BuildRootCause menebak jam dari pencarian substring “22” dan “6” di notes. Jam lain dapat tampil sebagai 00:00; angka itu juga dapat muncul di bagian lain teks.
- ImpactAssessment dan HasRepeatedPattern dihasilkan analisis, tetapi model AnomalyLog tidak terlihat mempunyai properti untuk menyimpannya. Perlu periksa detail response bila ingin memastikan perilakunya pada API.

## Notifikasi dan fitur terkait

Setelah insert dasar, server mengirim realtime instant alert atau alert khusus daya saat downtime melalui NotificationService. Kegagalan analisis tidak membatalkan pencatatan dasar.

AnomalyNotificationBackgroundService menangani jadwal ringkasan/laporan. Dashboard, tren, distribusi, ringkasan, dan laporan bulanan membaca AnomalyLogs; semuanya bukan pendeteksi telemetry. realtime-refresh.js memperbarui UI saat log berubah.

## API dan akses

| Endpoint | Fungsi | Otorisasi pada action yang diperiksa |
|---|---|---|
| GET /api/Api/get-ema-settings | Baca periode/mode/threshold global EMA dan baseline | Tidak terlihat atribut Authorize |
| GET /api/Api/get-notification-settings | Interval, konfirmasi, cooldown dan notifikasi | Tidak terlihat atribut Authorize |
| GET /api/Api/panels dan /api/Api/panels/{key}/chart | Daftar panel dan seri sampel | Verifikasi action masing-masing |
| GET /api/Api/get-initial-ema/{key} | Hitung baseline SMA data awal | Tidak terlihat atribut Authorize |
| GET /api/Api/get-anomaly-state | Baca state konfirmasi/cooldown bersama | Tidak terlihat atribut Authorize |
| POST /api/Api/save-anomaly-state | Simpan state konfirmasi/cooldown | RequireOperator |
| POST /api/Api/log-anomaly | Insert, analisis, snapshot awal, notifikasi, deduplikasi | RequireOperator |
| POST /api/Api/reset-anomaly-alert | Reset alert aktif | RequireOperator |
| POST /api/Api/anomaly-logs/{id}/chart-snapshot | Update snapshot sesudah kejadian | RequireOperator |
| GET /api/Api/anomaly-logs/summary, .../{deviceKey}, detail, trends/distribution/report | Baca log/analitik | Umumnya RequireViewer |

Client sengaja tidak mengirim log-anomaly untuk tamu karena action memerlukan Operator. Perhitungan client tetap dapat berjalan di halaman publik, tetapi tidak menghasilkan log/notifikasi. Endpoint baca/data chart yang dipakai loop perlu ditinjau bersama-sama; tidak semuanya terlihat dibatasi pada action.

## Temuan dan risiko

1. **Ketergantungan browser/layout.** Tidak tampak hosted service deteksi telemetry terdaftar; tanpa halaman yang menjalankan partial, deteksi berhenti. Browser tidur/ditutup, timer throttling, JS gagal, atau API gagal mengurangi sampling.
2. **Threshold per device dapat mematikan deteksi diam-diam.** Jika kedua nilai per-device nol, device dilewati meskipun threshold global tersedia. Pastikan panel baru mendapat konfigurasi.
3. **State dipisah per tab/device.** Scope tab memakai sessionStorage dan state lama dibersihkan setelah 30 hari. Tab yang diduplikasi browser mungkin mewarisi sessionStorage; request serentak dalam scope salinan masih dapat berlomba.
4. **Konfirmasi memeriksa jeda telemetry.** Counter direset setelah jeda lebih dari dua interval (minimum 2 menit), juga pada sampel pertama setelah pemuatan ulang halaman.
5. **Server memvalidasi kandidat.** log-anomaly memakai telemetry terbaru dan menghitung ulang nilai log. Deduplikasi active-alert masih read-then-write; request serentak dari beberapa proses aplikasi dapat berlomba.
6. **Deduplikasi berarti log episode, bukan setiap kejadian.** Selama alert aktif, tipe sama disupresi sampai reset/expiry.
7. **Waktu log berbeda dari waktu pengukuran.** Simpan timestamp sampel terpisah dari waktu pemrosesan untuk kronologi yang akurat.
8. **Zona waktu tampilan dan server.** Server memutuskan downtime pada waktu sampel; status tampilan client tetap memakai jam lokal.
9. **Pembagian nol pada deviation.** Guard client dan server mencegah kandidat saat EMA/threshold pembagi nol.
10. **Severity/root cause heuristik.** DROP normal selalu high; pengulangan membuat critical. Ini aturan tetap, bukan ukuran risiko terkalibrasi atau kausalitas.
11. **CooldownTime konfigurasi tidak berpengaruh sebagai timer.** Setting tetap diekspos meski cooldown aktif menunggu normal.
12. **Client/server punya masa aktif alert berbeda.** Client menunggu normal; server auto-expire 30 menit. State dapat menjadi tidak sinkron.
13. **Snapshot sesudah dapat tidak lengkap.** Browser berhenti atau request gagal sebelum 50 titik, snapshot tetap belum complete.
14. **Estimasi biaya memakai asumsi.** Statistik memakai faktor durasi 0,25 jam di beberapa perhitungan; itu bukan durasi anomali terukur.

## Tindak lanjut yang sudah diterapkan

- Endpoint pencatatan memverifikasi device, timestamp, freshness, kecocokan dengan sampel telemetry terbaru, threshold perangkat, dan tipe anomaly. Nilai log serta waktu deteksi berasal dari telemetry yang diverifikasi.
- Server menghitung ulang EMA/threshold dengan mode, periode, baseline, dan jumlah titik yang sama seperti loop client. Client mengirim waktu sampel, bukan hanya waktu request.
- Pemeriksaan downtime menerima waktu referensi sampel; client menggunakan cache downtime kategori bila downtime per-device tidak aktif.
- Counter/cooldown disimpan per tab dan per device, menghindari satu JSON besar yang terbatas 500 karakter. State yang tidak diperbarui 30 hari dibersihkan; reset data device menghapus state per-device.
- Client mereset counter saat jeda sampel melewati ambang, dan mencegah perhitungan deviation dengan pembagi nol/non-finite.
- Root cause downtime mengambil jam dari format notes terstruktur dengan fallback teks umum.
- Deteksi `DEVICE_OFFLINE` memantau perangkat yang pernah mengirim telemetry. Ambang dihitung per perangkat dari median jarak hingga 30 interval terakhir, dengan fallback `Anomaly.CheckInterval` bila hanya ada satu sampel. Alarm aktif bila umur sampel mencapai nilai terbesar dari 120 detik, 3× median cadence, atau 2× interval pemeriksaan. Server memverifikasi ulang sampel terakhir dan umur telemetry saat menerima kandidat; nilai watt terakhir hanya konteks dan tidak dipakai untuk menyimpulkan offline.
- Alarm `DEVICE_OFFLINE` bertahan sampai timestamp telemetry terbaru lebih baru daripada waktu alarm, lalu status aktif dipulihkan agar episode berikutnya dapat dicatat. Jenis ini tidak kedaluwarsa setelah 30 menit; notification dan tampilan log menandainya terpisah dari anomali daya. `DEVICE_DROP` lama tetap dikenali hanya untuk membaca log lama dan ditampilkan sebagai offline.
- Loop deteksi dan endpoint baca log anomaly dapat berjalan tanpa login. Endpoint pencatatan tetap memverifikasi telemetry terbaru, threshold, jenis anomaly, dan deduplikasi di server sebelum membuat log; reset active alert publik juga hanya membersihkan alert daya setelah sampel terbaru valid menunjukkan jenis anomaly tersebut sudah tidak aktif. Acknowledge, resolve, catatan operator, generate report, dan delete tetap memerlukan role sesuai kebijakan.
- Build berhasil dengan `dotnet build --no-restore` ke output terpisah; test suite tidak dijalankan.

### Batas deteksi perangkat diam

- Pemeriksaan dipicu oleh loop browser yang memuat `_AnomalyService`; tab yang tertutup/tertidur atau tanpa halaman monitoring aktif dapat menunda deteksi. Ambang ini mengurangi false alarm akibat jitter, tetapi waktu deteksi juga bergantung pada interval polling browser dan respons API.
- Hanya device yang memiliki setidaknya satu baris telemetry yang dapat dinilai. Device baru yang belum pernah mengirim data belum dapat terdeteksi dengan pendekatan ini karena sumber daftar panel berasal dari telemetry.
- Ambang minimum 120 detik berlaku walaupun cadence historis lebih cepat. Cadence tidak dapat dipelajari dengan baik dari satu sampel, sehingga interval pemeriksaan konfigurasi dipakai sebagai estimasi awal.

## Saran prioritas yang masih tersisa

1. Pindahkan loop sampling dan konfirmasi ke worker server yang mengonsumsi telemetry agar deteksi tetap berjalan tanpa halaman browser.
2. Gunakan klaim alert atomik lintas proses dan sepakati apakah log merepresentasikan awal episode atau tiap kejadian.
3. Sertakan timestamp sampel, validasi freshness/jarak antar sampel, dan simpan waktu sampel serta waktu proses terpisah.
4. Pertimbangkan pengecekan hak akses operator terhadap device yang dikirim dan kendali request paralel lintas instance.
5. Satukan aturan downtime dan zona waktu di server dengan zona waktu eksplisit.
6. Buat fallback threshold per-device yang tegas dan terlihat; jangan diam-diam lewati device.
7. Validasi baseline nol dan format multiplier Fibonacci.
8. Nyatakan bahwa severity/root-cause berupa rekomendasi heuristik, bukan diagnosis aktual.
9. Pisahkan cooldown aktual dari setting usang dan dokumentasikan timeout 30 menit server.
10. Tambahkan metrik health: device diperiksa, sampel stale/invalid, threshold kosong, request gagal, log tersupresi, keterlambatan deteksi.

## File utama

- KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring/Views/Shared/_AnomalyService.cshtml
- KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring/Controllers/ApiController.cs
- KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring/Services/AnomalyAnalysisService.cs
- KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring/Services/AnomalyNotificationBackgroundService.cs
- KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring/Models/AnomalyLog.cs
- KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring/Models/DeviceSettings.cs
- KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring/Services/DeviceSettingsService.cs
- KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring/Views/Shared/_Layout.cshtml
- KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring/wwwroot/js/realtime-refresh.js

