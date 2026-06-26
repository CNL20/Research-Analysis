using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ScholarTrend.API.Filters;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services;
using ScholarTrend.Application.Validators;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;
using ScholarTrend.Infrastructure.Data.Seeders;
using ScholarTrend.Infrastructure.ExternalApis;
using ScholarTrend.Infrastructure.Jobs;
using ScholarTrend.Infrastructure.Repositories;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.DTOs.Common;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ============ SERVICES ============

// 1. Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ScholarTrendDbContext>(options =>
    options.UseSqlServer(connectionString));

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
builder.Services.AddScoped<IJournalService, JournalService>();
builder.Services.AddScoped<IAuthorService, AuthorService>();
builder.Services.AddScoped<ITrendRepository, TrendRepository>();
builder.Services.AddScoped<IStatisticsRepository, StatisticsRepository>();
builder.Services.AddScoped<ITrendService, TrendService>();
builder.Services.AddScoped<IFollowService, FollowService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<IPaperImportRepository, PaperImportRepository>();
builder.Services.AddScoped<SyncJob>();
// Bind cấu hình EmailSettings từ appsettings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// Đăng ký Email Service vào DI Container
builder.Services.AddHttpClient<IEmailService, EmailService>();
builder.Services.AddScoped<IPaperAggregationService, PaperAggregationService>();

builder.Services.AddHttpClient<ISemanticScholarClient, SemanticScholarClient>();
builder.Services.AddHttpClient<IOpenAlexClient, OpenAlexClient>();
builder.Services.AddHttpClient<ICrossrefClient, CrossrefClient>();
builder.Services.AddHttpClient<IArXivClient, ArXivClient>();

builder.Services.AddScoped<ISyncSchedulerService, SyncSchedulerService>();
builder.Services.AddScoped<ISyncJob, SyncJob>();

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
          .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
          {
              CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
              SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
              QueuePollInterval = TimeSpan.FromSeconds(15),
              UseRecommendedIsolationLevel = true,
              DisableGlobalLocks = true
          }));
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

// Hangfire Dashboard (dev only)
app.UseHangfireDashboard("/hangfire");

// ============ HANGFIRE RECURRING JOB ============
// Configure sync schedule from appsettings.json
var syncEnabled = builder.Configuration.GetValue("SyncSchedule:Enabled", true);
var syncCron = builder.Configuration["SyncSchedule:CronExpression"] ?? "0 2 * * *";

if (syncEnabled)
{
    RecurringJob.AddOrUpdate<ISyncJob>("daily-paper-sync", job => job.RunAsync(), syncCron);
}

app.MapControllers();

app.Run();
