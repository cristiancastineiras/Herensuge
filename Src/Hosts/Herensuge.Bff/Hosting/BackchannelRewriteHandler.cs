namespace Herensuge.Bff.Hosting;

/// <summary>
/// Sends back-channel requests addressed to the public IdP URL to its internal address instead,
/// telling the IdP (through X-Forwarded-*) which public URL was meant. Issuer and redirects stay public.
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
