using System.ComponentModel.DataAnnotations;

namespace GymManager.Domain.Entities;

/// <summary>
/// 教练信息（工号为主键）。
/// </summary>
public sealed class Coach : AuditableEntity
{
    /// <summary>
    /// 工号（主键）。
    /// </summary>
    [Key]
    [Required]
    [MaxLength(32)]
    public string EmployeeNo { get; set; } = string.Empty;

    /// <summary>
    /// 教练姓名。
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;
}

