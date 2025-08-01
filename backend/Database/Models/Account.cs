namespace NzbWebDAV.Database.Models;

public class Account
{
    public AccountType Type { get; init; }
    public string Username { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string RandomSalt { get; init; } = string.Empty;

    public enum AccountType
    {
        Admin = 1,
        WebDav = 2,
    }
}