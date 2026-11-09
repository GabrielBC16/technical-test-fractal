using System.Globalization;
using System.Text.Json.Serialization;
using MaintenanceBoard.Api.Authentication;
using MaintenanceBoard.Api.Configuration;
using MaintenanceBoard.Api.Errors;
using MaintenanceBoard.Api.OpenApi;
using MaintenanceBoard.Application.Access;
using MaintenanceBoard.Application.Common;
using MaintenanceBoard.Application.Tickets;
using MaintenanceBoard.Domain.Tickets;
using MaintenanceBoard.Infrastructure;
using MaintenanceBoard.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.OpenApi;
using Microsoft.Extensions.Options;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(provider =>
{
    var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString("MaintenanceBoard");
    return !string.IsNullOrWhiteSpace(connectionString) ? connectionString
        : throw new InvalidOperationException("Set ConnectionStrings__MaintenanceBoard before starting the API.");
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<TicketTransitionPolicy>();
builder.Services.AddAuthentication(HeaderAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(HeaderAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder(HeaderAuthenticationHandler.SchemeName)
        .RequireAuthenticatedUser().Build();
});
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    // Reject actor-ID fields and other unknown input instead of silently accepting them.
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = ApiProblem.ModelValidation);

builder.Services.AddCors();
/*builder.Services.AddOptions<CorsOptions>().Configure<IConfiguration>((options, configuration) =>
{
    var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (allowedOrigins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
        uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
        uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)))
    {
        throw new InvalidOperationException("Cors:AllowedOrigins must contain HTTP or HTTPS origins without paths or credentials.");
    }

    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(allowedOrigins.Select(origin => origin.TrimEnd('/')).ToArray())
        .WithMethods("GET", "POST", "OPTIONS")
        .AllowAnyHeader()
        .WithExposedHeaders("Location"));
});*/

//For this time, we allow any origin
builder.Services.AddOptions<CorsOptions>().Configure(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader()
        .WithExposedHeaders("Location"));
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "MaintenanceBoard API",
            Version = "v1",
            Description = "Simulated authentication; replace with JWT/OIDC in production. Only the authentication handler needs to change."
        });
        options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "MaintenanceBoard.Api.xml"));
        options.AddSecurityDefinition(HeaderAuthenticationHandler.SchemeName, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = HeaderAuthenticationHandler.HeaderName,
            Description = "Enter the active userId returned by POST /api/auth/login."
        });
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(HeaderAuthenticationHandler.SchemeName, document)] = []
        });
        options.OperationFilter<AnonymousSecurityOperationFilter>();
    });
}

var app = builder.Build();
// Validate the final host configuration before accepting requests; opening SQL is deferred.
_ = app.Services.GetRequiredService<ISqlConnectionFactory>();
_ = app.Services.GetRequiredService<IOptions<CorsOptions>>().Value;
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseStatusCodePages(context => ApiProblem.WriteAsync(context.HttpContext,
    context.HttpContext.Response.StatusCode, ReasonPhrases.GetReasonPhrase(context.HttpContext.Response.StatusCode),
    $"http.{context.HttpContext.Response.StatusCode}"));
app.UseRouting();
app.UseCors("Frontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "MaintenanceBoard API v1"));
}

// Azure App Service terminates TLS. Enable HTTPS Only at the hosting platform;
// do not redirect its internal HTTP hop or require a local HTTPS certificate here.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
await app.SeedDevelopmentDataAsync();
app.Run();

public partial class Program;
