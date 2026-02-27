using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddTodoPositionAndMove : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "position",
                table: "todos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY user_id ORDER BY id) - 1 AS rn
                    FROM todos
                )
                UPDATE todos t
                SET position = ranked.rn
                FROM ranked
                WHERE t.id = ranked.id;
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_todos_user_id_position",
                table: "todos",
                columns: new[] { "user_id", "position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_todos_user_id_position",
                table: "todos");

            migrationBuilder.DropColumn(
                name: "position",
                table: "todos");
        }
    }
}
