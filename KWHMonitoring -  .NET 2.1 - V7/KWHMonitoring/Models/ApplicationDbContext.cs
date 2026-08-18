using Microsoft.EntityFrameworkCore;

namespace KWHMonitoring.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<KWHData> KWH_Monitoring { get; set; }
        public DbSet<KWHDataHistory> KWHData_History { get; set; }
        public DbSet<AnomalyLog> AnomalyLogs { get; set; }
        public DbSet<AppSettingsRecord> AppSettingsRecords { get; set; }
        public DbSet<HourlyEnergy> HourlyEnergy { get; set; }
        public DbSet<DailyEnergy> DailyEnergy { get; set; }
        public DbSet<MonthlyEnergy> MonthlyEnergy { get; set; }
        public DbSet<YearlyEnergy> YearlyEnergy { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<KWHData>()
                .ToTable("KWHData")
                .HasKey(x => x.Id);

            modelBuilder.Entity<KWHDataHistory>(entity =>
            {
                entity.ToTable("KWHData_History");
                entity.HasKey(x => x.HistoryId);

                entity.Property(x => x.HistoryId)
                    .ValueGeneratedOnAdd();

                entity.Property(x => x.OriginalId);

                entity.Property(x => x.DeviceKey)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.TerminalTime)
                    .HasColumnType("datetime2")
                    .IsRequired();

                entity.Property(x => x.ReceivedTime)
                    .HasColumnType("datetime2")
                    .IsRequired();

                entity.Property(x => x.GroupName)
                    .HasMaxLength(100)
                    .IsRequired(false);

                entity.Property(x => x.DeviceId)
                    .HasMaxLength(100)
                    .IsRequired(false);

                entity.Property(x => x.PhaseR)
                    .HasColumnType("decimal(18,4)")
                    .IsRequired();

                entity.Property(x => x.PhaseS)
                    .HasColumnType("decimal(18,4)")
                    .IsRequired(false);

                entity.Property(x => x.PhaseT)
                    .HasColumnType("decimal(18,4)")
                    .IsRequired(false);

                entity.Property(x => x.AmpereR)
                    .HasColumnType("decimal(18,4)")
                    .IsRequired();

                entity.Property(x => x.AmpereS)
                    .HasColumnType("decimal(18,4)")
                    .IsRequired(false);

                entity.Property(x => x.AmpereT)
                    .HasColumnType("decimal(18,4)")
                    .IsRequired(false);

                entity.Property(x => x.W)
                    .HasColumnType("decimal(18,4)")
                    .IsRequired();

                entity.Property(x => x.CosPhi)
                    .HasColumnType("decimal(18,4)")
                    .IsRequired();

                entity.Property(x => x.F)
                    .HasColumnType("decimal(18,4)")
                    .IsRequired();

                entity.Property(x => x.AktifPower)
                    .HasColumnType("decimal(18,4)")
                    .IsRequired();

                entity.Property(x => x.TotalW)
                    .HasColumnType("decimal(18,4)")
                    .IsRequired();

                entity.Property(x => x.TotalW1M)
                    .HasColumnType("decimal(18,4)")
                    .IsRequired();

                entity.Property(x => x.ArchivedAt)
                    .HasColumnType("datetime2")
                    .IsRequired(false);

                entity.HasIndex(x => x.DeviceKey)
                    .HasName("IX_KWHData_History_DeviceKey");

                entity.HasIndex(x => x.ReceivedTime)
                    .HasName("IX_KWHData_History_ReceivedTime");

                entity.HasIndex(x => new { x.DeviceKey, x.ReceivedTime })
                    .HasName("IX_KWHData_History_DeviceKey_ReceivedTime");
            });

            modelBuilder.Entity<AnomalyLog>()
                .ToTable("AnomalyLogs")
                .HasKey(x => x.Id);

            modelBuilder.Entity<AppSettingsRecord>()
                .ToTable("AppSettings")
                .HasKey(x => x.Id);

            modelBuilder.Entity<HourlyEnergy>()
                .ToTable("HourlyEnergy")
                .HasKey(x => x.Id);

            modelBuilder.Entity<DailyEnergy>()
                .ToTable("DailyEnergy")
                .HasKey(x => x.Id);

            modelBuilder.Entity<MonthlyEnergy>()
                .ToTable("MonthlyEnergy")
                .HasKey(x => x.Id);

            modelBuilder.Entity<YearlyEnergy>()
                .ToTable("YearlyEnergy")
                .HasKey(x => x.Id);
        }
    }
}
