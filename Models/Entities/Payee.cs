using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

/// <summary>
/// Represents a payee (person or company receiving/sending money)
/// </summary>
[Table("cfPayees")]
public class Payee
{
    [Key]
    [Column("PayeeId")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int PayeeId { get; set; }

    [Required]
    [Column("PayeeName")]
    [StringLength(64)]
    public string PayeeName { get; set; } = string.Empty;

    [Required]
    [Column("UserId")]
    public int UserId { get; set; }

    [Required]
    [Column("IsDeleted")]
    public bool IsDeleted { get; set; }

    // Navigation properties
    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    public virtual ICollection<Budget> Budgets { get; set; } = [];
    public virtual ICollection<Transaction> Transactions { get; set; } = [];
}
