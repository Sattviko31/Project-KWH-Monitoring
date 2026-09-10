using System.Diagnostics;
using System.Net;
using System.Runtime.Versioning;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using KWHMonitoring.Configuration;
using KWHMonitoring.Services;

[assembly: SupportedOSPlatform("windows")]

namespace KWHMonitoring;

public class Program
{
    private static readonly string ConfigFilePath = Path.Combine(
        AppContext.BaseDirectory, "appsettings.user.json");
    private const string ServiceName = "KWHMonitoring";
    private const string ServiceDisplayName = "KWH Monitoring Service";

    /// <summary>
    /// Enkripsi file konfigurasi user dengan DPAPI (CurrentUser).
    /// Service berjalan sebagai SYSTEM/LocalService di Windows, sehingga
    /// sebaiknya gunakan LocalMachine scope agar dapat dibaca oleh service.
    /// Untuk keamanan lebih tinggi, pertimbangkan sertifikat atau Azure Key Vault.
    /// </summary>
    private static string ProtectConfig(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.LocalMachine);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string UnprotectConfig(string cipherText)
    {
        var protectedBytes = Convert.FromBase64String(cipherText);
        var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.LocalMachine);
        return Encoding.UTF8.GetString(bytes);
    }

    public static async Task Main(string[] args)
    {
        // DETEKSI MODE
        bool isServiceMode = args.Contains("--run-as-service", StringComparer.OrdinalIgnoreCase);

        // Jika BUKAN service mode → jalankan setup wizard
        if (!isServiceMode)
        {
            RunConsoleSetup();
            // Setelah setup selesai, program akan exit di dalam RunConsoleSetup()
            return;
        }

        // MODE SERVICE: Setup logging (file only, tanpa console)
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: Path.Combine(logDir, "kwh-monitoring-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 2,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            Log.Information("═══════════════════════════════════════════════════════════");
            Log.Information("  KWH Monitoring Service Starting (Windows Service Mode)...");
            Log.Information("═══════════════════════════════════════════════════════════");

            var host = CreateHostBuilder(args).Build();
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
        }
        finally
        {
            Log.Information("KWH Monitoring Service stopped");
            Log.CloseAndFlush();
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService(options => options.ServiceName = ServiceName)
            .UseSerilog()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

                // Muat dan dekripsi appsettings.user.json (terenkripsi dengan DPAPI)
                var userJson = LoadUserConfigJson();
                if (!string.IsNullOrWhiteSpace(userJson))
                {
                    var bytes = Encoding.UTF8.GetBytes(userJson);
                    config.AddJsonStream(new MemoryStream(bytes));
                }

                config.AddEnvironmentVariables();
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.Configure<AppConfig>(hostContext.Configuration);
                services.AddHostedService<KwhMonitoringService>();
            });

    private static string? LoadUserConfigJson()
    {
        try
        {
            if (!File.Exists(ConfigFilePath)) return null;
            var cipherText = File.ReadAllText(ConfigFilePath);
            return UnprotectConfig(cipherText);
        }
        catch
        {
            // Backward compatibility: coba baca plain text
            try
            {
                if (!File.Exists(ConfigFilePath)) return null;
                return File.ReadAllText(ConfigFilePath);
            }
            catch { }
        }
        return null;
    }

    // ============================================================
    // CONSOLE SETUP WIZARD + AUTO INSTALL SERVICE
    // ============================================================
    private static void RunConsoleSetup()
    {
        Console.Clear();
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   HAIWELL ELECTRICAL - KWH MONITORING                    ║");
        Console.WriteLine("║   SETUP WIZARD + AUTO INSTALL SERVICE                    ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var config = LoadExistingConfig();

        Console.WriteLine("=== Konfigurasi MQTT ===");
        config.Mqtt.BrokerIp = ReadValidatedIp($"MQTT Broker IP (default: {config.Mqtt.BrokerIp}): ", config.Mqtt.BrokerIp);
        config.Mqtt.Port = ReadValidatedPort($"MQTT Port (default: {config.Mqtt.Port}): ", config.Mqtt.Port);
        Console.Write($"MQTT Use TLS [Y/N] (default: {(config.Mqtt.UseTls ? "Y" : "N")}): ");
        var tlsInput = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(tlsInput)) config.Mqtt.UseTls = tlsInput.StartsWith("Y", StringComparison.OrdinalIgnoreCase) || tlsInput.StartsWith("y", StringComparison.OrdinalIgnoreCase);

        Console.Write($"MQTT Use Client Certificate [Y/N] (default: {(config.Mqtt.UseClientCertificate ? "Y" : "N")}): ");
        var useCertInput = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(useCertInput)) config.Mqtt.UseClientCertificate = useCertInput.StartsWith("Y", StringComparison.OrdinalIgnoreCase) || useCertInput.StartsWith("y", StringComparison.OrdinalIgnoreCase);

        if (config.Mqtt.UseClientCertificate)
        {
            config.Mqtt.ClientCertificatePath = ReadValidatedPath($"MQTT Client Certificate Path (.pfx) (default: {config.Mqtt.ClientCertificatePath}): ", config.Mqtt.ClientCertificatePath ?? "");
            config.Mqtt.ClientCertificatePassword = ReadPassword($"MQTT Client Certificate Password (default: {new string('*', config.Mqtt.ClientCertificatePassword?.Length ?? 0)}): ", config.Mqtt.ClientCertificatePassword ?? "");
        }
        else
        {
            config.Mqtt.Username = ReadNonDangerous($"MQTT Username (default: {config.Mqtt.Username}): ", config.Mqtt.Username ?? "");
            config.Mqtt.Password = ReadPassword($"MQTT Password (default: {new string('*', config.Mqtt.Password?.Length ?? 0)}): ", config.Mqtt.Password ?? "");
        }

        Console.WriteLine("\n=== Konfigurasi Database ===");
        config.Database.Server = ReadValidatedServer($"SQL Server (default: {config.Database.Server}): ", config.Database.Server);
        config.Database.DatabaseName = ReadValidatedSqlIdentifier($"Database Name (default: {config.Database.DatabaseName}): ", config.Database.DatabaseName);

        Console.Write($"SQL Use Windows Authentication [Y/N] (default: {(config.Database.UseWindowsAuthentication ? "Y" : "N")}): ");
        var useWinAuthInput = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(useWinAuthInput)) config.Database.UseWindowsAuthentication = useWinAuthInput.StartsWith("Y", StringComparison.OrdinalIgnoreCase) || useWinAuthInput.StartsWith("y", StringComparison.OrdinalIgnoreCase);

        if (!config.Database.UseWindowsAuthentication)
        {
            config.Database.Username = ReadValidatedSqlIdentifier($"SQL Username (default: {config.Database.Username}): ", config.Database.Username);
            config.Database.Password = ReadPassword($"SQL Password (default: {new string('*', config.Database.Password.Length)}): ", config.Database.Password);
        }

        Console.Write($"SQL Encrypt [Y/N] (default: {(config.Database.Encrypt ? "Y" : "N")}): ");
        var encryptInput = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(encryptInput)) config.Database.Encrypt = encryptInput.StartsWith("Y", StringComparison.OrdinalIgnoreCase) || encryptInput.StartsWith("y", StringComparison.OrdinalIgnoreCase);
        Console.Write($"SQL Trust Server Certificate [Y/N] (default: {(config.Database.TrustServerCertificate ? "Y" : "N")}): ");
        var trustInput = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(trustInput)) config.Database.TrustServerCertificate = trustInput.StartsWith("Y", StringComparison.OrdinalIgnoreCase) || trustInput.StartsWith("y", StringComparison.OrdinalIgnoreCase);

        Console.WriteLine("\n=== Konfigurasi Sampling Data ===");
        config.Sampling.IntervalSeconds = ReadValidatedNonNegativeInt(
            $"Interval sampling (detik) [0 = tanpa sampling, default: {config.Sampling.IntervalSeconds}]: ",
            config.Sampling.IntervalSeconds);

        Console.WriteLine("\n╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   KONFIGURASI YANG AKAN DIGUNAKAN                        ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine($"- MQTT Broker: {config.Mqtt.BrokerIp}:{config.Mqtt.Port} (TLS: {config.Mqtt.UseTls})");
        if (config.Mqtt.UseClientCertificate)
        {
            Console.WriteLine($"- MQTT Auth: Client Certificate");
            Console.WriteLine($"- MQTT Certificate: {config.Mqtt.ClientCertificatePath}");
        }
        else
        {
            Console.WriteLine($"- MQTT Username: {config.Mqtt.Username}");
            Console.WriteLine($"- MQTT Password: {new string('*', config.Mqtt.Password?.Length ?? 0)}");
        }
        Console.WriteLine($"- SQL Server: {config.Database.Server}");
        Console.WriteLine($"- Database: {config.Database.DatabaseName}");
        Console.WriteLine($"- Sampling: {(config.Sampling.IntervalSeconds > 0 ? $"1 data per {config.Sampling.IntervalSeconds} detik per perangkat" : "tanpa sampling")}");
        Console.WriteLine($"- SQL Auth: {(config.Database.UseWindowsAuthentication ? "Windows Authentication" : "SQL Server Authentication")}");
        if (!config.Database.UseWindowsAuthentication)
        {
            Console.WriteLine($"- SQL User: {config.Database.Username}");
        }
        Console.WriteLine($"- SQL Encrypt: {config.Database.Encrypt}, TrustServerCertificate: {config.Database.TrustServerCertificate}");

        Console.WriteLine("\nApakah konfigurasi ini sudah benar? (Y/N)");
        var confirm = Console.ReadKey().KeyChar;
        Console.WriteLine();

        if (char.ToUpper(confirm) != 'Y')
        {
            Console.WriteLine("\nDibatalkan.");
            Environment.Exit(0);
        }

        // Simpan konfigurasi
        SaveUserConfig(config);
        Console.WriteLine($"\n[✓] Konfigurasi disimpan ke: {ConfigFilePath}");

        // ============================================================
        // AUTO INSTALL WINDOWS SERVICE
        // ============================================================
        Console.WriteLine("\n[*] Menginstall sebagai Windows Service...");

        bool isAdmin = IsRunningAsAdmin();
        if (!isAdmin)
        {
            Console.WriteLine("[!] PERINGATAN: Program tidak dijalankan sebagai Administrator");
            Console.WriteLine("[!] Service tidak bisa diinstall otomatis.");
            Console.WriteLine();
            Console.WriteLine("[*] Solusi:");
            Console.WriteLine("    1. Tutup program ini");
            Console.WriteLine("    2. Klik kanan KWHMonitoring.exe → 'Run as administrator'");
            Console.WriteLine("    3. Ulangi setup");
            Console.WriteLine();
            Console.WriteLine("[*] Atau install manual (sebagai Administrator):");

            var exePath = GetExePath();
            Console.WriteLine($"    sc create {ServiceName} binPath= \"{exePath} --run-as-service\" start= auto DisplayName= \"{ServiceDisplayName}\"");
            Console.WriteLine($"    sc start {ServiceName}");
            Console.WriteLine();
            Console.WriteLine("Tekan Enter untuk keluar...");
            Console.ReadLine();
            Environment.Exit(0);
        }

        // Admin mode: Auto install service
        var installResult = InstallAndStartService();

        if (installResult)
        {
            Console.WriteLine("\n╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   [✓] SERVICE BERHASIL DIINSTALL DAN DIJALANKAN          ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine($"[*] Nama Service: {ServiceDisplayName}");
            Console.WriteLine("[*] Cek di: services.msc");
            Console.WriteLine($"[*] Log: {Path.Combine(AppContext.BaseDirectory, "logs")}");
            Console.WriteLine();
            Console.WriteLine("[*] Console akan tertutup dalam 3 detik...");
            Thread.Sleep(3000);
        }
        else
        {
            Console.WriteLine("\n[!] Install service gagal. Console akan ditutup.");
            Console.WriteLine("Tekan Enter untuk keluar...");
            Console.ReadLine();
        }

        // TUTUP CONSOLE
        Environment.Exit(0);
    }

    // ============================================================
    // HELPER METHODS
    // ============================================================

    private static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static string GetExePath()
    {
        if (!string.IsNullOrEmpty(Environment.ProcessPath))
            return Environment.ProcessPath;

#pragma warning disable IL3000
        var location = Assembly.GetExecutingAssembly().Location;
#pragma warning restore IL3000
        if (!string.IsNullOrEmpty(location))
            return location;

        return Path.Combine(AppContext.BaseDirectory, "KWHMonitoring.exe");
    }

    private static bool InstallAndStartService()
    {
        try
        {
            var exePath = GetExePath();

            if (!File.Exists(exePath))
            {
                Console.WriteLine($"[!] File tidak ditemukan: {exePath}");
                return false;
            }

            Console.WriteLine($"[*] EXE Path: {exePath}");

            // 1. Stop service jika sudah ada
            Console.WriteLine("[*] Stop service lama (jika ada)...");
            RunCommand("sc", $"stop {ServiceName}");
            Thread.Sleep(1000);

            // 2. Delete service jika sudah ada
            Console.WriteLine("[*] Hapus service lama (jika ada)...");
            RunCommand("sc", $"delete {ServiceName}");
            Thread.Sleep(1500);

            // 3. Create service baru
            Console.WriteLine("[*] Install service baru...");
            var binPath = $"\"{exePath}\" --run-as-service";
            var createResult = RunCommand("sc",
                $"create {ServiceName} binPath= \"{binPath}\" start= auto DisplayName= \"{ServiceDisplayName}\"");

            if (!createResult.Contains("SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[!] Gagal create service: {createResult}");
                return false;
            }
            Console.WriteLine("[✓] Service berhasil dibuat");

            // 4. Set description
            RunCommand("sc",
                $"description {ServiceName} \"Haiwell Electrical - MQTT to SQL Server KWH Monitoring Service\"");

            // 5. Set failure recovery (restart on failure)
            RunCommand("sc", $"failure {ServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000");

            // 6. Start service
            Console.WriteLine("[*] Starting service...");
            var startResult = RunCommand("sc", $"start {ServiceName}");

            if (!startResult.Contains("START_PENDING", StringComparison.OrdinalIgnoreCase) &&
                !startResult.Contains("RUNNING", StringComparison.OrdinalIgnoreCase) &&
                !startResult.Contains("SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[!] Gagal start service: {startResult}");
                return false;
            }

            Console.WriteLine("[✓] Service berhasil dijalankan");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Error: {ex.Message}");
            return false;
        }
    }

    private static string RunCommand(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return "";

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(10000);

            return string.IsNullOrEmpty(output) ? error : output;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static AppConfig LoadExistingConfig()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var cipherText = File.ReadAllText(ConfigFilePath);
                var json = UnprotectConfig(cipherText);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch
        {
            // Jika gagal membaca file terenkripsi, coba baca sebagai plain text (backward compatibility)
            try
            {
                var json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null) return config;
            }
            catch { }
        }

        try
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json");
            var cfg = builder.Build();
            var appConfig = new AppConfig();
            cfg.Bind(appConfig);
            return appConfig;
        }
        catch { }

        return new AppConfig();
    }

    private static void SaveUserConfig(AppConfig config)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(config, options);
        var cipherText = ProtectConfig(json);
        File.WriteAllText(ConfigFilePath, cipherText);
    }

    // ============================================================
    // VALIDATION HELPERS
    // ============================================================

    private static string ReadValidatedIp(string prompt, string defaultValue)
    {
        while (true)
        {
            Console.Write(prompt);
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) return defaultValue;

            if (IPAddress.TryParse(input, out _)) return input;
            Console.WriteLine("[!] Alamat IP tidak valid. Contoh: 192.168.1.10");
        }
    }

    private static int ReadValidatedPort(string prompt, int defaultValue)
    {
        while (true)
        {
            Console.Write(prompt);
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) return defaultValue;

            if (int.TryParse(input, out var port) && port > 0 && port <= 65535) return port;
            Console.WriteLine("[!] Port harus berupa angka antara 1 dan 65535.");
        }
    }

    private static string ReadValidatedServer(string prompt, string defaultValue)
    {
        while (true)
        {
            Console.Write(prompt);
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) return defaultValue;

            // Izinkan hostname, IP, atau instance SQL (server\instance)
            if (Regex.IsMatch(input, @"^[a-zA-Z0-9._\-]+$")) return input;
            Console.WriteLine("[!] Nama server tidak valid. Hanya huruf, angka, titik, dash, underscore, dan backslash (instance) yang diizinkan.");
        }
    }

    private static string ReadValidatedSqlIdentifier(string prompt, string defaultValue)
    {
        while (true)
        {
            Console.Write(prompt);
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) return defaultValue;

            // SQL identifier valid: huruf, angka, underscore, dash, dollar. Tidak boleh mengandung karakter berbahaya.
            if (Regex.IsMatch(input, @"^[a-zA-Z_][a-zA-Z0-9_\-\$]*$")) return input;
            Console.WriteLine("[!] Identifier SQL tidak valid. Hindari spasi dan karakter khusus.");
        }
    }

    private static string ReadPassword(string prompt, string defaultValue)
    {
        Console.Write(prompt);
        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? defaultValue : input;
    }

    private static string ReadValidatedPath(string prompt, string defaultValue)
    {
        while (true)
        {
            Console.Write(prompt);
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) return defaultValue;

            if (File.Exists(input)) return input;
            Console.WriteLine("[!] File tidak ditemukan. Masukkan path sertifikat yang valid.");
        }
    }

    private static int ReadValidatedNonNegativeInt(string prompt, int defaultValue)
    {
        while (true)
        {
            Console.Write(prompt);
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) return defaultValue;

            if (int.TryParse(input, out var value) && value >= 0) return value;
            Console.WriteLine("[!] Nilai harus berupa angka bulat >= 0. Contoh: 0 (tanpa sampling), 5, 10.");
        }
    }

    /// <summary>
    /// Membaca input umum dan menolak karakter yang berpotensi berbahaya (injection).
    /// </summary>
    private static string ReadNonDangerous(string prompt, string defaultValue)
    {
        while (true)
        {
            Console.Write(prompt);
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) return defaultValue;

            if (!input.Contains(';') && !input.Contains('"') && !input.Contains('\'') && !input.Contains('$') && !input.Contains('&') && !input.Contains('|'))
                return input;

            Console.WriteLine("[!] Input mengandung karakter yang tidak diizinkan.");
        }
    }
}