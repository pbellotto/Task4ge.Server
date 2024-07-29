//------------------------------------------------------------------------------
// <copyright file="Cors.cs" company="DevConn">
//     Copyright (c) 2023 DevConn Software. All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">pbellotto</owner>
//------------------------------------------------------------------------------

namespace Task4ge.Server.Utils
{
    using System.Text;
    using System.Text.Json;
    using Task4ge.Server.HealthChecks.GCInfo;
    using Microsoft.AspNetCore.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Diagnostics.HealthChecks;

    public static class HealthChecks
    {
        #region Properties
        public static HealthCheckOptions Options
        {
            get
            {
                return
                    new HealthCheckOptions()
                    {
                        ResponseWriter =
                            (httpContext, healthReport) =>
                            {
                                httpContext.Response.ContentType = "application/json; charset=utf-8";
                                JsonWriterOptions options = new() { Indented = true };
                                using MemoryStream memoryStream = new();
                                using (Utf8JsonWriter jsonWriter = new(memoryStream, options))
                                {
                                    jsonWriter.WriteStartObject();
                                    jsonWriter.WriteString("status", healthReport.Status.ToString());
                                    jsonWriter.WriteStartObject("results");
                                    foreach (var healthReportEntry in healthReport.Entries)
                                    {
                                        jsonWriter.WriteStartObject(healthReportEntry.Key);
                                        jsonWriter.WriteString("status", healthReportEntry.Value.Status.ToString());
                                        jsonWriter.WriteString("description", healthReportEntry.Value.Description);
                                        jsonWriter.WriteStartObject("data");
                                        foreach (var item in healthReportEntry.Value.Data)
                                        {
                                            jsonWriter.WritePropertyName(item.Key);
                                            JsonSerializer.Serialize(jsonWriter, item.Value, item.Value?.GetType() ?? typeof(object));
                                        }

                                        jsonWriter.WriteEndObject();
                                        jsonWriter.WriteEndObject();
                                    }

                                    jsonWriter.WriteEndObject();
                                    jsonWriter.WriteEndObject();
                                }

                                return httpContext.Response.WriteAsync(Encoding.UTF8.GetString(memoryStream.ToArray()));
                            }
                    };
            }
        }
        #endregion

        #region Methods
        public static IHealthChecksBuilder AddGCInfo(this IHealthChecksBuilder builder, string name, HealthStatus? failureStatus = null, IEnumerable<string>? tags = null, long? thresholdInBytes = null)
        {
            builder.AddCheck<GCInfoHealthCheck>(name, failureStatus ?? HealthStatus.Degraded, tags ?? new List<string>());
            if (thresholdInBytes.HasValue)
            {
                builder.Services.Configure<GCInfoOptions>(name, options => { options.Threshold = thresholdInBytes.Value; });
            }

            return builder;
        }
        #endregion

    }
}