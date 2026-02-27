using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Entity;

[Table("todos")]
public class Todo
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("text")]
    public string Text { get; set; } = string.Empty;

    [Column("completed")]
    public bool Completed { get; set; }

    [Column("position")]
    public int Position { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }
}
