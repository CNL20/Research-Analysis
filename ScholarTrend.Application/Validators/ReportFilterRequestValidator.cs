using FluentValidation;
using ScholarTrend.Application.DTOs.Reports;

namespace ScholarTrend.Application.Validators;

public class ReportFilterRequestValidator : AbstractValidator<ReportFilterRequest>
{
    private static readonly string[] AllowedGroupBy = ["year", "keyword", "topic"];

    public ReportFilterRequestValidator()
    {
        RuleFor(x => x.GroupBy)
            .Must(g => AllowedGroupBy.Contains(g.ToLowerInvariant()))
            .WithMessage("GroupBy must be year, keyword, or topic.");

        RuleFor(x => x.YearTo)
            .GreaterThanOrEqualTo(x => x.YearFrom!.Value)
            .When(x => x.YearFrom.HasValue && x.YearTo.HasValue)
            .WithMessage("YearTo must be greater than or equal to YearFrom.");
    }
}
