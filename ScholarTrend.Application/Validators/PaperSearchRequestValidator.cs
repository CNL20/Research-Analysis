using FluentValidation;
using ScholarTrend.Application.DTOs.Papers;

namespace ScholarTrend.Application.Validators;

public class PaperSearchRequestValidator : AbstractValidator<PaperSearchRequest>
{
    private static readonly string[] ValidSearchTypes = ["keyword", "author", "journal", "all"];

    public PaperSearchRequestValidator()
    {
        RuleFor(x => x.SearchType)
            .Must(type => ValidSearchTypes.Contains(type.ToLowerInvariant()))
            .WithMessage($"SearchType must be one of: {string.Join(", ", ValidSearchTypes)}.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("PageSize must be between 1 and 50.");

        RuleFor(x => x.YearTo)
            .GreaterThanOrEqualTo(x => x.YearFrom!.Value)
            .When(x => x.YearFrom.HasValue && x.YearTo.HasValue)
            .WithMessage("YearTo must be greater than or equal to YearFrom.");
    }
}
