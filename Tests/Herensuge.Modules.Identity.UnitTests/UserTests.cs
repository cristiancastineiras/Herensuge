using Herensuge.Common.Domain;
using Herensuge.Modules.Identity.Domain.Users;
using Herensuge.Modules.Identity.Domain.Users.Events;

namespace Herensuge.Modules.Identity.UnitTests;

public sealed class UserTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Register_raises_user_registered()
    {
        User user = NewUser();

        UserRegisteredDomainEvent registered = Assert.Single(user.GetDomainEvents().OfType<UserRegisteredDomainEvent>());
        Assert.Equal(user.Id, registered.UserId);
        Assert.Equal("ana@example.com", user.Email);
        Assert.False(user.IsEmailVerified);
    }

    [Fact]
    public void Password_sign_in_requires_a_verified_email()
    {
        User user = NewUser();

        Result<SignInStep> result = user.SignInWithPassword(passwordMatches: true, Now);

        Assert.Equal(UserErrors.EmailNotVerified, result.Error);
    }

    [Fact]
    public void Five_failed_passwords_lock_the_account_for_fifteen_minutes()
    {
        User user = VerifiedUser();

        for (int attempt = 0; attempt < User.MaxFailedSignInAttempts; attempt++)
        {
            Assert.Equal(UserErrors.InvalidCredentials, user.SignInWithPassword(passwordMatches: false, Now).Error);
        }

        Assert.True(user.IsLockedOut(Now));
        Assert.Equal(UserErrors.LockedOut, user.SignInWithPassword(passwordMatches: true, Now).Error);
        Assert.Single(user.GetDomainEvents().OfType<UserLockedOutDomainEvent>());

        Assert.False(user.IsLockedOut(Now + User.LockoutDuration));
        Assert.Equal(SignInStep.Completed, user.SignInWithPassword(passwordMatches: true, Now + User.LockoutDuration).Value);
    }

    [Fact]
    public void Password_sign_in_asks_for_mfa_when_a_second_factor_exists()
    {
        User user = VerifiedUser();
        user.SetVerifiedPhoneNumber(PhoneNumber.Create("+34600000000").Value, Now);

        Assert.Equal(SignInStep.MfaRequired, user.SignInWithPassword(passwordMatches: true, Now).Value);
    }

    [Fact]
    public void Totp_codes_cannot_be_replayed()
    {
        User user = UserWithTotp(timeStep: 100);

        Assert.Equal(UserErrors.InvalidMfaCode, user.VerifyTotpCode(isValid: true, timeStep: 100, Now).Error);
        Assert.True(user.VerifyTotpCode(isValid: true, timeStep: 101, Now).IsSuccess);
        Assert.Equal(UserErrors.InvalidMfaCode, user.VerifyTotpCode(isValid: true, timeStep: 101, Now).Error);
    }

    [Fact]
    public void Enabling_the_first_second_factor_returns_ten_recovery_codes_usable_once()
    {
        User user = VerifiedUser();
        user.BeginTotpSetup("protected-secret");

        Result<IReadOnlyList<string>> codes = user.ConfirmTotpSetup(isValid: true, timeStep: 1, Now);

        Assert.Equal(User.RecoveryCodeCount, codes.Value.Count);
        Assert.True(user.RedeemRecoveryCode(codes.Value[0].ToUpperInvariant(), Now).IsSuccess);
        Assert.Equal(UserErrors.InvalidMfaCode, user.RedeemRecoveryCode(codes.Value[0], Now).Error);
    }

    [Fact]
    public void The_last_way_of_signing_in_cannot_be_removed()
    {
        var email = Email.Create("bob@example.com").Value;
        User user = User.RegisterWithExternalLogin(email, "Bob", "google", "sub-1", emailVerified: true, Now);

        Assert.Equal(UserErrors.LastAccessMethod, user.UnlinkExternalLogin("google").Error);

        user.AddPasskey([1, 2, 3], [4, 5, 6], 0, Guid.Empty, "Laptop", Now);
        Assert.True(user.UnlinkExternalLogin("google").IsSuccess);
        Assert.Equal(UserErrors.LastAccessMethod, user.RemovePasskey(user.Passkeys.Single().Id).Error);
    }

    [Fact]
    public void Mfa_cannot_be_disabled_while_an_organization_requires_it()
    {
        User user = UserWithTotp(timeStep: 1);

        Assert.Equal(UserErrors.MfaRequiredByOrganization, user.DisableTotp(mfaRequiredByOrganization: true).Error);
        Assert.True(user.DisableTotp(mfaRequiredByOrganization: false).IsSuccess);
        Assert.Empty(user.RecoveryCodes);
    }

    [Fact]
    public void A_new_device_is_reported_except_for_the_first_one()
    {
        User user = VerifiedUser();
        user.ClearDomainEvents();

        user.RegisterDevice("device-a", "10.0.0.1", "Firefox", Now);
        user.RegisterDevice("device-a", "10.0.0.1", "Firefox", Now);
        Assert.Empty(user.GetDomainEvents().OfType<NewDeviceSignedInDomainEvent>());

        user.RegisterDevice("device-b", "10.0.0.2", "Chrome", Now);
        Assert.Single(user.GetDomainEvents().OfType<NewDeviceSignedInDomainEvent>());
    }

    [Theory]
    [InlineData("  Ana@Example.COM ", "ana@example.com")]
    [InlineData("first.last+tag@sub.example.org", "first.last+tag@sub.example.org")]
    public void Email_is_normalized(string raw, string expected) => Assert.Equal(expected, Email.Create(raw).Value.Value);

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("ana@localhost")]
    [InlineData("Ana <ana@example.com>")]
    public void Invalid_emails_are_rejected(string raw) => Assert.Equal(UserErrors.InvalidEmail, Email.Create(raw).Error);

    [Theory]
    [InlineData("+34 600 000 000", "+34600000000")]
    [InlineData("+1 (415) 555-0100", "+14155550100")]
    public void Phone_numbers_use_e164(string raw, string expected) => Assert.Equal(expected, PhoneNumber.Create(raw).Value.Value);

    [Theory]
    [InlineData("600000000")]
    [InlineData("+0600000000")]
    [InlineData("+34abc")]
    public void Invalid_phone_numbers_are_rejected(string raw) => Assert.Equal(UserErrors.InvalidPhoneNumber, PhoneNumber.Create(raw).Error);

    private static User NewUser() => User.Register(Email.Create("ana@example.com").Value, "Ana", "hash", Now);

    private static User VerifiedUser()
    {
        User user = NewUser();
        user.VerifyEmail(Now);
        return user;
    }

    private static User UserWithTotp(long timeStep)
    {
        User user = VerifiedUser();
        user.BeginTotpSetup("protected-secret");
        user.ConfirmTotpSetup(isValid: true, timeStep, Now);
        return user;
    }
}
