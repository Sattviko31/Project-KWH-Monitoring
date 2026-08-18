using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using KWHMonitoring.Models;

namespace KWHMonitoring.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    partial class ApplicationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.1.14-rtm-31457")
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("KWHMonitoring.Models.KWHDataHistory", b =>
            {
                b.Property<long>("HistoryId")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<long>("OriginalId");

                b.Property<string>("DeviceKey")
                    .IsRequired()
                    .HasMaxLength(100);

                b.Property<System.DateTime>("TerminalTime")
                    .HasColumnType("datetime2");

                b.Property<System.DateTime>("ReceivedTime")
                    .HasColumnType("datetime2");

                b.Property<string>("GroupName")
                    .HasMaxLength(100);

                b.Property<string>("DeviceId")
                    .HasMaxLength(100);

                b.Property<decimal>("PhaseR")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal?>("PhaseS")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal?>("PhaseT")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal>("AmpereR")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal?>("AmpereS")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal?>("AmpereT")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal>("W")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal>("CosPhi")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal>("F")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal>("AktifPower")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal>("TotalW")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal>("TotalW1M")
                    .HasColumnType("decimal(18,4)");

                b.Property<System.DateTime?>("ArchivedAt")
                    .HasColumnType("datetime2");

                b.HasKey("HistoryId");

                b.HasIndex("DeviceKey")
                    .HasName("IX_KWHData_History_DeviceKey");

                b.HasIndex("ReceivedTime")
                    .HasName("IX_KWHData_History_ReceivedTime");

                b.HasIndex("DeviceKey", "ReceivedTime")
                    .HasName("IX_KWHData_History_DeviceKey_ReceivedTime");

                b.ToTable("KWHData_History");
            });

            modelBuilder.Entity("KWHMonitoring.Models.KWHData", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("DeviceKey");

                b.Property<string>("DeviceId");

                b.Property<string>("GroupName");

                b.Property<System.DateTime>("Waktu_Device")
                    .HasColumnType("datetime2");

                b.Property<System.DateTime>("Waktu_Server")
                    .HasColumnType("datetime2");

                b.Property<decimal>("Volt_R")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal?>("Volt_S")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal?>("Volt_T")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal>("Amp_R")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal?>("Amp_S")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal?>("Amp_T")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal>("Cos_Phi")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal>("Daya_Watt")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal>("TotalW1M_Wh")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal>("Energi_Aktif_Wh")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal>("Total_Energy_Wh")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal>("Frekuensi_Hz")
                    .HasColumnType("decimal(18,4)");

                b.HasKey("Id");

                b.HasIndex("DeviceKey", "Waktu_Server");

                b.HasIndex("Waktu_Server");

                b.ToTable("KWHData");
            });

            modelBuilder.Entity("KWHMonitoring.Models.AnomalyLog", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("DeviceKey");

                b.Property<string>("DeviceId");

                b.Property<string>("AnomalyType");

                b.Property<decimal>("PowerValue")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal>("ThresholdValue")
                    .HasColumnType("decimal(18,4)");

                b.Property<decimal>("Deviation")
                    .HasColumnType("decimal(18,4)");

                b.Property<System.DateTime>("DetectedTime")
                    .HasColumnType("datetime2");

                b.Property<decimal?>("EMAValue")
                    .HasColumnType("decimal(18,4)");

                b.Property<string>("ThresholdMode")
                    .HasMaxLength(50);

                b.Property<bool>("Acknowledged");

                b.Property<System.DateTime?>("AcknowledgedTime")
                    .HasColumnType("datetime2");

                b.Property<string>("Notes");

                b.HasKey("Id");

                b.HasIndex("DeviceKey", "DetectedTime");

                b.HasIndex("Acknowledged");

                b.ToTable("AnomalyLogs");
            });

            modelBuilder.Entity("KWHMonitoring.Models.AppSettingsRecord", b =>
            {
                b.Property<int>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("SettingKey")
                    .IsRequired()
                    .HasMaxLength(100);

                b.Property<string>("SettingValue")
                    .IsRequired()
                    .HasMaxLength(500);

                b.Property<System.DateTime>("UpdatedAt")
                    .HasColumnType("datetime2");

                b.HasKey("Id");

                b.HasIndex("SettingKey")
                    .IsUnique();

                b.ToTable("AppSettings");
            });

            modelBuilder.Entity("KWHMonitoring.Models.HourlyEnergy", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("DeviceKey")
                    .IsRequired()
                    .HasMaxLength(100);

                b.Property<System.DateTime>("Hour")
                    .HasColumnType("datetime2");

                b.Property<decimal>("EnergyKWh")
                    .HasColumnType("decimal(18,4)");

                b.Property<System.DateTime>("CalculatedAt")
                    .HasColumnType("datetime2");

                b.HasKey("Id");

                b.HasIndex("DeviceKey", "Hour")
                    .IsUnique();

                b.ToTable("HourlyEnergy");
            });

            modelBuilder.Entity("KWHMonitoring.Models.DailyEnergy", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("DeviceKey")
                    .IsRequired()
                    .HasMaxLength(100);

                b.Property<System.DateTime>("Date")
                    .HasColumnType("datetime2");

                b.Property<decimal>("EnergyKWh")
                    .HasColumnType("decimal(18,4)");

                b.Property<System.DateTime>("CalculatedAt")
                    .HasColumnType("datetime2");

                b.HasKey("Id");

                b.HasIndex("DeviceKey", "Date")
                    .IsUnique();

                b.ToTable("DailyEnergy");
            });

            modelBuilder.Entity("KWHMonitoring.Models.MonthlyEnergy", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("DeviceKey")
                    .IsRequired()
                    .HasMaxLength(100);

                b.Property<int>("Year");

                b.Property<int>("Month");

                b.Property<decimal>("EnergyKWh")
                    .HasColumnType("decimal(18,4)");

                b.Property<System.DateTime>("CalculatedAt")
                    .HasColumnType("datetime2");

                b.HasKey("Id");

                b.HasIndex("DeviceKey", "Year", "Month")
                    .IsUnique();

                b.ToTable("MonthlyEnergy");
            });

            modelBuilder.Entity("KWHMonitoring.Models.YearlyEnergy", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("DeviceKey")
                    .IsRequired()
                    .HasMaxLength(100);

                b.Property<int>("Year");

                b.Property<decimal>("EnergyKWh")
                    .HasColumnType("decimal(18,4)");

                b.Property<System.DateTime>("CalculatedAt")
                    .HasColumnType("datetime2");

                b.HasKey("Id");

                b.HasIndex("DeviceKey", "Year")
                    .IsUnique();

                b.ToTable("YearlyEnergy");
            });
#pragma warning restore 612, 618
        }
    }
}
