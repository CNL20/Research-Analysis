using FluentValidation;
using ScholarTrend.Application.DTOs.Trends;

namespace ScholarTrend.Application.Validators;

public class TrendFilterRequestValidator : AbstractValidator<TrendFilterRequest>
{
    public TrendFilterRequestValidator()
    {
        RuleFor(x => x.Top)
            .InclusiveBetween(1, 50)
            .When(x => x.Top > 0)
            .WithMessage("Top must be between 1 and 50.");

        RuleFor(x => x.YearTo)
            .GreaterThanOrEqualTo(x => x.YearFrom!.Value)
            .When(x => x.YearFrom.HasValue && x.YearTo.HasValue)
            .WithMessage("YearTo must be greater than or equal to YearFrom.");
    }
}
