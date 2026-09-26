using Azure.Storage.Blobs;
using DocIntel.API.BackgroundJobs;
using DocIntel.API.Hubs;
using DocIntel.API.Middleware;
using DocIntel.Core.Interfaces;
using DocIntel.Infrastructure.AI;
using DocIntel.Infrastructure.Data;
using DocIntel.Infrastructure.Processing;
using DocIntel.Infrastructure.Storage;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Serilog;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/docintel-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("DocuSense API starting up...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    var connectionString = builder.Configuration
        .GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Connection string 'DefaultConnection' not found in configuration.");

    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("JWT Key not configured.");

    var jwtIssuer = builder.Configuration["Jwt:Issuer"]
        ?? throw new InvalidOperationException("JWT Issuer not configured.");

    var jwtAudience = builder.Configuration["Jwt:Audience"]
        ?? throw new InvalidOperationException("JWT Audience not configured.");

    var openAiKey = builder.Configuration["OpenAI:ApiKey"]
        ?? throw new InvalidOperationException("OpenAI API Key not configured.");

    var embeddingModel = builder.Configuration["OpenAI:EmbeddingModel"]
        ?? "text-embedding-3-small";

    var chatModel = builder.Configuration["OpenAI:ChatModel"]
        ?? "gpt-4o";

    var blobConnectionString = builder.Configuration["Azure:BlobStorage:ConnectionString"]
        ?? throw new InvalidOperationException(
            "Azure Blob connection string not configured.");

    var blobContainerName = builder.Configuration["Azure:BlobStorage:ContainerName"]
        ?? "documents";

    builder.Services.AddDbContextFactory<AppDbContext>((provider, options) =>
        options.UseSqlServer(connectionString));

    builder.Services.AddScoped<AppDbContext>(provider =>
    {
        var factory = provider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();

        return factory.CreateDbContext();
    });

    builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

    var openAiClient = new OpenAIClient(openAiKey);

    builder.Services.AddSingleton(
        openAiClient.GetEmbeddingClient(embeddingModel));

    builder.Services.AddSingleton(
        openAiClient.GetChatClient(chatModel));

    builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
    builder.Services.AddScoped<IRagService, RagService>();

    builder.Services.AddScoped<PdfExtractor>();
    builder.Services.AddScoped<DocxExtractor>();
    builder.Services.AddScoped<TextChunker>();

    builder.Services.AddSingleton(
        new BlobServiceClient(blobConnectionString));

    builder.Services.AddScoped<IBlobStorageService>(sp =>
        new BlobStorageService(
            sp.GetRequiredService<BlobServiceClient>(),
            blobContainerName));

    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
        {
            QueuePollInterval = TimeSpan.FromSeconds(15),
            JobExpirationCheckInterval = TimeSpan.FromHours(1)
        }));

    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = 5;
    });

    builder.Services.AddScoped<DocumentProcessingJob>();

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)),

            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateLifetime = true,

            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

    builder.Services.AddAuthorization();

    builder.Services.AddSignalR();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ReactApp", policy =>
        {
            policy
                .WithOrigins(
                    "http://localhost:5173",
                    "http://localhost:3000",
                    "https://docusense.azurestaticapps.net"
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();

        using var context = contextFactory.CreateDbContext();

        await context.Database.MigrateAsync();

        Log.Information("Database migrations applied successfully.");
    }

    app.UseMiddleware<ErrorHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.UseCors("ReactApp");

    app.UseAuthentication();

    app.UseAuthorization();

    app.UseMiddleware<WorkspaceMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseHangfireDashboard("/hangfire");
    }

    app.MapControllers();
    app.MapHub<DocumentHub>("/hubs/documents");

    Log.Information("DocuSense API started. Ready to accept requests.");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "DocuSense API failed to start.");
}
finally
{
    Log.CloseAndFlush();
}