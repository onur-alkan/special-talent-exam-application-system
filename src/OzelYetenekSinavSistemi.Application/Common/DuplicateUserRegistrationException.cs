namespace OzelYetenekSinavSistemi.Application.Common;

public enum DuplicateUserRegistrationKind
{
    Identity,
    Email
}

public sealed class DuplicateUserRegistrationException : Exception
{
    public DuplicateUserRegistrationKind Kind { get; }

    public DuplicateUserRegistrationException(DuplicateUserRegistrationKind kind)
        : base($"Duplicate user registration: {kind}")
    {
        Kind = kind;
    }
}
