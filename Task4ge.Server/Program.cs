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
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Logging;
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
using Task4ge.Server.UserManagement;
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

            // Health checkers
            Log.Information("Adding health checkers");
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
                                    {
                                        jwtSecurityScheme,
                                        Array.Empty<string>()
                                    }
                            });
                        x.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
                    });

            // CORS
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
                                .AllowCredentials()
                                .SetIsOriginAllowedToAllowWildcardSubdomains();
                        });
                });

            // Authentication (Auth0 and JWT)
            JsonWebKeySet jwks;
            using (HttpClient httpClient = new())
            {
                jwks = JsonWebKeySet.Create(await httpClient.GetStringAsync(Environment.GetEnvironmentVariable("AUTH0_JWKS_ENDPOINT")));
            }

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(
                    opt =>
                    {
                        opt.Authority = Environment.GetEnvironmentVariable("AUTH0_AUTHORITY");
                        opt.Audience = Environment.GetEnvironmentVariable("AUTH0_AUDIENCE");
                        opt.TokenValidationParameters =
                            new TokenValidationParameters
                            {
                                ValidAlgorithms = ["RS256"],
                                ValidateIssuerSigningKey = true,
                                IssuerSigningKeys = jwks.Keys,
                                ValidateIssuer = true,
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
            builder.Services.AddScoped<IAuth0Api, Auth0Api>();

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

            // Configure AWS S3
            Log.Information("Configuring AWS S3");
            AwsSettings? awsSettings = builder.Configuration.GetSection("Aws").Get<AwsSettings>();
            builder.Services.AddScoped<IAmazonS3>(_ => new AmazonS3Client(new BasicAWSCredentials(awsSettings?.KeyId, awsSettings?.KeySecret), RegionEndpoint.USEast1));

            // Build application
            Log.Information("Building application");
            WebApplication app = builder.Build();
            app.Use(
                async (context, next) =>
                {
                    context.Response.Headers.Append("Cache-Control", "private, max-age=3600, must-revalidate");
                    context.Response.Headers.Append("X-Developed-By", "DevConn Software");
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
            switch (app.Environment.IsDevelopment())
            {
                case true:
                    app.MapControllers().AllowAnonymous();
                    break;

                default:
                    app.MapControllers();
                    break;
            }

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
}