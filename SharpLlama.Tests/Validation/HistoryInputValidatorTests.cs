using FluentAssertions;
using SharpLlama.Entities;
using SharpLlama.Entities.Validation;
using Xunit;

namespace SharpLlama.Tests.Validation;

public class HistoryInputValidatorTests
{
    private readonly HistoryInputValidator _validator = new();

    [Fact]
    public void Valid_History_Passes()
    {
        var input = new HistoryInput
        {
            Messages =
            [
                new() { Role = "user", Content = "Hi" },
                new() { Role = "assistant", Content = "Hello" }
            ]
        };
        var result = _validator.Validate(input);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_Messages_Fails()
    {
        var input = new HistoryInput();
        var result = _validator.Validate(input);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_Role_Fails()
    {
        var input = new HistoryInput
        {
            Messages = [new() { Role = "bad", Content = "Hi" }]
        };
        var result = _validator.Validate(input);
        result.IsValid.Should().BeFalse();
    }
}
