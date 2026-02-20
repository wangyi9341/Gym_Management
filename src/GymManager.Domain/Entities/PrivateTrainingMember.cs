using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GymManager.Domain.Enums;

namespace GymManager.Domain.Entities;

/// <summary>
/// 私教课会员。
/// </summary>
public sealed class PrivateTrainingMember : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>
    /// 姓名。
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 性别。
    /// </summary>
    public Gender Gender { get; set; } = Gender.Unknown;

    /// <summary>
    /// 电话号。
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// 已交费用（汇总值；来源于费用记录的累计，保存时会同步）。
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal PaidAmount { get; set; }

    /// <summary>
    /// 总课程数。
    /// </summary>
    [Range(0, int.MaxValue)]
    public int TotalSessions { get; set; }

    /// <summary>
    /// 已使用课程数（由课程消耗记录累计，保存时会同步）。
    /// </summary>
    [Range(0, int.MaxValue)]
    public int UsedSessions { get; set; }

    /// <summary>
    /// 剩余课程数（业务逻辑：总课程数 - 已使用课程数）。
    /// </summary>
    [NotMapped]
    public int RemainingSessions => Math.Max(0, TotalSessions - UsedSessions);

    public ICollection<PrivateTrainingFeeRecord> FeeRecords { get; set; } = new List<PrivateTrainingFeeRecord>();

    public ICollection<PrivateTrainingSessionRecord> SessionRecords { get; set; } = new List<PrivateTrainingSessionRecord>();
}

