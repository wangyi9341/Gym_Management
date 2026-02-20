using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GymManager.Domain.Entities;

/// <summary>
/// 私教课费用记录（用于追踪缴费明细）。
/// </summary>
public sealed class PrivateTrainingFeeRecord
{
    public int Id { get; set; }

    [ForeignKey(nameof(Member))]
    public int MemberId { get; set; }

    public PrivateTrainingMember? Member { get; set; }

    /// <summary>
    /// 缴费金额。
    /// </summary>
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    /// <summary>
    /// 缴费时间。
    /// </summary>
    public DateTime PaidAt { get; set; }

    /// <summary>
    /// 备注。
    /// </summary>
    [MaxLength(200)]
    public string? Note { get; set; }

    /// <summary>
    /// 记录创建时间。
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

