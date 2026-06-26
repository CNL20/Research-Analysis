using FluentValidation;
using ScholarTrend.Application.DTOs.Auth;

namespace ScholarTrend.Application.Validators;

public class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email format is invalid.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token is required.");
    }
}
