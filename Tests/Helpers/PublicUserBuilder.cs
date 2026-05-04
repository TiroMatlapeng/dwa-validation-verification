namespace dwa_ver_val.Tests.Helpers;

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
}
