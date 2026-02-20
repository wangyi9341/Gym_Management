using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GymManager.Domain.Enums;

namespace GymManager.Domain.Entities;

/// <summary>
/// 年卡会员。
/// </summary>
public sealed class AnnualCardMember : AuditableEntity
{
    public const int DefaultExpiringDays = 3;

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
    /// 开通年卡时间。
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// 年卡截止时间。
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// 当前状态（默认规则：到期前 3 天内为“即将到期”）。
    /// </summary>
    [NotMapped]
    public AnnualCardStatus Status => GetStatus(DateTime.Today, DefaultExpiringDays);

    /// <summary>
    /// 根据指定规则计算年卡状态。
    /// </summary>
    /// <param name="today">“今天”的日期（只取 Date 部分）。</param>
    /// <param name="expiringDays">即将到期阈值（例如：3 表示到期前 3 天内）。</param>
    public AnnualCardStatus GetStatus(DateTime today, int expiringDays)
    {
        var baseDate = today.Date;
        var end = EndDate.Date;

        if (end < baseDate)
        {
            return AnnualCardStatus.Expired;
        }

        if (end <= baseDate.AddDays(expiringDays))
        {
            return AnnualCardStatus.ExpiringSoon;
        }

        return AnnualCardStatus.Normal;
    }

    /// <summary>
    /// 距离到期剩余天数（负数表示已过期）。
    /// </summary>
    [NotMapped]
    public int DaysToExpire => (EndDate.Date - DateTime.Today).Days;
}

