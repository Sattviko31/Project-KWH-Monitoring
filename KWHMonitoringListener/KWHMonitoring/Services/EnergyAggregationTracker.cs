using System.Collections.Concurrent;
using KWHMonitoring.Configuration;
using KWHMonitoring.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace KWHMonitoring.Services;

/// <summary>
/// Implementasi in-memory tracker untuk agregasi energi real-time.
/// Data diflush ke database setiap 30 detik oleh EnergyAggregationService.
/// </summary>
public class EnergyAggregationTracker : IEnergyAggregationTracker
{
    private readonly ConcurrentDictionary<string, DeviceHourState> _runningStates = new();
    private readonly AppConfig _appConfig;
    private readonly ILogger<EnergyAggregationTracker> _logger;

    public EnergyAggregationTracker(IOptions<AppConfig> appConfig, ILogger<EnergyAggregationTracker> logger)
    {
        _appConfig = appConfig.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public void TrackReading(string deviceKey, DateTime receivedTime, decimal watt)
    {
        if (string.IsNullOrWhiteSpace(deviceKey))
        {
            _logger.LogWarning("TrackReading dipanggil dengan DeviceKey kosong");
            return;
        }

        var hour = new DateTime(receivedTime.Year, receivedTime.Month, receivedTime.Day, receivedTime.Hour, 0, 0);
        var key = GetStateKey(deviceKey, hour);

        _runningStates.AddOrUpdate(key,
            _ => new DeviceHourState
            {
                DeviceKey = deviceKey,
                Hour = hour,
                LastReceivedTime = receivedTime,
                LastWatt = watt,
                AccumulatedEnergyWh = GetExistingEnergyKWh(deviceKey, hour) * 1000
            },
            (_, state) =>
            {
                // Data lebih baru dari state saat ini
                if (receivedTime >= state.LastReceivedTime)
                {
                    var hours = (decimal)(receivedTime - state.LastReceivedTime).TotalHours;
                    if (hours > 0)
                    {
                        var avgWatt = (state.LastWatt + watt) / 2m;
                        state.AccumulatedEnergyWh += avgWatt * hours;
                    }

                    state.LastReceivedTime = receivedTime;
                    state.LastWatt = watt;
                }
                else
                {
                    _logger.LogDebug(
                        "Menerima data out-of-order untuk {DeviceKey} pada {ReceivedTime}. State: {StateTime}",
                        deviceKey, receivedTime, state.LastReceivedTime);
                }

                return state;
            });
    }

    /// <inheritdoc />
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        var states = _runningStates.Values.ToList();
        if (states.Count == 0) return;

        foreach (var state in states)
        {
            try
            {
                await UpsertHourlyEnergyAsync(state, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Gagal flush state untuk {DeviceKey} jam {Hour}",
                    state.DeviceKey, state.Hour);
            }
        }
    }

    /// <inheritdoc />
    public async Task FinalizePreviousHoursAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

        var keysToFinalize = _runningStates
            .Where(x => x.Value.Hour < currentHour)
            .Select(x => x.Key)
            .ToList();

        foreach (var key in keysToFinalize)
        {
            if (_runningStates.TryRemove(key, out var state))
            {
                try
                {
                    await UpsertHourlyEnergyAsync(state, cancellationToken);
                    _logger.LogDebug(
                        "Jam {Hour} untuk {DeviceKey} telah difinalisasi",
                        state.Hour, state.DeviceKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Gagal finalisasi state untuk {DeviceKey} jam {Hour}",
                        state.DeviceKey, state.Hour);
                }
            }
        }
    }

    /// <summary>
    /// Mengembalikan jumlah state yang sedang ada di memory (untuk diagnostics).
    /// </summary>
    public int GetRunningStateCount() => _runningStates.Count;

    private decimal GetExistingEnergyKWh(string deviceKey, DateTime hour)
    {
        try
        {
            const string sql = "SELECT [EnergyKWh] FROM [dbo].[HourlyEnergy] WHERE [DeviceKey] = @DeviceKey AND [Hour] = @Hour";
            using var connection = new SqlConnection(_appConfig.Database.GetConnectionString());
            connection.Open();
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@DeviceKey", deviceKey);
            command.Parameters.AddWithValue("@Hour", hour);
            var result = command.ExecuteScalar();
            return result == null || result == DBNull.Value ? 0m : (decimal)result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal membaca HourlyEnergy untuk {DeviceKey} jam {Hour}", deviceKey, hour);
            return 0m;
        }
    }

    private static string GetStateKey(string deviceKey, DateTime hour)
    {
        return $"{deviceKey}:{hour:yyyyMMddHH}";
    }

    private async Task UpsertHourlyEnergyAsync(DeviceHourState state, CancellationToken cancellationToken)
    {
        var energyKWh = Math.Round(state.AccumulatedEnergyWh / 1000m, 4);

        const string sql = @"
            MERGE [dbo].[HourlyEnergy] AS target
            USING (VALUES (@DeviceKey, @Hour, @EnergyKWh, GETDATE())) 
                   AS source ([DeviceKey], [Hour], [EnergyKWh], [CalculatedAt])
            ON target.[DeviceKey] = source.[DeviceKey] 
               AND target.[Hour] = source.[Hour]
            WHEN MATCHED THEN
                UPDATE SET [EnergyKWh] = source.[EnergyKWh], 
                           [CalculatedAt] = source.[CalculatedAt]
            WHEN NOT MATCHED THEN
                INSERT ([DeviceKey], [Hour], [EnergyKWh], [CalculatedAt])
                VALUES (source.[DeviceKey], source.[Hour], source.[EnergyKWh], source.[CalculatedAt]);";

        await using var connection = new SqlConnection(_appConfig.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@DeviceKey", state.DeviceKey);
        command.Parameters.AddWithValue("@Hour", state.Hour);
        command.Parameters.AddWithValue("@EnergyKWh", energyKWh);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
