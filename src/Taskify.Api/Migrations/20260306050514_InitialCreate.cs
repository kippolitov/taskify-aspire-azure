using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Taskify.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    name = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    description = table.Column<string>(
                        type: "character varying(1000)",
                        maxLength: 1000,
                        nullable: true
                    ),
                    created_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    display_name = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    role = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "task_items",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(
                        type: "character varying(300)",
                        maxLength: 300,
                        nullable: false
                    ),
                    description = table.Column<string>(
                        type: "character varying(4000)",
                        maxLength: 4000,
                        nullable: true
                    ),
                    status = table.Column<int>(type: "integer", nullable: false),
                    assignee_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    updated_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_items_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_task_items_users_assignee_id",
                        column: x => x.assignee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "comments",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    task_item_id = table.Column<int>(type: "integer", nullable: false),
                    author_id = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(
                        type: "character varying(10000)",
                        maxLength: 10000,
                        nullable: false
                    ),
                    created_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    edited_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_comments_task_items_task_item_id",
                        column: x => x.task_item_id,
                        principalTable: "task_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "fk_comments_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.InsertData(
                table: "projects",
                columns: new[] { "id", "created_at", "description", "name" },
                values: new object[,]
                {
                    {
                        1,
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                        "Redesign and re-platform the mobile app experience",
                        "Mobile Relaunch",
                    },
                    {
                        2,
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                        "Build the next-generation internal API gateway",
                        "API Gateway v2",
                    },
                    {
                        3,
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                        "Establish shared UI component library and tokens",
                        "Design System",
                    },
                }
            );

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "display_name", "role" },
                values: new object[,]
                {
                    { 1, "Jordan Rivera", 0 },
                    { 2, "Alex Chen", 1 },
                    { 3, "Priya Sharma", 1 },
                    { 4, "Marcus Johnson", 1 },
                    { 5, "Sofia Lindqvist", 1 },
                }
            );

            migrationBuilder.InsertData(
                table: "task_items",
                columns: new[]
                {
                    "id",
                    "assignee_id",
                    "created_at",
                    "description",
                    "project_id",
                    "status",
                    "title",
                    "updated_at",
                },
                values: new object[,]
                {
                    {
                        1,
                        1,
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                        null,
                        1,
                        3,
                        "Define new navigation structure",
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                    },
                    {
                        2,
                        2,
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                        null,
                        1,
                        2,
                        "Implement bottom tab bar",
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                    },
                    {
                        3,
                        3,
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                        null,
                        1,
                        1,
                        "Auth flow redesign",
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                    },
                    {
                        4,
                        4,
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                        null,
                        1,
                        1,
                        "Accessibility audit",
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                    },
                    {
                        5,
                        null,
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                        null,
                        1,
                        0,
                        "Beta release preparation",
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                    },
                    {
                        6,
                        2,
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                        null,
                        2,
                        2,
                        "Route configuration schema",
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                    },
                    {
                        7,
                        5,
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                        null,
                        2,
                        1,
                        "Rate limiting middleware",
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                    },
                    {
                        8,
                        null,
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                        null,
                        2,
                        0,
                        "Load test report",
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                    },
                    {
                        9,
                        1,
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                        null,
                        3,
                        3,
                        "Color token definition",
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                    },
                    {
                        10,
                        3,
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                        null,
                        3,
                        1,
                        "Button component",
                        new DateTimeOffset(
                            new DateTime(2026, 3, 5, 12, 0, 0, 0, DateTimeKind.Unspecified),
                            new TimeSpan(0, 0, 0, 0, 0)
                        ),
                    },
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_comments_author_id",
                table: "comments",
                column: "author_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_comments_task_item_id",
                table: "comments",
                column: "task_item_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_projects_name",
                table: "projects",
                column: "name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_task_items_assignee_id",
                table: "task_items",
                column: "assignee_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_task_items_project_id",
                table: "task_items",
                column: "project_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_users_display_name",
                table: "users",
                column: "display_name",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "comments");

            migrationBuilder.DropTable(name: "task_items");

            migrationBuilder.DropTable(name: "projects");

            migrationBuilder.DropTable(name: "users");
        }
    }
}
