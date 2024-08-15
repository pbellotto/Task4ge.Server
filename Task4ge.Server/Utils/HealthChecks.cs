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

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Task4ge.Server.HealthChecks;

namespace Task4ge.Server.Utils;

public static class HealthChecks
{
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

    public static IHealthChecksBuilder AddGCInfo(this IHealthChecksBuilder builder, string name, HealthStatus? failureStatus = null, IEnumerable<string>? tags = null, long? thresholdInBytes = null)
    {
        builder.AddCheck<GCInfo>(name, failureStatus ?? HealthStatus.Degraded, tags ?? []);
        if (thresholdInBytes.HasValue)
        {
            builder.Services.Configure<GCInfo.Options>(name, options => { options.Threshold = thresholdInBytes.Value; });
        }

        return builder;
    }
}