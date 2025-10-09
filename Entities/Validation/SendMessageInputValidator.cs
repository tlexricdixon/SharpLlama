using FluentValidation;

namespace Entities.Validation;

public sealed class SendMessageInputValidator : AbstractValidator<SendMessageInput>
{
    public SendMessageInputValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Text is required.")
            .MaximumLength(ChatValidationConstants.MaxMessageChars);
    }
}
