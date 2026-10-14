# TODO Implementasi Cutoff Estimasi Biaya per Device

**Status:** Implementasi repository selesai; verifikasi database, browser, staging, dan data PLN masih terbuka  
**Tanggal dibuat:** 12 Oktober 2026  
**Referensi rencana:** `RENCANA_CUTOFF_ESTIMASI_BIAYA_PER_DEVICE.md`

> Checklist ini harus selalu diperbarui setelah perubahan kode, validasi, atau keputusan implementasi.
>
> Keterangan: `[x]` selesai/terverifikasi, `[~]` diterapkan sebagian atau menunggu validasi eksternal, `[ ]` belum dikerjakan.

## 0. Aturan Pelaksanaan

- [x] Jangan mengubah atau mereset perubahan existing pada `Views/Monitoring/Charts.cshtml`.
- [ ] Buat branch khusus untuk implementasi.
- [ ] Pastikan backup database tersedia sebelum menjalankan migration.
- [x] Implementasikan perubahan secara bertahap dan dapat diuji per fase.
- [x] Jangan mengubah histori `MonthlyEnergy` secara otomatis tanpa keputusan bisnis tertulis.
- [x] Semua perubahan response API harus additive selama masa kompatibilitas.

## 1. Keputusan Bisnis Sebelum Coding

- [x] Konfirmasi bahwa default device lama dan device yang belum dikonfigurasi tetap memakai periode bulan kalender penuh, dari tanggal 1 sampai hari terakhir bulan.
- [x] Konfirmasi rentang cutoff yang valid: tanggal **25–31**.
- [ ] Tentukan apakah cutoff hanya tanggal nominal atau harus mencari tanggal pembacaan meter aktual.
- [ ] Tentukan aturan jika data pembacaan tidak tersedia tepat pada cutoff.
- [ ] Tentukan apakah sistem menggunakan cutoff nominal, pembacaan terdekat, atau status estimasi tidak tersedia.
- [ ] Tentukan nilai toleransi pencarian pembacaan aktual jika fitur tersebut diperlukan.
- [ ] Tentukan apakah perubahan cutoff berlaku langsung pada periode berjalan atau mulai periode berikutnya.
- [x] Tentukan apakah field API lama `projectedMonthKWh` dipertahankan sebagai legacy.
- [ ] Tentukan apakah perlu menyimpan snapshot periode/tagihan agar histori tidak berubah ketika settings berubah.
- [x] Dokumentasikan keputusan final fase pertama: cutoff nominal, interval `[periodStart, periodEnd)`, clamping tanggal pendek, dan perubahan berlaku pada perhitungan baru.

## 2. Audit dan Baseline Perhitungan Saat Ini

- [x] Petakan endpoint per-device dan aggregate yang berhubungan dengan `billProjection`.
- [x] Petakan pemakaian field projection lama dan field periode baru pada frontend.
- [x] Petakan jalur perhitungan biaya per-device dan aggregate.
- [x] Identifikasi sumber data energi yang digunakan: `HourlyEnergy`, `DailyEnergy`, dan `MonthlyEnergy`.
- [x] Pastikan jalur cutoff memakai `DailyEnergy` lintas bulan serta `HourlyEnergy` untuk hari berjalan.
- [x] Catat formula existing dan formula cutoff pada `FINANCIAL_CALCULATIONS.md`.
- [ ] Siapkan contoh hasil existing dari beberapa device untuk perbandingan before/after.
- [ ] Identifikasi device dengan tarif berbeda, device tanpa settings, dan device dengan data tidak lengkap.

## 3. Perancangan Periode Billing

- [x] Tetapkan konvensi interval timestamp: `[periodStart, periodEnd)`.
- [x] Tetapkan definisi `periodStart` dan `periodEnd` untuk mode default bulan kalender penuh.
- [x] Tetapkan definisi `periodStart` berdasarkan cutoff periode sebelumnya untuk device yang mengaktifkan cutoff.
- [x] Tetapkan definisi `periodEnd` berdasarkan cutoff periode berjalan untuk device yang mengaktifkan cutoff.
- [x] Tetapkan aturan `asOfDate` sebelum cutoff bulan berjalan.
- [x] Tetapkan aturan `asOfDate` tepat pada cutoff.
- [x] Tetapkan aturan `asOfDate` setelah cutoff.
- [x] Tetapkan aturan untuk cutoff 29, 30, atau 31 pada bulan yang lebih pendek melalui clamping tanggal.
- [x] Tetapkan status hasil resolusi periode, termasuk `configured` dan mode kalender.
- [x] Buat tabel contoh periode untuk cutoff 25, 28, 30, dan 31 pada `RENCANA_CUTOFF_ESTIMASI_BIAYA_PER_DEVICE.md`.
- [x] Buat tabel contoh periode default dari tanggal 1 sampai akhir bulan pada `RENCANA_CUTOFF_ESTIMASI_BIAYA_PER_DEVICE.md`.

## 4. Model, Database, dan Migration

- [x] Tambahkan property cutoff pada `Models/DeviceSettings.cs`.
- [x] Gunakan kombinasi `BillingCutoffDay` nullable dan `BillingCutoffEnabled`; tolerance tidak termasuk scope fase ini.
- [x] Tetapkan tipe data dan default property.
- [x] Tambahkan mapping kolom pada `Models/ApplicationDbContext.cs`.
- [x] Tambahkan validasi rentang cutoff 25–31.
- [x] Tambahkan migration EF Core dengan flag nonaktif/default aman.
- [x] Pastikan migration tidak mengubah device existing menjadi cutoff tanggal 25.
- [x] Pastikan device existing tetap berada pada mode bulan kalender penuh.
- [x] Periksa migration snapshot agar konsisten.
- [ ] Uji migration pada database salinan/staging.
- [x] Siapkan rollback plan migration melalui method `Down()` dan dokumentasikan nama migration final.

## 5. Device Settings Service dan API Settings

- [x] Update `DeviceSettingsService.GetEffectiveAsync()` dengan default mode bulan kalender penuh.
- [x] Update sanitasi/default settings pada `DeviceSettingsService`.
- [x] Update pembuatan settings agar device baru tetap mode bulan kalender sampai cutoff diaktifkan.
- [x] Update save settings agar cutoff tersimpan.
- [x] Tambahkan validasi server-side untuk cutoff invalid ketika cutoff diaktifkan.
- [ ] Tentukan perilaku untuk nilai null, nol, negatif, dan lebih besar dari 31.
- [ ] Tambahkan logging jika nilai invalid dikoreksi ke default.
- [ ] Pastikan cache settings di-refresh setelah perubahan cutoff.
- [x] Update request/response endpoint device settings, termasuk mode bulan kalender atau mode cutoff.
- [ ] Uji GET settings untuk device existing pada database/staging.
- [ ] Uji POST/save settings untuk device existing pada database/staging.
- [ ] Uji pembuatan settings untuk device baru pada database/staging.

## 6. Helper atau Service Periode Billing

- [x] Buat `Services/BillingPeriodCalculator.cs` untuk menentukan periode billing per device.
- [x] Hindari menaruh formula periode tersebar di `ApiController` dan view.
- [x] Implementasikan normalisasi cutoff terhadap jumlah hari dalam bulan.
- [x] Implementasikan `periodStart`, `periodEnd`, `periodDays`, `elapsedDays`, dan `daysRemaining`.
- [x] Implementasikan mode kalender dan cutoff dengan status resolusi.
- [~] Smoke test helper sudah dirancang untuk kalender, boundary, Februari, dan clamping; eksekusi executable belum dapat direkam karena keterbatasan restore/shell pada environment.

## 7. Perhitungan Konsumsi dan Estimasi Biaya

- [x] Ubah perhitungan aktual agar memfilter `DeviceKey`.
- [x] Ubah batas data menjadi interval periode device.
- [x] Pastikan periode yang melewati dua bulan kalender dihitung lengkap.
- [x] Hindari double counting antara data mentah dan data agregasi: hari berjalan memakai `HourlyEnergy` sebagai pengganti `DailyEnergy`, bukan dijumlahkan.
- [x] Implementasikan `actualKWh` pada periode billing.
- [x] Implementasikan `averageKWhPerDay` dengan `actualKWh / elapsedDays`.
- [x] Implementasikan `projectedPeriodKWh`.
- [x] Implementasikan `projectedPeriodCost` menggunakan tarif device terkait.
- [x] Tangani `elapsedDays <= 0` tanpa pembagian dengan nol.
- [x] Tangani periode yang sudah selesai dengan konsumsi aktual; clamping membuat proyeksi sama dengan aktual saat `elapsedDays == periodDays`.
- [x] Pertahankan aturan pembulatan finansial existing.
- [x] Pastikan tarif fallback hanya digunakan sesuai aturan yang sudah berlaku.
- [x] Pastikan WBP/LWBP tetap memakai settings device terkait.
- [x] Pastikan budget dan actual cost memakai periode billing yang sama dengan projection pada endpoint per-device dan batch.
- [x] MoM/YoY tetap memakai bulan kalender; cutoff hanya berlaku untuk konsumsi/biaya/proyeksi billing aktif.

Catatan implementasi: endpoint aggregate kalender lama tetap dipertahankan. Endpoint batch dan endpoint per-device menghitung billing secara per-device, dengan data `DailyEnergy` lintas bulan sesuai interval `[periodStart, periodEnd)` dan penggantian data hari berjalan oleh `HourlyEnergy` bila tersedia.

## 8. Perubahan Response API

- [x] Tambahkan metadata periode billing ke response per-device.
- [x] Tambahkan `configuredCutoffDay`, `effectiveCutoffDate`, `periodDays`, `elapsedDays`, dan `daysRemaining`.
- [x] Tambahkan `cutoffResolutionStatus`.
- [x] Tambahkan `projectedPeriodKWh` dan `projectedPeriodCost`.
- [x] Pertahankan field legacy `projectedMonthKWh` dan `projectedCost` untuk kompatibilitas.
- [x] Dokumentasikan kontrak response dan formula pada `FINANCIAL_CALCULATIONS.md`.
- [x] Pastikan endpoint per-device menggunakan helper periode; aggregate existing sengaja dipertahankan pada perilaku kalender.
- [ ] Uji response untuk device cutoff 25, 28, dan 31 pada database/staging.

## 9. Agregasi “Semua Panel”

- [ ] Pastikan aggregate mengambil hasil finansial dari setiap device.
- [ ] Pastikan aggregate tidak memakai cutoff device pertama.
- [ ] Pastikan aggregate tidak memakai tarif device pertama.
- [ ] Jumlahkan `projectedPeriodKWh` per-device.
- [ ] Jumlahkan `projectedPeriodCost` per-device.
- [ ] Pertahankan perhitungan ulang persentase aggregate sesuai kebutuhan.
- [ ] Tambahkan label bahwa periode dihitung per-device.
- [ ] Jangan menampilkan satu cutoff sebagai cutoff global.
- [ ] Jika diperlukan, tampilkan distribusi jumlah device berdasarkan cutoff.
- [ ] Uji dua device dengan cutoff berbeda.
- [ ] Uji device tanpa settings bersama device yang memiliki settings.

## 10. UI Settings Per Device

- [x] Tambahkan input `Tanggal Cutoff Meter PLN` pada `_DeviceSettingsTab.cshtml`.
- [x] Gunakan pilihan tanggal 25–31 ketika cutoff aktif.
- [x] Tampilkan cutoff nonaktif sebagai default untuk device lama.
- [x] Tampilkan keterangan mode default tanggal 1 sampai akhir bulan.
- [x] Tambahkan help text dan validasi client-side.
- [x] Pastikan nilai cutoff terbaca dan terkirim saat save, termasuk bulk save.

## 11. UI Usage Statistics dan Details

- [x] Tampilkan periode billing aktif.
- [x] Tampilkan tanggal cutoff device hanya jika cutoff aktif.
- [x] Tampilkan konsumsi aktual, estimasi sampai cutoff, dan sisa hari.
- [x] Tampilkan label periode bulan kalender penuh untuk device default/nonaktif.
- [x] Bedakan label period-based dari label month-based.
- [x] Pertahankan tampilan lama selama field legacy masih digunakan.
- [ ] Uji device sebelum cutoff.
- [ ] Uji device tepat pada cutoff.
- [ ] Uji device setelah cutoff.

## 12. Testing

**Status validasi saat ini:** compile-only berhasil (`EXIT=0`) dan `git diff --check` bersih. Smoke test helper belum berhasil direkam karena kendala shell/terminal saat membuat project sementara; ini bukan error source aplikasi. Build normal dan migration database masih perlu divalidasi setelah proses yang mengunci `KWHMonitoring.dll` dihentikan.

### 12.1 Unit test konfigurasi

- [ ] Cutoff 25 diterima.
- [ ] Cutoff 26–31 diterima.
- [ ] Cutoff 1–24 ditolak atau dinormalisasi sesuai keputusan.
- [ ] Cutoff 32 ditolak atau dinormalisasi sesuai keputusan.
- [ ] Nilai null/kosong memakai periode bulan kalender penuh.
- [ ] Device lama tanpa settings memakai periode bulan kalender penuh.
- [ ] Device baru tanpa konfigurasi memakai periode bulan kalender penuh.

### 12.2 Unit test kalender

- [ ] Bulan 31 hari.
- [ ] Bulan 30 hari.
- [ ] Februari tahun biasa.
- [ ] Februari tahun kabisat.
- [ ] Cutoff 31 pada bulan 30 hari.
- [ ] Pergantian periode pada tengah malam.
- [ ] Timestamp tepat di `periodStart` masuk perhitungan.
- [ ] Timestamp tepat di `periodEnd` tidak masuk perhitungan.

### 12.3 Unit test formula

- [ ] Actual kWh dihitung dari interval yang benar.
- [ ] Proyeksi tidak membagi nol.
- [ ] Proyeksi periode selesai memakai aktual.
- [ ] Tarif per-device digunakan.
- [ ] Pembulatan biaya konsisten.
- [ ] Data lintas bulan tidak hilang.
- [ ] Data kosong menghasilkan status yang jelas.

### 12.4 Integration dan regression test

- [ ] GET device settings.
- [ ] POST/save device settings.
- [ ] Endpoint usage statistics per-device.
- [ ] Endpoint usage statistics aggregate.
- [ ] WBP/LWBP tidak berubah secara tidak sengaja.
- [ ] Budget tidak berubah secara tidak sengaja.
- [ ] Waste dan anomaly cost impact tidak berubah secara tidak sengaja.
- [ ] MoM/YoY sesuai keputusan bisnis.
- [ ] Frontend lama tetap dapat membaca response.
- [x] Compile project berhasil dengan target compile (`EXIT=0`).
- [ ] Build project normal berhasil setelah file `KWHMonitoring.dll` tidak lagi terkunci.
- [ ] Migration berhasil pada database staging.

## 13. Verifikasi dengan Data PLN

- [ ] Pilih minimal tiga device dengan cutoff berbeda.
- [ ] Ambil tanggal pencatatan meter PLN aktual untuk setiap device.
- [ ] Bandingkan periode PLN dengan `periodStart` dan `periodEnd` aplikasi.
- [ ] Bandingkan kWh aplikasi dengan kWh pada pencatatan PLN.
- [ ] Bandingkan biaya estimasi dengan tagihan/rekap PLN.
- [ ] Catat selisih dan penyebabnya.
- [ ] Dapatkan persetujuan pemilik bisnis atas toleransi selisih.
- [ ] Simpan hasil validasi sebagai bukti uji penerimaan.

## 14. Staging dan Rollout

**Status:** Belum dilakukan pada environment staging/production.

- [ ] Deploy migration ke staging.
- [ ] Isi atau verifikasi cutoff setiap device di staging.
- [ ] Jalankan smoke test API.
- [ ] Jalankan smoke test halaman settings.
- [ ] Jalankan smoke test Usage Statistics dan Details.
- [ ] Bandingkan hasil lama dan baru untuk periode yang sama.
- [ ] Pastikan tidak ada error query atau timeout akibat filter periode.
- [ ] Pastikan cache settings tidak menyajikan cutoff lama.
- [ ] Siapkan feature flag atau opsi rollback bila diperlukan.
- [ ] Rilis ke production secara bertahap.
- [ ] Pantau error log dan hasil estimasi setelah rilis.

## 15. Dokumentasi Setelah Implementasi

- [x] Update `FINANCIAL_CALCULATIONS.md`.
- [x] Update dokumentasi API melalui kontrak response pada `FINANCIAL_CALCULATIONS.md` dan source endpoint.
- [x] Update dokumentasi settings per device pada rencana implementasi dan UI help text.
- [x] Update changelog project.
- [x] Catat migration yang digunakan: `20261012085233_AddBillingCutoffToDeviceSettings`.
- [x] Catat formula final dan aturan fallback.
- [x] Catat keputusan bahwa `projectedMonthKWh` dan `projectedCost` dipertahankan sebagai field legacy additive.
- [ ] Catat hasil validasi dengan data PLN.

## 16. Definition of Done

- [x] Cutoff dapat dikonfigurasi per device.
- [x] Default device lama tetap periode bulan kalender penuh.
- [x] Device tanpa cutoff aktif tetap dihitung dari tanggal 1 sampai hari terakhir bulan.
- [x] Periode billing berbasis cutoff hanya digunakan untuk device yang mengaktifkannya.
- [x] Periode lintas bulan kalender pada mode cutoff diimplementasikan.
- [x] Estimasi kWh dan biaya menggunakan tarif device terkait pada jalur per-device.
- [x] Aggregate existing tetap memakai perilaku kalender dan tidak diubah secara tidak sengaja.
- [x] UI menampilkan cutoff dan periode aktif.
- [x] Field fallback/legacy tetap dipertahankan untuk kompatibilitas.
- [~] Migration database, build normal, unit test executable, integration test, dan regression test masih memerlukan validasi environment.
- [ ] Hasil sudah dibandingkan dengan pencatatan PLN nyata.
- [ ] Dokumentasi final sudah diperbarui.
- [~] Tidak ada perubahan tidak disengaja teridentifikasi; review final dan validasi staging masih terbuka.
