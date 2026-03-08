using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GymManager.Domain.Entities;

/// <summary>
/// 年卡停卡记录（用于审计/回溯）。
/// </summary>
public sealed class AnnualCardPauseRecord : AuditableEntity
{
    public int Id { get; set; }

    [ForeignKey(nameof(Member))]
    public int MemberId { get; set; }

    public AnnualCardMember? Member { get; set; }

    /// <summary>
    /// 会员姓名快照（防止后续改名导致历史记录难以追溯）。
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string MemberName { get; set; } = string.Empty;

    /// <summary>
    /// 会员电话快照。
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string MemberPhone { get; set; } = string.Empty;

    /// <summary>
    /// 停卡开始日期（Date）。
    /// </summary>
    public DateTime PauseStartDate { get; set; }

    /// <summary>
    /// 恢复日期（停卡结束的下一天；Date）。
    /// </summary>
    public DateTime ResumeDate { get; set; }

    /// <summary>
    /// 停卡天数（>= 1）。
    /// </summary>
    public int PauseDays { get; set; }

    /// <summary>
    /// 停卡前的截至日期（Date）。
    /// </summary>
    public DateTime EndDateBefore { get; set; }

    /// <summary>
    /// 停卡后的截至日期（Date）。
    /// </summary>
    public DateTime EndDateAfter { get; set; }

    /// <summary>
    /// 备注（可选）。
    /// </summary>
    [MaxLength(200)]
    public string? Note { get; set; }
}
