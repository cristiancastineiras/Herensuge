using FluentValidation;

namespace Herensuge.Modules.Audit.Application.Entries.GetAuditEntries;

internal sealed class GetAuditEntriesQueryValidator : AbstractValidator<GetAuditEntriesQuery>
{
    public GetAuditEntriesQueryValidator()
    {
        RuleFor(query => query.PageSize).InclusiveBetween(1, 200);
        RuleFor(query => query.Action).MaximumLength(100);
    }
}
