namespace Herensuge.Modules.Identity.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerificationResult Verify(string passwordHash, string password);

    /// <summary>Spends the time of a real verification, so unknown accounts cannot be told apart by timing.</summary>
    void SimulateVerification(string password);
}

public enum PasswordVerificationResult
{
    Failed,
    Success,
    SuccessRehashNeeded,
}
