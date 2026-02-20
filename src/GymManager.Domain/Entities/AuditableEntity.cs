using System.ComponentModel.DataAnnotations;

namespace GymManager.Domain.Entities;

/// <summary>
/// 带创建/更新时间的基础实体（由数据层在保存时自动维护）。
/// </summary>
public abstract class AuditableEntity
{
    /// <summary>
    /// 创建时间（本地时间）。
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间（本地时间）。
    /// </summary>
    [Required]
    public DateTime UpdatedAt { get; set; }
}

