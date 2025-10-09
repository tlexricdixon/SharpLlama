using Entities;
using FluentValidation;
using Microsoft.Extensions.Options;
using Infrastructure;

namespace Entities.Validation;

public sealed class ChatRequestValidator : AbstractValidator<ChatRequest>
{
    public ChatRequestValidator(IOptions<RequestLimitsOptions> limitsOptions)
    {
        var limits = limitsOptions.Value;

        RuleFor(r => r.Messages)
            .NotNull().WithMessage("Messages collection is required.")
            .Must(m => m.Count > 0).WithMessage("At least one message is required.")
            .Must(m => m.Count <= limits.MaxMessages)
            .WithMessage($"Messages collection exceeds maximum of {limits.MaxMessages}.");

        RuleForEach(r => r.Messages)
            .ChildRules(msg =>
            {
                msg.RuleFor(m => m.Content)
                   .NotEmpty().WithMessage("Message content required.")
                   .MaximumLength(limits.MaxMessageChars)
                   .WithMessage($"Message exceeds maximum length of {limits.MaxMessageChars} characters.");
                msg.RuleFor(m => m.Author)
                   .NotEmpty().WithMessage("Author is required.");
            });
    }
}