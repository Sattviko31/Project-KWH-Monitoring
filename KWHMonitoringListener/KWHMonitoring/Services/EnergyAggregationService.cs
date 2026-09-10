using KWHMonitoring.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace KWHMonitoring.Services;

/// <summary>
/// Background service untuk agregasi energi.
/// - Flush in-memory state setiap 30 detik
/// - Finalisasi jam yang sudah lewat
/// - Agregasi Daily/Monthly/Yearly pada jadwalnya
/// - Backfill otomatis saat startup
/// </summary>
public class EnergyAggregationService : BackgroundService
{
    private readonly IEnergyAggregationTracker _tracker;
    private readonly AppConfig _appConfig;
    private readonly ILogger<EnergyAggregationService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public EnergyAggregationService(
        IEnergyAggregationTracker tracker,
        IOptions<AppConfig> appConfig,
        ILogger<EnergyAggregationService> logger)
    {
        _tracker = tracker;
        _appConfig = appConfig.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Energy Aggregation Service started");

        try
        {
            await BackfillAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backfill gagal saat startup");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await _lock.WaitAsync(0, stoppingToken))
                {
                    _logger.LogWarning("Skip tick: previous aggregation masih berjalan");
                    continue;
                }

                try
                {
                    var now = DateTime.Now;

                    await _tracker.FlushAsync(stoppingToken);
                    await _tracker.FinalizePreviousHoursAsync(now, stoppingToken);

                    await AggregateDailyAsync(now, stoppingToken);
                    await AggregateMonthlyAsync(now, stoppingToken);
                    await AggregateYearlyAsync(now, stoppingToken);
                }
                finally
                {
                    _lock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error di Energy Aggregation Service");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Energy Aggregation Service stopped");
    }

    /// <summary>
    /// Agregasi jam berdasarkan data di KWHData. Digunakan untuk backfill.
    /// </summary>
    public async Task AggregateHourlyAsync(DateTime hour, CancellationToken cancellationToken = default)
    {
        var hourStart = new DateTime(hour.Year, hour.Month, hour.Day, hour.Hour, 0, 0);
        var hourEnd = hourStart.AddHours(1);

        await using var connection = new SqlConnection(_appConfig.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        // Ambil semua device yang ada data di jam ini
        var deviceKeys = new List<string>();
        const string deviceKeysSql = @"
            SELECT DISTINCT [DeviceKey]
            FROM [dbo].[KWHData]
            WHERE [ReceivedTime] >= @HourStart AND [ReceivedTime] < @HourEnd";

        await using (var cmd = new SqlCommand(deviceKeysSql, connection))
        {
            cmd.Parameters.AddWithValue("@HourStart", hourStart);
            cmd.Parameters.AddWithValue("@HourEnd", hourEnd);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                deviceKeys.Add(reader.GetString(0));
            }
        }

        if (deviceKeys.Count == 0) return;

        foreach (var deviceKey in deviceKeys)
        {
            var totalEnergyWh = await CalculateHourlyEnergyAsync(connection, deviceKey, hourStart, hourEnd, cancellationToken);
            if (totalEnergyWh <= 0) continue;

            var energyKWh = Math.Round(totalEnergyWh / 1000m, 4);
            await UpsertHourlyEnergyAsync(connection, deviceKey, hourStart, energyKWh, cancellationToken);
        }
    }

    private async Task<decimal> CalculateHourlyEnergyAsync(SqlConnection connection, string deviceKey, DateTime hourStart, DateTime hourEnd, CancellationToken cancellationToken)
    {
        var dataPoints = new List<(DateTime Time, decimal Watt)>();

        // Data sebelum jam target
        const string beforeSql = @"
            SELECT TOP 1 [ReceivedTime], [W]
            FROM [dbo].[KWHData]
            WHERE [DeviceKey] = @DeviceKey AND [ReceivedTime] < @HourStart
            ORDER BY [ReceivedTime] DESC";

        await using (var cmd = new SqlCommand(beforeSql, connection))
        {
            cmd.Parameters.AddWithValue("@DeviceKey", deviceKey);
            cmd.Parameters.AddWithValue("@HourStart", hourStart);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var receivedTime = reader.GetDateTime(0);
                var watt = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                dataPoints.Add((receivedTime, watt));
            }
        }

        // Data dalam jam target
        const string inHourSql = @"
            SELECT [ReceivedTime], [W]
            FROM [dbo].[KWHData]
            WHERE [DeviceKey] = @DeviceKey 
              AND [ReceivedTime] >= @HourStart 
              AND [ReceivedTime] < @HourEnd
            ORDER BY [ReceivedTime] ASC";

        await using (var cmd = new SqlCommand(inHourSql, connection))
        {
            cmd.Parameters.AddWithValue("@DeviceKey", deviceKey);
            cmd.Parameters.AddWithValue("@HourStart", hourStart);
            cmd.Parameters.AddWithValue("@HourEnd", hourEnd);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var receivedTime = reader.GetDateTime(0);
                var watt = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                dataPoints.Add((receivedTime, watt));
            }
        }

        // Data setelah jam target
        const string afterSql = @"
            SELECT TOP 1 [ReceivedTime], [W]
            FROM [dbo].[KWHData]
            WHERE [DeviceKey] = @DeviceKey AND [ReceivedTime] >= @HourEnd
            ORDER BY [ReceivedTime] ASC";

        await using (var cmd = new SqlCommand(afterSql, connection))
        {
            cmd.Parameters.AddWithValue("@DeviceKey", deviceKey);
            cmd.Parameters.AddWithValue("@HourEnd", hourEnd);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var receivedTime = reader.GetDateTime(0);
                var watt = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                dataPoints.Add((receivedTime, watt));
            }
        }

        if (dataPoints.Count < 2) return 0;

        decimal totalEnergyWh = 0;
        for (int i = 1; i < dataPoints.Count; i++)
        {
            var prev = dataPoints[i - 1];
            var curr = dataPoints[i];

            var overlapStart = prev.Time > hourStart ? prev.Time : hourStart;
            var overlapEnd = curr.Time < hourEnd ? curr.Time : hourEnd;

            if (overlapStart >= overlapEnd) continue;

            var intervalHours = (decimal)(curr.Time - prev.Time).TotalHours;
            var overlapHours = (decimal)(overlapEnd - overlapStart).TotalHours;
            if (intervalHours <= 0 || overlapHours <= 0) continue;

            var avgPower = (prev.Watt + curr.Watt) / 2m;
            totalEnergyWh += avgPower * overlapHours;
        }

        return totalEnergyWh;
    }

    private async Task AggregateDailyAsync(DateTime now, CancellationToken cancellationToken)
    {
        if (now.Hour != 0 || now.Minute > 2) return;

        var dayStart = now.Date.AddDays(-1);
        _logger.LogInformation("Menjalankan agregasi daily untuk {Date}", dayStart.Date);

        const string sql = @"
            MERGE [dbo].[DailyEnergy] AS target
            USING (
                SELECT [DeviceKey], SUM([EnergyKWh]) AS TotalKWh
                FROM [dbo].[HourlyEnergy]
                WHERE [Hour] >= @DayStart AND [Hour] < @DayEnd
                GROUP BY [DeviceKey]
            ) AS source ON target.[DeviceKey] = source.[DeviceKey] AND target.[Date] = @DayStart
            WHEN MATCHED THEN
                UPDATE SET [EnergyKWh] = source.TotalKWh, [CalculatedAt] = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT ([DeviceKey], [Date], [EnergyKWh], [CalculatedAt])
                VALUES (source.[DeviceKey], @DayStart, source.TotalKWh, GETDATE());";

        await using var connection = new SqlConnection(_appConfig.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@DayStart", dayStart);
        command.Parameters.AddWithValue("@DayEnd", dayStart.AddDays(1));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task AggregateMonthlyAsync(DateTime now, CancellationToken cancellationToken)
    {
        if (now.Day != 1 || now.Hour != 0 || now.Minute > 2) return;

        var monthStart = now.AddMonths(-1);
        _logger.LogInformation("Menjalankan agregasi monthly untuk {Year}-{Month}", monthStart.Year, monthStart.Month);

        const string sql = @"
            MERGE [dbo].[MonthlyEnergy] AS target
            USING (
                SELECT [DeviceKey], SUM([EnergyKWh]) AS TotalKWh
                FROM [dbo].[DailyEnergy]
                WHERE [Date] >= @MonthStart AND [Date] < @MonthEnd
                GROUP BY [DeviceKey]
            ) AS source ON target.[DeviceKey] = source.[DeviceKey] 
               AND target.[Year] = @Year AND target.[Month] = @Month
            WHEN MATCHED THEN
                UPDATE SET [EnergyKWh] = source.TotalKWh, [CalculatedAt] = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT ([DeviceKey], [Year], [Month], [EnergyKWh], [CalculatedAt])
                VALUES (source.[DeviceKey], @Year, @Month, source.TotalKWh, GETDATE());";

        await using var connection = new SqlConnection(_appConfig.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@MonthStart", monthStart);
        command.Parameters.AddWithValue("@MonthEnd", monthStart.AddMonths(1));
        command.Parameters.AddWithValue("@Year", monthStart.Year);
        command.Parameters.AddWithValue("@Month", monthStart.Month);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task AggregateYearlyAsync(DateTime now, CancellationToken cancellationToken)
    {
        if (now.Month != 1 || now.Day != 1 || now.Hour != 0 || now.Minute > 2) return;

        var year = now.Year - 1;
        _logger.LogInformation("Menjalankan agregasi yearly untuk {Year}", year);

        const string sql = @"
            MERGE [dbo].[YearlyEnergy] AS target
            USING (
                SELECT [DeviceKey], SUM([EnergyKWh]) AS TotalKWh
                FROM [dbo].[MonthlyEnergy]
                WHERE [Year] = @Year
                GROUP BY [DeviceKey]
            ) AS source ON target.[DeviceKey] = source.[DeviceKey] AND target.[Year] = @Year
            WHEN MATCHED THEN
                UPDATE SET [EnergyKWh] = source.TotalKWh, [CalculatedAt] = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT ([DeviceKey], [Year], [EnergyKWh], [CalculatedAt])
                VALUES (source.[DeviceKey], @Year, source.TotalKWh, GETDATE());";

        await using var connection = new SqlConnection(_appConfig.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Year", year);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task BackfillAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Memulai backfill agregasi");

        DateTime? minDate;
        await using (var connection = new SqlConnection(_appConfig.Database.GetConnectionString()))
        {
            await connection.OpenAsync(cancellationToken);
            const string minSql = "SELECT MIN([ReceivedTime]) FROM [dbo].[KWHData]";
            await using var command = new SqlCommand(minSql, connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            minDate = result as DateTime?;
        }

        if (!minDate.HasValue)
        {
            _logger.LogInformation("Tidak ada data di KWHData, skip backfill");
            return;
        }

        var currentHour = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, 0, 0);
        var startHour = new DateTime(minDate.Value.Year, minDate.Value.Month, minDate.Value.Day, minDate.Value.Hour, 0, 0);

        _logger.LogInformation("Backfill dari {Start} sampai {End}", startHour, currentHour);

        var hour = startHour;
        while (hour <= currentHour)
        {
            await AggregateHourlyAsync(hour, cancellationToken);
            hour = hour.AddHours(1);
        }

        // Backfill daily untuk periode historis
        await BackfillDailyAsync(cancellationToken);

        // Backfill monthly
        await BackfillMonthlyAsync(cancellationToken);

        // Backfill yearly
        await BackfillYearlyAsync(cancellationToken);

        _logger.LogInformation("Backfill selesai");
    }

    private async Task BackfillDailyAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            MERGE [dbo].[DailyEnergy] AS target
            USING (
                SELECT CAST([Hour] AS DATE) AS [Date], [DeviceKey], SUM([EnergyKWh]) AS TotalKWh
                FROM [dbo].[HourlyEnergy]
                GROUP BY CAST([Hour] AS DATE), [DeviceKey]
            ) AS source ON target.[DeviceKey] = source.[DeviceKey] AND target.[Date] = source.[Date]
            WHEN MATCHED THEN
                UPDATE SET [EnergyKWh] = source.TotalKWh, [CalculatedAt] = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT ([DeviceKey], [Date], [EnergyKWh], [CalculatedAt])
                VALUES (source.[DeviceKey], source.[Date], source.TotalKWh, GETDATE());";

        await using var connection = new SqlConnection(_appConfig.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task BackfillMonthlyAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            MERGE [dbo].[MonthlyEnergy] AS target
            USING (
                SELECT [DeviceKey], YEAR([Date]) AS [Year], MONTH([Date]) AS [Month], SUM([EnergyKWh]) AS TotalKWh
                FROM [dbo].[DailyEnergy]
                GROUP BY [DeviceKey], YEAR([Date]), MONTH([Date])
            ) AS source ON target.[DeviceKey] = source.[DeviceKey] 
               AND target.[Year] = source.[Year] AND target.[Month] = source.[Month]
            WHEN MATCHED THEN
                UPDATE SET [EnergyKWh] = source.TotalKWh, [CalculatedAt] = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT ([DeviceKey], [Year], [Month], [EnergyKWh], [CalculatedAt])
                VALUES (source.[DeviceKey], source.[Year], source.[Month], source.TotalKWh, GETDATE());";

        await using var connection = new SqlConnection(_appConfig.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task BackfillYearlyAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            MERGE [dbo].[YearlyEnergy] AS target
            USING (
                SELECT [DeviceKey], [Year], SUM([EnergyKWh]) AS TotalKWh
                FROM [dbo].[MonthlyEnergy]
                GROUP BY [DeviceKey], [Year]
            ) AS source ON target.[DeviceKey] = source.[DeviceKey] AND target.[Year] = source.[Year]
            WHEN MATCHED THEN
                UPDATE SET [EnergyKWh] = source.TotalKWh, [CalculatedAt] = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT ([DeviceKey], [Year], [EnergyKWh], [CalculatedAt])
                VALUES (source.[DeviceKey], source.[Year], source.TotalKWh, GETDATE());";

        await using var connection = new SqlConnection(_appConfig.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task UpsertHourlyEnergyAsync(SqlConnection connection, string deviceKey, DateTime hour, decimal energyKWh, CancellationToken cancellationToken)
    {
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

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@DeviceKey", deviceKey);
        command.Parameters.AddWithValue("@Hour", hour);
        command.Parameters.AddWithValue("@EnergyKWh", energyKWh);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
