using Herensuge.Common.Domain;
using Herensuge.Modules.Identity.Domain.Users.Events;

namespace Herensuge.Modules.Identity.Domain.Users;

public sealed class User : AggregateRoot
{
    public const int MaxDisplayNameLength = 100;
    public const int MaxFailedSignInAttempts = 5;
    public const int MaxPasskeys = 20;
    public const int MaxKnownDevices = 20;
    public const int RecoveryCodeCount = 10;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly List<RecoveryCode> _recoveryCodes = [];
    private readonly List<ExternalLogin> _externalLogins = [];
    private readonly List<PasskeyCredential> _passkeys = [];
    private readonly List<KnownDevice> _knownDevices = [];

    private User()
    {
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public DateTime? EmailVerifiedAtUtc { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string? PasswordHash { get; private set; }

    public string? PhoneNumber { get; private set; }

    public DateTime? PhoneVerifiedAtUtc { get; private set; }

    /// <summary>TOTP secret, encrypted at rest by the infrastructure.</summary>
    public string? TotpSecret { get; private set; }

    public string? PendingTotpSecret { get; private set; }

    public DateTime? TotpEnabledAtUtc { get; private set; }

    public long? TotpLastUsedTimeStep { get; private set; }

    public int FailedSignInAttempts { get; private set; }

    public DateTime? LockoutEndsAtUtc { get; private set; }

    public bool IsPlatformAdmin { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<RecoveryCode> RecoveryCodes => _recoveryCodes;

    public IReadOnlyCollection<ExternalLogin> ExternalLogins => _externalLogins;

    public IReadOnlyCollection<PasskeyCredential> Passkeys => _passkeys;

    public IReadOnlyCollection<KnownDevice> KnownDevices => _knownDevices;

    public bool IsEmailVerified => EmailVerifiedAtUtc is not null;

    public bool HasPassword => PasswordHash is not null;

    public bool IsTotpEnabled => TotpEnabledAtUtc is not null;

    public bool HasVerifiedPhone => PhoneNumber is not null;

    /// <summary>A second factor exists for password and federated sign-ins.</summary>
    public bool HasMfa => IsTotpEnabled || HasVerifiedPhone;

    private int AccessMethodCount => (HasPassword ? 1 : 0) + _externalLogins.Count + _passkeys.Count;

    public static User Register(Email email, string displayName, string passwordHash, DateTime utcNow)
    {
        User user = Create(email, displayName, utcNow);
        user.PasswordHash = passwordHash;
        user.Raise(new UserRegisteredDomainEvent(user.Id, user.Email, user.DisplayName, "password"));
        return user;
    }

    public static User RegisterWithExternalLogin(
        Email email,
        string? displayName,
        string provider,
        string subject,
        bool emailVerified,
        DateTime utcNow)
    {
        User user = Create(email, displayName, utcNow);
        if (emailVerified)
        {
            user.EmailVerifiedAtUtc = utcNow;
        }

        user._externalLogins.Add(ExternalLogin.Create(user.Id, provider, subject, email.Value, utcNow));
        user.Raise(new UserRegisteredDomainEvent(user.Id, user.Email, user.DisplayName, provider));
        return user;
    }

    public bool IsLockedOut(DateTime utcNow) => LockoutEndsAtUtc > utcNow;

    public IReadOnlyList<string> GetMfaMethods()
    {
        var methods = new List<string>(3);
        if (IsTotpEnabled)
        {
            methods.Add(MfaMethods.Totp);
        }

        if (HasVerifiedPhone)
        {
            methods.Add(MfaMethods.Sms);
        }

        if (_recoveryCodes.Any(code => code.UsedAtUtc is null))
        {
            methods.Add(MfaMethods.RecoveryCode);
        }

        return methods;
    }

    public void VerifyEmail(DateTime utcNow)
    {
        if (IsEmailVerified)
        {
            return;
        }

        EmailVerifiedAtUtc = utcNow;
        Raise(new EmailVerifiedDomainEvent(Id, Email));
    }

    public void UpdateDisplayName(string displayName) => DisplayName = NormalizeDisplayName(displayName, Email);

    public void ChangeEmail(Email newEmail, DateTime utcNow)
    {
        string previous = Email;
        Email = newEmail.Value;
        EmailVerifiedAtUtc = utcNow;
        Raise(new EmailChangedDomainEvent(Id, previous, Email));
    }

    public void GrantPlatformAdmin()
    {
        if (IsPlatformAdmin)
        {
            return;
        }

        IsPlatformAdmin = true;
        Raise(new PlatformAdminGrantedDomainEvent(Id));
    }

    public void Delete() => Raise(new UserDeletedDomainEvent(Id, Email, DisplayName));

    // ---------------------------------------------------------------- sign-in

    public Result<SignInStep> SignInWithPassword(bool passwordMatches, DateTime utcNow)
    {
        if (IsLockedOut(utcNow))
        {
            return UserErrors.LockedOut;
        }

        if (!passwordMatches)
        {
            RegisterFailedAttempt("invalid_password", utcNow);
            return UserErrors.InvalidCredentials;
        }

        if (!IsEmailVerified)
        {
            return UserErrors.EmailNotVerified;
        }

        return HasMfa ? SignInStep.MfaRequired : SignInStep.Completed;
    }

    public Result<SignInStep> SignInWithExternalLogin(DateTime utcNow)
    {
        if (IsLockedOut(utcNow))
        {
            return UserErrors.LockedOut;
        }

        return HasMfa ? SignInStep.MfaRequired : SignInStep.Completed;
    }

    public Result VerifyTotpCode(bool isValid, long timeStep, DateTime utcNow)
    {
        if (IsLockedOut(utcNow))
        {
            return UserErrors.LockedOut;
        }

        if (!IsTotpEnabled)
        {
            return UserErrors.MfaMethodNotAvailable;
        }

        // A code can be used once: the matched time step must be newer than the last one accepted.
        if (!isValid || timeStep <= (TotpLastUsedTimeStep ?? long.MinValue))
        {
            RegisterFailedAttempt("invalid_totp_code", utcNow);
            return UserErrors.InvalidMfaCode;
        }

        TotpLastUsedTimeStep = timeStep;
        return Result.Success();
    }

    public Result RedeemRecoveryCode(string code, ISecretHasher hasher, DateTime utcNow)
    {
        if (IsLockedOut(utcNow))
        {
            return UserErrors.LockedOut;
        }

        string hash = RecoveryCode.Hash(code, hasher);
        RecoveryCode? match = _recoveryCodes.FirstOrDefault(candidate => candidate.UsedAtUtc is null && candidate.Matches(hash));
        if (match is null)
        {
            RegisterFailedAttempt("invalid_recovery_code", utcNow);
            return UserErrors.InvalidMfaCode;
        }

        match.MarkUsed(utcNow);
        return Result.Success();
    }

    public Result RegisterFailedSmsCode(DateTime utcNow)
    {
        RegisterFailedAttempt("invalid_sms_code", utcNow);
        return UserErrors.InvalidMfaCode;
    }

    public Result RecordPasskeySignIn(byte[] credentialId, long signCount, DateTime utcNow)
    {
        if (IsLockedOut(utcNow))
        {
            return UserErrors.LockedOut;
        }

        PasskeyCredential? passkey = FindPasskey(credentialId);
        if (passkey is null)
        {
            return UserErrors.InvalidCredentials;
        }

        passkey.RecordUse(signCount, utcNow);
        return Result.Success();
    }

    public void RegisterFailedPasskey(DateTime utcNow) => RegisterFailedAttempt("invalid_passkey", utcNow);

    /// <summary>Called once every factor has been verified: clears the failure counter.</summary>
    public void CompleteSignIn()
    {
        FailedSignInAttempts = 0;
        LockoutEndsAtUtc = null;
    }

    public void RegisterDevice(string deviceIdHash, string? ipAddress, string? userAgent, DateTime utcNow)
    {
        KnownDevice? device = _knownDevices.FirstOrDefault(known => known.DeviceIdHash == deviceIdHash);
        if (device is not null)
        {
            device.Touch(utcNow);
            return;
        }

        bool isFirstDevice = _knownDevices.Count == 0;
        if (_knownDevices.Count >= MaxKnownDevices)
        {
            _knownDevices.Remove(_knownDevices.MinBy(known => known.LastSeenAtUtc)!);
        }

        _knownDevices.Add(KnownDevice.Create(Id, deviceIdHash, utcNow));
        if (!isFirstDevice)
        {
            Raise(new NewDeviceSignedInDomainEvent(Id, ipAddress, userAgent));
        }
    }

    // ---------------------------------------------------------------- password

    public void ChangePassword(string passwordHash, Guid? currentSessionId, DateTime utcNow)
    {
        PasswordHash = passwordHash;
        Raise(new PasswordChangedDomainEvent(Id, currentSessionId, utcNow));
    }

    /// <summary>The reset link proves ownership of the mailbox, so it also verifies the email.</summary>
    public void ResetPassword(string passwordHash, DateTime utcNow)
    {
        PasswordHash = passwordHash;
        FailedSignInAttempts = 0;
        LockoutEndsAtUtc = null;
        VerifyEmail(utcNow);
        Raise(new PasswordChangedDomainEvent(Id, null, utcNow));
    }

    public void UpgradePasswordHash(string passwordHash) => PasswordHash = passwordHash;

    // ---------------------------------------------------------------- MFA

    public Result BeginTotpSetup(string protectedSecret)
    {
        if (IsTotpEnabled)
        {
            return UserErrors.TotpAlreadyEnabled;
        }

        PendingTotpSecret = protectedSecret;
        return Result.Success();
    }

    /// <returns>New recovery codes when this is the first second factor; otherwise an empty list.</returns>
    public Result<IReadOnlyList<string>> ConfirmTotpSetup(bool isValid, long timeStep, ISecretHasher hasher, DateTime utcNow)
    {
        if (IsTotpEnabled)
        {
            return UserErrors.TotpAlreadyEnabled;
        }

        if (PendingTotpSecret is null)
        {
            return UserErrors.TotpSetupNotStarted;
        }

        if (!isValid)
        {
            return UserErrors.InvalidMfaCode;
        }

        TotpSecret = PendingTotpSecret;
        PendingTotpSecret = null;
        TotpEnabledAtUtc = utcNow;
        TotpLastUsedTimeStep = timeStep;
        Raise(new MfaEnabledDomainEvent(Id, MfaMethods.Totp));

        return Result.Success(EnsureRecoveryCodes(hasher, utcNow));
    }

    public Result DisableTotp(bool mfaRequiredByOrganization)
    {
        if (!IsTotpEnabled)
        {
            return UserErrors.TotpNotEnabled;
        }

        if (mfaRequiredByOrganization && !HasVerifiedPhone)
        {
            return UserErrors.MfaRequiredByOrganization;
        }

        TotpSecret = null;
        TotpEnabledAtUtc = null;
        TotpLastUsedTimeStep = null;
        ClearRecoveryCodesWithoutMfa();
        Raise(new MfaDisabledDomainEvent(Id, MfaMethods.Totp));
        return Result.Success();
    }

    public Result<IReadOnlyList<string>> SetVerifiedPhoneNumber(PhoneNumber phoneNumber, ISecretHasher hasher, DateTime utcNow)
    {
        PhoneNumber = phoneNumber.Value;
        PhoneVerifiedAtUtc = utcNow;
        Raise(new MfaEnabledDomainEvent(Id, MfaMethods.Sms));
        return Result.Success(EnsureRecoveryCodes(hasher, utcNow));
    }

    public Result RemovePhoneNumber(bool mfaRequiredByOrganization)
    {
        if (!HasVerifiedPhone)
        {
            return UserErrors.PhoneNumberNotSet;
        }

        if (mfaRequiredByOrganization && !IsTotpEnabled)
        {
            return UserErrors.MfaRequiredByOrganization;
        }

        PhoneNumber = null;
        PhoneVerifiedAtUtc = null;
        ClearRecoveryCodesWithoutMfa();
        Raise(new MfaDisabledDomainEvent(Id, MfaMethods.Sms));
        return Result.Success();
    }

    public Result<IReadOnlyList<string>> RegenerateRecoveryCodes(ISecretHasher hasher, DateTime utcNow)
    {
        if (!HasMfa)
        {
            return UserErrors.MfaNotEnabled;
        }

        return Result.Success<IReadOnlyList<string>>(GenerateRecoveryCodes(hasher, utcNow));
    }

    // ---------------------------------------------------------------- passkeys

    public Result<PasskeyCredential> AddPasskey(
        byte[] credentialId,
        byte[] publicKey,
        long signCount,
        Guid aaguid,
        string name,
        DateTime utcNow)
    {
        if (_passkeys.Count >= MaxPasskeys)
        {
            return UserErrors.PasskeyLimitReached;
        }

        if (FindPasskey(credentialId) is not null)
        {
            return UserErrors.PasskeyAlreadyRegistered;
        }

        var passkey = PasskeyCredential.Create(Id, credentialId, publicKey, signCount, aaguid, name, utcNow);
        _passkeys.Add(passkey);
        Raise(new PasskeyAddedDomainEvent(Id, passkey.Id, passkey.Name));
        return passkey;
    }

    public Result RenamePasskey(Guid passkeyId, string name)
    {
        PasskeyCredential? passkey = _passkeys.FirstOrDefault(candidate => candidate.Id == passkeyId);
        if (passkey is null)
        {
            return UserErrors.PasskeyNotFound;
        }

        passkey.Rename(name);
        return Result.Success();
    }

    public Result RemovePasskey(Guid passkeyId)
    {
        PasskeyCredential? passkey = _passkeys.FirstOrDefault(candidate => candidate.Id == passkeyId);
        if (passkey is null)
        {
            return UserErrors.PasskeyNotFound;
        }

        if (AccessMethodCount <= 1)
        {
            return UserErrors.LastAccessMethod;
        }

        _passkeys.Remove(passkey);
        Raise(new PasskeyRemovedDomainEvent(Id, passkey.Id));
        return Result.Success();
    }

    // ---------------------------------------------------------------- external logins

    public Result LinkExternalLogin(string provider, string subject, string? email, DateTime utcNow)
    {
        if (_externalLogins.Any(login => login.Provider == provider))
        {
            return UserErrors.ExternalProviderAlreadyLinked;
        }

        _externalLogins.Add(ExternalLogin.Create(Id, provider, subject, email, utcNow));
        Raise(new ExternalLoginLinkedDomainEvent(Id, provider));
        return Result.Success();
    }

    public Result UnlinkExternalLogin(string provider)
    {
        ExternalLogin? login = _externalLogins.FirstOrDefault(candidate => candidate.Provider == provider);
        if (login is null)
        {
            return UserErrors.ExternalLoginNotFound;
        }

        if (AccessMethodCount <= 1)
        {
            return UserErrors.LastAccessMethod;
        }

        _externalLogins.Remove(login);
        Raise(new ExternalLoginUnlinkedDomainEvent(Id, provider));
        return Result.Success();
    }

    // ---------------------------------------------------------------- helpers

    private static User Create(Email email, string? displayName, DateTime utcNow) => new()
    {
        Id = Guid.CreateVersion7(),
        Email = email.Value,
        DisplayName = NormalizeDisplayName(displayName, email.Value),
        CreatedAtUtc = utcNow,
    };

    private static string NormalizeDisplayName(string? displayName, string email)
    {
        string name = displayName?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            name = email[..email.IndexOf('@', StringComparison.Ordinal)];
        }

        return name.Length <= MaxDisplayNameLength ? name : name[..MaxDisplayNameLength];
    }

    private PasskeyCredential? FindPasskey(byte[] credentialId) =>
        _passkeys.FirstOrDefault(candidate => candidate.CredentialId.AsSpan().SequenceEqual(credentialId));

    private void RegisterFailedAttempt(string reason, DateTime utcNow)
    {
        FailedSignInAttempts++;
        Raise(new SignInFailedDomainEvent(Id, reason));

        if (FailedSignInAttempts >= MaxFailedSignInAttempts)
        {
            FailedSignInAttempts = 0;
            LockoutEndsAtUtc = utcNow + LockoutDuration;
            Raise(new UserLockedOutDomainEvent(Id, LockoutEndsAtUtc.Value));
        }
    }

    private IReadOnlyList<string> EnsureRecoveryCodes(ISecretHasher hasher, DateTime utcNow) =>
        _recoveryCodes.Any(code => code.UsedAtUtc is null) ? [] : GenerateRecoveryCodes(hasher, utcNow);

    private List<string> GenerateRecoveryCodes(ISecretHasher hasher, DateTime utcNow)
    {
        _recoveryCodes.Clear();
        var codes = new List<string>(RecoveryCodeCount);
        for (int i = 0; i < RecoveryCodeCount; i++)
        {
            string code = RecoveryCode.Generate();
            codes.Add(code);
            _recoveryCodes.Add(RecoveryCode.Create(Id, code, hasher, utcNow));
        }

        return codes;
    }

    private void ClearRecoveryCodesWithoutMfa()
    {
        if (!HasMfa)
        {
            _recoveryCodes.Clear();
        }
    }
}

public enum SignInStep
{
    Completed,
    MfaRequired,
}

public static class MfaMethods
{
    public const string Totp = "totp";
    public const string Sms = "sms";
    public const string RecoveryCode = "recovery_code";
}
