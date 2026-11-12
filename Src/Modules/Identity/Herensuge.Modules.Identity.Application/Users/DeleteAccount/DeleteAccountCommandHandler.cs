using Herensuge.Modules.Tenancy.Contracts;

namespace Herensuge.Modules.Identity.Application.Users.DeleteAccount;

/// <summary>
/// Hard delete (right to erasure). Sessions, tokens and credentials go with the row; the other
/// modules react to UserDeleted (memberships, OIDC authorizations).
/// </summary>
internal sealed class DeleteAccountCommandHandler(
    IUserRepository users,
    ITenancyModuleApi tenancy,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteAccountCommand>
{
    public async Task<Result> Handle(DeleteAccountCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (await tenancy.IsLastOwnerOfAnyTenantAsync(user.Id, cancellationToken))
        {
            return UserErrors.LastOwnerOfOrganization;
        }

        user.Delete();
        users.Remove(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
