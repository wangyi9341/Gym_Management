namespace GymManager.Domain.Exceptions;

/// <summary>
/// 领域校验异常：用于表达业务规则不满足（例如：课程消耗超过总课程数）。
/// </summary>
public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message)
    {
    }

    public DomainValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

