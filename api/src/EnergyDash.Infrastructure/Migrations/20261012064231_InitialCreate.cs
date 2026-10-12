using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyDash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Agreement",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fuel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TariffCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ValidFromUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    ValidToUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agreement", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlertRule",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    ParamsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BatteryInterval",
                columns: table => new
                {
                    IntervalStartUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    GridImportKwh = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    GridExportKwh = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    BattChargeKwh = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    BattDischargeKwh = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    HomeKwh = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    SolarKwh = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    SocEndPct = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatteryInterval", x => x.IntervalStartUtc);
                });

            migrationBuilder.CreateTable(
                name: "Consumption",
                columns: table => new
                {
                    Fuel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IntervalStartUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    IntervalEndUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    Kwh = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    RawValue = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    RawUnit = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FetchedUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consumption", x => new { x.Fuel, x.IntervalStartUtc });
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FriendlyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Xml = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Dispatch",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    DeltaKwh = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsBoost = table.Column<bool>(type: "bit", nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dispatch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiveAccumulator",
                columns: table => new
                {
                    SlotStartUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    ImportWh = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Samples = table.Column<int>(type: "int", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    HasGap = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveAccumulator", x => x.SlotStartUtc);
                });

            migrationBuilder.CreateTable(
                name: "Meter",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fuel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    MpanMprn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Serial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsExport = table.Column<bool>(type: "bit", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meter", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PricedDay",
                columns: table => new
                {
                    Fuel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EnergyCostPence = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    StandingPence = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    SlotCount = table.Column<int>(type: "int", nullable: false),
                    MissingSlots = table.Column<int>(type: "int", nullable: false),
                    UnpricedSlots = table.Column<int>(type: "int", nullable: false),
                    WarningsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PricedUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricedDay", x => new { x.Fuel, x.LocalDate });
                });

            migrationBuilder.CreateTable(
                name: "PricedSlot",
                columns: table => new
                {
                    Fuel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SlotStartUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    Kwh = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    RatePence = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    Band = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RateSource = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CostPence = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    TariffCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PricedUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricedSlot", x => new { x.Fuel, x.SlotStartUtc });
                });

            migrationBuilder.CreateTable(
                name: "ReportRun",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WeekStartLocal = table.Column<DateOnly>(type: "date", nullable: false),
                    SentUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportRun", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Secret",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Protected = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Hint = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Secret", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Setting",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ValueJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Setting", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "StandingCharge",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fuel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TariffCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ValidFromUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    ValidToUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    PencePerDayIncVat = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandingCharge", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncCommand",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Argument = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    ClaimedUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncCommand", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Source = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    FinishedUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Ok = table.Column<bool>(type: "bit", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitRate",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fuel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TariffCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Register = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ValidFromUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    ValidToUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    PenceIncVat = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitRate", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlertEvent",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleId = table.Column<long>(type: "bigint", nullable: false),
                    SubjectKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FiredUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Threshold = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    DeliveryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReconciledValue = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertEvent_AlertRule_RuleId",
                        column: x => x.RuleId,
                        principalTable: "AlertRule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Agreement_Fuel_TariffCode_ValidFromUtc",
                table: "Agreement",
                columns: new[] { "Fuel", "TariffCode", "ValidFromUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvent_RuleId_SubjectKey",
                table: "AlertEvent",
                columns: new[] { "RuleId", "SubjectKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertRule_Kind",
                table: "AlertRule",
                column: "Kind",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dispatch_StartUtc_EndUtc_Source_DeviceId",
                table: "Dispatch",
                columns: new[] { "StartUtc", "EndUtc", "Source", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meter_Fuel_MpanMprn_Serial",
                table: "Meter",
                columns: new[] { "Fuel", "MpanMprn", "Serial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportRun_WeekStartLocal",
                table: "ReportRun",
                column: "WeekStartLocal",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StandingCharge_Fuel_TariffCode_ValidFromUtc",
                table: "StandingCharge",
                columns: new[] { "Fuel", "TariffCode", "ValidFromUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncCommand_CompletedUtc_ClaimedUtc",
                table: "SyncCommand",
                columns: new[] { "CompletedUtc", "ClaimedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncLog_Source_StartedUtc",
                table: "SyncLog",
                columns: new[] { "Source", "StartedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UnitRate_Fuel_TariffCode_Register_ValidFromUtc",
                table: "UnitRate",
                columns: new[] { "Fuel", "TariffCode", "Register", "ValidFromUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Agreement");

            migrationBuilder.DropTable(
                name: "AlertEvent");

            migrationBuilder.DropTable(
                name: "BatteryInterval");

            migrationBuilder.DropTable(
                name: "Consumption");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "Dispatch");

            migrationBuilder.DropTable(
                name: "LiveAccumulator");

            migrationBuilder.DropTable(
                name: "Meter");

            migrationBuilder.DropTable(
                name: "PricedDay");

            migrationBuilder.DropTable(
                name: "PricedSlot");

            migrationBuilder.DropTable(
                name: "ReportRun");

            migrationBuilder.DropTable(
                name: "Secret");

            migrationBuilder.DropTable(
                name: "Setting");

            migrationBuilder.DropTable(
                name: "StandingCharge");

            migrationBuilder.DropTable(
                name: "SyncCommand");

            migrationBuilder.DropTable(
                name: "SyncLog");

            migrationBuilder.DropTable(
                name: "UnitRate");

            migrationBuilder.DropTable(
                name: "AlertRule");
        }
    }
}
