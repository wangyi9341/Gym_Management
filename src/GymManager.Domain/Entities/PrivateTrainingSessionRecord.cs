using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GymManager.Domain.Entities;

/// <summary>
/// 私教课课程消耗记录（用于追踪消课明细）。
/// </summary>
public sealed class PrivateTrainingSessionRecord
{
    public int Id { get; set; }

    [ForeignKey(nameof(Member))]
    public int MemberId { get; set; }

    public PrivateTrainingMember? Member { get; set; }

    /// <summary>
    /// 本次消耗的课程数（默认 1，可手动输入 >1）。
    /// </summary>
    [Range(1, int.MaxValue)]
    public int SessionsUsed { get; set; } = 1;

    /// <summary>
    /// 消课时间。
    /// </summary>
    public DateTime UsedAt { get; set; }

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

