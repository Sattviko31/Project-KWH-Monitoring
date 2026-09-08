using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using KWHMonitoring.Models;

namespace KWHMonitoring.Services
{
    public class MqttSettings
    {
        public string Broker { get; set; } = "localhost";
        public int Port { get; set; } = 1883;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool UseTls { get; set; } = false;
        public string ClientId { get; set; } = "KWHMonitoringWeb";

        // ===== TLS / SSL =====
        // Nama file sertifikat yang tersimpan di folder AppData/MqttCerts
        public string CaCertificateFile { get; set; } = string.Empty;
        public string ClientCertificateFile { get; set; } = string.Empty;
        public string ClientCertificatePassword { get; set; } = string.Empty;
        public bool SkipCertificateValidation { get; set; } = false;
    }

    public class MqttService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHostingEnvironment _environment;
        private readonly ILogger<MqttService> _logger;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private IMqttClient _mqttClient;
        private string _lastSettingsJson;

        public MqttService(IServiceProvider serviceProvider, IHostingEnvironment environment, ILogger<MqttService> logger)
        {
            _serviceProvider = serviceProvider;
            _environment = environment;
            _logger = logger;
        }

        private async Task<string> GetSettingOrDefaultAsync(DbSet<AppSettingsRecord> settings, string key, string defaultValue)
        {
            var record = await settings.FirstOrDefaultAsync(x => x.SettingKey == key);
            return record != null ? record.SettingValue : defaultValue;
        }

        public async Task<MqttSettings> LoadSettingsAsync()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var settings = context.AppSettingsRecords;

                var broker = await GetSettingOrDefaultAsync(settings, "MQTT.Broker", "localhost");
                var portStr = await GetSettingOrDefaultAsync(settings, "MQTT.Port", "1883");
                var useTlsStr = await GetSettingOrDefaultAsync(settings, "MQTT.UseTls", "false");
                var skipValidationStr = await GetSettingOrDefaultAsync(settings, "MQTT.TlsSkipCertValidation", "false");
                var clientId = await GetSettingOrDefaultAsync(settings, "MQTT.ClientId", "KWHMonitoringWeb");

                if (!int.TryParse(portStr, out var port))
                {
                    port = 1883;
                }

                return new MqttSettings
                {
                    Broker = broker,
                    Port = port,
                    Username = await GetSettingOrDefaultAsync(settings, "MQTT.Username", string.Empty),
                    Password = await GetSettingOrDefaultAsync(settings, "MQTT.Password", string.Empty),
                    UseTls = bool.TryParse(useTlsStr, out var useTls) && useTls,
                    ClientId = string.IsNullOrWhiteSpace(clientId) ? "KWHMonitoringWeb" : clientId,
                    CaCertificateFile = await GetSettingOrDefaultAsync(settings, "MQTT.TlsCaCertFile", string.Empty),
                    ClientCertificateFile = await GetSettingOrDefaultAsync(settings, "MQTT.TlsClientCertFile", string.Empty),
                    ClientCertificatePassword = await GetSettingOrDefaultAsync(settings, "MQTT.TlsClientCertPassword", string.Empty),
                    SkipCertificateValidation = bool.TryParse(skipValidationStr, out var skipValidation) && skipValidation
                };
            }
        }

        public string GetMqttCertificateDirectory()
        {
            return Path.Combine(_environment.ContentRootPath, "AppData", "MqttCerts");
        }

        public async Task<bool> EnsureConnectedAsync()
        {
            var settings = await LoadSettingsAsync();
            return await EnsureConnectedAsync(settings);
        }

        public async Task<bool> EnsureConnectedAsync(MqttSettings settings)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_mqttClient != null && _mqttClient.IsConnected)
                {
                    // Bandingkan settings (bukan options) karena options TLS memuat
                    // delegate dan X509Certificate yang tidak bisa di-serialize dengan aman.
                    var currentJson = JsonConvert.SerializeObject(settings);
                    if (currentJson == _lastSettingsJson)
                    {
                        return true;
                    }

                    try
                    {
                        await _mqttClient.DisconnectAsync();
                    }
                    catch
                    {
                        // ignore
                    }

                    _mqttClient.Dispose();
                    _mqttClient = null;
                }

                var factory = new MqttFactory();
                var client = factory.CreateMqttClient();
                var options = BuildOptions(settings);

                await client.ConnectAsync(options, CancellationToken.None);

                _mqttClient = client;
                _lastSettingsJson = JsonConvert.SerializeObject(settings);

                _logger.LogInformation("Connected to MQTT broker at {Broker}:{Port} (TLS: {Tls})", settings.Broker, settings.Port, settings.UseTls);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to MQTT broker at {Broker}:{Port}", settings.Broker, settings.Port);
                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private IMqttClientOptions BuildOptions(MqttSettings settings)
        {
            var builder = new MqttClientOptionsBuilder()
                .WithTcpServer(settings.Broker, settings.Port)
                .WithClientId(settings.ClientId)
                .WithProtocolVersion(MqttProtocolVersion.V311);

            if (!string.IsNullOrWhiteSpace(settings.Username))
            {
                builder.WithCredentials(settings.Username, settings.Password);
            }

            if (settings.UseTls)
            {
                builder.WithTls(BuildTlsParameters(settings));
            }

            return builder.Build();
        }

        private MqttClientOptionsBuilderTlsParameters BuildTlsParameters(MqttSettings settings)
        {
            var caCertificate = LoadCertificateFile(settings.CaCertificateFile);
            var clientCertificate = LoadClientCertificateFile(settings.ClientCertificateFile, settings.ClientCertificatePassword);

            var tlsParameters = new MqttClientOptionsBuilderTlsParameters
            {
                UseTls = true,
                AllowUntrustedCertificates = settings.SkipCertificateValidation,
                IgnoreCertificateChainErrors = settings.SkipCertificateValidation,
                IgnoreCertificateRevocationErrors = true,
                SslProtocol = SslProtocols.Tls12
            };

            // Sertifikat client (mutual TLS) dikirim ke broker
            if (clientCertificate != null)
            {
                tlsParameters.Certificates = new List<X509Certificate> { clientCertificate };
            }

            // Validasi sertifikat broker terhadap CA certificate yang dipilih
            if (!settings.SkipCertificateValidation && caCertificate != null)
            {
                var trustedCa = caCertificate;
                tlsParameters.CertificateValidationHandler = context =>
                    ValidateServerCertificate(context.Certificate, trustedCa);
            }

            return tlsParameters;
        }

        private string ResolveCertificatePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            // Hanya nama file tanpa path, cegah path traversal
            if (fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar))
            {
                return null;
            }

            var directory = Path.GetFullPath(GetMqttCertificateDirectory());
            var path = Path.GetFullPath(Path.Combine(directory, fileName));
            if (!path.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return path;
        }

        private X509Certificate2 LoadCertificateFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            try
            {
                var path = ResolveCertificatePath(fileName);
                if (path == null || !File.Exists(path))
                {
                    _logger.LogWarning("MQTT CA certificate file not found: {FileName}", fileName);
                    return null;
                }

                var raw = File.ReadAllBytes(path);

                // .NET Core 2.1 tidak dapat memuat PEM langsung, konversi ke DER
                var text = Encoding.ASCII.GetString(raw);
                if (text.Contains("-----BEGIN CERTIFICATE-----"))
                {
                    var pem = text.Replace("-----BEGIN CERTIFICATE-----", string.Empty)
                                  .Replace("-----END CERTIFICATE-----", string.Empty)
                                  .Trim();
                    return new X509Certificate2(Convert.FromBase64String(pem));
                }

                return new X509Certificate2(raw);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load MQTT CA certificate {FileName}", fileName);
                return null;
            }
        }

        private X509Certificate2 LoadClientCertificateFile(string fileName, string password)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            try
            {
                var path = ResolveCertificatePath(fileName);
                if (path == null || !File.Exists(path))
                {
                    _logger.LogWarning("MQTT client certificate file not found: {FileName}", fileName);
                    return null;
                }

                return new X509Certificate2(path, password ?? string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load MQTT client certificate {FileName}", fileName);
                return null;
            }
        }

        private static bool ValidateServerCertificate(X509Certificate serverCertificate, X509Certificate2 caCertificate)
        {
            if (serverCertificate == null || caCertificate == null)
            {
                return false;
            }

            try
            {
                // Broker menyajikan CA certificate-nya sendiri
                if (string.Equals(serverCertificate.GetCertHashString(), caCertificate.Thumbprint, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                using (var chain = new X509Chain())
                {
                    chain.ChainPolicy.ExtraStore.Add(caCertificate);
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

                    using (var serverCert = new X509Certificate2(serverCertificate))
                    {
                        chain.Build(serverCert);
                    }

                    if (chain.ChainElements.Count == 0)
                    {
                        return false;
                    }

                    // Trust hanya jika chain berakhir di CA yang dipilih
                    var rootCertificate = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
                    return string.Equals(rootCertificate.Thumbprint, caCertificate.Thumbprint, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> PublishRelayControlAsync(string deviceId, string rcValue)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new ArgumentException("DeviceId is required", nameof(deviceId));
            }

            if (!await EnsureConnectedAsync())
            {
                return false;
            }

            string groupName;
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                groupName = await context.DeviceRegistry
                    .Where(x => x.DeviceId == deviceId)
                    .Select(x => x.GroupName)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false) ?? "RELAY_CONTROL";
            }

            var topic = $"data/KWHAPP/{deviceId}/12345678";
            var payload = new
            {
                _terminalTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                _groupName = groupName,
                RC = rcValue
            };

            var json = JsonConvert.SerializeObject(payload);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(json)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(false)
                .Build();

            await _semaphore.WaitAsync();
            try
            {
                if (_mqttClient == null || !_mqttClient.IsConnected)
                {
                    return false;
                }

                await _mqttClient.PublishAsync(message, CancellationToken.None);
                _logger.LogInformation("Published to {Topic}: {Payload}", topic, json);
                return true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<bool> PublishRelayPulseAsync(string deviceId, string rcValue, int pulseDurationMs = 1000)
        {
            var onResult = await PublishRelayControlAsync(deviceId, rcValue);
            if (!onResult)
            {
                return false;
            }

            await Task.Delay(pulseDurationMs);

            return await PublishRelayControlAsync(deviceId, "0");
        }

        public async Task<(bool Connected, string Error)> TestConnectionAsync(MqttSettings settings)
        {
            IMqttClient client = null;
            try
            {
                var factory = new MqttFactory();
                client = factory.CreateMqttClient();
                var options = BuildOptions(settings);

                await client.ConnectAsync(options, CancellationToken.None);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT test connection failed to {Broker}:{Port}", settings.Broker, settings.Port);
                return (false, ex.Message);
            }
            finally
            {
                if (client != null)
                {
                    try
                    {
                        if (client.IsConnected)
                        {
                            await client.DisconnectAsync();
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                    client.Dispose();
                }
            }
        }
    }
}
