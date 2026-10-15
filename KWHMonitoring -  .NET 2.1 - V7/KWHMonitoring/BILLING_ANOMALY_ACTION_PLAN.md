# Rencana Action Anomaly Tagihan, Konfirmasi, dan Pembentukan Tren Baru

> Status: Perencanaan saja. Belum ada perubahan source code, database, migration, atau konfigurasi yang dilakukan berdasarkan dokumen ini.
>
> Tujuan: menyediakan workflow yang dapat diaudit untuk anomaly tagihan estimasi-vs-aktual dan kenaikan tagihan periode-ke-periode, termasuk notifikasi email/WhatsApp, konfirmasi alasan, pengulangan notifikasi sampai tiga konfirmasi, dan pembentukan baseline tren baru per device.

## 1. Tujuan Fitur

Fitur ini dirancang untuk:

1. Mendeteksi anomaly antara estimasi tagihan dan tagihan aktual ERP.
2. Mendeteksi anomaly kenaikan tagihan periode berjalan dibandingkan periode sebelumnya.
3. Mengirim notifikasi anomaly melalui email dan WhatsApp.
4. Menyertakan link aman untuk konfirmasi anomaly.
5. Menyimpan keputusan, kategori alasan, dan alasan bebas ke histori anomaly.
6. Mengikuti perkembangan anomaly yang sama pada periode-periode berikutnya.
7. Mengirim notifikasi lanjutan jika nilai tagihan masih sama atau mendekati kandidat tren.
8. Menetapkan tren baru setelah anomaly yang wajar dikonfirmasi pada tiga periode berbeda.
9. Menggunakan tren baru sebagai baseline evaluasi berikutnya tanpa menghapus histori anomaly lama.

## 2. Prinsip Bisnis Utama

### 2.1 Anomaly tidak langsung menjadi baseline baru

Tagihan yang melonjak tidak boleh langsung dianggap normal. Nilai tersebut menjadi `candidate trend` terlebih dahulu.

Contoh:

| Periode | Tagihan | Baseline aktif | Hasil |
|---|---:|---:|---|
| Januari | Rp10.000.000 | Rp4.000.000 | Anomaly pertama |
| Februari | Rp10.500.000 | Rp4.000.000 | Anomaly lanjutan |
| Maret | Rp10.200.000 | Rp4.000.000 | Anomaly lanjutan |
| Setelah konfirmasi ketiga | Rp10.200.000 | Rp10.200.000 | Tren baru diterima |

### 2.2 Konfirmasi harus berasal dari pengguna

Sistem tidak boleh menaikkan jumlah konfirmasi hanya karena nilai tagihan berulang. Pengguna harus memberikan keputusan melalui link konfirmasi.

Keputusan yang dapat dihitung sebagai konfirmasi tren:

- `Wajar`.
- `Wajar dengan alasan`.
- Alasan operasional yang valid sesuai kategori yang ditentukan.

Keputusan yang tidak dihitung sebagai konfirmasi tren:

- `Tidak wajar`.
- `Perlu pemeriksaan`.
- Link hanya dibuka tanpa submit keputusan.

### 2.3 Satu anomaly event untuk kombinasi anomaly yang sama

Jika estimasi-vs-aktual dan period-over-period terjadi pada device dan periode yang sama, sistem membuat satu anomaly event dengan tipe gabungan:

```text
ESTIMATE_VS_ERP,PERIOD_OVER_PERIOD
```

Notifikasi tetap dikirim satu kali per channel, bukan dua notifikasi terpisah.

## 3. Jenis Anomaly

### 3.1 Estimasi dibandingkan aktual ERP

```text
selisih = tagihan aktual ERP - estimasi tagihan
```

Anomaly terjadi jika:

```text
abs(selisih) > threshold
```

Data yang perlu disimpan:

- Estimasi tagihan.
- Tagihan aktual.
- Selisih nominal.
- Persentase selisih jika dapat dihitung.
- Threshold saat anomaly dideteksi.
- Tarif dan energi yang digunakan saat evaluasi.

### 3.2 Periode sekarang dibandingkan periode sebelumnya

```text
selisih periode = tagihan periode sekarang - tagihan periode sebelumnya
```

Anomaly kenaikan terjadi jika:

```text
selisih periode > threshold
```

Penurunan besar tetap dapat dicatat sebagai delta informasi, tetapi tidak otomatis dianggap anomaly kenaikan period-over-period sesuai aturan saat ini.

Data yang perlu disimpan:

- Tagihan periode sekarang.
- Tagihan periode sebelumnya.
- Selisih nominal.
- Persentase perubahan.
- Threshold saat evaluasi.
- Periode pembanding.

## 4. Definisi Tagihan yang Sama atau Mendekati

Nilai periode berikutnya dianggap masih bagian dari kandidat tren jika memenuhi toleransi nominal dan persentase.

Rekomendasi konfigurasi awal:

```text
BillingTrendTolerancePercent = 10%
BillingTrendRequiredConfirmations = 3
```

Toleransi efektif:

```text
toleransi efektif = max(threshold nominal, candidate trend x tolerance percent)
```

Kondisi mendekati:

```text
abs(tagihan sekarang - candidate trend) <= toleransi efektif
```

Contoh:

```text
candidate trend      = Rp10.000.000
threshold nominal    = Rp1.000.000
toleransi persentase = 10%
toleransi efektif    = Rp1.000.000
```

| Tagihan berikutnya | Selisih | Hasil |
|---:|---:|---|
| Rp10.500.000 | Rp500.000 | Mendekati |
| Rp11.000.000 | Rp1.000.000 | Mendekati |
| Rp12.000.000 | Rp2.000.000 | Tidak mendekati |

Jika nilai berikutnya tidak mendekati kandidat tren:

- Konfirmasi tidak bertambah.
- Baseline lama tetap aktif.
- Kandidat lama tetap tersimpan sebagai histori.
- Sistem dapat membuat kandidat anomaly baru jika aturan anomaly terpenuhi.

## 5. Siklus Hidup Anomaly

Status workflow yang disarankan:

```text
DETECTED
WAITING_CONFIRMATION
CONFIRMED_REASONABLE
CONFIRMED_UNREASONABLE
NEEDS_REVIEW
ACCEPTED_AS_NEW_TREND
CLOSED
```

Alur utama:

```text
Tagihan masuk
    -> Evaluasi anomaly
    -> Tidak anomaly: selesai
    -> Anomaly: buat anomaly event
    -> Buat kandidat tren atau hubungkan ke kandidat aktif
    -> Buat link konfirmasi
    -> Kirim email dan WhatsApp melalui outbox
    -> Pengguna mengirim keputusan dan alasan
    -> Simpan histori konfirmasi
    -> Evaluasi periode berikutnya
    -> Jika mendekati kandidat: kirim notifikasi lanjutan
    -> Jika tiga konfirmasi wajar tercapai: terima baseline baru
```

## 6. Aturan Konfirmasi Tiga Periode

Konfirmasi hanya dihitung jika seluruh kondisi berikut terpenuhi:

1. Device sama.
2. Kandidat tren sama.
3. Periode tagihan berbeda.
4. Nilai tagihan masih berada dalam toleransi kandidat.
5. Keputusan pengguna menyatakan wajar atau memiliki alasan yang diterima.
6. Belum ada konfirmasi untuk device, kandidat, dan periode tersebut.

Contoh:

| Periode | Tagihan | Keputusan | Jumlah konfirmasi |
|---|---:|---|---:|
| Januari | Rp10.000.000 | Wajar, ada penambahan beban | 1 |
| Februari | Rp10.500.000 | Wajar, beban masih berjalan | 2 |
| Maret | Rp10.200.000 | Wajar, kondisi operasional normal | 3 |

### 6.1 Nilai baseline baru

Setelah konfirmasi ketiga, baseline baru direkomendasikan menggunakan median dari tiga nilai yang dikonfirmasi, bukan nilai terakhir secara langsung.

Contoh:

```text
Rp10.000.000
Rp10.500.000
Rp10.200.000

baseline baru = median = Rp10.200.000
```

Median dipilih agar satu nilai yang terlalu tinggi atau terlalu rendah tidak langsung menggeser baseline.

### 6.2 Perilaku setelah tren baru diterima

Histori anomaly lama tidak dihapus. Sistem hanya mengubah baseline aktif.

Contoh:

| Tagihan baru | Baseline baru | Hasil |
|---:|---:|---|
| Rp10.000.000 | Rp10.200.000 | Normal |
| Rp10.800.000 | Rp10.200.000 | Tergantung threshold |
| Rp14.000.000 | Rp10.200.000 | Anomaly baru |

## 7. Keputusan Pengguna dan Alasan

### 7.1 Wajar

Sistem:

- Menyimpan keputusan.
- Menyimpan alasan jika diberikan.
- Menambah jumlah konfirmasi kandidat tren.
- Melanjutkan pemantauan periode berikutnya.

### 7.2 Wajar dengan alasan

Alasan wajib diisi. Submit ditolak jika alasan kosong.

### 7.3 Tidak wajar

Sistem:

- Tidak menambah jumlah konfirmasi.
- Menandai anomaly sebagai `CONFIRMED_UNREASONABLE`.
- Mempertahankan baseline lama.
- Dapat mengirim notifikasi eskalasi kepada administrator.

### 7.4 Perlu pemeriksaan

Sistem:

- Tidak menambah jumlah konfirmasi.
- Menandai anomaly sebagai `NEEDS_REVIEW`.
- Menyimpan catatan pengguna.
- Dapat mengirim reminder sampai keputusan final diberikan.

### 7.5 Kategori alasan

Gunakan kategori terstruktur agar laporan dapat difilter:

```text
NEW_LOAD
OPERATIONAL_CHANGE
TARIFF_CHANGE
SEASONAL_USAGE
METER_ISSUE
ERP_CORRECTION
PAYMENT_DELAY
OTHER
```

Selain kategori, simpan `ReasonText` untuk alasan bebas pengguna.

## 8. Link Konfirmasi pada Email dan WhatsApp

Format konseptual:

```text
https://domain-aplikasi/Monitoring/BillingAnomalyConfirmation/{token}
```

Halaman konfirmasi menampilkan:

- Device dan kode lokasi.
- Periode anomaly.
- Estimasi tagihan.
- Tagihan aktual.
- Tagihan periode sebelumnya.
- Selisih dan persentase.
- Threshold.
- Jenis anomaly.
- Kandidat tren aktif.
- Riwayat konfirmasi.
- Progress konfirmasi, misalnya `2 dari 3`.
- Form keputusan dan alasan.

### 8.1 Keamanan token

Rekomendasi:

- Token dibuat acak dan tidak dapat ditebak.
- Hanya hash token yang disimpan di database.
- Token memiliki masa berlaku 7–14 hari.
- GET hanya menampilkan halaman.
- POST baru menyimpan keputusan.
- Token tidak boleh dapat dipakai untuk mengubah data lain.
- Duplikasi submit untuk anomaly/periode yang sama ditolak.
- Token dapat dicabut jika anomaly ditutup atau dibatalkan.

### 8.2 Opsi autentikasi

Rekomendasi utama: pengguna internal wajib login agar audit user jelas.

Jika WhatsApp harus dapat dibuka tanpa login, gunakan token aman dengan expiry, pembatasan akses, dan pencatatan channel.

## 9. Model Data yang Disarankan

### 9.1 `AnomalyLog`

Field workflow tambahan yang mungkin diperlukan:

```text
WorkflowStatus
ConfirmationRequired
ConfirmedAt
ConfirmedBy
ConfirmationReason
ConfirmationCategory
TrendCandidateId
NotificationCount
LastNotificationAt
```

Namun histori konfirmasi sebaiknya tidak ditumpuk di satu row anomaly.

### 9.2 `BillingAnomalyConfirmation`

```text
Id
AnomalyLogId
DeviceKey
BillingPeriod
Decision
ReasonCategory
ReasonText
ConfirmedByUserId
ConfirmedByName
ConfirmedAt
CreatedFromChannel
TokenId
```

Tambahkan unique constraint untuk mencegah konfirmasi ganda pada anomaly dan periode yang sama sesuai aturan bisnis.

### 9.3 `BillingTrendProfile`

```text
Id
DeviceKey
BaselineAmount
BaselinePeriod
BaselineSource
CandidateAmount
CandidateMedianAmount
CandidateStartedPeriod
CandidateConfirmationCount
RequiredConfirmationCount
TrendStatus
LastConfirmedPeriod
LastUpdatedAt
```

Status trend:

```text
STABLE
PENDING_CONFIRMATION
ACCEPTED
RESET
```

### 9.4 `BillingAnomalyNotification`

```text
Id
AnomalyLogId
Channel
Recipient
NotificationType
AttemptCount
SentAt
DeliveredAt
FailedAt
MessageId
ConfirmationTokenId
```

Unique key yang disarankan:

```text
AnomalyLogId + Channel + NotificationType
```

Tujuannya agar refresh tabel atau retry tidak membuat notifikasi ganda.

## 10. Arsitektur Proses

### 10.1 Jalur pembacaan tabel

Endpoint tabel hanya boleh:

```text
Read data
Calculate anomaly
Return status
```

Endpoint tabel tidak boleh:

```text
Save anomaly log
Send email
Send WhatsApp
Create confirmation token
```

### 10.2 Service workflow

Buat service terpisah, misalnya:

```text
BillingAnomalyWorkflowService
```

Tugasnya:

1. Menerima hasil evaluasi anomaly.
2. Membuat anomaly event secara idempotent.
3. Mencari trend profile aktif per device.
4. Menentukan apakah anomaly merupakan lanjutan kandidat tren.
5. Membuat token konfirmasi.
6. Membuat record notification outbox.
7. Menjalankan evaluasi konfirmasi.
8. Mengaktifkan baseline baru setelah tiga konfirmasi.

### 10.3 Outbox notification

Notifikasi sebaiknya tidak dikirim langsung dalam request utama.

```text
Anomaly detected
    -> Simpan anomaly dan notification outbox
    -> Background worker mengambil outbox
    -> Kirim email
    -> Kirim WhatsApp
    -> Simpan hasil pengiriman
```

Keuntungannya:

- Request utama tetap cepat.
- Kegagalan channel tidak membatalkan penyimpanan anomaly.
- Pengiriman dapat di-retry.
- Duplikasi dapat dicegah.
- Status pengiriman dapat diaudit.

## 11. Aturan Pengiriman Notifikasi

### 11.1 Notifikasi pertama

Dikirim ketika anomaly baru terdeteksi dan belum ada notification event yang sama.

### 11.2 Notifikasi lanjutan

Dikirim jika seluruh kondisi berikut terpenuhi:

1. Device sama.
2. Kandidat tren masih aktif.
3. Tagihan periode baru mendekati kandidat.
4. Belum ada konfirmasi pada periode baru.
5. Jumlah konfirmasi wajar belum mencapai tiga.

Contoh:

| Periode | Kondisi | Notifikasi |
|---|---|---|
| Januari | Anomaly pertama | Ya |
| Februari | Mendekati kandidat | Ya |
| Maret | Mendekati kandidat | Ya |
| Setelah tren diterima | Nilai masih dalam baseline baru | Tidak |

Jika kedua channel aktif, setiap channel memiliki status dan retry sendiri.

## 12. Data yang Wajib Diaudit

Setiap keputusan dan pengiriman perlu mencatat:

- Device.
- Periode konsumsi.
- Periode pembayaran ERP.
- Jenis anomaly.
- Estimasi.
- Aktual.
- Baseline yang dipakai.
- Threshold.
- Kandidat tren.
- Keputusan pengguna.
- Kategori alasan.
- Alasan bebas.
- User atau identitas token.
- Waktu keputusan.
- Channel notifikasi.
- Status pengiriman.
- Jumlah retry.

## 13. Kasus Khusus yang Harus Ditangani

- Perubahan tarif: simpan tarif saat anomaly agar kenaikan tidak salah dibaca sebagai kenaikan pemakaian.
- Penambahan beban atau perangkat: gunakan kategori `NEW_LOAD` atau `OPERATIONAL_CHANGE`.
- Koreksi ERP: gunakan kategori `ERP_CORRECTION`.
- Pembayaran terlambat: bandingkan periode konsumsi dan periode pembayaran sesuai mapping yang sudah ditetapkan.
- Duplikasi data ERP: gunakan kombinasi device, periode, `TagihanListrikID`, dan tipe anomaly.
- Periode tanpa tagihan aktual: status tetap `Estimasi`, tidak membuat anomaly aktual.
- Nilai threshold nol atau tidak valid: gunakan fallback yang sudah ditentukan oleh aturan Settings.
- Perubahan baseline: histori anomaly lama tetap immutable.

## 14. To-Do List Implementasi

### Fase 1 — Finalisasi aturan bisnis

- [ ] Pastikan definisi anomaly estimasi-vs-aktual.
- [ ] Pastikan definisi anomaly period-over-period.
- [ ] Tentukan apakah tiga konfirmasi harus dari tiga bulan berturut-turut.
- [ ] Tentukan toleransi persentase kandidat tren.
- [ ] Tentukan toleransi nominal minimum.
- [ ] Tentukan apakah alasan wajib untuk semua keputusan wajar.
- [ ] Tentukan masa berlaku link.
- [ ] Tentukan user/role yang boleh mengonfirmasi.
- [ ] Tentukan penerima email dan nomor WhatsApp.
- [ ] Tentukan apakah baseline baru menggunakan median atau rata-rata.

### Fase 2 — Model dan database

- [ ] Finalisasi perubahan `AnomalyLog`.
- [ ] Buat model `BillingAnomalyConfirmation`.
- [ ] Buat model `BillingTrendProfile`.
- [ ] Buat model `BillingAnomalyNotification`.
- [ ] Buat model/token konfirmasi.
- [ ] Tambahkan unique index untuk anomaly dan konfirmasi.
- [ ] Buat migration EF Core.
- [ ] Uji migration pada database development.
- [ ] Uji rollback migration.

### Fase 3 — Service backend

- [ ] Pisahkan evaluasi read-only dari workflow penyimpanan.
- [ ] Buat `BillingAnomalyWorkflowService`.
- [ ] Buat proses anomaly event idempotent.
- [ ] Buat pencarian trend profile.
- [ ] Buat perhitungan toleransi.
- [ ] Buat pembentukan candidate trend.
- [ ] Buat penambahan confirmation count.
- [ ] Buat penerimaan baseline baru setelah tiga konfirmasi.
- [ ] Buat reset candidate jika kondisi tidak lagi mendekati.
- [ ] Pastikan request tabel tidak mengirim notifikasi.

### Fase 4 — Halaman dan endpoint konfirmasi

- [ ] Buat endpoint GET halaman konfirmasi.
- [ ] Buat endpoint POST keputusan.
- [ ] Validasi token dan expiry.
- [ ] Validasi akses user atau token.
- [ ] Validasi alasan wajib.
- [ ] Cegah submit ganda.
- [ ] Tampilkan ringkasan anomaly.
- [ ] Tampilkan histori konfirmasi.
- [ ] Tampilkan progress konfirmasi.
- [ ] Tampilkan status baseline aktif dan candidate trend.

### Fase 5 — Notifikasi

- [ ] Buat template email.
- [ ] Buat template WhatsApp.
- [ ] Tambahkan ringkasan anomaly.
- [ ] Tambahkan link konfirmasi.
- [ ] Buat notification outbox.
- [ ] Buat background worker atau scheduler.
- [ ] Tambahkan retry dengan batas maksimal.
- [ ] Simpan message ID dan status delivery.
- [ ] Cegah notifikasi ganda.
- [ ] Tambahkan mekanisme retry manual untuk administrator.

### Fase 6 — Integrasi billing

- [ ] Tentukan trigger resmi setelah data ERP tersedia.
- [ ] Jalankan workflow sekali per device dan periode.
- [ ] Pastikan anomaly gabungan hanya satu event.
- [ ] Hubungkan anomaly lanjutan ke candidate trend.
- [ ] Pastikan refresh tabel tidak membuat anomaly atau notifikasi baru.
- [ ] Pastikan baseline baru dipakai pada periode berikutnya.

### Fase 7 — UI dan laporan

- [ ] Tampilkan status konfirmasi pada Anomaly Center.
- [ ] Tampilkan jumlah konfirmasi.
- [ ] Tampilkan candidate trend.
- [ ] Tampilkan baseline aktif.
- [ ] Tampilkan alasan pengguna.
- [ ] Tampilkan histori perubahan baseline.
- [ ] Tambahkan filter berdasarkan kategori alasan.
- [ ] Tambahkan filter status workflow.

### Fase 8 — Pengujian

- [ ] Anomaly estimasi-vs-aktual.
- [ ] Anomaly period-over-period.
- [ ] Kedua anomaly terjadi sekaligus.
- [ ] Tagihan berikutnya sama persis.
- [ ] Tagihan berikutnya masih dalam toleransi.
- [ ] Tagihan berikutnya di luar toleransi.
- [ ] Konfirmasi wajar tanpa alasan.
- [ ] Konfirmasi wajar dengan alasan.
- [ ] Konfirmasi tidak wajar.
- [ ] Perlu pemeriksaan.
- [ ] Token expired.
- [ ] Token sudah dipakai.
- [ ] Konfirmasi ganda pada periode sama.
- [ ] Tiga konfirmasi berhasil.
- [ ] Baseline baru aktif.
- [ ] Anomaly baru terhadap baseline baru.
- [ ] Email berhasil dan gagal.
- [ ] WhatsApp berhasil dan gagal.
- [ ] Retry tidak menggandakan pesan.
- [ ] Refresh tabel tidak mengirim ulang notifikasi.
- [ ] Request paralel tidak menggandakan anomaly.

## 15. Rekomendasi Keputusan Teknis

```text
Evaluasi anomaly       : read-only dan dapat dipanggil cepat
Penyimpanan anomaly    : workflow terpisah dan idempotent
Notifikasi             : outbox + background worker
Konfirmasi             : login internal atau token aman dengan expiry
Toleransi              : max(threshold nominal, persentase candidate trend)
Jumlah konfirmasi      : 3 periode berbeda
Baseline baru          : median nilai yang dikonfirmasi
Tabel                  : read-only, tidak membuat log/notifikasi
Histori                : immutable dan selalu dapat diaudit
```

## 16. Kriteria Selesai

Implementasi dapat dianggap selesai apabila:

- Anomaly kedua jenis dapat dideteksi dengan benar.
- Notifikasi email dan WhatsApp memiliki link konfirmasi valid.
- Link tidak dapat digunakan setelah expired atau sudah dipakai sesuai aturan.
- Keputusan dan alasan tersimpan lengkap.
- Notifikasi lanjutan hanya dikirim ketika nilai masih mendekati kandidat tren.
- Tiga konfirmasi dari tiga periode berbeda dapat mengaktifkan baseline baru.
- Baseline baru digunakan pada evaluasi selanjutnya.
- Anomaly lama tidak terhapus atau berubah secara tidak semestinya.
- Refresh halaman tabel tidak membuat log dan notifikasi duplikat.
- Kegagalan email/WhatsApp dapat di-retry tanpa menggandakan notifikasi.
- Seluruh proses dapat ditelusuri dari anomaly, konfirmasi, tren, dan notifikasi.