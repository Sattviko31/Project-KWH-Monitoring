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
