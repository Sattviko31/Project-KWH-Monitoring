# Catatan Perubahan Sesi — Monitoring Panel dan Perbandingan Tagihan

**Tanggal:** 2026-10-08  
**Status:** ✅ Perubahan kode selesai dan build berhasil  
**Proyek:** KWHMonitoring — .NET Core 2.1

## Ringkasan

Perubahan pada sesi ini mencakup dua kelompok utama:

1. Penataan ulang panel monitoring agar lebih proporsional, ringkas, dan mendekati layout referensi.
2. Penyesuaian perbandingan tagihan ERP, validasi tegangan, serta penyempurnaan label dan tampilan pada halaman monitoring.

## Daftar File yang Berubah

- `Controllers/ApiController.cs`
- `Models/KWHData.cs`
- `Views/Monitoring/Details.cshtml`
- `Views/Monitoring/Index.cshtml`
- `Views/Monitoring/UsageStatistics.cshtml`
- `Views/Monitoring/_PanelCard.cshtml`
- `wwwroot/css/site.css`

## Perubahan Backend/API

### `Controllers/ApiController.cs`

- Periode pembayaran ERP diselaraskan dengan periode konsumsi/tagihan monitoring.
- Saat mengambil riwayat billing ERP, rentang periode digeser satu bulan menggunakan `AddMonths(1)`.
- Pencocokan setiap periode monitoring sekarang menggunakan `expectedPaymentPeriod`, yaitu periode konsumsi ditambah satu bulan.
- Pencocokan tetap menggunakan tahun dan bulan serta pemilihan record terakhir berdasarkan `TagihanListrikID` terbesar jika terdapat lebih dari satu record.

### `Models/KWHData.cs`

- Validasi status tegangan diperketat untuk mendeteksi tegangan terlalu tinggi maupun terlalu rendah.
- Status `danger` berlaku jika:
  - tegangan di bawah 200 V;
  - tegangan di atas 240 V; atau
  - arus di atas 80 A.
- Status `warning` berlaku jika:
  - tegangan di bawah 210 V;
  - tegangan di atas 230 V; atau
  - arus di atas 70 A.
- Nilai di luar kondisi tersebut tetap menghasilkan status `success`.

## Perubahan Panel Monitoring

### `Views/Monitoring/_PanelCard.cshtml`

- Markup metrik panel disusun ulang menjadi kelompok:
  - `.panel-primary-metrics` untuk metrik realtime utama yang selalu terlihat;
  - `.panel-secondary-metrics` untuk metrik tambahan di dalam area collapse.
- Kartu daya utama dan matriks fase dibuat lebih seimbang secara horizontal.
- Layout bagian atas menggunakan `.panel-reference-topline`.
- Nilai beban dihitung dengan perlindungan ketika `MaxCapacity` bernilai nol.
- Warna gauge beban mengikuti threshold normal dan medium perangkat.
- Matriks fase dipisahkan secara jelas menjadi pasangan tegangan dan arus:
  - `VR / AR`;
  - `VS / AS`;
  - `VT / AT`.
- Penanganan 1-fase dan 3-fase tetap dipertahankan.
- Badge rata-rata tegangan dan arus diringkas menggunakan `.panel-average-row`.
- Energi aktif dan energi satu bulan ditempatkan dalam `.panel-energy-row`.
- Frekuensi ditempatkan pada `.frequency-highlight`.
- Metrik berikut tetap berada di dalam area collapse:
  - Cos φ / power factor;
  - energi aktif;
  - energi satu bulan;
  - frekuensi;
  - kontrol perangkat.
- Selector realtime dipertahankan, termasuk:
  - `data-value`;
  - `data-label`;
  - `data-gauge`;
  - `data-volt-badge`;
  - `data-amp-badge`.
- Selector realtime `data-value="energi-bulanan"` dipertahankan/ditambahkan untuk pembaruan energi bulanan.
- Kontras teks pada badge warning dan elemen berwarna diperbaiki agar tetap terbaca.
- Empty spacer `<div>` dan whitespace yang tidak diperlukan dihapus.
- Struktur tag `<div>` diverifikasi seimbang.

### `Views/Monitoring/Index.cshtml`

- Perubahan kecil pada halaman panel monitoring untuk mendukung layout dan struktur panel terbaru.
- Integrasi halaman monitoring tetap menggunakan partial `_PanelCard`.

## Perubahan CSS

### `wwwroot/css/site.css`

- Menambahkan aturan layout proporsional untuk panel monitoring.
- Tinggi dan padding dikurangi pada beberapa komponen agar kartu tidak terlalu tinggi:
  - power highlight;
  - phase pill/matrix;
  - average badge;
  - progress gauge;
  - kartu energi;
  - kartu frekuensi.
- Menambahkan aturan grid/flex untuk:
  - `.panel-reference-topline`;
  - `.phase-matrix`;
  - `.panel-average-row`;
  - `.panel-secondary-metrics`;
  - `.panel-energy-row`.
- Menambahkan gaya khusus untuk pasangan metrik fase, termasuk label, nilai, unit, dan overflow text.
- Menambahkan kontras teks khusus pada:
  - phase matrix;
  - average badge;
  - label/nilai frequency highlight.
- Menambahkan media query `max-width: 575.98px` untuk layar kecil:
  - mengubah bagian referensi menjadi satu kolom;
  - mengurangi tinggi kartu utama;
  - mengecilkan font label/nilai;
  - menyesuaikan tinggi phase cell, average badge, energy card, dan frequency card.
- Warna teks pada elemen `bg-warning` disesuaikan agar tetap kontras.

## Perubahan Halaman Detail

### `Views/Monitoring/Details.cshtml`

- Penyempurnaan tampilan dan keterangan pada bagian perbandingan billing ERP.
- Penyesuaian markup agar istilah dan informasi tagihan lebih konsisten dengan perubahan periode pembayaran.

## Perubahan Usage Statistics

### `Views/Monitoring/UsageStatistics.cshtml`

- Label tampilan `Perbandingan ERP` diubah menjadi `Perbandingan Tagihan`.
- Indikator loading ranking diberi ikon spinner Font Awesome.
- Label tabel diubah agar lebih jelas:
  - `Tagihan ERP` menjadi `Tagihan Aktual`;
  - `Bukti Periode ERP` menjadi `Tanggal Pembayaran`;
  - `Perubahan Biaya` menjadi `Selisih Tagihan`.
- Elemen ringkasan rata-rata yang tidak lagi digunakan dihapus.
- Cache key perbandingan diubah dengan versi `comparison.v2` agar hasil lama tidak tercampur dengan format data baru.
- Logika pembuatan ringkasan rata-rata lama dihapus karena tidak lagi sesuai dengan tampilan tabel yang baru.

## Validasi yang Dilakukan

### Pemeriksaan Struktur

- `_PanelCard.cshtml`:
  - pembukaan `<div>`: 53;
  - penutupan `</div>`: 53.
- `site.css`:
  - jumlah `{`: 1212;
  - jumlah `}`: 1212.
- Empty `<div>` dan whitespace yang tidak diperlukan pada bagian yang diperiksa telah dibersihkan.
- `git diff --check` tidak menemukan whitespace error.

### Pemeriksaan Selector Realtime

Selector berikut masih tersedia pada `_PanelCard.cshtml`:

- `data-value="power"`;
- `data-value="cosphi"`;
- `data-value="energi-aktif"`;
- `data-value="energi-bulanan"`;
- `data-value="freq"`;
- `data-label="voltage-avg"`;
- `data-label="current-avg"`;
- `data-label="load-value"`;
- `data-gauge="load"`;
- `data-gauge="power-factor"`;
- `data-volt-badge`;
- `data-amp-badge`.

### Build

Build dilakukan ke output alternatif untuk menghindari konflik dengan DLL aplikasi yang sedang berjalan.

Log build:

`C:\Users\viko\AppData\Local\Temp\kwh-proportional-build4.log`

Hasil:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed: 00:00:08.42
```

## Catatan Validasi Visual

- Validasi kode dan build telah selesai.
- Render visual langsung pada browser desktop dan mobile belum dijalankan secara otomatis pada sesi ini.
- Pemeriksaan lanjutan yang disarankan:
  1. buka panel pada lebar desktop;
  2. buka panel pada lebar mobile/narrow;
  3. periksa kartu 1-fase dan 3-fase;
  4. buka/tutup collapse pada beberapa device;
  5. pastikan pembaruan realtime daya, energi bulanan, gauge, badge tegangan, dan badge arus tetap berjalan;
  6. pastikan warna teks pada status warning tetap terbaca.

## Ringkasan Status Git Saat Dokumentasi Dibuat

Perubahan yang terdokumentasi pada sesi ini terdapat pada tujuh file kode di atas. File catatan ini ditambahkan sebagai dokumentasi perubahan sesi dan tidak menggantikan `CHANGELOG.md` atau catatan sesi sebelumnya.