using OrderMgmt.Domain.Common;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Domain.Entities.Organization;

public class Branch : BaseEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Address { get; set; }

    /// <summary>Period lock: vouchers dated on or before this VN date can no longer change.</summary>
    public DateOnly? LockedUntil { get; set; }

    /// <summary>Opening date of the branch (P8): opening stock and opening balances post on it. Fixed once documents exist.</summary>
    public DateOnly? OpeningDate { get; set; }

    /// <summary>Demo branch created for training: filled with sample data by a background worker.</summary>
    public bool IsDemo { get; set; }
    /// <summary>Fill state of a demo branch: None for a normal branch, Running, then Ready or Failed.</summary>
    public SampleDataStatus SampleDataStatus { get; set; } = SampleDataStatus.None;
    /// <summary>Fill progress in percent (0-100), written by the sample data runner.</summary>
    public int SampleDataProgress { get; set; }
    /// <summary>Why the fill failed: "step key: message", cut to 1000 characters.</summary>
    public string? SampleDataError { get; set; }
}
