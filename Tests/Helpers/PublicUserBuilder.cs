namespace dwa_ver_val.Tests.Helpers;

/// <summary>
/// Test factory for <see cref="PublicUser"/> in known states.
/// All factories set the 5 required props; tests can override individual fields.
/// </summary>
public static class PublicUserBuilder
{
    public static PublicUser Active(string email = "test@example.com")
    {
        return new PublicUser
        {
            EmailAddress = email,
            PasswordHash = "hashed",
            FirstName = "Test",
            LastName = "User",
            Status = "Active",
            EmailConfirmed = true,
            RegistrationDate = DateTime.UtcNow
        };
    }

    public static PublicUser Pending(string email = "pending@example.com")
    {
        return new PublicUser
        {
            EmailAddress = email,
            PasswordHash = "hashed",
            FirstName = "Pending",
            LastName = "User",
            Status = "Pending",
            EmailConfirmed = false,
            RegistrationDate = DateTime.UtcNow
        };
    }

    public static PublicUser Suspended(string email = "suspended@example.com")
    {
        return new PublicUser
        {
            EmailAddress = email,
            PasswordHash = "hashed",
            FirstName = "Suspended",
            LastName = "User",
            Status = "Suspended",
            EmailConfirmed = true,
            RegistrationDate = DateTime.UtcNow
        };
    }
}
