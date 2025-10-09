using FluentValidation;

namespace SharpLlama.Entities.Validation;

public sealed class HistoryInputValidator : AbstractValidator<HistoryInput>
{
    private static readonly string[] AllowedRoles = ["system","user","assistant"];

    public HistoryInputValidator()
    {
        RuleFor(x => x.Messages)
            .NotNull().WithMessage("Messages collection required.")
            .NotEmpty().WithMessage("At least one message required.")
            .Must(m => m.Count <= ChatValidationConstants.MaxMessages)
            .WithMessage($"Too many messages (> {ChatValidationConstants.MaxMessages}).");

        RuleForEach(x => x.Messages).ChildRules(msg =>
        {
            msg.RuleFor(m => m.Role)
                .NotEmpty().WithMessage("Role required.")
                .Must(r => AllowedRoles.Contains(r.Trim().ToLowerInvariant()))
                .WithMessage("Invalid role.")
                .MaximumLength(32);

            msg.RuleFor(m => m.Content)
                .NotEmpty().WithMessage("Content required.")
                .MaximumLength(ChatValidationConstants.MaxMessageChars);
        });
    }
}
