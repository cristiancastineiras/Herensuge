using FluentValidation;

namespace Herensuge.Modules.Identity.Application.Services;

public static class PasswordRules
{
    public const int MinimumLength = 12;

    // Bounded so a huge payload cannot make Argon2 burn CPU and memory.
    public const int MaximumLength = 128;

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(MinimumLength)
            .MaximumLength(MaximumLength);
}
