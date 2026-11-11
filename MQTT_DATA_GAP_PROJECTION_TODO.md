# TODO Eksekusi Proyeksi Data Saat MQTT Gap

**Status:** Implementasi fase fondasi sedang berjalan — feature flag default nonaktif; belum diterapkan ke database.  
**Tanggal:** 9 November 2026  
**Dokumen acuan:** `MQTT_DATA_GAP_PROJECTION_PLAN.md`

## 0. Aturan keselamatan sebelum implementasi

- [x] Pastikan perubahan hanya menambah kemampuan proyeksi; jangan mengubah cara kerja data aktual yang sudah berjalan.
- [x] Jangan menghapus atau mengubah arti field existing pada `KWHData`, `HourlyEnergy`, `DailyEnergy`, `MonthlyEnergy`, dan `YearlyEnergy`.
- [x] Jangan mengganti perhitungan energi aktual/trapezoidal rule yang sudah ada.
- [x] Jangan mengubah topic MQTT, payload MQTT, QoS, retry, koneksi broker, relay control, autentikasi, atau authorization.
- [x] Jangan mengubah response existing secara breaking; semua field baru harus additive/nullable.
- [x] Jangan menjadikan data projected sebagai data actual.
- [x] Jangan memasukkan nilai projected ke histori baseline proyeksi berikutnya.
- [x] Jangan memakai angka, device, tarif, tanggal, jam, atau jumlah bucket hardcode.
- [x] Pastikan semua perubahan harus dapat dimatikan melalui feature flag atau konfigurasi tanpa mengganggu mode existing.
- [ ] Buat backup database dan catat commit sebelum migration atau perubahan source.
- [ ] Tetapkan prosedur rollback sebelum migration dijalankan.

## 1. Baseline dan inventaris fungsi existing

- [x] Identifikasi project web app dan listener yang aktif digunakan.
- [x] Identifikasi model, `DbContext`, migration, service, controller, API, view, dan JavaScript terkait Usage Statistics.
- [x] Catat endpoint existing beserta request/response sebelum perubahan.
- [x] Catat perhitungan existing untuk energi per jam, hari, bulan, tahun, biaya, peak, ranking, comparison, dan export.
- [x] Catat semua konfigurasi tarif, timezone, interval MQTT, cache, dan aggregation.
- [ ] Simpan snapshot hasil dashboard existing untuk kasus:
  - [ ] Hari tanpa gap.
  - [ ] Satu device.
  - [ ] Banyak device.
  - [ ] Periode hari, bulan, dan tahun.
  - [ ] Biaya existing.
- [ ] Buat baseline query untuk membandingkan hasil sebelum dan sesudah fitur.
- [x] Pastikan feature flag default `false` atau mode existing tetap aktif saat deployment pertama.

## 2. Kontrak status dan konfigurasi

- [x] Tetapkan status `ACTUAL`, `MISSING`, `PROJECTED`, `MIXED`, `RECONCILED`, `INVALID`, `NO_DATA`, dan `NO_BASELINE`.
- [x] Tetapkan `ProjectionMethod` dan `CalculationVersion`.
- [ ] Tetapkan konfigurasi:
  - [ ] `ExpectedIntervalSeconds` per device atau hasil median historis.
  - [ ] `GapMultiplier`.
  - [ ] `MinimumGapSeconds`.
  - [ ] `OfflineThreshold`.
  - [x] Jumlah histori minimum.
  - [x] Batas confidence.
  - [ ] Batas faktor skala.
  - [x] Feature flag proyeksi.
- [x] Pastikan konfigurasi invalid memiliki fallback aman dan tidak menghasilkan angka palsu.
- [x] Pastikan perubahan konfigurasi memiliki version/invalidation cache.

## 3. Database dan migration

- [x] Buat model `EnergyProjectionGap`.
- [x] Buat model `EnergyProjectionValue`.
- [x] Tambahkan status, confidence, method, source period, gap ID, dan calculation version.
- [x] Tambahkan index untuk query:
  - [x] `DeviceKey + GapStartUtc`.
  - [x] `DeviceKey + BucketStartUtc`.
  - [x] Status gap terbuka/projected.
  - [x] Rekonsiliasi bucket.
- [x] Tambahkan unique constraint agar worker idempotent dan tidak membuat projected duplikat.
- [x] Pastikan migration `Up` dan `Down` simetris.
- [ ] Uji migration pada database kosong.
- [ ] Uji migration pada database existing yang berisi data.
- [x] Pastikan migration tidak mengubah atau menghapus data aktual.
- [ ] Pastikan rollback tidak menghapus data production tanpa prosedur backup/approval.
- [x] Jangan menjalankan migration production pada tahap checklist ini.

## 4. Deteksi gap MQTT per device

- [ ] Ambil reading valid terakhir per `DeviceKey`.
- [ ] Hitung `gapSeconds` berdasarkan timestamp yang konsisten.
- [ ] Terapkan rumus:

  ```text
  gapSeconds > max(ExpectedIntervalSeconds * GapMultiplier, MinimumGapSeconds)
  ```

- [ ] Bedakan timestamp terminal/device dan timestamp penerimaan/server.
- [ ] Validasi timestamp mundur, timestamp terlalu jauh, dan duplicate reading.
- [x] Buat gap terbuka saat device melewati `OfflineThreshold`.
- [ ] Tutup gap ketika reading valid berikutnya masuk.
- [ ] Tentukan `missingStart` dan `missingEnd` berdasarkan boundary timezone bisnis.
- [ ] Jangan membuat gap palsu saat aplikasi restart.
- [ ] Jangan membuat gap untuk downtime yang ditandai `PLANNED_DOWNTIME`.
- [x] Simpan alasan dan sumber deteksi gap.
- [ ] Uji gap pendek akibat jitter.
- [ ] Uji gap lintas jam, hari, bulan, dan tahun.

## 5. Pembentukan bucket per jam

- [ ] Bentuk bucket jam berdasarkan timezone aplikasi.
- [ ] Gunakan boundary periode runtime, bukan jumlah bucket hardcode.
- [ ] Pastikan bucket hanya dibuat untuk interval gap yang valid.
- [ ] Tandai bucket sebagai actual jika memiliki data actual valid.
- [ ] Tandai bucket sebagai missing jika tidak memiliki actual.
- [ ] Pastikan bucket projected tidak menggantikan record actual.
- [x] Pastikan satu `(DeviceKey, BucketStart)` tidak menghasilkan data projected ganda pada calculation version aktif.
- [ ] Uji boundary tengah malam, akhir bulan, akhir tahun, dan perubahan timezone.

## 6. Baseline histori per device dan jam yang sama

- [x] Ambil histori hanya untuk `DeviceKey` yang sama.
- [x] Ambil histori pada `HourOfDay` yang sama.
- [ ] Prioritaskan `DayClass` yang sama: `WORKDAY`, `WEEKEND`, atau `HOLIDAY`.
- [x] Jangan memakai bucket `PROJECTED`, `MIXED`, atau `INVALID` sebagai baseline.
- [ ] Buang hari dengan coverage aktual di bawah threshold.
- [ ] Pastikan histori pembanding tidak memiliki gap.
- [x] Terapkan bobot recency dan kemiripan `DayClass`.
- [ ] Implementasikan fallback bertingkat:
  - [ ] Device sama, jam sama, class sama.
  - [ ] Device sama, jam sama, class serupa.
  - [ ] Device sama, jam sama, histori lebih lama.
  - [ ] Fallback device/global hanya jika diizinkan dan confidence rendah.
- [x] Jika histori tidak cukup, kembalikan `NO_BASELINE` dan jangan membuat angka.
- [ ] Uji contoh Device A pukul 03:00 dan pukul 12:00.
- [x] Pastikan Device A tidak menggunakan histori Device B sebagai baseline utama.
- [x] Pastikan jam 12:00 tidak menggunakan jam 11:00/13:00 sebagai baseline utama.

## 7. Mesin proyeksi per jam

- [x] Implementasikan median/weighted median histori device + jam yang sama.
- [ ] Implementasikan pola mingguan pada jam yang sama.
- [ ] Implementasikan hari sejenis pada jam yang sama.
- [ ] Implementasikan tanggal historis pada jam yang sama jika valid.
- [ ] Implementasikan faktor skala per device dan jam dengan batas aman.
- [ ] Implementasikan analog day dengan mengambil nilai pada jam target yang sama.
- [ ] Jalankan backtesting untuk setiap kandidat metode.
- [ ] Hitung MAE, RMSE, WAPE, bias, dan residual.
- [ ] Hitung bobot ensemble berdasarkan error aktual device tersebut.
- [x] Hitung `ProjectedKWh(DeviceKey, Date, HourOfDay)`.
- [x] Batasi nilai projected agar tidak negatif.
- [x] Hitung lower/upper bound.
- [x] Hitung confidence score dan level confidence.
- [ ] Simpan method, sample count, confidence, bounds, dan calculation version.
- [ ] Pastikan hasil proyeksi idempotent.
- [ ] Pastikan restart worker tidak menggandakan data.

## 8. Worker dan rekonsiliasi

- [x] Buat worker terjadwal dengan interval konfigurasi.
- [ ] Ambil gap `OPEN` dan gap yang baru ditutup.
- [ ] Proses gap berdasarkan device dan calculation version.
- [ ] Gunakan transaction/locking yang mencegah dua worker menghitung gap yang sama.
- [ ] Simpan hasil secara bertahap jika gap panjang.
- [ ] Tandai gap `PROJECTED` setelah hasil berhasil disimpan.
- [x] Saat actual terlambat masuk, tandai bucket projected menjadi `RECONCILED`.
- [ ] Rebuild hanya bucket yang masih missing.
- [ ] Rebuild agregat level atas dari bucket jam.
- [ ] Catat audit log perhitungan dan rekonsiliasi.
- [ ] Uji worker saat aplikasi restart.
- [ ] Uji worker saat database sementara tidak tersedia.
- [ ] Uji worker saat data actual masuk bersamaan dengan proses proyeksi.

## 9. Agregasi harian, bulanan, dan tahunan

- [ ] Jangan membuat algoritma proyeksi terpisah untuk harian.
- [ ] Jangan membuat algoritma proyeksi terpisah untuk bulanan.
- [ ] Jangan membuat algoritma proyeksi terpisah untuk tahunan.
- [ ] Hitung daily dari bucket hourly.
- [ ] Hitung monthly dari daily/hourly sesuai jalur agregasi normal.
- [ ] Hitung yearly dari monthly/hourly sesuai jalur agregasi normal.
- [x] Tetapkan status agregat `ACTUAL`, `PROJECTED`, atau `MIXED` berdasarkan bucket penyusun.
- [ ] Pastikan total hourly sama dengan total daily/monthly/yearly.
- [ ] Pastikan agregasi existing tanpa projected menghasilkan angka yang sama seperti sebelumnya.
- [ ] Pastikan data projected tidak masuk ke baseline histori berikutnya.

## 10. Perhitungan biaya

- [ ] Ambil tarif secara dinamis berdasarkan device, tanggal, jam, mode tarif, dan periode efektif.
- [ ] Hitung biaya actual per bucket jam.
- [ ] Hitung biaya projected per bucket jam.
- [ ] Hitung biaya complete per bucket jam.
- [ ] Hitung biaya harian dari penjumlahan biaya jam.
- [ ] Hitung biaya bulanan dari agregasi biaya harian.
- [ ] Hitung biaya tahunan dari agregasi biaya bulanan.
- [ ] Jangan menggunakan `rata-rata jam * 24` jika seri jam tersedia.
- [ ] Jika tarif berubah di tengah hari, gunakan tarif sesuai bucket masing-masing.
- [ ] Jika tarif tidak valid, kembalikan `TARIFF_UNAVAILABLE`.
- [ ] Pastikan `NO_BASELINE` tidak dimasukkan ke total biaya.
- [ ] Pastikan semua nilai biaya dihitung ulang saat device, periode, tarif, atau mode berubah.

## 11. Kartu Estimasi Biaya

- [ ] Pertahankan layout kartu existing.
- [ ] Pada mode `complete`, gunakan rumus:

  ```text
  EstimatedCost = ActualBillCost + ProjectedBillCost
  ```

- [ ] Tampilkan total gabungan sebagai angka utama kartu.
- [ ] Tampilkan tarif dan biaya proyeksi pada baris metadata yang sama.
- [ ] Tampilkan indikator proyeksi dengan warna/penanda merah.
- [ ] Tampilkan `Proyeksi: Rp0` atau sembunyikan indikator hanya jika tidak ada projected, sesuai keputusan UI final.
- [ ] Pada mode `actual-only`, angka utama hanya biaya aktual.
- [ ] Jika semua periode projected, tampilkan status `PROJECTED` dan confidence.
- [ ] Jika `NO_BASELINE`, jangan masukkan biaya ke total dan tampilkan status estimasi belum lengkap.
- [ ] Tooltip kartu menampilkan biaya aktual, projected, complete, dan confidence.
- [ ] Pastikan formatter Rupiah dinamis dan tidak memakai nominal hardcode.
- [ ] Uji contoh actual Rp15.000.000 + projected Rp726.000 = Rp15.726.000.

## 12. API tanpa breaking change

- [x] Pertahankan semua field response existing.
- [x] Tambahkan field additive/nullable untuk actual, projected, complete, status, confidence, dan cost.
- [ ] Tambahkan `includeProjection=true|false`.
- [ ] Tambahkan `projectionMode=actual|complete`.
- [ ] Pastikan default endpoint existing tetap kompatibel.
- [ ] Pastikan response mengikuti device dan periode runtime.
- [ ] Kirim angka numerik, bukan string nominal yang sudah diformat.
- [ ] Sertakan `DeviceKey`, `HourOfDay`, `GapId`, `ProjectionMethod`, dan `CalculationVersion` pada seri jika diperlukan.
- [ ] Bedakan response `NO_DATA`, `NO_BASELINE`, `TARIFF_UNAVAILABLE`, dan error sistem.
- [ ] Uji consumer/API existing agar tidak gagal karena field baru.

## 13. Dashboard Details per device

- [x] Render data berdasarkan response device yang dipilih.
- [x] Tampilkan actual dengan warna existing.
- [x] Tampilkan projected dengan bar merah.
- [x] Pastikan hanya bucket gap yang berwarna merah.
- [ ] Tambahkan legend actual/projected jika seri tersedia.
- [ ] Tambahkan tooltip status, metode, histori, confidence, bounds, dan biaya.
- [ ] Tambahkan actual peak dan complete peak tanpa mengubah peak existing.
- [ ] Tambahkan pemisahan aktual, projected, dan complete pada kartu.
- [ ] Refresh data saat tanggal/device/mode berubah.
- [ ] Pastikan tidak ada data device sebelumnya yang tertinggal setelah filter berubah.

## 14. Tab utama Usage Statistics

- [ ] Hitung seri per device terlebih dahulu.
- [ ] Jumlahkan actual seluruh device.
- [x] Jumlahkan projected seluruh device.
- [x] Bentuk total complete.
- [ ] Tampilkan projected gabungan dengan merah/stacked merah.
- [ ] Tooltip gabungan dapat menjelaskan kontribusi per device.
- [ ] Filter device/panel memakai metode yang sama dengan Details.
- [ ] Pastikan satu device dan banyak device sama-sama bekerja.
- [ ] Pastikan device tanpa data tidak diperlakukan sebagai konsumsi nol tanpa aturan.
- [ ] Pastikan kartu, peak, ranking, biaya, dan chart mengikuti filter baru.

## 15. Tampilan dinamis dan responsif

- [ ] Tidak ada label, nominal, jumlah bar, atau device hardcode dari screenshot.
- [ ] Jumlah label sumbu mengikuti data API.
- [ ] Dataset actual/projected dibuat dinamis.
- [ ] Legend hanya menampilkan seri yang tersedia.
- [ ] Ukuran chart mengikuti container/viewport.
- [ ] Implementasikan state `loading`, empty, error, `NO_DATA`, dan `NO_BASELINE`.
- [ ] Pastikan tooltip tetap lengkap saat label sumbu dipadatkan.
- [ ] Pastikan kartu menyesuaikan angka besar dan format Rupiah.
- [ ] Pastikan layout tetap berfungsi untuk satu device, banyak device, periode pendek, dan periode panjang.

## 16. Laporan harian dan export

- [ ] Laporan menyatakan `AKTUAL PENUH` jika tidak ada proyeksi.
- [ ] Jika ada proyeksi, tampilkan status campuran.
- [ ] Tampilkan actual energy, projected energy, dan complete energy.
- [ ] Tampilkan actual cost, projected cost, dan complete cost.
- [ ] Rincikan setiap device yang menggunakan proyeksi.
- [ ] Rincikan jam projected per device.
- [ ] Tampilkan method, histori, confidence, bounds, dan gap ID.
- [ ] Tambahkan catatan bahwa projected bukan pembacaan meter actual.
- [ ] Simpan parameter runtime laporan: filter, periode, timezone, projection mode, tariff version, calculation version, dan waktu generate.
- [ ] Tambahkan pilihan export `Aktual saja` dan `Aktual + proyeksi`.
- [ ] Tambahkan kolom actual/projected/complete energy dan cost.
- [ ] Pastikan export tidak mencampur data dari filter sebelumnya.

## 17. Cache dan refresh

- [ ] Identifikasi cache existing yang memengaruhi Usage Statistics.
- [x] Perluas cache key dengan device, periode, timezone, projection mode, tariff version, dan calculation version.
- [ ] Invalidate cache saat reading MQTT baru relevan masuk.
- [ ] Invalidate cache saat gap/projection direkalkulasi.
- [ ] Invalidate cache saat tarif atau konfigurasi berubah.
- [ ] Jangan menjadikan cache frontend sebagai sumber kebenaran.
- [ ] Uji cache hit dan cache miss.
- [ ] Uji refresh manual dan refresh otomatis.
- [ ] Pastikan response lama tidak tertinggal setelah rekonsiliasi.

## 18. Testing dan validasi akurasi

### 18.1 Unit test

- [ ] Gap threshold per device.
- [ ] Boundary bucket jam.
- [ ] Pemilihan `DeviceKey + HourOfDay + DayClass`.
- [ ] Exclusion projected dari baseline.
- [ ] Weighted median.
- [ ] Scaling factor.
- [ ] MAE, RMSE, WAPE, bias, confidence, dan bounds.
- [ ] Agregasi hourly ke daily/monthly/yearly.
- [ ] Biaya actual/projected/complete.
- [ ] Formatter kartu Estimasi Biaya.
- [ ] Status `NO_DATA`, `NO_BASELINE`, `MIXED`, dan `TARIFF_UNAVAILABLE`.

### 18.2 Integration test

- [ ] MQTT reading normal tidak mengubah hasil existing.
- [ ] Gap MQTT membuat projected hanya untuk bucket yang hilang.
- [ ] Late reading merekonsiliasi projected menjadi actual.
- [ ] Worker restart tidak membuat duplikat.
- [ ] Multiple device diproses terpisah.
- [ ] Filter Details dan tab utama menghasilkan metode konsisten.
- [ ] Perubahan tarif menghitung ulang biaya.
- [ ] API backward compatible.
- [ ] Cache invalidation bekerja.

### 18.3 Backtesting

- [ ] Simulasikan gap 1 jam.
- [ ] Simulasikan gap 3 jam.
- [ ] Simulasikan gap 6 jam.
- [ ] Simulasikan gap 12 jam.
- [ ] Simulasikan gap 24 jam.
- [ ] Simulasikan gap 48 jam.
- [ ] Simulasikan gap 7 hari.
- [ ] Ukur hasil per device.
- [ ] Ukur hasil per `DayClass`.
- [ ] Validasi WAPE, bias, dan coverage interval terhadap target.
- [ ] Turunkan confidence jika target tidak tercapai.

### 18.4 Regression test fungsi existing

- [ ] Dashboard tanpa gap tetap sama.
- [ ] Perhitungan data aktual tetap sama.
- [ ] Biaya existing tanpa projected tetap sama.
- [ ] Peak/ranking existing tetap sama pada mode actual-only.
- [ ] Relay control tetap berjalan.
- [ ] MQTT ingest normal tetap berjalan.
- [ ] Login, authorization, device monitoring, dan settings tidak berubah.
- [ ] Export existing tetap dapat digunakan.

## 19. Deployment bertahap dan rollback

- [ ] Deploy migration ke database staging terlebih dahulu.
- [ ] Jalankan backfill/initialization hanya pada staging.
- [ ] Aktifkan feature flag untuk satu device uji.
- [ ] Bandingkan actual-only dengan complete.
- [ ] Pantau error, latency, duplicate, dan penggunaan database.
- [ ] Aktifkan bertahap per device/group.
- [ ] Jangan mengaktifkan default global sebelum acceptance test selesai.
- [ ] Siapkan rollback feature flag ke mode existing.
- [ ] Siapkan rollback migration sesuai backup dan prosedur database.
- [ ] Pastikan rollback tidak menghapus `KWHData` atau agregat aktual.
- [ ] Dokumentasikan hasil deployment dan calculation version.

## 20. Kriteria selesai

- [ ] Semua checklist keselamatan terpenuhi.
- [ ] Semua fungsi existing tetap lulus regression test.
- [ ] Proyeksi hanya berjalan per device dan jam yang sama.
- [ ] Harian/bulanan/tahunan hanya agregasi dari hourly.
- [ ] Bar merah hanya muncul pada bucket projected.
- [ ] Kartu Estimasi Biaya menghitung actual + projected pada mode complete.
- [ ] Biaya actual/projected/complete konsisten pada jam, hari, bulan, dan tahun.
- [ ] Details dan tab utama memakai metode yang sama.
- [ ] Laporan harian menyebut device dan jam yang memakai proyeksi.
- [ ] Semua tampilan dan perhitungan dinamis terhadap filter/runtime.
- [ ] Backtesting memenuhi target atau confidence diturunkan secara tepat.
- [ ] Feature flag, monitoring, backup, dan rollback tervalidasi.
- [ ] Build dan test yang tersedia berhasil.
- [ ] Persetujuan final diberikan sebelum aktivasi production.