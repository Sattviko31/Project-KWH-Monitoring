# Environment Variables Setup Guide

Dokumen ini menjelaskan cara mengkonfigurasi environment variables untuk aplikasi KWH Monitoring agar data sensitif tidak lagi disimpan di dalam kode atau `appsettings.json`.

---

## ⚠️ Penting

File `appsettings.json` sekarang hanya berisi placeholder untuk data sensitif. Aplikasi **tidak akan berjalan** jika environment variables yang diperlukan tidak diatur.

---

## Daftar Environment Variables

| Variable | Keterangan | Contoh |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | Connection string SQL Server | `Server=192.168.1.10,1433;Database=HaiwellElectrical;User Id=kwhapp;Password=Str0ngP@ssw0rd!;TrustServerCertificate=True;` |
| `Encryption__Key` | Kunci AES-256 untuk enkripsi data | `KWH-Monitoring-2024-AES256-SecureKey!` |
| `Qwen__ApiKey` | API Key untuk AI Chatbot Qwen | `sk-...` |
| `Qwen__Model` | Model Qwen yang digunakan | `qwen-plus-2025-04-28` |
| `Wablas__Token` | Token WhatsApp Gateway Wablas | `...` |
| `Wablas__SecretKey` | Secret Key Wablas | `...` |
| `Admin__Email` | Email master admin default | `admin@example.com` |
| `Admin__Password` | Password master admin default | `Str0ngP@ssw0rd!` |

---

## Cara Mengatur Environment Variables

### 1. Windows PowerShell (Development)

Buka PowerShell dan jalankan:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=YOUR_SERVER,1433;Database=HaiwellElectrical;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
$env:Encryption__Key = "YOUR_ENCRYPTION_KEY"
$env:Qwen__ApiKey = "YOUR_QWEN_API_KEY"
$env:Qwen__Model = "qwen-plus-2025-04-28"
$env:Wablas__Token = "YOUR_WABLAS_TOKEN"
$env:Wablas__SecretKey = "YOUR_WABLAS_SECRET_KEY"
$env:Admin__Email = "admin@example.com"
$env:Admin__Password = "YOUR_STRONG_PASSWORD"

dotnet run --project "KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring/KWHMonitoring.csproj"
```

### 2. Windows Command Prompt (CMD)

```cmd
set ConnectionStrings__DefaultConnection=Server=YOUR_SERVER,1433;Database=HaiwellElectrical;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;
set Encryption__Key=YOUR_ENCRYPTION_KEY
set Qwen__ApiKey=YOUR_QWEN_API_KEY
set Qwen__Model=qwen-plus-2025-04-28
set Wablas__Token=YOUR_WABLAS_TOKEN
set Wablas__SecretKey=YOUR_WABLAS_SECRET_KEY
set Admin__Email=admin@example.com
set Admin__Password=YOUR_STRONG_PASSWORD

dotnet run --project "KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring/KWHMonitoring.csproj"
```

### 3. Windows (System Environment Variables)

Untuk produksi, atur environment variables secara permanen melalui System Properties:

1. Buka `System Properties` → `Advanced` → `Environment Variables`
2. Klik `New` pada bagian System variables
3. Tambahkan satu per satu variable di atas
4. Restart aplikasi / IIS

### 4. Linux / macOS

```bash
export ConnectionStrings__DefaultConnection="Server=YOUR_SERVER,1433;Database=HaiwellElectrical;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
export Encryption__Key="YOUR_ENCRYPTION_KEY"
export Qwen__ApiKey="YOUR_QWEN_API_KEY"
export Qwen__Model="qwen-plus-2025-04-28"
export Wablas__Token="YOUR_WABLAS_TOKEN"
export Wablas__SecretKey="YOUR_WABLAS_SECRET_KEY"
export Admin__Email="admin@example.com"
export Admin__Password="YOUR_STRONG_PASSWORD"

dotnet run --project "KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring/KWHMonitoring.csproj"
```

### 5. IIS (Internet Information Services)

1. Buka IIS Manager
2. Pilih Application Pool yang digunakan aplikasi
3. Klik kanan → `Advanced Settings`
4. Cari bagian `Environment Variables` dan tambahkan variable yang diperlukan
5. Atau gunakan `web.config` dengan `<environmentVariables>` di dalam `<system.webServer><aspNetCore>`

Contoh `web.config`:

```xml
<configuration>
  <system.webServer>
    <aspNetCore processPath="dotnet" arguments="KWHMonitoring.dll">
      <environmentVariables>
        <environmentVariable name="ConnectionStrings__DefaultConnection" value="Server=YOUR_SERVER,1433;Database=HaiwellElectrical;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;" />
        <environmentVariable name="Encryption__Key" value="YOUR_ENCRYPTION_KEY" />
        <environmentVariable name="Qwen__ApiKey" value="YOUR_QWEN_API_KEY" />
        <environmentVariable name="Qwen__Model" value="qwen-plus-2025-04-28" />
        <environmentVariable name="Admin__Email" value="admin@example.com" />
        <environmentVariable name="Admin__Password" value="YOUR_STRONG_PASSWORD" />
      </environmentVariables>
    </aspNetCore>
  </system.webServer>
</configuration>
```

### 6. Docker

Tambahkan di `docker run`:

```bash
docker run -e ConnectionStrings__DefaultConnection="Server=..." \
           -e Encryption__Key="..." \
           -e Qwen__ApiKey="..." \
           -e Admin__Email="..." \
           -e Admin__Password="..." \
           kwhmonitoring:latest
```

Atau di `docker-compose.yml`:

```yaml
services:
  kwhmonitoring:
    image: kwhmonitoring:latest
    environment:
      - ConnectionStrings__DefaultConnection=Server=YOUR_SERVER,1433;Database=HaiwellElectrical;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;
      - Encryption__Key=YOUR_ENCRYPTION_KEY
      - Qwen__ApiKey=YOUR_QWEN_API_KEY
      - Qwen__Model=qwen-plus-2025-04-28
      - Wablas__Token=YOUR_WABLAS_TOKEN
      - Wablas__SecretKey=YOUR_WABLAS_SECRET_KEY
      - Admin__Email=admin@example.com
      - Admin__Password=YOUR_STRONG_PASSWORD
```

---

## User Secrets (Development Only)

ASP.NET Core mendukung User Secrets untuk development agar secret tidak tertulis di `appsettings.json`:

```bash
cd "KWHMonitoring -  .NET 2.1 - V7/KWHMonitoring"
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=..."
dotnet user-secrets set "Encryption:Key" "..."
dotnet user-secrets set "Qwen:ApiKey" "..."
```

---

## Verifikasi

Setelah mengatur environment variables, jalankan aplikasi dan periksa log startup. Jika ada data sensitif yang belum dikonfigurasi, aplikasi akan menampilkan error yang jelas.
