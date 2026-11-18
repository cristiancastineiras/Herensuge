namespace Herensuge.Access.AspNetCore;

/// <summary>
/// Sends requests addressed to Herensuge's public URL to its internal address instead, telling Herensuge (through
/// X-Forwarded-*) which public URL was meant: issuer, JWKS and the decision point answer as if called from outside.
/// Same rule as Herensuge.Bff's handler.
/// </summary>
internal sealed class BackchannelRewriteHandler(Uri publicAuthority, Uri internalAddress) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is { } uri
            && string.Equals(uri.Scheme, publicAuthority.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Authority, publicAuthority.Authority, StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", publicAuthority.Scheme);
            request.Headers.TryAddWithoutValidation("X-Forwarded-Host", publicAuthority.Authority);
            request.RequestUri = new UriBuilder(uri)
            {
                Scheme = internalAddress.Scheme,
                Host = internalAddress.Host,
                Port = internalAddress.Port,
            }.Uri;
        }

        return base.SendAsync(request, cancellationToken);
    }
}
