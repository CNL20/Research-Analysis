using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ScholarTrend.API.Filters;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Interfaces.Services;
using ScholarTrend.Application.Services;
using ScholarTrend.Application.Validators;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;
using ScholarTrend.Infrastructure.Data.Seeders;
using ScholarTrend.Infrastructure.ExternalApis;
using ScholarTrend.Infrastructure.HostedServices;
using ScholarTrend.Infrastructure.Jobs;
using ScholarTrend.Infrastructure.Persistence.Repositories;
using ScholarTrend.Infrastructure.Repositories;
using ScholarTrend.Infrastructure.Services;
using ScholarTrend.Infrastructure.Storage;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.Options;
using Microsoft.AspNetCore.Http.Features;
using System.Text;
using System.Text;
using HangfireBasicAuthenticationFilter;

// PostgreSQL requires UTC for timestamptz; allow legacy DateTime from seed/import code paths.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ============ SERVICES ============

// 1. Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (!string.IsNullOrEmpty(connectionString) && (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://")))
{
    var uri = new Uri(connectionString);
    var userInfo = uri.UserInfo.Split(':');
    connectionString = $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};Database={uri.LocalPath.TrimStart('/')};Username={(userInfo.Length > 0 ? userInfo[0] : "")};Password={(userInfo.Length > 1 ? userInfo[1] : "")};SslMode=Require;TrustServerCertificate=true";
}

builder.Services.AddDbContext<ScholarTrendDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Identity
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
    .AddEntityFrameworkStores<ScholarTrendDbContext>()
    .AddDefaultTokenProviders();

// 3. JWT Authentication
var secretKey = builder.Configuration["Authentication:Jwt:SecretKey"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
if (string.IsNullOrWhiteSpace(secretKey))
{
    throw new InvalidOperationException("JWT SecretKey is missing. Set Authentication:Jwt:SecretKey or JWT_SECRET_KEY.");
}

var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RequireSignedTokens = true
        };
    });

// 4. CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// 5. Dependency Injection — Repositories & Services
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IResearchPaperRepository, ResearchPaperRepository>();
builder.Services.AddScoped<IBookmarkRepository, BookmarkRepository>();
builder.Services.AddScoped<IResearchTopicRepository, ResearchTopicRepository>();
builder.Services.AddScoped<IJournalRepository, JournalRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPaperService, PaperService>();
builder.Services.AddScoped<IBookmarkService, BookmarkService>();
builder.Services.AddScoped<ITopicService, TopicService>();
builder.Services.AddScoped<ITopicInsightService, TopicInsightService>();
builder.Services.AddScoped<IJournalService, JournalService>();
builder.Services.AddScoped<IAuthorService, AuthorService>();
builder.Services.AddScoped<ITrendRepository, TrendRepository>();
builder.Services.AddScoped<IStatisticsRepository, StatisticsRepository>();
builder.Services.AddScoped<ITrendService, TrendService>();
builder.Services.AddScoped<IFollowService, FollowService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// Payment & Subscriptions
builder.Services.AddSingleton<IPaymentProvider, PayOSPaymentProvider>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// Extration Jobs
builder.Services.AddScoped<IAiExtractionService, GroqExtractionService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<IPaperImportRepository, PaperImportRepository>();
builder.Services.AddScoped<IPaperKeywordLinkerService, PaperKeywordLinkerService>();
builder.Services.AddScoped<IPaperAuthorLinkerService, PaperAuthorLinkerService>();
builder.Services.AddScoped<IJournalResolver, JournalResolver>();
builder.Services.AddScoped<IEnrichmentFetcher, EnrichmentFetcher>();
builder.Services.AddScoped<IEnrichPaperSourcesEnqueuer, EnrichPaperSourcesEnqueuer>();
builder.Services.AddScoped<EnrichPaperSourcesJob>();
builder.Services.AddScoped<RecalculateKeywordTrendsJob>();
builder.Services.AddScoped<IUserFileRepository, UserFileRepository>();
builder.Services.AddScoped<IPaperPdfFileRepository, PaperPdfFileRepository>();
builder.Services.AddScoped<IPaperQualityRepository, PaperQualityRepository>();
builder.Services.AddSingleton<IPaperPdfChannel, PaperPdfChannel>();
builder.Services.AddScoped<IPaperPdfEnqueuer, PaperPdfDownloadService>();
builder.Services.AddScoped<IPaperPdfProcessor, PaperPdfDownloadService>();
builder.Services.AddScoped<IPaperFileStorage, LocalPaperFileStorage>();
builder.Services.AddHostedService<PaperPdfDownloadWorker>();
builder.Services.AddHostedService<PaperPdfStartupRecovery>();
builder.Services.Configure<StorageSettings>(builder.Configuration.GetSection("FileUpload"));
builder.Services.Configure<FileUploadSettings>(builder.Configuration.GetSection("FileUpload"));
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 20 * 1024 * 1024;
});
builder.Services.AddScoped<SyncJob>();
// Bind cấu hình EmailSettings từ appsettings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// Đăng ký Email Service vào DI Container
builder.Services.AddHttpClient<IEmailService, EmailService>();
builder.Services.AddScoped<IPaperAggregationService, PaperAggregationService>();
builder.Services.AddHttpClient<IAiExtractionService, GroqExtractionService>();
builder.Services.AddScoped<IPdfAnalysisService, GeminiPdfAnalysisService>();

builder.Services.AddHttpClient<ISemanticScholarClient, SemanticScholarClient>();
builder.Services.AddHttpClient<IOpenAlexClient, OpenAlexClient>();
builder.Services.AddHttpClient<ICrossrefClient, CrossrefClient>();
builder.Services.AddHttpClient<IArXivClient, ArXivClient>();
builder.Services.AddHttpClient<IDocumentDownloader, HttpDocumentDownloader>();
builder.Services.AddHttpClient<IArxivDoiResolver, ArxivDoiResolver>();

builder.Services.AddScoped<ISyncSchedulerService, SyncSchedulerService>();
builder.Services.AddScoped<ISyncJob, SyncJob>();
builder.Services.AddScoped<TopicInsightExtractionJob>();
builder.Services.AddScoped<TopicInsightAggregationJob>();

// New Research Gap Analysis Jobs
builder.Services.AddScoped<PaperQualityAssessmentJob>();
builder.Services.AddScoped<PaperAnalysisExtractionJob>();
builder.Services.AddScoped<PatternMiningJob>();
builder.Services.AddScoped<ResearchGapAnalysisJob>();

// New Research Gap Analysis Services
builder.Services.AddScoped<IPaperQualityService, PaperQualityService>();
builder.Services.AddScoped<IPaperAnalysisService, PaperAnalysisService>();
builder.Services.AddScoped<IPatternMiningService, PatternMiningService>();
builder.Services.AddScoped<ITrendAnalysisService, TrendAnalysisService>();
builder.Services.AddScoped<IResearchGapService, ResearchGapService>();
builder.Services.AddScoped<ICoverageReportService, CoverageReportService>();

// New Research Gap Analysis Repositories (required for the services above)
builder.Services.AddScoped<IPaperQualityRepository, PaperQualityRepository>();
builder.Services.AddScoped<IPaperAnalysisRepository, PaperAnalysisRepository>();
builder.Services.AddScoped<IAnalysisJobRepository, AnalysisJobRepository>();
builder.Services.AddScoped<IPatternRepository, PatternRepository>();
builder.Services.AddScoped<IResearchGapRepository, ResearchGapRepository>();
builder.Services.AddScoped<IGapTimelineRepository, GapTimelineRepository>();
builder.Services.AddScoped<ICoverageReportRepository, CoverageReportRepository>();

builder.Services.AddMemoryCache();

builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();

// 7. Controllers
builder.Services.AddControllers();
builder.Services.AddApiValidationResponse();

// 6. Hangfire — Background Job Scheduler
builder.Services.AddHangfire(config =>
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));
builder.Services.AddHangfireServer();

// 8. Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ScholarTrend API", Version = "v1" });

    // Add JWT Bearer authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token. Example: eyJhbGciOi..."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ============ BUILD APP ============
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ScholarTrendDbContext>();
    await dbContext.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(app.Services);
    await ApiDataSourceSeeder.SeedAsync(dbContext);
}

// ============ MIDDLEWARE ============
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ScholarTrend API v1"));

if (app.Environment.IsDevelopment())
{
    // Các cấu hình chỉ dành riêng cho Dev (nếu có)
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Hangfire Dashboard
var hangfireUsername = builder.Configuration["Hangfire:Username"];
var hangfirePassword = builder.Configuration["Hangfire:Password"];

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[]
    {
        new HangfireCustomBasicAuthenticationFilter
        {
            User = hangfireUsername,
            Pass = hangfirePassword
        }
    }
});

// ============ HANGFIRE RECURRING JOB ============
// Configure sync schedule from appsettings.json
var syncEnabled = builder.Configuration.GetValue("SyncSchedule:Enabled", true);
var syncCron = builder.Configuration["SyncSchedule:CronExpression"] ?? "0 2 * * *";

if (syncEnabled)
{
    RecurringJob.AddOrUpdate<ISyncJob>("daily-paper-sync", job => job.RunAsync(), syncCron);
    RecurringJob.AddOrUpdate<TopicInsightExtractionJob>("topic-insight-extraction", job => job.RunExtractionAsync(CancellationToken.None), "*/10 * * * *"); // Run every 10 mins
    RecurringJob.AddOrUpdate<TopicInsightAggregationJob>("topic-insight-aggregation", job => job.RunAggregationAsync(CancellationToken.None), "0 2 * * *"); // Run daily at 2 AM
    RecurringJob.AddOrUpdate<RecalculateKeywordTrendsJob>("keyword-trend-recalc", job => job.RunAsync(CancellationToken.None), "0 3 * * *"); // Run daily at 3 AM
}

app.MapControllers();

app.Run();
