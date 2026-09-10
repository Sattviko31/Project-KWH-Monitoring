namespace KWHMonitoring.Models;

/// <summary>
/// State perangkat untuk satu jam. Disimpan di memory selama jam berjalan.
/// </summary>
public class DeviceHourState
{
    public string DeviceKey { get; set; } = string.Empty;

    public DateTime Hour { get; set; }

    /// <summary>
    /// Waktu pembacaan terakhir yang masuk untuk jam ini.
    /// </summary>
    public DateTime LastReceivedTime { get; set; }

    /// <summary>
    /// Nilai daya (Watt) terakhir.
    /// </summary>
    public decimal LastWatt { get; set; }

    /// <summary>
    /// Energi terakumulasi dalam jam ini dalam satuan Wh.
    /// </summary>
    public decimal AccumulatedEnergyWh { get; set; }
}
