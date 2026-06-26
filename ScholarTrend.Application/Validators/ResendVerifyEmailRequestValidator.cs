using FluentValidation;
using ScholarTrend.Application.DTOs.Auth;

namespace ScholarTrend.Application.Validators;

public class ResendVerifyEmailRequestValidator : AbstractValidator<ResendVerifyEmailRequest>
{
    public ResendVerifyEmailRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email format is invalid.");
    }
}
