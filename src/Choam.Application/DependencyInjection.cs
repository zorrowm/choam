using Choam.Application.Interfaces;
using Choam.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Choam.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IChatService, ChatService>();

        return services;
    }
}
