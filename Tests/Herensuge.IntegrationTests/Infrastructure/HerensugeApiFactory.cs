using System.Collections.Concurrent;
using Herensuge.Modules.Notifications.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Herensuge.IntegrationTests.Infrastructure;

/// <summary>Herensuge.Api against real PostgreSQL and Valkey containers (needs Docker).</summary>
public sealed class HerensugeApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string BaseAddress = "https://localhost";
    public const string ClientId = "herensuge-portal";
    public const string ClientSecret = "integration-tests-secret";
    public const string RedirectUri = "https://portal.test/bff/callback";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RedisContainer _valkey = new RedisBuilder("valkey/valkey:8-alpine").Build();
    private readonly string _keysDirectory = Path.Combine(Path.GetTempPath(), "herensuge-tests", Guid.NewGuid().ToString("N"));

    public CapturingSenders Messages { get; } = new();

    public async ValueTask InitializeAsync() => await Task.WhenAll(_postgres.StartAsync(), _valkey.StartAsync());

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _valkey.DisposeAsync();
    }

    public HttpClient CreateBrowser()
    {
        HttpClient client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(BaseAddress),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        client.DefaultRequestHeaders.Add("X-CSRF", "1");
        return client;
    }

    public HttpClient CreateBackchannel() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri(BaseAddress),
        AllowAutoRedirect = false,
        HandleCookies = false,
    });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Valkey", _valkey.GetConnectionString());
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Oidc:Issuer", BaseAddress + "/");
        builder.UseSetting("Oidc:Keys:Directory", _keysDirectory);
        builder.UseSetting("Oidc:PortalClient:ClientSecret", ClientSecret);
        builder.UseSetting("Oidc:PortalClient:RedirectUris:0", RedirectUri);
        builder.UseSetting("Messaging:PollingInterval", "00:00:00.200");
        builder.UseSetting("RateLimiting:StrictPermitLimit", "10000");
        builder.UseSetting("RateLimiting:PermitLimit", "10000");
        builder.UseSetting("Identity:Argon2:MemorySizeKiB", "8192");
        builder.UseSetting("Identity:Argon2:Iterations", "1");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.RemoveAll<ISmsSender>();
            services.AddSingleton<IEmailSender>(Messages);
            services.AddSingleton<ISmsSender>(Messages);
        });
    }
}

/// <summary>Replaces the email and SMS providers: the tests read the messages the users would receive.</summary>
public sealed class CapturingSenders : IEmailSender, ISmsSender
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private readonly ConcurrentQueue<EmailMessage> _emails = new();
    private readonly ConcurrentQueue<SmsMessage> _sms = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _emails.Enqueue(message);
        return Task.CompletedTask;
    }

    public Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        _sms.Enqueue(message);
        return Task.CompletedTask;
    }

    public async Task<EmailMessage> WaitForEmailAsync(string to, string subject)
    {
        DateTime deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            EmailMessage? message = _emails.LastOrDefault(email => email.To == to && email.Subject == subject);
            if (message is not null)
            {
                return message;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"No email '{subject}' arrived for {to}.");
    }
}
