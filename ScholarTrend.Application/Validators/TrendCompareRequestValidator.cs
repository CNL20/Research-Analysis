using FluentValidation;
using ScholarTrend.Application.DTOs.Trends;

namespace ScholarTrend.Application.Validators;

public class TrendCompareRequestValidator : AbstractValidator<TrendCompareRequest>
{
    private static readonly string[] ValidTypes = ["keyword", "topic", "journal"];

    public TrendCompareRequestValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Type is required.")
            .Must(type => ValidTypes.Contains(type.ToLowerInvariant()))
            .WithMessage($"Type must be one of: {string.Join(", ", ValidTypes)}.");

        RuleFor(x => x.Ids)
            .NotEmpty().WithMessage("At least one ID is required.")
            .Must(ids => ids.Count is >= 2 and <= 3)
            .WithMessage("Compare requires 2 to 3 IDs.");
    }
}
