using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Http;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Timeout;
using SharpLlama.Entities.Validation;
using SharpLlama.Infrastructure;
using SharpLlama.Middleware;
using SharpLlama.Security;
using SharpLlama.ServiceExtentions;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ---- Request size limits (Kestrel) ----
builder.WebHost.ConfigureKestrel(o =>
{
    // Default 256 KB if not configured
    var maxBytes = builder.Configuration.GetValue<long?>("RequestLimits:MaxRequestBodyBytes") ?? 256 * 1024;
    o.Limits.MaxRequestBodySize = maxBytes;
});

// Data & MVC
builder.Services.ConfigureData(builder.Configuration);
builder.Services.AddControllers();

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<SendMessageInputValidator>();
builder.Services.AddFluentValidationAutoValidation()
                .AddFluentValidationClientsideAdapters();

builder.Services.AddSingleton<ProblemDetailsFactory, CustomProblemDetailsFactory>();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateValidationProblemDetails(context.HttpContext, context.ModelState);
        return new ObjectResult(problem) { StatusCode = problem.Status };
    };
});

// Limits + API key
builder.Services.Configure<RequestLimitsOptions>(builder.Configuration.GetSection("RequestLimits"));
builder.Services.AddSingleton<IApiKeyValidator, ApiKeyValidator>();

// Authentication
builder.Services
    .AddAuthentication(o =>
    {
        o.DefaultAuthenticateScheme = "ApiKey";
        o.DefaultChallengeScheme = "ApiKey";
    })
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", options =>
    {
        options.HeaderName = "X-API-Key";
    });

builder.Services.AddAuthorization();

// Rate Limiter (unchanged)
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var services = httpContext.RequestServices;
        var validator = services.GetRequiredService<IApiKeyValidator>();

        if (httpContext.Request.Headers.TryGetValue("X-API-Key", out var apiKeyRaw))
        {
            var apiKey = apiKeyRaw.ToString().Trim();
            if (validator.TryValidate(apiKey, out var identity))
            {
                return RateLimitPartition.GetTokenBucketLimiter($"api:{identity.UserName}", _ => new TokenBucketRateLimiterOptions
                {
                    AutoReplenishment = true,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    TokensPerPeriod = 50,
                    TokenLimit = 100,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
            }
        }

        if (httpContext.User?.Identity?.IsAuthenticated == true &&
            !string.IsNullOrWhiteSpace(httpContext.User.Identity!.Name))
        {
            return RateLimitPartition.GetTokenBucketLimiter($"user:{httpContext.User.Identity.Name}", _ => new TokenBucketRateLimiterOptions
            {
                AutoReplenishment = true,
                ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                TokensPerPeriod = 40,
                TokenLimit = 80,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
        }

        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetTokenBucketLimiter($"ip:{ip}", _ => new TokenBucketRateLimiterOptions
        {
            AutoReplenishment = true,
            ReplenishmentPeriod = TimeSpan.FromSeconds(10),
            TokensPerPeriod = 25,
            TokenLimit = 50,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers["Retry-After"] = "10";
        if (!context.HttpContext.Response.HasStarted)
        {
            var pdFactory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
            var pd = pdFactory.CreateProblemDetails(context.HttpContext,
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "RateLimitExceeded",
                detail: "Rate limit exceeded for this credential/identity.");
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsJsonAsync(pd, ct);
        }
    };
});

// OpenTelemetry
var serviceName = "SharpLlama";
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName: serviceName, serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString());

builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m
        .SetResourceBuilder(resourceBuilder)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddMeter("SharpLlama.Chat")
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri(builder.Configuration["OTLP:MetricsEndpoint"] ?? "http://localhost:4318");
            o.Protocol = OtlpExportProtocol.HttpProtobuf;
        })
        .AddPrometheusExporter()
        .AddConsoleExporter())
    .WithTracing(t => t
        .SetResourceBuilder(resourceBuilder)
        .AddSource("SharpLlama.Chat")
        .AddAspNetCoreInstrumentation(o => o.RecordException = true)
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri(builder.Configuration["OTLP:TracesEndpoint"] ?? "http://localhost:4317");
            o.Protocol = OtlpExportProtocol.Grpc;
        })
        .AddConsoleExporter());

// Resilient HttpClient (default)
builder.Services.AddHttpClient("default")
    .AddHttpMessageHandler(() =>
        new PolicyHttpMessageHandler(
            Policy.WrapAsync(
                Policy<HttpResponseMessage>
                    .Handle<HttpRequestException>()
                    .OrResult(r => (int)r.StatusCode >= 500)
                    .WaitAndRetryAsync(
                        retryCount: 3,
                        sleepDurationProvider: attempt =>
                            TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1))),
                Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(5)),
                Policy<HttpResponseMessage>
                    .Handle<TimeoutRejectedException>()
                    .Or<HttpRequestException>()
                    .OrResult(r => (int)r.StatusCode >= 500)
                    .CircuitBreakerAsync(
                        handledEventsAllowedBeforeBreaking: 10,
                        durationOfBreak: TimeSpan.FromSeconds(20),
                        onBreak: (_, _) => { },
                        onReset: () => { }),
                Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(15))
            )
        )
    );

// Named HttpClient for plugins
builder.Services.AddHttpClient("Plugins")
    .AddHttpMessageHandler(() =>
    {
        return new PolicyHttpMessageHandler(
            Policy.WrapAsync(
                Policy<HttpResponseMessage>
                    .Handle<HttpRequestException>()
                    .OrResult(r => (int)r.StatusCode >= 500)
                    .WaitAndRetryAsync(
                        retryCount: 4,
                        sleepDurationProvider: attempt =>
                            TimeSpan.FromMilliseconds(150 * Math.Pow(2, attempt - 1))),
                Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(4)),
                Policy<HttpResponseMessage>
                    .Handle<TimeoutRejectedException>()
                    .Or<HttpRequestException>()
                    .OrResult(r => (int)r.StatusCode >= 500)
                    .CircuitBreakerAsync(
                        handledEventsAllowedBeforeBreaking: 12,
                        durationOfBreak: TimeSpan.FromSeconds(25),
                        onBreak: (_, _) => { },
                        onReset: () => { }),
                Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(12))
            )
        );
    });

// CORS, logging, domain services, memory + ingestion
builder.Services.ConfigureCors(builder.Configuration);
builder.Services.ConfigureLoggerService();
builder.Services.ConfigureChatServices(builder.Configuration);
builder.Services.AddMemoryServices(builder.Configuration);
builder.Services.ConfigureIngestion();
builder.Services.AddSqlRagWithLocalEmbeddings(builder.Configuration);
// Warmup hosted service
builder.Services.AddHostedService<SharpLlama.HostedServices.ModelWarmupHostedService>();

// OpenAPI
builder.Services.AddOpenApi();

// OPTIONS (strongly typed + validation)
builder.Services
    .AddOptions<ModelOptions>()
    .Bind(builder.Configuration.GetSection("ModelOptions")) // CHANGED from "Model"
    .ValidateDataAnnotations()
    .Validate(o => !string.IsNullOrWhiteSpace(o.ModelPath), "ModelPath must be provided.")
    .ValidateOnStart();

builder.Services
    .AddOptions<ChatServiceOptions>()
    .Bind(builder.Configuration.GetSection("ChatService"))
    .ValidateDataAnnotations()
    .Validate(o => o.MemorySearchTimeoutSeconds <= o.RequestTimeoutSeconds,
        "MemorySearchTimeoutSeconds must be <= RequestTimeoutSeconds.")
    .ValidateOnStart();

builder.Services
    .AddOptions<OtlpOptions>()
    .Bind(builder.Configuration.GetSection("OTLP"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", p =>
        p.WithOrigins("https://localhost:7200", "http://localhost:5200") // adjust to ChatUI ports
         .AllowAnyHeader()
         .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseRequestCorrelation();
app.UseLogEnrichment();
app.UseContentTypeAndAcceptEnforcement();
app.UseAuthentication();
app.UseRateLimiter();
app.UseRequestLogging();
app.UseRequestMetrics();
app.UseGlobalProblemDetails();
app.UseAuthorization();
app.UseCors("Frontend");


// Prometheus scrape endpoint (default path /metrics)
app.MapPrometheusScrapingEndpoint("/metrics");

// Moved API welcome off "/" to avoid conflict with Blazor root page
app.MapGet("/api", () => "Welcome to the Chat API!");
app.MapControllers();

await app.RunAsync();