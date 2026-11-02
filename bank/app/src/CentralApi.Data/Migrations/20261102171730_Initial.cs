using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Banks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: false),
                    SwiftCode = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    Location = table.Column<string>(type: "NVARCHAR2(256)", maxLength: 256, nullable: false),
                    ApiKey = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    ApiAddress = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    PaymentUrl = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    CardPaymentUrl = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    BankIdentificationCardNumbers = table.Column<string>(type: "NVARCHAR2(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banks", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Banks");
        }
    }
}
