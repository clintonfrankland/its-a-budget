using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

[Table("MigrationErrors")]
public class MigrationError
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [StringLength(256)]
    public string? MigrationName { get; set; }

    [Required]
    public string ErrorMessage { get; set; } = string.Empty;

    [Required]
    public string StackTrace { get; set; } = string.Empty;

    [Required]
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
