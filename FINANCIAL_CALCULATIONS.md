# Financial Calculations — Reference Document

**Project:** KWHMonitoring v7  
**Last Updated:** 2025-07-13  
**Scope:** Semua perhitungan financial di Usage Statistics (server-side + client-side)

---

## Daftar Isi

1. [Konstanta & Threshold](#1-konstanta--threshold)
2. [DeviceSettings — Field Financial](#2-devicesettings--field-financial)
3. [Helper Methods (Server-Side)](#3-helper-methods-server-side)
4. [Perhitungan Per-Device (Server-Side)](#4-perhitungan-per-device-server-side)
5. [Perhitungan Aggregate (Server-Side)](#5-perhitungan-aggregate-server-side)
6. [Agregasi Client-Side (Dashboard Utama)](#6-agregasi-client-side-dashboard-utama)
7. [Threshold Highlight (UI)](#7-threshold-highlight-ui)
8. [Perbandingan 3 Sumber Perhitungan](#8-perbandingan-3-sumber-perhitungan)

---

## 1. Konstanta & Threshold

### Konstanta Server-Side (C#)

| Konstanta | Nilai | Digunakan Di |
|---|---|---|
| Default Tariff | `1500m` Rp/kWh | `GetTariffPerKWh()` fallback, `CalculateWbpLwbp` flatTariff, `CalculateWaste` tariff |
| Durasi Anomali | `0.25m` jam (15 menit) | Estimasi excess kWh per anomali |
| Watt → kWh | `/ 1000m` | Anomali excess kWh, Load Factor (W → kW) |
| Load Factor cap | `100m` (%) | Dibatasi maks 100% |

### Threshold UI (Client-Side)

| Metric | Threshold | Klasifikasi | CSS Class |
|---|---|---|---|
| Waste | `> 5%` | Bahaya | `fin-critical` + `text-danger` |
| | `≤ 5%` | Aman | `fin-ok` + `text-success` |
| MoM Change | `|x| > 20%` | Kritis | `fin-critical` |
| | `|x| > 10%` | Perhatian | `fin-warning` |
| | Penurunan | Baik | `fin-ok` |
| YoY Change | `|x| > 50%` | Kritis | `fin-critical` |
| | `|x| > 20%` | Perhatian | `fin-warning` |
| | Penurunan | Baik | `fin-ok` |
| Load Factor | `< 30%` | Under-utilized | `fin-warning` + `text-warning` |
| | `30% – 80%` | Optimal | `fin-ok` + `text-success` |
| | `> 80%` | Risiko Tinggi | `fin-critical` + `text-danger` |
| Budget | `> 100%` | Over Budget | Header `bg-danger`, warna merah |
| | `80% – 100%` | Mendekati | Header amber `#e0a800`, warna kuning |
| | `< 80%` | Aman | Header `bg-success`, warna hijau |
| Bill Projection | `daysRemaining ≤ 5` | Mendekati akhir bulan | `fin-warning` |

---

## 2. DeviceSettings — Field Financial

| Field | Tipe | Default | Keterangan |
|---|---|---|---|
| `TariffPerKWh` | `decimal(18,2)` | `0` → fallback 1500 | Tarif flat Rp/kWh |
| `TariffWBP` | `decimal(18,2)` | `0` | Tarif Waktu Beban Puncak Rp/kWh |
| `TariffLWBP` | `decimal(18,2)` | `0` | Tarif Luar Waktu Beban Puncak Rp/kWh |
| `WbpStartHour` | `int` | `18` | Jam mulai WBP (0-23) |
| `WbpEndHour` | `int` | `22` | Jam selesai WBP (0-23) |
| `BudgetKWh` | `decimal(18,2)` | `0` | Budget kWh bulanan. 0 = nonaktif |
| `SurfaceArea` | `decimal(18,2)` | `0` | Luas permukaan media (m²). 0 = N/A |
| `MaxCapacity` | `decimal(18,2)` | `0` | Kapasitas maksimal (Watt). 0 = N/A |
| `DowntimeEnabled` | `bool` | `false` | Aktifkan deteksi waste |
| `DowntimeStart` | `TimeSpan` | `22:00` | Jam mulai downtime |
| `DowntimeEnd` | `TimeSpan` | `06:00` | Jam selesai downtime |

---

## 3. Helper Methods (Server-Side)

### 3a. GetTariffPerKWh

```
GetTariffPerKWh(string deviceKey = null) → decimal
```

```
JIKA deviceKey tidak null/empty:
    Cari DeviceSettings WHERE DeviceKey == deviceKey
    JIKA ditemukan DAN TariffPerKWh > 0:
        RETURN DeviceSettings.TariffPerKWh
RETURN 1500m
```

**Catatan penting:**
- Dipanggil **tanpa deviceKey** oleh endpoint aggregate → selalu return `1500m`
- Dipanggil **dengan deviceKey** oleh endpoint per-device → return tarif spesifik device

---

### 3b. CalculateWbpLwbp

```
CalculateWbpLwbp(decimal[] hourlyKwhByHour, DeviceSettings ds)
    → (wbpKWh, lwbpKWh, wbpCost, lwbpCost, totalCostWBP, wbpRatio, configured, tariffWBP, tariffLWBP)
```

**Step 1 — Resolve tarif:**

```
tariffWBP     = ds.TariffWBP > 0  ? ds.TariffWBP  : 0m
tariffLWBP    = ds.TariffLWBP > 0 ? ds.TariffLWBP : 0m
flatTariff    = ds.TariffPerKWh > 0 ? ds.TariffPerKWh : 1500m
useWbpSplit   = (tariffWBP > 0) AND (tariffLWBP > 0)
```

**Step 2 — Klasifikasi jam:**

```
UNTUK h = 0..23:
    JIKA wbpStart < wbpEnd (window hari sama, mis. 18-22):
        isWbp = (h >= wbpStart) AND (h < wbpEnd)
    LAINNYA (window overnight, mis. 22-06):
        isWbp = (h >= wbpStart) OR (h < wbpEnd)

    JIKA isWbp: wbpKWh += hourlyKwhByHour[h]
    LAINNYA:    lwbpKWh += hourlyKwhByHour[h]
```

**Step 3 — Hitung rasio:**

```
wbpKWh = Round(wbpKWh, 2)
lwbpKWh = Round(lwbpKWh, 2)
total  = Round(wbpKWh + lwbpKWh, 2)
ratio  = total > 0 ? Round(wbpKWh / total * 100, 1) : 0
```

**Step 4 — Hitung biaya (2 jalur):**

```
JIKA TIDAK useWbpSplit (tarif flat):
    wbpCost     = Round(wbpKWh  × flatTariff, 0)
    lwbpCost    = Round(lwbpKWh × flatTariff, 0)
    totalCostWBP = Round(total  × flatTariff, 0)
    configured  = false
    tariffWBP   = flatTariff
    tariffLWBP  = flatTariff

JIKA useWbpSplit (tarif differential):
    wbpCost     = Round(wbpKWh  × tariffWBP, 0)
    lwbpCost    = Round(lwbpKWh × tariffLWBP, 0)
    totalCostWBP = Round(wbpKWh × tariffWBP + lwbpKWh × tariffLWBP, 0)
    configured  = true
```

---

### 3c. CalculateWaste

```
CalculateWaste(decimal[] hourlyKwhByHour, DeviceSettings ds)
    → (wasteKWh, wasteCost, wastePercent, configured, dtStart, dtEnd)
```

**Early exit:**

```
JIKA BUKAN ds.DowntimeEnabled:
    RETURN (0, 0, 0, false, "", "")
```

**Step 1 — Resolve downtime & tarif:**

```
startH  = ds.DowntimeStart.Hours   // mis. 22
endH    = ds.DowntimeEnd.Hours     // mis. 6
tariff  = ds.TariffPerKWh > 0 ? ds.TariffPerKWh : 1500m
```

**Step 2 — Klasifikasi jam:**

```
UNTUK h = 0..23:
    JIKA startH < endH (window hari sama):
        isDt = (h >= startH) AND (h < endH)
    LAINNYA (window overnight):
        isDt = (h >= startH) OR (h < endH)

    totalKWh += hourlyKwhByHour[h]
    JIKA isDt DAN hourlyKwhByHour[h] > 0:
        wasteKWh += hourlyKwhByHour[h]
```

**Step 3 — Hitung hasil:**

```
wasteKWh     = Round(wasteKWh, 2)
wastePercent = totalKWh > 0 ? Round(wasteKWh / totalKWh × 100, 1) : 0
wasteCost    = Round(wasteKWh × tariff, 0)
dtStart      = Format("{0:D2}:00", startH)
dtEnd        = Format("{0:D2}:00", endH)
```

**Catatan:** Waste menggunakan tarif **flat** (`TariffPerKWh`), BUKAN tarif WBP/LWBP split.

---

## 4. Perhitungan Per-Device (Server-Side)

Endpoint: `POST /api/Api/usage-statistics/{deviceKey}`

**Sumber data:** Semua query menggunakan `DeviceKey == deviceKey`  
**Sumber tarif:** `GetTariffPerKWh(deviceKey)` → tarif per-device atau 1500  
**Sumber settings:** `GetEffectiveAsync(deviceKey)` → settings device ini

### 4a. WBP/LWBP Split

```
hourlyKwhByHour[24] ← hourlyData (data jam device ini)
wbpResult = CalculateWbpLwbp(hourlyKwhByHour, devSettings)
```

### 4b. Waste Detection

```
wasteResult = CalculateWaste(hourlyKwhByHour, devSettings)
```

### 4c. Budget vs Actual

```
budgetKWh       = devSettings.BudgetKWh
variance        = budgetKWh > 0 ? Round(monthKWh − budgetKWh, 2)     : 0
variancePercent = budgetKWh > 0 ? Round((monthKWh − budgetKWh) / budgetKWh × 100, 1) : 0
budgetCost      = budgetKWh > 0 ? Round(budgetKWh × tariffPerKWh, 0) : 0
actualCost      = Round(monthKWh × tariffPerKWh, 0)
```

**Konvensi variance:** `variance = actualKWh − budgetKWh`
- Positif = over-budget (melebihi batas)
- Negatif = hemat (di bawah budget)

### 4d. Period Comparison — MoM

```
lastMonthKWh     = MonthlyEnergy WHERE DeviceKey=deviceKey, Year=lastMonth.Year, Month=lastMonth.Month, Sum(EnergyKWh)
lastMonthKWh     = Round(lastMonthKWh, 2)
momChange        = Round(monthKWh − lastMonthKWh, 2)
momChangePercent = lastMonthKWh > 0 ? Round((monthKWh − lastMonthKWh) / lastMonthKWh × 100, 1)
                   : (monthKWh > 0 ? 100m : 0m)
```

### 4e. Period Comparison — YoY

```
lastYearSameMonthKWh = MonthlyEnergy WHERE DeviceKey=deviceKey, Year=(year−1), Month=sameMonth, Sum(EnergyKWh)
lastYearSameMonthKWh = Round(lastYearSameMonthKWh, 2)
yoyChange            = Round(monthKWh − lastYearSameMonthKWh, 2)
yoyChangePercent     = lastYearSameMonthKWh > 0 ? Round((monthKWh − lastYearSameMonthKWh) / lastYearSameMonthKWh × 100, 1)
                       : (monthKWh > 0 ? 100m : 0m)
```

**Edge case:** Jika periode pembanding = 0 dan bulan ini > 0 → return 100% (kenaikan dari nol).

### 4f. Bill Projection

```
dayOfMonth        = DateTime.Now.Day
daysInMonth       = DateTime.DaysInMonth(year, month)
projectedMonthKWh = dayOfMonth > 0 ? Round(monthKWh / dayOfMonth × daysInMonth, 2) : monthKWh
projectedCost     = Round(projectedMonthKWh × tariffPerKWh, 0)
daysRemaining     = daysInMonth − dayOfMonth
```

**Formula:** `proyeksi = (konsumsi saat ini / hari berjalan) × total hari bulan`

### 4g. Anomaly Cost Impact

```
overloadAnomalies = AnomalyLogs
    WHERE DeviceKey=deviceKey, DetectedTime dalam bulan ini, AnomalyType="OVERLOAD"
    ORDER BY Deviation DESC, TAKE 20

per anomali:
    excessKWh    = Max(PowerValue − ThresholdValue, 0) × 0.25 / 1000
    estimatedCost = Round(excessKWh × tariffPerKWh, 0)

total:
    anomalyExcessKWh    = Sum(excessKWh per anomali)
    anomalyExcessKWh    = Round(anomalyExcessKWh, 2)
    anomalyCostImpact   = Round(anomalyExcessKWh × tariffPerKWh, 0)
    topAnomalies        = overloadAnomalies.Take(5) dengan estimatedCost
```

**Konstanta:** `0.25` = asumsi durasi anomali 15 menit (0.25 jam)

### 4h. Load Factor

```
maxCap    = devSettings.MaxCapacity   // dalam Watt
JIKA maxCap > 0 DAN dayOfMonth > 0:
    opHours    = dayOfMonth × 24
    maxCap_kW  = maxCap / 1000
    loadFactor = Min(Round(monthKWh / (maxCap_kW × opHours) × 100, 1), 100)
    status     = loadFactor < 30 ? "Under-utilized"
               : loadFactor <= 80 ? "Optimal"
               : "High Risk"
LAINNYA:
    loadFactor = 0, status = "N/A"
```

**Formula:** `Load Factor = monthKWh / (maxCapacity_kW × operatingHours) × 100%`  
**Catatan:** API mengembalikan nilai 0-100 (persentase), BUKAN 0-1 desimal.  
**Dibatasi:** Maksimal 100%.

### 4i. Unit Economics

```
surfaceArea = devSettings.SurfaceArea
costPerM2   = surfaceArea > 0 ? Round(actualCost / surfaceArea, 0)     : 0
costPerHour = dayOfMonth > 0 ? Round(actualCost / (dayOfMonth × 24), 0) : 0
```

---

## 5. Perhitungan Aggregate (Server-Side)

Endpoint: `POST /api/Api/usage-statistics` (tanpa deviceKey)

### ⚠️ Masalah yang Diketahui

| Aspek | Masalah |
|---|---|
| **Tarif** | `GetTariffPerKWh()` tanpa deviceKey → **selalu return 1500m** |
| **Settings** | `representativeSettings` = settings **device pertama** dari dictionary |
| **Budget** | BudgetKWh dari device pertama saja |
| **WBP/LWBP** | Jam & tarif WBP dari device pertama saja |
| **Load Factor** | MaxCapacity dari device pertama saja |
| **Unit Economics** | SurfaceArea dari device pertama saja |

**Akibat:** Semua perhitungan biaya (estimatedCost, budgetCost, actualCost, projectedCost, anomalyCostImpact, costPerM2, costPerHour) menggunakan tarif yang salah jika ada device dengan tarif berbeda dari 1500. WBP/LWBP split menggunakan jam & tarif dari device pertama, bukan agregasi.

### Formula

Formula-nya **identik** dengan per-device (Section 4), dengan perbedaan:
- `tariffPerKWh` = selalu `1500m`
- `representativeSettings` menggantikan `devSettings`
- Query energy tanpa filter `DeviceKey` (aggregate across all devices)
- Query anomaly tanpa filter `DeviceKey`

---

## 6. Agregasi Client-Side (Dashboard Utama)

**Dipakai oleh:** UsageStatistics.cshtml saat "Semua Panel" dipilih  
**Fungsi:** `fetchAndAggregateDeviceFinancialData()` → `aggregateFinancialData()`  
**Sumber data:** Memanggil per-device endpoint untuk **setiap device** secara paralel

### Strategi Agregasi per Metric

| Metric | Strategi | Detail |
|---|---|---|
| **WBP/LWBP kWh & Cost** | **Sum** | Hanya dari device yang `configured=true` |
| **WBP tarif & jam** | **Last overwrite** | Ambil dari device terakhir yang configured (sama untuk semua PLN) |
| **WBP Ratio** | **Recalculate** | `wbpKWh / (wbpKWh + lwbpKWh) × 100` |
| **Waste kWh & Cost** | **Sum** | Hanya dari device yang `configured=true` |
| **Waste Percent** | **Recalculate** | `wasteKWh / totalConsumption × 100` |
| **Budget kWh** | **Sum** | Hanya dari device yang `configured=true` |
| **Actual kWh** | **Sum** | Dari **semua** device (bukan hanya configured) |
| **Budget Cost & Actual Cost** | **Sum** | Dari **semua** device |
| **Variance & Percent** | **Recalculate** | `actualKWh − budgetKWh`, `(actualKWh − budgetKWh) / budgetKWh × 100` |
| **MoM/YoY kWh & Change** | **Sum** | Jumlahkan nilai absolut dari setiap device |
| **MoM/YoY Percent** | **Recalculate** | `momChange / lastMonthKWh × 100`, `yoyChange / lastYearSameMonthKWh × 100` |
| **Bill Projection kWh & Cost** | **Sum** | Masing-masing device pakai tarif sendiri |
| **Anomaly** | **Sum & Merge** | Jumlahkan total; merge semua topAnomalies, sort by cost DESC, ambil top 5 |
| **Load Factor** | **Recalculate** | `lfTotalKWh / (lfTotalMaxCapKVA × daysElapsed × 24) × 100` |
| **Unit Economics** | **Recalculate** | `actualCost / totalSurfaceArea`, `actualCost / (daysElapsed × 24)` |

### Formula Detail — Client-Side Recalculation

```
totalConsumption = wbpKwh + lwbpKwh
JIKA totalConsumption == 0: totalConsumption = actualKWh

// Rasio WBP
wbpRatio = totalConsumption > 0
    ? Round(wbpKwh / totalConsumption × 100 × 10) / 10    // 1 desimal
    : 0

// Persentase Waste
wastePercent = totalConsumption > 0
    ? Round(wasteKwh / totalConsumption × 100 × 10) / 10
    : 0

// Budget Variance
budgetVariance = anyBudgetConfigured
    ? Round((actualKWh − budgetKWh) × 100) / 100          // 2 desimal
    : 0
budgetVariancePct = (anyBudgetConfigured AND budgetKWh > 0)
    ? Round((actualKWh − budgetKWh) / budgetKWh × 100 × 10) / 10
    : 0

// MoM Percent
momChangePercent = lastMonthKWh > 0
    ? Round(momChange / lastMonthKWh × 100 × 10) / 10
    : (actualKWh > 0 ? 100 : 0)

// YoY Percent
yoyChangePercent = lastYearSameMonthKWh > 0
    ? Round(yoyChange / lastYearSameMonthKWh × 100 × 10) / 10
    : (actualKWh > 0 ? 100 : 0)

// Load Factor — dihitung dari total, BUKAN rata-rata
loadFactor = (anyLFConfigured AND lfTotalMaxCapKVA > 0 AND daysElapsed > 0)
    ? Min(Round(lfTotalKWh / (lfTotalMaxCapKVA × daysElapsed × 24) × 100 × 10) / 10, 100)
    : 0
lfStatus = loadFactor > 80 ? "High Risk"
         : loadFactor < 30 ? "Under-utilized"
         : "Optimal"

// Unit Economics
costPerM2   = (anyUEConfigured AND totalSurfaceArea > 0) ? Round(actualCost / totalSurfaceArea) : 0
costPerHour = daysElapsed > 0 ? Round(actualCost / (daysElapsed × 24)) : 0
```

**Throttle:** Agregasi financial hanya di-refresh setiap 60 detik saat auto-refresh (10 detik). Manual interaction selalu trigger refresh.

---

## 7. Threshold Highlight (UI)

### WBP/LWBP Chart

| Kondisi | Tampilan |
|---|---|
| `configured = false` | Pesan "Belum dikonfigurasi" |
| `configured = true` tapi `total = 0` | Pesan "Tidak ada data konsumsi" |
| `configured = true` dan `total > 0` | Doughnut chart dengan center text |

**Warna chart:**
- WBP: `rgba(91,94,166,0.85)` (indigo)
- LWBP: `rgba(25,135,84,0.85)` (hijau)

### Budget Chart

| Kondisi | Header | Chart Color |
|---|---|---|
| `pct > 100` (over budget) | `bg-danger` (merah) | `rgba(220,53,69,0.85)` |
| `pct 80-100` (mendekati) | `#e0a800` (amber) | `rgba(255,193,7,0.85)` |
| `pct < 80` (aman) | `bg-success` (hijau) | `rgba(25,135,84,0.85)` |
| `budgetKWh = 0` (nonaktif) | `bg-success` | Pesan "Belum dikonfigurasi" |

### Financial Metrics Grid

| Metric | Value | Sub-line |
|---|---|---|
| Waste | `wastePercent.toFixed(1) + '%'` | kWh · Rp · downtime jam · batas 5% |
| MoM | `▲/▼ + abs(momChangePercent).toFixed(1) + '%'` | Naik/Turun X kWh (bln lalu: Y kWh) |
| YoY | `▲/▼ + abs(yoyChangePercent).toFixed(1) + '%'` | Naik/Turun X kWh (thn lalu: Y kWh) |
| Proyeksi | `projectedMonthKWh + ' kWh'` | Rp projectedCost · avgDaily kWh/hari · sisa X hari |
| Load Factor | `loadFactor.toFixed(1) + '%'` | Status · Max X kVA · server status |
| Unit Econ | `Rp costPerM2/m²` atau `Rp costPerHour/jam` | Rp/jam · Area: X m² |

### Anomaly Cost Impact

| Kondisi | Tampilan |
|---|---|
| Tidak ada anomali | Total cost + badge jumlah + "Tidak ada anomali aktif" |
| Ada anomali | Total cost + badge merah + daftar top 5 |
| Per baris anomali | Icon ↑/↓ + nama device + severity badge (HIGH/MED) + Rp cost |

---

## 8. Perbandingan 3 Sumber Perhitungan

| Aspek | Aggregate Endpoint | Per-Device Endpoint | Client-Side Aggregation |
|---|---|---|---|
| **Tarif** | Selalu 1500 (hardcoded) | Per-device dari DB | Sum dari per-device (masing-masing pakai tarif sendiri) |
| **Settings** | Device pertama (representative) | Device spesifik | Masing-masing device pakai settings sendiri |
| **WBP/LWBP** | Jam & tarif dari device pertama | Jam & tarif device ini | Sum kWh/cost dari device yang configured |
| **Budget** | BudgetKWh device pertama | BudgetKWh device ini | Sum budget dari device yang configured; sum actual dari semua |
| **Load Factor** | 1 MaxCapacity dari device pertama | 1 MaxCapacity dari device ini | Recalculate: totalKWh / (totalMaxCap_kVA × hours) |
| **Unit Economics** | 1 SurfaceArea dari device pertama | 1 SurfaceArea device ini | Recalculate: totalCost / totalSurfaceArea |
| **Anomaly** | Semua device, top 20 → 5 | 1 device, top 20 → 5 | Merge semua, re-sort, top 5 |
| **Akurasi** | ⚠️ Salah jika device settings berbeda | ✅ Benar | ✅ Benar (derived from per-device) |

---

## Appendix: Quick Reference — Semua Formula

```
// === TARIF ===
flatTariff = TariffPerKWh > 0 ? TariffPerKWh : 1500
wbpSplit   = TariffWBP > 0 AND TariffLWBP > 0

// === WBP/LWBP ===
wbpKWh     = Σ hourlyKwhByHour[h]  WHERE isWbp(h)
lwbpKWh    = Σ hourlyKwhByHour[h]  WHERE NOT isWbp(h)
wbpCost    = wbpKWh  × (wbpSplit ? TariffWBP  : flatTariff)
lwbpCost   = lwbpKWh × (wbpSplit ? TariffLWBP : flatTariff)
wbpRatio   = wbpKWh / (wbpKWh + lwbpKWh) × 100

// === WASTE ===
wasteKWh     = Σ hourlyKwhByHour[h]  WHERE isDowntime(h) AND kWh > 0
wastePercent = wasteKWh / totalKWh × 100
wasteCost    = wasteKWh × flatTariff

// === BUDGET ===
variance        = actualKWh − budgetKWh          // + = over, − = hemat
variancePercent = (actualKWh − budgetKWh) / budgetKWh × 100
budgetCost      = budgetKWh × tariffPerKWh
actualCost      = monthKWh × tariffPerKWh

// === MoM / YoY ===
momChange        = monthKWh − lastMonthKWh
momChangePercent = (monthKWh − lastMonthKWh) / lastMonthKWh × 100
yoyChange        = monthKWh − lastYearSameMonthKWh
yoyChangePercent = (monthKWh − lastYearSameMonthKWh) / lastYearSameMonthKWh × 100

// === BILL PROJECTION ===
projectedMonthKWh = monthKWh / dayOfMonth × daysInMonth
projectedCost     = projectedMonthKWh × tariffPerKWh

// === ANOMALY COST ===
excessKWh per anomaly = Max(PowerValue − ThresholdValue, 0) × 0.25 / 1000
estimatedCost         = excessKWh × tariffPerKWh

// === LOAD FACTOR ===
loadFactor = monthKWh / (maxCapacity_kW × dayOfMonth × 24) × 100
            = monthKWh / (maxCapacity_W / 1000 × dayOfMonth × 24) × 100
            // Capped at 100%

// === UNIT ECONOMICS ===
costPerM2   = actualCost / surfaceArea
costPerHour = actualCost / (dayOfMonth × 24)
```
