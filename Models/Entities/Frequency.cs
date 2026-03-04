using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

/// <summary>
/// Represents a budget frequency (Weekly, Monthly, Quarterly, etc.)
/// </summary>
[Table("cfFrequencies")]
public class Frequency
{
    [Key]
    [Column("FrequencyId")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int FrequencyId { get; set; }

    [Required]
    [Column("FrequencyName")]
    [StringLength(32)]
    public string FrequencyName { get; set; } = string.Empty;

    [Column("Sort")]
    public int? Sort { get; set; }

    // Navigation properties
    public virtual ICollection<Budget> Budgets { get; set; } = [];
}
