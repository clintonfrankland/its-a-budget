using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

/// <summary>
/// Represents an account type (Checking, Savings, Credit Card, etc.)
/// </summary>
[Table("cfAccountTypes")]
public class AccountType
{
    [Key]
    [Column("AccountTypeId")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)] // Not identity - manually assigned
    public int AccountTypeId { get; set; }

    [Required]
    [Column("AccountTypeName")]
    [StringLength(32)]
    public string AccountTypeName { get; set; } = string.Empty;

    // Navigation properties
    public virtual ICollection<Account> Accounts { get; set; } = [];
}
