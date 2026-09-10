namespace KWHMonitoring.Services;

/// <summary>
/// Tracker agregasi energi real-time.
/// Dipanggil oleh KwhMonitoringService setiap kali data MQTT masuk,
/// dan oleh EnergyAggregationService untuk flush periodic ke database.
/// </summary>
public interface IEnergyAggregationTracker
{
    /// <summary>
    /// Mencatat satu pembacaan daya (Watt) untuk perangkat tertentu.
    /// State disimpan di memory dan diflush ke database secara periodic.
    /// </summary>
    void TrackReading(string deviceKey, DateTime receivedTime, decimal watt);

    /// <summary>
    /// Menyimpan semua in-memory state ke tabel HourlyEnergy.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Memfinalisasi jam yang sudah lewat dan menghapusnya dari memory.
    /// </summary>
    Task FinalizePreviousHoursAsync(DateTime now, CancellationToken cancellationToken = default);
}
