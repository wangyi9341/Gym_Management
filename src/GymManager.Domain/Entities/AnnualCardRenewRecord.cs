using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GymManager.Domain.Entities;

/// <summary>
/// 年卡续费记录（用于审计/回溯）。
/// </summary>
public sealed class AnnualCardRenewRecord : AuditableEntity
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
    /// 续费时间（本地时间）。
    /// </summary>
    public DateTime RenewedAt { get; set; }

    /// <summary>
    /// 续费前的开通日期（Date）。
    /// </summary>
    public DateTime StartDateBefore { get; set; }

    /// <summary>
    /// 续费前的截至日期（Date）。
    /// </summary>
    public DateTime EndDateBefore { get; set; }

    /// <summary>
    /// 续费后的开通日期（Date）。
    /// </summary>
    public DateTime StartDateAfter { get; set; }

    /// <summary>
    /// 续费后的截至日期（Date）。
    /// </summary>
    public DateTime EndDateAfter { get; set; }

    /// <summary>
    /// 备注（可选）。
    /// </summary>
    [MaxLength(200)]
    public string? Note { get; set; }
}

