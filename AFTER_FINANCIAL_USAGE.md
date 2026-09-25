# Financial-Grade Usage Statistics — Implementation Record

**Date:** 2026-09-23
**Scope:** Upgrade Usage Statistics dari dashboard operasional ke dashboard finansial

---

## Summary

Mengimplementasikan 10 fitur finansial (P1/P2/P3) di Usage Statistics tanpa mengubah fitur dan fungsi yang sudah running. Semua perubahan bersifat **additive only** — tidak ada existing field/response yang diubah.

---

## Files Changed

| File | Change Type |
|---|---|
| `Models/DeviceSettings.cs` | Added 6 new fields |
| `Models/ApplicationDbContext.cs` | Added EF Core mapping for 6 new fields |
| `Services/DeviceSettingsService.cs` | Added defaults + SaveAsync for new fields |
| `Controllers/ApiController.cs` | Added financial helpers + extended 2 endpoints + extended request models |
| `Views/Monitoring/_DeviceSettingsTab.cshtml` | Added 6 new form fields per device |
| `Views/Monitoring/UsageStatistics.cshtml` | Redesigned financial UI: 2 doughnut charts + compact metric grid + anomaly card |
| `Views/Monitoring/Details.cshtml` | Redesigned financial UI (same pattern with device-prefixed IDs) |
| `wwwroot/css/site.css` | Added `.fin-chart-body`, `.fin-metric-grid`, `.fin-metric` etc. + responsive rules |
| `Migrations/20260923120000_AddFinancialFieldsToDeviceSettings.cs` | New migration |

---

## New DeviceSettings Fields

| Field | Type | Default | Purpose |
|---|---|---|---|
| `TariffWBP` | decimal(18,2) | 0 | Tarif Waktu Beban Puncak (Rp/kWh). 0 = tidak dikonfigurasi |
| `TariffLWBP` | decimal(18,2) | 0 | Tarif Luar Waktu Beban Puncak (Rp/kWh). 0 = tidak dikonfigurasi |
| `WbpStartHour` | int | 18 | Jam mulai WBP (0-23) |
| `WbpEndHour` | int | 22 | Jam selesai WBP (0-23) |
| `BudgetKWh` | decimal(18,2) | 0 | Budget kWh bulanan. 0 = tidak di-set |
| `SurfaceArea` | decimal(18,2) | 0 | Luas permukaan media (m²). 0 = N/A |

---

## New API Response Fields

Both `POST /api/Api/usage-statistics` and `POST /api/Api/usage-statistics/{deviceKey}` now return 8 additional objects:

### 1. `wbpLwbp` — WBP/LWBP Tariff Split
```json
{
  "configured": true/false,
  "wbpKWh": 12.5, "lwbpKWh": 38.2,
  "wbpCost": 29375, "lwbpCost": 57300,
  "totalCostWBP": 86675, "wbpRatio": 24.6,
  "tariffWBP": 2350, "tariffLWBP": 1500,
  "wbpStart": 18, "wbpEnd": 22
}
```
- Jika `configured = false`: tarif flat digunakan untuk kedua slot
- WBP/LWBP split menggunakan `HourlyEnergy` data per jam

### 2. `waste` — Waste Detection (Downtime Consumption)
```json
{
  "configured": true/false,
  "wasteKWh": 3.2, "wasteCost": 4800,
  "wastePercent": 6.4,
  "downtimeStart": "22:00", "downtimeEnd": "06:00"
}
```
- Cross-reference `HourlyEnergy` + `DeviceSettings.DowntimeStart/End`
- Jika downtime tidak aktif: `configured = false`

### 3. `budget` — Budget vs Actual
```json
{
  "configured": true/false,
  "budgetKWh": 500, "actualKWh": 480.5,
  "variance": -19.5, "variancePercent": -3.9,
  "budgetCost": 750000, "actualCost": 720750
}
```
- Variance negatif = hemat, positif = over-budget
- Jika `BudgetKWh = 0`: `configured = false`

### 4. `periodComparison` — MoM/YoY
```json
{
  "lastMonthKWh": 450, "momChange": 30.5, "momChangePercent": 6.8,
  "lastYearSameMonthKWh": 420, "yoyChange": 60.5, "yoyChangePercent": 14.4
}
```
- Data dari `MonthlyEnergy` table

### 5. `billProjection` — End of Month Projection
```json
{
  "projectedMonthKWh": 520.3, "projectedCost": 780450,
  "daysElapsed": 15, "daysRemaining": 15
}
```
- Formula: `projectedMonthKWh = (currentMonthKWh / daysElapsed) × totalDaysInMonth`

### 6. `anomalyCostImpact` — Anomaly Cost Impact
```json
{
  "totalAnomalies": 5, "estimatedExcessKWh": 2.8,
  "estimatedCostImpact": 4200,
  "topAnomalies": [
    { "deviceKey": "...", "anomalyType": "OVERLOAD", "powerValue": 25000, "thresholdValue": 20000, "deviation": 25, "severity": "high", "detectedTime": "...", "estimatedCost": 1875 }
  ]
}
```
- Estimasi: `(PowerValue - ThresholdValue) × 0.25h / 1000 × tarif`
- 0.25 jam = asumsi rata-rata durasi anomali ~15 menit

### 7. `loadFactorInfo` — Load Factor
```json
{
  "configured": true/false,
  "loadFactor": 45.2, "maxCapacity": 30000, "status": "Optimal"
}
```
- Formula: `Load Factor = Actual kWh / (MaxCapacity kW × operating hours) × 100%`
- Status: <30% Under-utilized, 30-80% Optimal, >80% High Risk

### 8. `unitEconomics` — Unit Economics
```json
{
  "configured": true/false,
  "costPerM2": 85000, "costPerHour": 3125,
  "surfaceArea": 12.5, "totalCost": 720750
}
```
- `costPerM2` = totalCost / surfaceArea (jika surfaceArea > 0)
- `costPerHour` = totalCost / (daysElapsed × 24)

---

## New Settings UI Fields

Di Per-Device Settings tab (Settings → Per-Device):

| Label | Field | Type | Placeholder |
|---|---|---|---|
| Tarif WBP (Rp) | TariffWBP | number | 0 = flat |
| Tarif LWBP (Rp) | TariffLWBP | number | 0 = flat |
| WBP Start | WbpStartHour | select 0-23 | — |
| WBP End | WbpEndHour | select 0-23 | — |
| Budget kWh/bln | BudgetKWh | number | 0 = nonaktif |
| Luas (m²) | SurfaceArea | number | 0 = N/A |

---

## Financial UI Sections (UsageStatistics & Details)

Redesigned dari 11 card (6 section) menjadi 3 compact section dengan chart:

1. **🍩 Financial Charts Row** (2 card sejajar) — Doughnut chart WBP/LWBP Split + Doughnut chart Budget vs Actual
   - WBP/LWBP: doughnut 2 segment (indigo/green) dengan center text total kWh + rasio
   - Budget: doughnut 2 segment (terpakai/sisa) dengan center text persentase, warna adaptif (hijau <80%, kuning 80-100%, merah >100%)
   - Jika belum dikonfigurasi: canvas hidden, tampil pesan "Belum dikonfigurasi"
2. **📊 Financial Summary** (1 card, 6 metric compact grid 3×2) — Waste, MoM, YoY, Bill Projection, Load Factor, Unit Economics
   - Masing-masing metric: label icon, value (color-coded), sub-line detail
   - "Belum dikonfigurasi" ditampilkan inline di sub-line (bukan card terpisah)
3. **⚠️ Dampak Biaya Anomali** (1 card) — Total dampak + daftar top 5 anomali

Chart.js center-text plugin (id: `centerText`) didaftarkan sekali per halaman menggunakan `window._centerTextPluginRegistered` guard untuk mencegah duplikasi.

### Client-Side Financial Aggregation (UsageStatistics)

Dashboard utama (UsageStatistics) **tidak menggunakan financial data dari endpoint aggregate**. Sebaliknya, ia menghitung financial data secara dinamis dari perhitungan setiap device:

1. **Saat "Semua Panel" dipilih**: `fetchAndAggregateDeviceFinancialData()` memanggil per-device endpoint untuk setiap device secara paralel, lalu `aggregateFinancialData()` menggabungkan hasilnya:
   - **WBP/LWBP**: Sum kWh & cost dari devices yang dikonfigurasi. WBP ratio dihitung ulang dari total.
   - **Waste**: Sum kWh & cost. Waste percent dihitung ulang dari total konsumsi.
   - **Budget**: Sum budgetKWh, actualKWh, budgetCost, actualCost. Variance & percent dihitung ulang.
   - **Period Comparison**: Sum lastMonthKWh, momChange, lastYearSameMonthKWh, yoyChange. Percent dihitung ulang.
   - **Bill Projection**: Sum projectedMonthKWh & projectedCost per device (masing-masing pakai tarif sendiri).
   - **Anomaly Cost Impact**: Sum total & merge top anomalies, sort by cost, ambil top 5.
   - **Load Factor**: Dihitung ulang dari total kWh / (total maxCapacity kVA × hours). Bukan rata-rata.
   - **Unit Economics**: costPerM2 = totalCost / totalSurfaceArea, costPerHour = totalCost / (days × 24).
2. **Saat device tertentu dipilih**: Langsung gunakan financial data dari per-device response.
3. **Throttle**: Agregasi financial hanya di-refresh setiap 60 detik saat auto-refresh (10 detik). Manual interactions (filter, date change, refresh button) selalu trigger financial refresh.

### CSS Classes Baru
- `.fin-chart-body` — container doughnut chart (height: 220px)
- `.fin-metric-grid` — 3-column CSS grid untuk compact metrics
- `.fin-metric` — individual metric block (centered, bordered)
- `.fin-metric-label`, `.fin-metric-value`, `.fin-metric-sub` — metric typography
- Responsive: `.fin-metric-grid` → 2-column pada ≤768px

---

## Backward Compatibility

- Semua field baru default ke 0/off — jika tidak dikonfigurasi, sistem tetap berfungsi seperti sebelumnya
- Response API **100% backward compatible** — semua existing field tetap ada
- WBP/LWBP: jika kedua tarif = 0, sistem menggunakan flat tariff (sama seperti sebelumnya)
- Budget: jika BudgetKWh = 0, section budget menampilkan "Belum dikonfigurasi"
- SurfaceArea: jika = 0, unit economics menampilkan "N/A"
- Frontend gracefully handles missing/undefined data

---

## Migration

File: `Migrations/20260923120000_AddFinancialFieldsToDeviceSettings.cs`

Menambahkan 6 kolom ke tabel `DeviceSettings`:
- `TariffWBP` decimal(18,2) DEFAULT 0
- `TariffLWBP` decimal(18,2) DEFAULT 0
- `WbpStartHour` int DEFAULT 18
- `WbpEndHour` int DEFAULT 22
- `BudgetKWh` decimal(18,2) DEFAULT 0
- `SurfaceArea` decimal(18,2) DEFAULT 0

**Migration harus dijalankan manual** setelah restart server.

---

## Build Status

✅ C# compilation: 0 errors (DLL lock MSB3027 hanya karena server running)
⚠️ Server perlu restart untuk apply perubahan biner
⚠️ Migration perlu dijalankan: `dotnet ef database update`
