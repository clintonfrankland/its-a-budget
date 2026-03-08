using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

/// <summary>
/// Represents a financial transaction (debit or credit)
/// </summary>
[Table("cfTransactions")]
public class Transaction
{
    [Key]
    [Column("TransactionId")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TransactionId { get; set; }

    [Required]
    [Column("TransactionDate", TypeName = "date")]
    public DateOnly TransactionDate { get; set; }

    [Required]
    [Column("Amount", TypeName = "decimal(9, 2)")]
    public decimal Amount { get; set; }

    [Required]
    [Column("PayeeId")]
    public int PayeeId { get; set; }

    [Required]
    [Column("CategoryId")]
    public int CategoryId { get; set; }

    [Required]
    [Column("AccountId")]
    public int AccountId { get; set; }

    [Required]
    [Column("Cleared")]
    public bool Cleared { get; set; }

    [Column("UserId")]
    public int? UserId { get; set; }

    [Column("Notes")]
    [MaxLength(500)]
    public string? Notes { get; set; }

    [Column("AttachmentPath")]
    [MaxLength(255)]
    public string? AttachmentPath { get; set; }

    // Navigation properties
    [ForeignKey("AccountId")]
    public virtual Account? Account { get; set; }

    [ForeignKey("CategoryId")]
    public virtual Category? Category { get; set; }

    [ForeignKey("PayeeId")]
    public virtual Payee? Payee { get; set; }

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }
}
