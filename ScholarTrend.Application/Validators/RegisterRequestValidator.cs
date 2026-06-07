using FluentValidation;
using ScholarTrend.Application.DTOs.Auth;

namespace ScholarTrend.Application.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(100).WithMessage("Full name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email format is invalid.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Confirm password is required.")
            .Equal(x => x.Password).WithMessage("Password and Confirm Password do not match.");

        RuleFor(x => x.Institution)
            .MaximumLength(200).WithMessage("Institution must not exceed 200 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Institution));

        RuleFor(x => x.ResearchField)
            .MaximumLength(200).WithMessage("Research field must not exceed 200 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.ResearchField));
    }
}
