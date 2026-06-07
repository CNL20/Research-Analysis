using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Common;

namespace ScholarTrend.API.Filters;

public static class ApiBehaviorExtensions
{
    public static IServiceCollection AddApiValidationResponse(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .SelectMany(entry => entry.Value!.Errors.Select(error => error.ErrorMessage))
                    .ToList();

                var response = ApiResponse<object>.FailResponse("Validation failed.", errors);
                return new BadRequestObjectResult(response);
            };
        });

        return services;
    }
}
