# Rencana Implementasi Billing Anomaly Action, Konfirmasi, dan Baseline Tren

> Status: Implementasi bertahap. Evaluator read-only, token confirmation, trend persistence, nominal budget fields, endpoint status/confirmation, and outbox worker have been implemented and build-verified. Database application remains deferred until the target and backup are explicitly verified.
>
> Dokumen ini melanjutkan deteksi billing V7 yang sudah tersedia. `BILLING_VARIANCE`, threshold global/per-device, field billing pada `AnomalyLog`, dan notifikasi langsung existing sudah ada. Workflow konfirmasi, candidate trend, baseline baru, token, dan outbox belum tersedia.

## 1. Tujuan dan batasan

Fitur akhir harus dapat:

1. Mendeteksi selisih estimasi terhadap aktual ERP.
2. Mendeteksi kenaikan aktual terhadap periode billing sebelumnya.
3. Membentuk satu anomaly event untuk device, periode, dan ERP billing yang sama.
4. Menyimpan anomaly secara idempotent.
5. Mengikuti anomaly lanjutan melalui satu candidate trend aktif.
6. Meminta keputusan pengguna melalui link konfirmasi yang aman.
7. Menghitung konfirmasi hanya dari POST yang valid.
8. Mengirim email dan WhatsApp melalui outbox, bukan dari endpoint tabel.
9. Mengaktifkan baseline baru setelah tiga konfirmasi valid dari tiga periode berturut-turut.
10. Menyimpan histori anomaly, konfirmasi, notifikasi, baseline, dan perubahan anggaran secara immutable.
11. Menggunakan nilai trend yang sudah diterima sebagai anggaran tagihan bulanan nominal per-device secara otomatis.

Di luar cakupan: perubahan anomaly perangkat existing, menganggap GET sebagai konfirmasi, menghapus histori, atau mengirim provider notification dari endpoint read-only.

## 2. Kondisi source V7 yang harus diperhitungkan

Komponen existing:

```text
Models/AnomalyLog.cs
Models/ErpBillingComparison.cs
Models/DeviceSettings.cs
Models/ApplicationDbContext.cs
Services/IBillingAnomalyDetectionService.cs
Services/BillingAnomalyDetectionService.cs
Services/NotificationService.cs
Services/AnomalyNotificationBackgroundService.cs
Controllers/ApiController.cs
Views/Monitoring/UsageStatistics.cshtml
Views/Monitoring/AnomalyLogs.cshtml
```

Temuan yang harus diselesaikan sebelum workflow:

- `BillingAnomalyDetectionService` masih dapat menyimpan dan mengirim notifikasi melalui `persistAnomaly`.
- Evaluasi dua subtype dapat menghasilkan dua row terpisah, sedangkan action plan memerlukan satu event gabungan.
- Notifikasi billing existing dikirim langsung, belum memakai outbox per channel.
- Belum ada model confirmation, trend profile, token billing, outbox, atau baseline history.
- `DeviceSettings.BudgetKWh` adalah anggaran energi bulanan dalam kWh dan tidak boleh dipakai sebagai anggaran nominal tagihan rupiah.
- Belum ada kontrak `MonthlyBillingBudgetAmount` sebagai anggaran tagihan nominal per-device.
- Migration billing memiliki operasi `Up()` yang dikomentari, sementara `Down()` masih mencoba menghapus objek tersebut.
- Unique index deduplication belum boleh dianggap tersedia sebelum diverifikasi pada database target.

## 3. Keputusan bisnis wajib

### 3.1 Identitas satu anomaly event

Satu event memakai kunci bisnis:

```text
DeviceKey + BillingPeriod + ErpBillingId + AnomalyType
```

`AnomalyType` untuk event billing adalah `BILLING_VARIANCE`. Subtype disimpan sebagai:

```text
ESTIMATE_VS_ERP
PERIOD_OVER_PERIOD
ESTIMATE_VS_ERP,PERIOD_OVER_PERIOD
```

Jika dua kondisi terjadi pada device/periode/ERP ID yang sama, gunakan satu parent event dan satu notifikasi awal per channel. Detail kedua kondisi boleh disimpan sebagai evidence, tetapi tidak menjadi parent event kedua.

### 3.2 Periode dan validitas ERP

Periode konsumsi memakai aturan existing:

```text
expectedPaymentPeriod = consumptionPeriod + 1 bulan
```

Jika ada beberapa record ERP, pilih `Periode` terbesar lalu `TagihanListrikID` terbesar. Tagihan valid jika device cocok, periode cocok, `TagihanListrikID > 0`, `JumlahTagihan >= 0`, dan estimasi tersedia.

Jika ERP belum tersedia, status tetap `ESTIMATE_ONLY`/`ERP_NOT_AVAILABLE` dan tidak membuat anomaly aktual.

Simpan snapshot:

```text
BillingPeriodStart, BillingPeriodEnd, BillingPeriodMode, ConfiguredCutoffDay
```

### 3.3 Formula

```text
differenceEstimate = actualBilling - estimatedBilling
estimateVsErpAnomaly = abs(differenceEstimate) > threshold

periodDifference = actualBilling - previousActualBilling
periodOverPeriodAnomaly = periodDifference > threshold
```

Penurunan besar dicatat sebagai delta informasi, tetapi bukan anomaly kenaikan.

Threshold anomaly mengikuti urutan:

1. `DeviceSettings.BillingAnomalyThreshold` positif.
2. `AppSettingsRecords[UsageStatistics.BillingAnomalyThreshold]` positif.
3. `UsageStatistics:BillingAnomalyThreshold` positif.
4. Jika konfigurasi gagal atau invalid, fallback nominal `Rp1.000.000`.

Threshold dapat diatur dengan dua mode:

```text
AMOUNT      = nominal rupiah
PERCENTAGE  = persentase terhadap reference amount
```

Kontrak konfigurasi yang direncanakan:

```text
BillingAnomalyThresholdMode
BillingAnomalyThresholdAmount
BillingAnomalyThresholdPercent
```

Aturan resolusi:

1. Mode `AMOUNT` memakai nilai nominal positif.
2. Mode `PERCENTAGE` memakai persentase positif dan reference amount yang valid.
3. Mode tidak dikenal, nilai kosong, nol, negatif, reference tidak tersedia, atau kalkulasi gagal memakai `Rp1.000.000`.
4. Mode, input konfigurasi, reference amount, dan threshold efektif disimpan pada snapshot anomaly.

Untuk mode persentase:

```text
effectiveThreshold = referenceAmount * thresholdPercent / 100
```

Threshold efektif tidak boleh nol. Jika hasil tidak valid, gunakan fallback nominal `Rp1.000.000`.

Threshold, tarif, energi, dan baseline yang dipakai harus disimpan sebagai snapshot.

### 3.4 Candidate dan baseline

Pisahkan nilai berikut:

```text
ActiveBaselineAmount  = baseline yang dipakai evaluasi
CandidateAnchorAmount = nilai anomaly pertama
CandidateMedianAmount = median nilai yang sudah dikonfirmasi
```

Aturan:

- candidate dibuat dari anomaly valid pertama jika tidak ada candidate aktif;
- anchor tidak berubah;
- hanya periode billing berikutnya yang diterima;
- diperlukan tiga periode billing berturut-turut;
- hanya keputusan pengguna valid yang menambah count;
- baseline baru adalah median tiga nilai yang dikonfirmasi;
- baseline baru memiliki version dan histori;
- baseline aktif digunakan pada evaluasi berikutnya;
- histori lama tidak diubah.

Konfigurasi awal:

```text
BillingTrendTolerancePercent = 10%
BillingTrendRequiredConfirmations = 3
BillingTrendMaximumGapPeriods = 0
```

Toleransi juga mendukung dua mode:

```text
AMOUNT      = toleransi nominal rupiah
PERCENTAGE  = toleransi persentase terhadap candidate reference
```

Kontrak konfigurasi yang direncanakan:

```text
BillingTrendToleranceMode
BillingTrendToleranceAmount
BillingTrendTolerancePercent
```

Aturan resolusi tolerance:

1. Mode `AMOUNT` memakai nilai nominal positif.
2. Mode `PERCENTAGE` memakai persentase positif terhadap `candidateReference`.
3. Mode tidak dikenal, nilai kosong, nol, negatif, reference tidak tersedia, atau kalkulasi gagal memakai fallback nominal `Rp1.000.000`.
4. Mode, input konfigurasi, reference amount, dan tolerance efektif disimpan pada trend profile serta evaluasi lanjutan.

```text
candidateReference = CandidateMedianAmount jika ada, selain itu CandidateAnchorAmount
percentageTolerance = candidateReference * tolerancePercent
configuredTolerance = mode AMOUNT ? toleranceAmount : percentageTolerance
effectiveTolerance = max(threshold, configuredTolerance)
isContinuation = abs(actualBilling - candidateReference) <= effectiveTolerance
```

Jika tidak continuation: count tidak bertambah, candidate menjadi `RESET`, baseline lama tetap aktif, dan anomaly baru boleh membuat candidate baru.

### 3.5 Confirmation decision

```text
REASONABLE
REASONABLE_WITH_REASON
UNREASONABLE
NEEDS_REVIEW
```

Yang dihitung: `REASONABLE`, atau `REASONABLE_WITH_REASON` dengan kategori dan teks valid. GET, token invalid, `UNREASONABLE`, dan `NEEDS_REVIEW` tidak dihitung.

## 4. State machine

Anomaly event:

```text
DETECTED -> WAITING_CONFIRMATION
WAITING_CONFIRMATION -> CONFIRMED_REASONABLE
WAITING_CONFIRMATION -> CONFIRMED_UNREASONABLE
WAITING_CONFIRMATION -> NEEDS_REVIEW
Semua state final -> CLOSED
```

Trend profile:

```text
STABLE -> PENDING_CONFIRMATION -> ACCEPTED
PENDING_CONFIRMATION -> RESET
```

Hanya satu trend profile aktif per device. Profile lama tidak dihapus.

## 5. Arsitektur wajib

### 5.1 Evaluator read-only

Refactor menjadi `IBillingAnomalyEvaluationService`/`BillingAnomalyEvaluationService` yang hanya memvalidasi dan menghitung. Evaluator tidak boleh melakukan `Add`, `SaveChanges`, provider notification, token, atau update trend.

Parameter `persistAnomaly` tidak boleh menjadi satu-satunya perlindungan side effect.

### 5.2 Workflow persisten

Buat `IBillingAnomalyWorkflowService`/`BillingAnomalyWorkflowService` untuk:

1. get-or-create anomaly event;
2. menggabungkan subtype;
3. get/create trend profile;
4. menilai continuation/reset;
5. membuat confirmation token;
6. membuat notification outbox;
7. memproses keputusan;
8. mengaktifkan baseline.

### 5.3 Transaksi

Dalam satu transaksi database:

```text
Get/Create event
Get/Create trend profile
Create confirmation
Update count/state
Accept/reset trend
Create outbox
```

Email/WhatsApp hanya dikirim worker setelah commit.

Alur utama:

```text
Load billing -> read-only evaluation -> begin transaction
-> get/create event -> link/create trend -> create token/outbox
-> commit -> outbox worker mengirim
```

## 6. Model dan constraint yang direncanakan

### 6.0 Anggaran tagihan bulanan per-device

Nilai trend yang sudah diterima harus dapat mengisi anggaran tagihan bulanan pada Settings per-device secara otomatis.

Konsep ini harus dipisahkan dari field existing:

```text
BudgetKWh                  = anggaran energi bulanan dalam kWh
MonthlyBillingBudgetAmount = anggaran nominal tagihan bulanan dalam rupiah
```

Jangan mengubah arti `BudgetKWh` menjadi rupiah karena kontrak Usage Statistics existing memakai field tersebut untuk membandingkan kWh aktual terhadap budget energi.

Field yang direncanakan:

```text
MonthlyBillingBudgetAmount
MonthlyBillingBudgetMode
MonthlyBillingBudgetSource
MonthlyBillingBudgetTrendProfileId
MonthlyBillingBudgetBaselineVersion
MonthlyBillingBudgetUpdatedAt
MonthlyBillingBudgetManualOverride
```

Nilai otomatis:

```text
MonthlyBillingBudgetAmount = ActiveBaselineAmount
```

Nilai hanya boleh diisi otomatis jika:

1. trend profile berstatus `ACCEPTED`;
2. baseline amount positif dan valid;
3. baseline memiliki version dan effective period;
4. update budget berhasil dalam transaksi yang sama dengan penerimaan baseline, atau memiliki retry idempotent;
5. histori perubahan budget tersimpan.

Sumber budget:

```text
TREND_AUTO
MANUAL
FALLBACK
```

Aturan override:

- mode default adalah `TREND_AUTO`;
- pengguna boleh memilih `MANUAL` jika bisnis membutuhkan budget tetap;
- workflow tidak boleh menimpa budget manual tanpa tindakan eksplisit pengguna;
- ketika manual override dimatikan, baseline trend terbaru boleh mengisi budget kembali;
- setiap perubahan otomatis/manual menyimpan previous value, new value, source, baseline version, user, dan waktu.

Jika baseline trend belum tersedia atau tidak valid, jangan menyalin nilai anomaly mentah ke budget. Gunakan budget existing jika valid; jika tidak ada budget nominal valid, gunakan fallback `Rp1.000.000` dan tandai source `FALLBACK`.

Anggaran otomatis harus digunakan oleh perhitungan billing nominal, tetapi tidak boleh mengubah `BudgetKWh` atau perhitungan budget energi existing.

### 6.1 Event billing

Perluasan additive pada `AnomalyLog`, atau parent `BillingAnomalyEvent` jika diperlukan:

```text
Id, DeviceKey, BillingPeriod, BillingPeriodStart, BillingPeriodEnd
BillingPeriodMode, ErpBillingId, AnomalyType, BillingAnomalyType
EstimatedBilling, ActualBilling, BillingDifference, BillingDifferencePercent
PreviousBilling, PreviousBillingPeriod, PreviousErpBillingId
ActiveBaselineAmount, BaselineVersion, ThresholdValue
ThresholdMode, ThresholdConfiguredAmount, ThresholdConfiguredPercent
ThresholdReferenceAmount, ThresholdEffectiveAmount
TariffSnapshot, EnergySnapshot, WorkflowStatus, ConfirmationRequired
TrendProfileId, DetectedAt, ClosedAt
```

Unique key: `DeviceKey + BillingPeriod + ErpBillingId + AnomalyType`.

Jika ERP ID yang sama direvisi, gunakan aturan re-evaluation/version yang terdokumentasi; jangan membuat event duplikat tanpa jejak.

### 6.2 `BillingTrendProfile`

```text
Id, DeviceKey, ActiveBaselineAmount, ActiveBaselinePeriod
ActiveBaselineVersion, BaselineSource, CandidateAnchorAmount
CandidateMedianAmount, CandidateStartedPeriod, CandidateLastPeriod
CandidateConfirmationCount, RequiredConfirmationCount
ToleranceMode, ToleranceAmount, TolerancePercent
ToleranceReferenceAmount, EffectiveToleranceAmount, MaximumGapPeriods
TrendStatus, AcceptedAt, ResetAt, CreatedAt, UpdatedAt
```

Jamin satu profile aktif per device melalui filtered unique index atau mekanisme version/status yang setara.

### 6.3 `BillingAnomalyConfirmation`

```text
Id, AnomalyEventId, TrendProfileId, DeviceKey, BillingPeriod
ActualBilling, CandidateReferenceAmount, Decision, ReasonCategory
ReasonText, ConfirmedByUserId, ConfirmedByName, CreatedFromChannel
TokenId, ConfirmedAt
```

Unique key: `TrendProfileId + BillingPeriod`. Confirmation immutable.

### 6.4 `BillingConfirmationToken`

```text
Id, AnomalyEventId, TrendProfileId, TokenHash, Purpose
CreatedFromChannel, IssuedTo, CreatedAt, ExpiresAt, UsedAt, RevokedAt
```

Simpan hash saja. Index `TokenHash + Purpose` dan `ExpiresAt`. Expiry awal tujuh hari, maksimum empat belas hari.

### 6.5 `BillingAnomalyNotification`

```text
Id, AnomalyEventId, TrendProfileId, ConfirmationTokenId
Channel, Recipient, NotificationType, Status, AttemptCount
MaxAttemptCount, NextAttemptAt, LockedAt, SentAt, DeliveredAt
FailedAt, LastError, MessageId, CreatedAt
```

Channel: `EMAIL`, `WHATSAPP`.

Type: `INITIAL_ANOMALY`, `FOLLOW_UP_CONFIRMATION`, `REVIEW_REMINDER`, `ESCALATION`, `BASELINE_ACCEPTED`.

Unique key awal: `AnomalyEventId + Channel + NotificationType`. Untuk follow-up lintas periode gunakan `TrendProfileId + BillingPeriod + Channel + NotificationType`.

### 6.6 Budget dan histori baseline

Tambahkan field nominal budget per-device pada model Settings sesuai Bab 6.0 dan buat histori terpisah:

```text
BillingBudgetHistory:
Id, DeviceKey, PreviousAmount, NewAmount
Source, TrendProfileId, BaselineVersion
ManualOverride, ChangedBy, ChangedAt, Reason
```

Unique/idempotency reference untuk update otomatis:

```text
DeviceKey + TrendProfileId + BaselineVersion + Source
```

### 6.7 Histori baseline

```text
BillingBaselineHistory:
Id, TrendProfileId, DeviceKey, PreviousBaselineAmount
NewBaselineAmount, BaselineVersion, SourceConfirmationIds
EffectivePeriod, ChangedAt, ChangedBy
```

## 7. Confirmation flow dan alasan

URL:

```text
/Monitoring/BillingAnomalyConfirmation/{token}
```

GET hanya membaca token, event, trend, progress, baseline, dan histori. GET tidak menyimpan atau mengonsumsi token.

POST wajib memvalidasi:

1. hash, purpose, expiry, dan token belum terpakai;
2. event/trend masih dapat dikonfirmasi;
3. decision dan alasan;
4. unique `TrendProfileId + BillingPeriod`;
5. concurrency melalui lock/unique constraint;
6. user/channel audit;
7. update state, count, token, dan outbox dalam satu transaksi.

Kategori alasan:

```text
NEW_LOAD, OPERATIONAL_CHANGE, TARIFF_CHANGE, SEASONAL_USAGE
METER_ISSUE, ERP_CORRECTION, PAYMENT_DELAY, OTHER
```

`REASONABLE_WITH_REASON` wajib kategori dan teks. `NEEDS_REVIEW` wajib teks. `OTHER` wajib teks.

## 8. Outbox worker

Outbox dibuat setelah event commit. Follow-up hanya dibuat jika candidate pending, periode berurutan, nilai masih toleran, belum ada confirmation, count kurang dari tiga, dan unique outbox belum ada.

Worker wajib:

- claim record secara atomik;
- memproses email/WhatsApp independen;
- menyimpan status dan provider message ID;
- retry dengan bounded backoff;
- menghentikan retry setelah maksimum;
- memakai row yang sama saat retry;
- menyediakan retry manual administrator.

Kegagalan satu channel tidak membatalkan channel lain.

## 9. Migration dan database gate

Sebelum membuat migration workflow:

- [ ] Bandingkan `ApplicationDbContext` dan model snapshot.
- [ ] Pastikan semua kolom model dibuat `Up()`.
- [ ] Benahi `Down()` yang menghapus objek yang tidak dibuat `Up()`.
- [ ] Verifikasi unique index event pada database target.
- [ ] Pastikan migration threshold tidak tumpang tindih.
- [ ] Uji database kosong.
- [ ] Uji upgrade database existing.
- [ ] Uji rollback database development.
- [ ] Periksa production; migration induk menyatakan belum diterapkan ke production.
- [ ] Backup sebelum penerapan.

Gate ini harus lulus sebelum tabel confirmation/trend/outbox dibuat.

## 10. Fase implementasi

### Fase 0 — Finalisasi dan database gate

- [ ] Setujui satu event gabungan.
- [ ] Setujui tiga periode berturut-turut.
- [ ] Setujui formula candidate, reset, dan baseline.
- [ ] Audit migration dan verifikasi schema/index.

### Fase 1 — Evaluator

- [x] Tambahkan kontrak settings untuk budget nominal, source/override, serta mode threshold/tolerance.
- [x] Tambahkan resolver pure logic dengan fallback `Rp1.000.000`.
- [ ] Pisahkan evaluator read-only.
- [ ] Hapus side effect persistence/provider dari evaluator.
- [ ] Buat DTO yang memuat dua subtype dan snapshot data.
- [ ] Tambahkan baseline amount/version pada hasil evaluasi.
- [ ] Pastikan Usage Statistics tetap read-only.

### Fase 2 — Model dan migration

- [ ] Finalisasi event billing.
- [x] Buat trend profile, confirmation, token, outbox, dan baseline history.
- [x] Tambahkan field `MonthlyBillingBudgetAmount` dan metadata source/version pada Settings per-device.
- [x] Pisahkan budget nominal billing dari `BudgetKWh`.
- [x] Tambahkan mode threshold/tolerance amount atau percentage.
- [x] Tambahkan konfigurasi fallback nominal `Rp1.000.000`.
- [x] Tambahkan budget history dan unique/idempotency reference.
- [x] Tambahkan relationship dan unique constraint.
- [x] Buat migration setelah schema existing lulus audit.
- [x] Uji up/down secara struktural melalui migration Up/Down yang simetris.

### Fase 3 — Workflow

- [x] Buat workflow service.
- [x] Implementasikan transaction boundary.
- [x] Implementasikan get-or-create candidate.
- [ ] Implementasikan subtype merge event billing.
- [x] Implementasikan satu active trend per device.
- [x] Implementasikan continuation/reset/median.
- [ ] Saat trend menjadi `ACCEPTED`, isi budget nominal per-device dari `ActiveBaselineAmount`.
- [ ] Hormati manual override dan jangan menimpa budget manual otomatis.
- [ ] Terapkan fallback budget `Rp1.000.000` jika baseline tidak valid dan budget existing tidak valid.
- [ ] Simpan histori perubahan budget dalam transaksi yang sama atau retry idempotent.
- [x] Implementasikan token hash-only, expiry, dan one-time confirmation endpoint.
- [ ] Implementasikan worker outbox.
- [ ] Uji request paralel.

### Fase 4 — Endpoint confirmation

- [x] Buat GET dan POST.
- [ ] Tambahkan antiforgery global/endpoint sesuai kontrak deployment.
- [x] Tambahkan token, expiry, dan one-time use.
- [ ] Terapkan unique confirmation.
- [ ] Tampilkan progress, histori, candidate, dan baseline.

### Fase 5 — Worker notifikasi

- [ ] Buat worker outbox terpisah dari scheduler laporan existing.
- [ ] Implementasikan claim, channel-independent send, retry, status, dan manual retry.
- [ ] Pastikan retry tidak menggandakan pesan.

### Fase 6 — UI, laporan, dan deployment

- [ ] Tampilkan workflow status, count, candidate, baseline, alasan, dan histori.
- [ ] Tampilkan anggaran tagihan nominal, source `TREND_AUTO`/`MANUAL`/`FALLBACK`, dan baseline version.
- [ ] Tampilkan pilihan mode threshold dan tolerance: nominal atau persentase.
- [ ] Tampilkan warning jika fallback `Rp1.000.000` sedang digunakan.
- [ ] Pastikan label `BudgetKWh` tetap berarti anggaran energi, bukan rupiah.
- [ ] Tambahkan filter status/kategori.
- [ ] Deploy schema ke development/staging.
- [ ] Aktifkan recipient terbatas.
- [ ] Monitor lalu aktifkan penuh.
- [ ] Dokumentasikan rollback aplikasi tanpa menghapus histori.

## 11. Pengujian wajib

### Event dan evaluasi

- [ ] Dua subtype bersamaan menghasilkan satu event.
- [ ] Estimasi nol, aktual lebih rendah, ERP kosong, ID invalid.
- [ ] Duplicate ERP memilih ID terbesar.
- [ ] Threshold device/global/fallback.
- [ ] Re-evaluation ERP ID yang sama.
- [ ] Snapshot baseline/tarif/energi tersimpan.

### Trend

- [ ] Candidate pertama.
- [ ] Periode berikutnya dalam toleransi.
- [ ] Periode di luar toleransi reset.
- [ ] Tolerance mode nominal.
- [ ] Tolerance mode persentase.
- [ ] Threshold mode nominal.
- [ ] Threshold mode persentase.
- [ ] Konfigurasi nol/negatif/tidak dikenal memakai fallback `Rp1.000.000`.
- [ ] Periode tidak berturut-turut ditolak.
- [ ] Refresh tidak menambah count.
- [ ] Tiga confirmation valid mengaktifkan median baseline.
- [ ] Baseline baru dipakai periode berikutnya.
- [ ] Histori immutable.

### Budget nominal per-device

- [ ] Baseline trend accepted mengisi `MonthlyBillingBudgetAmount`.
- [ ] `BudgetKWh` tetap tidak berubah maknanya.
- [ ] Budget source `TREND_AUTO` tercatat.
- [ ] Manual override tidak tertimpa otomatis.
- [ ] Manual override dapat dikembalikan ke mode trend otomatis.
- [ ] Baseline invalid dan budget existing invalid memakai fallback `Rp1.000.000`.
- [ ] Update budget idempotent dan tercatat pada histori.
- [ ] Perubahan baseline memperbarui budget sesuai aturan override.

### Confirmation dan keamanan

- [ ] GET tidak mengubah data.
- [ ] Decision valid/invalid.
- [ ] Token expired, revoked, used, dan wrong purpose.
- [ ] Submit duplicate serial/paralel.
- [ ] Validasi alasan.
- [ ] User dan channel tercatat.

### Outbox dan regresi

- [ ] Email/WhatsApp sukses dan gagal.
- [ ] Satu channel gagal tidak menggagalkan channel lain.
- [ ] Retry bounded memakai row existing.
- [ ] Provider ID tersimpan.
- [ ] Refresh tabel tidak membuat outbox.
- [ ] Migration empty/existing/rollback.
- [ ] Build dan endpoint existing.
- [ ] Anomaly perangkat, MQTT, relay, dan response API tidak berubah.

## 12. Kriteria penerimaan

- Satu kombinasi device/periode/ERP billing hanya memiliki satu event.
- Dua subtype tidak membuat dua parent event atau dua notifikasi awal.
- Endpoint tabel dan evaluator tidak memiliki side effect.
- Workflow aman terhadap request paralel.
- Hanya POST valid yang dihitung.
- Tiga confirmation berasal dari tiga periode berturut-turut.
- Candidate di luar toleransi di-reset.
- Median tiga nilai menjadi baseline baru.
- Baseline baru digunakan pada evaluasi berikutnya.
- Baseline accepted mengisi anggaran tagihan nominal per-device secara otomatis.
- `BudgetKWh` tetap digunakan sebagai anggaran energi dan tidak dipetakan ke rupiah.
- Threshold dan tolerance mendukung mode nominal serta persentase.
- Konfigurasi invalid atau kalkulasi gagal memakai fallback nominal `Rp1.000.000`.
- Budget manual tidak ditimpa otomatis tanpa override eksplisit.
- Source budget, baseline version, dan histori perubahan dapat diaudit.
- Histori tidak dihapus.
- Token expired/revoked/used tidak dapat mengubah data.
- Email/WhatsApp memiliki status, retry, dan audit terpisah.
- Retry tidak menggandakan pesan.
- Migration `Up()`/`Down()` konsisten dan unique index aktif.
- Build, workflow test, migration test, dan regression test berhasil.

## 13. Keputusan teknis final

```text
Event identity       : DeviceKey + BillingPeriod + ErpBillingId + AnomalyType
Combined anomaly     : satu parent event dengan subtype gabungan
Evaluator            : read-only tanpa persist side effect
Workflow             : transaction-safe dan idempotent
Confirmation         : tiga periode billing berturut-turut
Candidate anchor     : anomaly pertama, tidak berubah
Tolerance reference  : median berjalan, anchor sebelum confirmation
Baseline baru        : median tiga nilai valid
Baseline usage       : dipakai periode berikutnya
Budget nominal       : ActiveBaselineAmount setelah trend ACCEPTED
Budget field         : terpisah dari BudgetKWh
Threshold mode       : AMOUNT atau PERCENTAGE
Tolerance mode       : AMOUNT atau PERCENTAGE
Invalid fallback     : Rp1.000.000
Budget override      : MANUAL tidak ditimpa oleh workflow otomatis
Token                : random, hash-only, expiry, one-time
Notification         : outbox per channel dan type
Retry                : row outbox yang sama, bounded backoff
Table endpoint       : read-only
History              : immutable dan audit-ready
Migration gate       : wajib lulus sebelum workflow
```

## 14. Status dokumen

```text
Status               : Implementation in progress; source build verified
Source code action   : Evaluator read-only, trend confirmation/token flow, endpoint validation/policy, budget history, and outbox retry worker implemented
Database action      : Migration files/model snapshot present; not applied to any database
Migration action     : Static consistency/build checked; target connection and backup still required
Notification action  : Billing outbox worker implemented; billing event producers and provider delivery integration require staging validation
Remaining validation : Concurrent SQL transaction test, subtype event producer integration, provider contract test, formal test project, and staging migration
Blocker utama        : verified database target/backup and formal integration-test environment
```