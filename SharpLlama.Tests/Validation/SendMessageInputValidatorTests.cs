using FluentAssertions;
using SharpLlama.Entities;
using SharpLlama.Entities.Validation;
using Xunit;

namespace SharpLlama.Tests.Validation;

public class SendMessageInputValidatorTests
{
    private readonly SendMessageInputValidator _validator = new();

    [Fact]
    public void Valid_Text_Passes()
    {
        var input = new SendMessageInput { Text = new string('a', 10) };
        var result = _validator.Validate(input);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_Text_Fails()
    {
        var input = new SendMessageInput { Text = string.Empty };
        var result = _validator.Validate(input);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Too_Long_Text_Fails()
    {
        var input = new SendMessageInput { Text = new string('a', ChatValidationConstants.MaxMessageChars + 1) };
        var result = _validator.Validate(input);
        result.IsValid.Should().BeFalse();
    }
}
