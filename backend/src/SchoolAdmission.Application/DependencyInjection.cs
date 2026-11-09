using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Application.Classes.CreateClass;
using SchoolAdmission.Application.Common.Behaviors;

namespace SchoolAdmission.Application;

public static class DependencyInjection
{
    /// <summary>MediatR handlers, pipeline behaviors (logging outermost, then validation) and validators.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CreateClassCommand>();
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssemblyContaining<CreateClassValidator>(includeInternalTypes: true);

        return services;
    }
}
