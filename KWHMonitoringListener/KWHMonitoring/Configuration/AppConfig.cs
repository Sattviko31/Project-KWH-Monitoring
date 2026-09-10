using Microsoft.Data.SqlClient;

namespace KWHMonitoring.Configuration;

public class AppConfig
{
    public MqttConfig Mqtt { get; set; } = new();
    public DatabaseConfig Database { get; set; } = new();
    public SamplingConfig Sampling { get; set; } = new();
}

public class SamplingConfig
{
    /// <summary>
    /// Interval sampling dalam detik. 0 berarti tidak ada sampling (simpan semua data).
    /// </summary>
    public int IntervalSeconds { get; set; } = 0;
}

public class MqttConfig
{
    public string BrokerIp { get; set; } = "192.168.150.10";
    public int Port { get; set; } = 1883;
    public bool UseTls { get; set; } = false;
    public bool UseClientCertificate { get; set; } = false;
    public string? ClientCertificatePath { get; set; } = "";
    public string? ClientCertificatePassword { get; set; } = "";
    public string? Username { get; set; } = "";
    public string? Password { get; set; } = "";
}

public class DatabaseConfig
{
    public string Server { get; set; } = "192.168.168.38";
    public string DatabaseName { get; set; } = "HaiwellElectrical";
    public bool UseWindowsAuthentication { get; set; } = false;
    public string Username { get; set; } = "kwhapp";
    public string Password { get; set; } = "";

    /// <summary>
    /// Jika true (default), koneksi SQL Server menggunakan enkripsi TLS.
    /// Ubah ke false hanya untuk development/legacy server tanpa sertifikat valid.
    /// </summary>
    public bool Encrypt { get; set; } = true;

    /// <summary>
    /// Jika true dan Encrypt=true, tetap percaya sertifikat self-signed/server.
    /// Default false agar aman; true hanya untuk development.
    /// </summary>
    public bool TrustServerCertificate { get; set; } = false;

    public string GetConnectionString()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{Server},1433",
            InitialCatalog = DatabaseName,
            Encrypt = Encrypt,
            TrustServerCertificate = TrustServerCertificate,
            ConnectTimeout = 30
        };

        if (UseWindowsAuthentication)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = Username;
            builder.Password = Password;
        }

        return builder.ConnectionString;
    }

    public string GetMasterConnectionString()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{Server},1433",
            InitialCatalog = "master",
            Encrypt = Encrypt,
            TrustServerCertificate = TrustServerCertificate,
            ConnectTimeout = 30
        };

        if (UseWindowsAuthentication)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = Username;
            builder.Password = Password;
        }

        return builder.ConnectionString;
    }
}