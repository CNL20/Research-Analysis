using FluentValidation;
using ScholarTrend.Application.DTOs.Auth;

namespace ScholarTrend.Application.Validators;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(100).WithMessage("Full name must not exceed 100 characters.");

        RuleFor(x => x.Institution)
            .MaximumLength(200).WithMessage("Institution must not exceed 200 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Institution));

        RuleFor(x => x.ResearchField)
            .MaximumLength(200).WithMessage("Research field must not exceed 200 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.ResearchField));
    }
}
