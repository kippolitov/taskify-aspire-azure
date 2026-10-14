using Microsoft.Extensions.DependencyInjection;

namespace Taskify.Shared;

public static class ValidationRegistration
{
    /// <summary>
    /// The validation source generator only covers types in the assembly that calls
    /// <c>AddValidation</c>. This call makes it cover the request records in this assembly.
    /// </summary>
    public static IServiceCollection AddTaskifyContractValidation(
        this IServiceCollection services
    ) => services.AddValidation();
}
