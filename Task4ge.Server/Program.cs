/*
 * Copyright (C) 2024 pbellotto (pedro.augusto.bellotto@gmail.com)
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Auth0.ManagementApi;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Task4ge.Server.Database;
using Task4ge.Server.Dto.Task;
using Task4ge.Server.Services;
using Task4ge.Server.Utils;
using Task4ge.Server.Utils.Secrets;

namespace Task4ge.Server;

public class Program
{
    private const string LOGS_FILE_NAME = ".log";
    private const string ALLOW_SPECIFIC_ORIGINS_POLICY = "ALLOW_SPECIFIC_ORIGINS_POLICY";

    static async Task Main(string[] args)
    {
        // Create logger (before creating builder)
        string logsDirectoryPath = Environment.GetEnvironmentVariable("LOGS_DIRECTORY_PATH")!;
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override(nameof(Microsoft), LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Debug()
            .WriteTo.File(
                new CompactJsonFormatter(),
                Path.Combine(logsDirectoryPath, LOGS_FILE_NAME),
                rollingInterval: RollingInterval.Day,
                buffered: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1),
                encoding: Encoding.UTF8,
                rollOnFileSizeLimit: true,
                restrictedToMinimumLevel: LogEventLevel.Debug)
            .CreateBootstrapLogger();

        try
        {
            // Create builder
            Log.Information("Starting server");
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            // Create logger
            builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.Debug()
                .WriteTo.File(
                    new CompactJsonFormatter(),
                    Path.Combine(logsDirectoryPath, LOGS_FILE_NAME),
                    rollingInterval: RollingInterval.Day,
                    buffered: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1),
                    encoding: Encoding.UTF8,
                    rollOnFileSizeLimit: true,
                    restrictedToMinimumLevel: LogEventLevel.Debug));

            // Health checks
            Log.Information("Adding health checks");
            MongoClientSettings mongoConfig = MongoClientSettings.FromConnectionString(builder.Configuration.GetConnectionString("MongoDB")!);
            string dbName = builder.Configuration.GetValue<string>("DBName")!;
            int dbConnectionTimeoutMs = int.Parse(Environment.GetEnvironmentVariable("DB_CONNECTION_TIMEOUT_MS")!);
            builder.Services.AddHealthChecks()
                .AddGCInfo("gcinfo")
                .AddMongoDb(mongoConfig, mongoDatabaseName: dbName, timeout: TimeSpan.FromMilliseconds(dbConnectionTimeoutMs));

            // Add controllers
            Log.Information("Adding controllers");
            builder.Services
                .AddControllers()
                .AddJsonOptions(
                    opt =>
                    {
                        opt.JsonSerializerOptions.PropertyNamingPolicy = null;
                    });

            // Configure services
            Log.Information("Configuring services");
            builder.Services
                .AddProblemDetails()
                .AddMemoryCache()
                .AddResponseCaching()
                .AddEndpointsApiExplorer()
                .AddSwaggerGen(
                    x =>
                    {
                        x.SwaggerDoc("v1", new OpenApiInfo { Title = "Task4ge.API - v1", Version = "v1" });
                        var jwtSecurityScheme =
                            new OpenApiSecurityScheme
                            {
                                BearerFormat = "JWT",
                                Name = "JWT Authentication",
                                In = ParameterLocation.Header,
                                Type = SecuritySchemeType.Http,
                                Scheme = JwtBearerDefaults.AuthenticationScheme,
                                Reference =
                                    new OpenApiReference
                                    {
                                        Id = JwtBearerDefaults.AuthenticationScheme,
                                        Type = ReferenceType.SecurityScheme
                                    }
                            };
                        x.AddSecurityDefinition(jwtSecurityScheme.Reference.Id, jwtSecurityScheme);
                        x.AddSecurityRequirement(
                            new OpenApiSecurityRequirement()
                            {
                                { jwtSecurityScheme, Array.Empty<string>() }
                            });
                        x.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
                    })
                .AddScoped<ILogControl, LogControl>();

            // Configure CORS
            ConfigureCors(builder);

            // Authentication (Auth0 and JWT)
            string? auth0Authority = Environment.GetEnvironmentVariable("AUTH0_AUTHORITY");
            string? auth0Audience = Environment.GetEnvironmentVariable("AUTH0_AUDIENCE");
            ConfigurationManager<OpenIdConnectConfiguration> configurationManager = new(
                Environment.GetEnvironmentVariable("AUTH0_OPENID_CONFIGURATION"),
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());
            OpenIdConnectConfiguration openIdConfig = await configurationManager.GetConfigurationAsync();
            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(
                    opt =>
                    {
                        opt.Authority = auth0Authority;
                        opt.Audience = auth0Audience;
                        opt.TokenValidationParameters =
                            new TokenValidationParameters
                            {
                                ClockSkew = TimeSpan.FromMinutes(5),
                                IssuerSigningKeys = openIdConfig.SigningKeys,
                                ValidAudience = auth0Audience,
                                ValidIssuer = auth0Authority,
                                NameClaimType = ClaimTypes.NameIdentifier
                            };
                        opt.Events =
                            new JwtBearerEvents()
                            {
                                OnAuthenticationFailed =
                                    context =>
                                    {
                                        Log.Error("Error on Auth0 authentication: {Message}", context.Exception.Message);
                                        return Task.CompletedTask;
                                    }
                            };
                    });

            // Configure DB context
            Log.Information("Configuring database");
            DbSettings? dbSettings = builder.Configuration.GetSection("Db").Get<DbSettings>();
            mongoConfig.ServerApi = new ServerApi(ServerApiVersion.V1);
            mongoConfig.SslSettings =
                new SslSettings()
                {
                    ClientCertificates = [new(Environment.GetEnvironmentVariable("DB_CERTIFICATE_PATH")!, dbSettings?.CertificateKey)],
                };
            MongoClient mongoClient = new(mongoConfig);
            builder.Services.AddSingleton(mongoClient);
            builder.Services
                .AddDbContext<Context>(
                    opt =>
                    {
                        opt
                            .UseMongoDB(mongoClient, dbName)
                            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                            .EnableSensitiveDataLogging(Debugger.IsAttached);
                    });
            BsonSerializer.RegisterIdGenerator(typeof(string), new StringObjectIdGenerator());

            // Check DB connection
            Log.Information("Checking database connection");
            IMongoDatabase database = mongoClient.GetDatabase(dbName);
            bool dbIsAlive = database.RunCommandAsync((Command<BsonDocument>)"{ping:1}").Wait(dbConnectionTimeoutMs);
            if (!dbIsAlive)
            {
                throw new Exception($"Database connection not established at: {mongoClient.Settings.Server.Host}:{mongoClient.Settings.Server.Port} ({dbName})");
            }

            Log.Information("Database connection established successfully at: {Host}:{Port} ({DbName})", mongoClient.Settings.Server.Host, mongoClient.Settings.Server.Port, dbName);

            // Add validators
            Log.Information("Adding validators");
            builder.Services.AddValidatorsFromAssemblyContaining<PostRequest.Validator>(ServiceLifetime.Scoped);

            // Configure APIs
            ConfigureApis(builder);

            // Build application
            Log.Information("Building application");
            WebApplication app = builder.Build();
            app.Use(
                async (context, next) =>
                {
                    // Security
                    switch (!context.Request.IsHttps)
                    {
                        case true:
                            context.Response.Headers.Remove("Strict-Transport-Security");
                            break;

                        default:
                            context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
                            break;
                    }

                    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'");
                    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
                    context.Response.Headers.Append("X-Frame-Options", "DENY");
                    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

                    // Performance
                    context.Response.Headers.Append("Cache-Control", "private, max-age=3600, must-revalidate");

                    // Privacy
                    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
                    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=()");

                    // Custom
                    context.Response.Headers.Append("X-Developed-By", "DevConn Software");

                    // CORS (Preflight)
                    if (context.Request.Method == HttpMethods.Options)
                    {
                        context.Response.Headers.Append("Access-Control-Allow-Origin", Environment.GetEnvironmentVariable("CORS_ORIGIN_1")!);
                        context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                        context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, Authorization");
                        context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
                        context.Response.StatusCode = StatusCodes.Status204NoContent;
                        return;
                    }

                    await next();
                });
            if (app.Environment.IsDevelopment())
            {
                IdentityModelEventSource.ShowPII = true;
                IdentityModelEventSource.LogCompleteSecurityArtifact = true;
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHealthChecks("/status", Utils.HealthChecks.Options);
            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseCors(ALLOW_SPECIFIC_ORIGINS_POLICY);
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            // Run app
            await app.RunAsync();
        }
        catch (Exception exp)
        {
            Log.Fatal(exp, "Application terminated unexpectedly.");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureCors(WebApplicationBuilder builder)
    {
        builder.Services
            .AddCors(opt =>
            {
                opt.AddPolicy(
                    ALLOW_SPECIFIC_ORIGINS_POLICY,
                    builder =>
                    {
                        builder
                            .WithOrigins([Environment.GetEnvironmentVariable("CORS_ORIGIN_1")!])
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials();
                    });
            });
    }

    private static void ConfigureApis(WebApplicationBuilder builder)
    {
        // Configure AWS S3
        Log.Information("Configuring AWS S3");
        AwsSettings? awsSettings = builder.Configuration.GetSection("Aws").Get<AwsSettings>();
        builder.Services.AddScoped<IAmazonS3>(_ => new AmazonS3Client(new BasicAWSCredentials(awsSettings?.KeyId, awsSettings?.KeySecret), RegionEndpoint.USEast1));
        builder.Services.AddScoped<IAmazonS3Api, AmazonS3Api>();

        // Configure Auth0
        Log.Information("Configuring Auth0");
        Auth0Settings? auth0Settings = builder.Configuration.GetSection("Auth0").Get<Auth0Settings>();
        builder.Services.AddScoped<IManagementApiClient>(_ => new ManagementApiClient(auth0Settings?.ManagementApiToken, new Uri(Environment.GetEnvironmentVariable("AUTH0_AUDIENCE")!)));
        builder.Services.AddScoped<IAuth0Api, Auth0Api>();
    }
}