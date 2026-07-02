using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SubjectHitman.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subjects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    inn = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    snils = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subjects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "report_usages",
                columns: table => new
                {
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_free = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    ordered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_usages", x => x.report_id);
                    table.ForeignKey(
                        name: "fk_report_usages_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "search_keys",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key_type = table.Column<short>(type: "smallint", nullable: false),
                    hash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_keys", x => x.id);
                    table.ForeignKey(
                        name: "fk_search_keys_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subject_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type_code = table.Column<string>(type: "text", nullable: false),
                    series = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    number = table.Column<string>(type: "text", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subject_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_subject_documents_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subject_names",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    middle_name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subject_names", x => x.id);
                    table.ForeignKey(
                        name: "fk_subject_names_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_report_usages_count",
                table: "report_usages",
                columns: new[] { "subject_id", "ordered_at" },
                filter: "is_free AND status = 1");

            migrationBuilder.CreateIndex(
                name: "ix_search_keys_hash",
                table: "search_keys",
                column: "hash");

            migrationBuilder.CreateIndex(
                name: "ix_search_keys_subject_id_key_type_hash",
                table: "search_keys",
                columns: new[] { "subject_id", "key_type", "hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subject_documents_subject_id_type_code_series_number_issue_",
                table: "subject_documents",
                columns: new[] { "subject_id", "type_code", "series", "number", "issue_date" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_subject_names_subject_id_last_name_first_name_middle_name",
                table: "subject_names",
                columns: new[] { "subject_id", "last_name", "first_name", "middle_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_usages");

            migrationBuilder.DropTable(
                name: "search_keys");

            migrationBuilder.DropTable(
                name: "subject_documents");

            migrationBuilder.DropTable(
                name: "subject_names");

            migrationBuilder.DropTable(
                name: "subjects");
        }
    }
}
