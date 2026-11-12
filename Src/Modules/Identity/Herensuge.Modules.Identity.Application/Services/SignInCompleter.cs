using Herensuge.Common.Application.Context;
using Herensuge.Modules.Identity.Domain.Sessions;
using Herensuge.Modules.Identity.Domain.Users;

namespace Herensuge.Modules.Identity.Application.Services;

/// <summary>Last step of every successful sign-in: resets the failure counter, records the device and opens the SSO session.</summary>
public sealed class SignInCompleter(
    IUserSessionRepository sessions,
    IRequestContext requestContext,
    ISecretHasher hasher,
    TimeProvider timeProvider)
{
    public UserSession Complete(User user, IEnumerable<string> authenticationMethods)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        user.CompleteSignIn();

        string? deviceIdHash = string.IsNullOrEmpty(requestContext.DeviceId)
            ? null
            : hasher.Hash("identity.device_id", requestContext.DeviceId);

        if (deviceIdHash is not null)
        {
            user.RegisterDevice(deviceIdHash, requestContext.IpAddress, requestContext.UserAgent, now);
        }

        UserSession session = UserSession.Start(
            user.Id,
            authenticationMethods,
            requestContext.IpAddress,
            requestContext.UserAgent,
            deviceIdHash,
            now);

        sessions.Add(session);
        return session;
    }
}
