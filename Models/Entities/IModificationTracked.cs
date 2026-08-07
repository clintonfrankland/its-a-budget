namespace ClintonFrankland.Models.Entities;

/// <summary>UTC persistence metadata for records whose current state can change.</summary>
public interface IModificationTracked
{
    DateTime UpdatedAtUtc { get; set; }
}
