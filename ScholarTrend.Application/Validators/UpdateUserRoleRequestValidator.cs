using FluentValidation;
using ScholarTrend.Application.DTOs.Auth;
using ScholarTrend.Domain.Constants;

namespace ScholarTrend.Application.Validators;

public class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
{
    public UpdateUserRoleRequestValidator()
    {
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(role => RoleConstants.All.Contains(role))
            .WithMessage($"Role must be one of: {string.Join(", ", RoleConstants.All)}.");
    }
}
