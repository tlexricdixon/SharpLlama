using ChatService;
using ChatService.Plugins;
using Contracts;
using Entities;
using Infrastructure;
using LoggerService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options; // added for IOptions<T>

namespace ServiceExtentions;

public static class ServiceExtensions
{
    public static IServiceCollection ConfigureData(this IServiceCollection services, IConfiguration configuration)
    {
        var cs = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Connection string 'DefaultConnection' missing or empty.");
        services.AddDbContext<NorthwindStarterContext>(opt =>
        {
            opt.UseSqlServer(cs);
        });
        return services;
    }

    public static IServiceCollection ConfigureCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("CorsPolicy", builder =>
            {
                var allowed = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

                if (allowed.Length == 0)
                {
                    // Development fallback
                    builder.WithOrigins(
                            "https://localhost:5001",
                            "http://localhost:5000",
                            "https://localhost:7055",
                            "http://localhost:5106")
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
                else
                {
                    builder.WithOrigins(allowed)
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }

                builder.SetPreflightMaxAge(TimeSpan.FromHours(1));
            });
        });

        return services;
    }

    public static IServiceCollection ConfigureLoggerService(this IServiceCollection services)
    {
        services.AddSingleton<ILoggerManager, LoggerManager>();
        return services;
    }

    public static IServiceCollection ConfigureChatServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();

        services.AddSingleton<ILLamaWeightManager, LLamaWeightManager>();
        services.AddSingleton<IChatMetrics, ChatMetrics>();
        services.AddSingleton<IRagDiagnosticsCollector, RagDiagnosticsCollector>(); // added

        services.AddSingleton<IChatResponseCache>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerManager>();
            var capacity = int.TryParse(configuration["ChatService:ResponseCache:Capacity"], out var cap) ? cap : 500;
            var ttlMinutes = int.TryParse(configuration["ChatService:ResponseCache:DefaultTtlMinutes"], out var ttl) ? ttl : 30;
            return new LruChatResponseCache(capacity, logger, TimeSpan.FromMinutes(ttl));
        });

        services.AddSingleton<IChatServicePool, ChatServicePool>();

        // Ensure options are bound (in case not done elsewhere)
        services.Configure<ModelOptions>(configuration.GetSection("ModelOptions"));
        services.Configure<ChatServiceOptions>(configuration.GetSection("ChatService"));

        services.AddScoped<IStatefulChatService>(provider =>
        {
            var logger = provider.GetRequiredService<ILoggerManager>();
            var weightManager = provider.GetRequiredService<ILLamaWeightManager>();
            var cache = provider.GetRequiredService<IChatResponseCache>();
            var metrics = provider.GetRequiredService<IChatMetrics>();
            var modelOptions = provider.GetRequiredService<IOptions<ModelOptions>>();
            var chatOptions = provider.GetRequiredService<IOptions<ChatServiceOptions>>();

            return new StatefulChatService(modelOptions, chatOptions, logger, weightManager, cache, metrics);
        });

        services.AddScoped<IStatelessChatService>(provider =>
        {
            var logger = provider.GetRequiredService<ILoggerManager>();
            var weightManager = provider.GetRequiredService<ILLamaWeightManager>();
            var cache = provider.GetRequiredService<IChatResponseCache>();
            var metrics = provider.GetRequiredService<IChatMetrics>();
            return new StatelessChatService(configuration, logger, weightManager, cache, metrics);
        });

        return services;
    }

    public static IServiceCollection ConfigureChatServicesWithPlugins(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<ISemanticKernelPlugin, InputValidationPlugin>();
        services.AddTransient<ISemanticKernelPlugin, ContextEnhancementPlugin>();
        services.AddTransient<ISemanticKernelPlugin, ResponseFormattingPlugin>();

        services.AddSingleton<IStatelessChatService>(provider =>
        {
            var logger = provider.GetRequiredService<ILoggerManager>();
            var weightManager = provider.GetRequiredService<ILLamaWeightManager>();
            var cache = provider.GetRequiredService<IChatResponseCache>();
            var metrics = provider.GetRequiredService<IChatMetrics>();
            var plugins = provider.GetServices<ISemanticKernelPlugin>();
            return new EnhancedStatelessChatService(configuration, logger, weightManager, cache, metrics, plugins);
        });

        return services;
    }

    public static IServiceCollection ConfigureIngestion(this IServiceCollection services)
    {
        services.AddScoped<EmployeeRagIngestionService>();
        return services;
    }

    public static IServiceCollection ConfigureModelOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Infrastructure.ModelOptions>(configuration.GetSection("ModelOptions"));
        return services;
    }
}
