namespace GymManager.Domain.Enums;

/// <summary>
/// 年卡状态（用于列表颜色标识与到期提醒）。
/// </summary>
public enum AnnualCardStatus
{
    /// <summary>
    /// 正常（绿色）。
    /// </summary>
    Normal = 0,

    /// <summary>
    /// 即将到期（橙色，默认到期前 3 天内）。
    /// </summary>
    ExpiringSoon = 1,

    /// <summary>
    /// 已过期（红色）。
    /// </summary>
    Expired = 2
}

