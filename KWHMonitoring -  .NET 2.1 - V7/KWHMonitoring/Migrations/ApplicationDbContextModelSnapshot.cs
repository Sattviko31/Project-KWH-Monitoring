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

            modelBuilder.Entity("KWHMonitoring.Models.DeviceRegistry", b =>
            {
                b.Property<int>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("DeviceKey")
                    .IsRequired()
                    .HasColumnType("varchar(20)")
                    .HasMaxLength(20);

                b.Property<string>("DeviceId")
                    .IsRequired()
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                b.Property<string>("GroupName")
                    .HasColumnType("varchar(100)")
                    .HasMaxLength(100);

                b.Property<string>("Location")
                    .HasColumnType("varchar(200)")
                    .HasMaxLength(200);

                b.Property<System.DateTime>("FirstSeen")
                    .HasColumnType("datetime2");

                b.Property<System.DateTime>("LastSeen")
                    .HasColumnType("datetime2");

                b.Property<bool>("IsActive");

                b.Property<long>("MessageCount");

                b.Property<System.DateTime>("CreatedAt")
                    .HasColumnType("datetime2");

                b.Property<System.DateTime>("UpdatedAt")
                    .HasColumnType("datetime2");

                b.HasKey("Id");

                b.HasIndex("DeviceKey")
                    .IsUnique()
                    .HasName("IX_DeviceRegistry_DeviceKey");

                b.HasIndex("DeviceId")
                    .HasName("IX_DeviceRegistry_DeviceId");

                b.ToTable("DeviceRegistry");
            });

            modelBuilder.Entity("KWHMonitoring.Models.KWHData", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("DeviceKey")
                    .IsRequired()
                    .HasColumnType("varchar(20)")
                    .HasMaxLength(20);

                b.Property<System.DateTime?>("Waktu_Device")
                    .HasColumnName("TerminalTime")
                    .HasColumnType("datetime2");

                b.Property<System.DateTime>("Waktu_Server")
                    .HasColumnName("ReceivedTime")
                    .HasColumnType("datetime2");

                b.Property<string>("GroupName")
                    .HasColumnType("nvarchar(100)")
                    .HasMaxLength(100);

                b.Property<string>("DeviceId")
                    .HasColumnType("nvarchar(50)")
                    .HasMaxLength(50);

                b.Property<decimal?>("Volt_R")
                    .HasColumnName("PHASE_R")
                    .HasColumnType("decimal(18,2)");

                b.Property<decimal?>("Volt_S")
                    .HasColumnName("PHASE_S")
                    .HasColumnType("decimal(18,2)");

                b.Property<decimal?>("Volt_T")
                    .HasColumnName("PHASE_T")
                    .HasColumnType("decimal(18,2)");

                b.Property<decimal?>("Amp_R")
                    .HasColumnName("AMPERE_R")
                    .HasColumnType("decimal(18,3)");

                b.Property<decimal?>("Amp_S")
                    .HasColumnName("AMPERE_S")
                    .HasColumnType("decimal(18,3)");

                b.Property<decimal?>("Amp_T")
                    .HasColumnName("AMPERE_T")
                    .HasColumnType("decimal(18,3)");

                b.Property<decimal?>("Daya_Watt")
                    .HasColumnName("W")
                    .HasColumnType("decimal(18,1)");

                b.Property<decimal?>("Cos_Phi")
                    .HasColumnName("CosPhi")
                    .HasColumnType("decimal(18,3)");

                b.Property<decimal?>("Frekuensi_Hz")
                    .HasColumnName("F")
                    .HasColumnType("decimal(18,2)");

                b.Property<decimal?>("Energi_Aktif_Wh")
                    .HasColumnName("Aktif_Power")
                    .HasColumnType("decimal(18,2)");

                b.Property<decimal?>("Total_Energy_Wh")
                    .HasColumnName("TotalW")
                    .HasColumnType("decimal(18,2)");

                b.Property<decimal?>("TotalW1M_Wh")
                    .HasColumnName("TotalW1M")
                    .HasColumnType("decimal(18,2)");

                b.HasKey("Id");

                b.HasIndex("DeviceKey")
                    .HasName("IX_KWHData_DeviceKey");

                b.HasIndex("Waktu_Server")
                    .HasName("IX_KWHData_ReceivedTime");

                b.HasIndex("Waktu_Device")
                    .HasName("IX_KWHData_TerminalTime");

                b.HasIndex("DeviceKey", "Waktu_Server")
                    .HasName("IX_KWHData_DeviceKey_TerminalTime");

                b.HasOne("KWHMonitoring.Models.DeviceRegistry", null)
                    .WithMany()
                    .HasPrincipalKey("DeviceKey")
                    .HasForeignKey("DeviceKey")
                    .HasConstraintName("FK_KWHData_DeviceRegistry")
                    .OnDelete(DeleteBehavior.Restrict);

                b.ToTable("KWHData");
            });

            modelBuilder.Entity("KWHMonitoring.Models.KWHDataHistory", b =>
            {
                b.Property<long>("HistoryId")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<long>("OriginalId");

                b.Property<string>("DeviceKey")
                    .IsRequired()
                    .HasColumnType("varchar(20)")
                    .HasMaxLength(20);

                b.Property<System.DateTime?>("TerminalTime")
                    .HasColumnType("datetime2");

                b.Property<System.DateTime>("ReceivedTime")
                    .HasColumnType("datetime2");

                b.Property<string>("GroupName")
                    .HasColumnType("nvarchar(100)")
                    .HasMaxLength(100);

                b.Property<string>("DeviceId")
                    .HasColumnType("nvarchar(50)")
                    .HasMaxLength(50);

                b.Property<decimal?>("PhaseR")
                    .HasColumnName("PHASE_R")
                    .HasColumnType("decimal(18,2)");

                b.Property<decimal?>("PhaseS")
                    .HasColumnName("PHASE_S")
                    .HasColumnType("decimal(18,2)");

                b.Property<decimal?>("PhaseT")
                    .HasColumnName("PHASE_T")
                    .HasColumnType("decimal(18,2)");

                b.Property<decimal?>("AmpereR")
                    .HasColumnName("AMPERE_R")
                    .HasColumnType("decimal(18,3)");

                b.Property<decimal?>("AmpereS")
                    .HasColumnName("AMPERE_S")
                    .HasColumnType("decimal(18,3)");

                b.Property<decimal?>("AmpereT")
                    .HasColumnName("AMPERE_T")
                    .HasColumnType("decimal(18,3)");

                b.Property<decimal?>("W")
                    .HasColumnType("decimal(18,1)");

                b.Property<decimal?>("CosPhi")
                    .HasColumnType("decimal(18,3)");

                b.Property<decimal?>("F")
                    .HasColumnType("decimal(18,2)");

                b.Property<decimal?>("AktifPower")
                    .HasColumnName("Aktif_Power")
                    .HasColumnType("decimal(18,2)");

                b.Property<decimal?>("TotalW")
                    .HasColumnType("decimal(18,2)");

                b.Property<decimal?>("TotalW1M")
                    .HasColumnType("decimal(18,2)");

                b.Property<System.DateTime>("ArchivedAt")
                    .HasColumnType("datetime2");

                b.HasKey("HistoryId");

                b.HasIndex("DeviceKey")
                    .HasName("IX_KWHData_History_DeviceKey");

                b.HasIndex("ArchivedAt")
                    .HasName("IX_KWHData_History_ArchivedAt");

                b.ToTable("KWHData_History");
            });

            modelBuilder.Entity("KWHMonitoring.Models.AnomalyLog", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("DeviceKey")
                    .IsRequired()
                    .HasColumnType("nvarchar(50)")
                    .HasMaxLength(50);

                b.Property<string>("DeviceId")
                    .HasColumnType("nvarchar(50)")
                    .HasMaxLength(50);

                b.Property<string>("AnomalyType")
                    .IsRequired()
                    .HasColumnType("nvarchar(20)")
                    .HasMaxLength(20);

                b.Property<decimal>("PowerValue")
                    .HasColumnType("decimal(18,2)");

                b.Property<decimal>("ThresholdValue")
                    .HasColumnType("decimal(18,2)");

                b.Property<decimal>("Deviation")
                    .HasColumnType("decimal(5,2)");

                b.Property<System.DateTime>("DetectedTime")
                    .HasColumnType("datetime2");

                b.Property<decimal?>("EMAValue")
                    .HasColumnType("decimal(18,2)");

                b.Property<string>("ThresholdMode")
                    .HasColumnType("nvarchar(20)")
                    .HasMaxLength(20);

                b.Property<bool?>("Acknowledged");

                b.Property<System.DateTime?>("AcknowledgedTime")
                    .HasColumnType("datetime2");

                b.Property<string>("Notes")
                    .HasColumnType("nvarchar(500)")
                    .HasMaxLength(500);

                b.HasKey("Id");

                b.HasIndex("DetectedTime")
                    .HasName("IX_AnomalyLogs_DetectedTime");

                b.HasIndex("DeviceKey")
                    .HasName("IX_AnomalyLogs_DeviceKey");

                b.ToTable("AnomalyLogs");
            });

            modelBuilder.Entity("KWHMonitoring.Models.AppLog", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("LogLevel")
                    .IsRequired()
                    .HasColumnType("varchar(20)")
                    .HasMaxLength(20);

                b.Property<string>("Message")
                    .IsRequired()
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("Topic")
                    .HasColumnType("varchar(200)")
                    .HasMaxLength(200);

                b.Property<string>("DeviceKey")
                    .HasColumnType("varchar(20)")
                    .HasMaxLength(20);

                b.Property<System.DateTime>("CreatedAt")
                    .HasColumnType("datetime2");

                b.HasKey("Id");

                b.HasIndex("CreatedAt")
                    .HasName("IX_AppLog_CreatedAt");

                b.HasIndex("LogLevel")
                    .HasName("IX_AppLog_Level");

                b.ToTable("AppLog");
            });

            modelBuilder.Entity("KWHMonitoring.Models.AppSettingsRecord", b =>
            {
                b.Property<int>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("SettingKey")
                    .IsRequired()
                    .HasColumnType("nvarchar(100)")
                    .HasMaxLength(100);

                b.Property<string>("SettingValue")
                    .IsRequired()
                    .HasColumnType("nvarchar(500)")
                    .HasMaxLength(500);

                b.Property<System.DateTime?>("UpdatedAt")
                    .HasColumnType("datetime2");

                b.HasKey("Id");

                b.HasIndex("SettingKey")
                    .IsUnique()
                    .HasName("IX_AppSettings_SettingKey");

                b.ToTable("AppSettings");
            });

            modelBuilder.Entity("KWHMonitoring.Models.ColumnMapping", b =>
            {
                b.Property<int>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("OldColumnName")
                    .IsRequired()
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                b.Property<string>("NewColumnName")
                    .IsRequired()
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                b.Property<bool>("IsActive");

                b.Property<System.DateTime>("CreatedAt")
                    .HasColumnType("datetime2");

                b.HasKey("Id");

                b.HasIndex("OldColumnName")
                    .IsUnique()
                    .HasName("IX_ColumnMapping_OldName");

                b.ToTable("ColumnMapping");
            });

            modelBuilder.Entity("KWHMonitoring.Models.ColumnScaleConfig", b =>
            {
                b.Property<string>("ColumnName")
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                b.Property<decimal>("ScaleFactor")
                    .HasColumnType("decimal(18,5)");

                b.Property<string>("RegisterAddress")
                    .HasColumnType("varchar(10)")
                    .HasMaxLength(10);

                b.Property<string>("DataType")
                    .IsRequired()
                    .HasColumnType("varchar(20)")
                    .HasMaxLength(20);

                b.Property<string>("Unit")
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                b.Property<string>("Category")
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                b.Property<string>("Description")
                    .HasColumnType("varchar(500)")
                    .HasMaxLength(500);

                b.Property<bool>("IsDynamic");

                b.Property<System.DateTime>("LastUpdated")
                    .HasColumnType("datetime2");

                b.HasKey("ColumnName");

                b.ToTable("ColumnScaleConfig");
            });

            modelBuilder.Entity("KWHMonitoring.Models.HourlyEnergy", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("DeviceKey")
                    .IsRequired()
                    .HasColumnType("nvarchar(100)")
                    .HasMaxLength(100);

                b.Property<System.DateTime>("Hour")
                    .HasColumnType("datetime2");

                b.Property<decimal>("EnergyKWh")
                    .HasColumnType("decimal(18,4)");

                b.Property<System.DateTime>("CalculatedAt")
                    .HasColumnType("datetime2");

                b.HasKey("Id");

                b.ToTable("HourlyEnergy");
            });

            modelBuilder.Entity("KWHMonitoring.Models.DailyEnergy", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("DeviceKey")
                    .IsRequired()
                    .HasColumnType("nvarchar(100)")
                    .HasMaxLength(100);

                b.Property<System.DateTime>("Date")
                    .HasColumnType("date");

                b.Property<decimal>("EnergyKWh")
                    .HasColumnType("decimal(18,4)");

                b.Property<System.DateTime>("CalculatedAt")
                    .HasColumnType("datetime2");

                b.HasKey("Id");

                b.ToTable("DailyEnergy");
            });

            modelBuilder.Entity("KWHMonitoring.Models.FailedMessage", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("Topic")
                    .IsRequired()
                    .HasColumnType("varchar(500)")
                    .HasMaxLength(500);

                b.Property<string>("Payload")
                    .IsRequired()
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("Reason")
                    .HasColumnType("nvarchar(500)")
                    .HasMaxLength(500);

                b.Property<int>("RetryCount");

                b.Property<bool>("IsResolved");

                b.Property<System.DateTime>("ReceivedAt")
                    .HasColumnType("datetime2");

                b.Property<System.DateTime?>("ResolvedAt")
                    .HasColumnType("datetime2");

                b.HasKey("Id");

                b.HasIndex("IsResolved")
                    .HasName("IX_FailedMessages_IsResolved");

                b.HasIndex("ReceivedAt")
                    .HasName("IX_FailedMessages_ReceivedAt");

                b.ToTable("FailedMessages");
            });

            modelBuilder.Entity("KWHMonitoring.Models.MonthlyEnergy", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("DeviceKey")
                    .IsRequired()
                    .HasColumnType("nvarchar(100)")
                    .HasMaxLength(100);

                b.Property<int>("Year");

                b.Property<int>("Month");

                b.Property<decimal>("EnergyKWh")
                    .HasColumnType("decimal(18,4)");

                b.Property<System.DateTime>("CalculatedAt")
                    .HasColumnType("datetime2");

                b.HasKey("Id");

                b.ToTable("MonthlyEnergy");
            });

            modelBuilder.Entity("KWHMonitoring.Models.YearlyEnergy", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                b.Property<string>("DeviceKey")
                    .IsRequired()
                    .HasColumnType("nvarchar(100)")
                    .HasMaxLength(100);

                b.Property<int>("Year");

                b.Property<decimal>("EnergyKWh")
                    .HasColumnType("decimal(18,4)");

                b.Property<System.DateTime>("CalculatedAt")
                    .HasColumnType("datetime2");

                b.HasKey("Id");

                b.ToTable("YearlyEnergy");
            });
#pragma warning restore 612, 618
        }
    }
}
