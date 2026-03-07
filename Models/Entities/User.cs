using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClintonFrankland.Models.Entities;

/// <summary>
/// Represents an application user
/// </summary>
[Table("cfUsers")]
public class User
{
    [Key]
    [Column("UserId")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)] // Not identity - manually assigned
    public int UserId { get; set; }

    [Required]
    [Column("SiteId")]
    public int SiteId { get; set; }

    [Required]
    [Column("UserName")]
    [StringLength(64)]
    public string UserName { get; set; } = string.Empty;

    [Column("EmailAddress")]
    [StringLength(256)]
    public string? EmailAddress { get; set; }

    [Required]
    [Column("DisplayName")]
    [StringLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [Column("IsAdmin")]
    public bool IsAdmin { get; set; }

    [Required]
    [Column("ListButtonsRight")]
    public bool ListButtonsRight { get; set; } = true;

    [Required]
    [Column("Salt")]
    [StringLength(32)]
    public string Salt { get; set; } = string.Empty;

    [Required]
    [Column("PasswordHash")]
    [StringLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [Column("IsDeleted")]
    public bool IsDeleted { get; set; }

    [Required]
    [Column("FirstLogin")]
    public DateTime FirstLogin { get; set; }

    [Required]
    [Column("LastLogin")]
    public DateTime LastLogin { get; set; }

    [Column("LastIPAddress")]
    [StringLength(16)]
    public string? LastIPAddress { get; set; }

    [Column("PasswordResetToken")]
    public Guid? PasswordResetToken { get; set; }

    [Column("PasswordResetRequestOn")]
    public DateTime? PasswordResetRequestOn { get; set; }

    // Navigation properties
    public virtual ICollection<Account> Accounts { get; set; } = [];
    public virtual ICollection<Budget> Budgets { get; set; } = [];
    public virtual ICollection<Category> Categories { get; set; } = [];
    public virtual ICollection<Payee> Payees { get; set; } = [];
    public virtual ICollection<Transaction> Transactions { get; set; } = [];
}
