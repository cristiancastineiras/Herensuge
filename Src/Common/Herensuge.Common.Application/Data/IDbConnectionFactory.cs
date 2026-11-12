using System.Data.Common;

namespace Herensuge.Common.Application.Data;

/// <summary>Read side of CQRS: query handlers open a connection and use Dapper against the module schema.</summary>
public interface IDbConnectionFactory
{
    ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
