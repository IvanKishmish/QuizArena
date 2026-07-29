using FluentValidation.TestHelper;
using QuizArena.Application.Features.Auth.Login;
using QuizArena.Application.Features.Auth.RefreshToken;
using QuizArena.Application.Features.Auth.Register;

namespace QuizArena.Application.UnitTests.Features.Auth.Validators;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_WithInvalidEmail_HasValidationErrorForEmail(string email)
    {
        var command = new LoginCommand(email, "password123");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_WithEmptyPassword_HasValidationErrorForPassword()
    {
        var command = new LoginCommand("ivan@test.com", "");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_WithValidCommand_HasNoValidationErrors()
    {
        var command = new LoginCommand("ivan@test.com", "password123");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("Ivan")] // 4 characters — one short of the 5-character minimum
    public void Validate_WithInvalidNickName_HasValidationErrorForNickName(string nickName)
    {
        var command = new RegisterCommand(nickName, "ivan@test.com", "Password123");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.NickName);
    }

    [Fact]
    public void Validate_WithFiveCharacterNickName_HasNoValidationErrorForNickName()
    {
        // Arrange: boundary case — exactly at the minimum length
        var command = new RegisterCommand("Ivan9", "ivan@test.com", "Password123");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.NickName);
    }

    [Fact]
    public void Validate_WithInvalidEmail_HasValidationErrorForEmail()
    {
        var command = new RegisterCommand("Ivan99", "not-an-email", "Password123");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")] // 5 characters — below the 8-character minimum
    public void Validate_WithInvalidPassword_HasValidationErrorForPassword(string password)
    {
        var command = new RegisterCommand("Ivan99", "ivan@test.com", password);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_WithValidCommand_HasNoValidationErrors()
    {
        var command = new RegisterCommand("Ivan99", "ivan@test.com", "Password123");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class RefreshTokenCommandValidatorTests
{
    private readonly RefreshTokenCommandValidator _validator = new();

    [Fact]
    public void Validate_WithEmptyRefreshToken_HasValidationErrorForRefreshToken()
    {
        var command = new RefreshTokenCommand("");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }

    [Fact]
    public void Validate_WithNonEmptyRefreshToken_HasNoValidationErrors()
    {
        var command = new RefreshTokenCommand("some-token-value");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
