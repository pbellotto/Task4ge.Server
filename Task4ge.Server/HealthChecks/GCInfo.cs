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

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Task4ge.Server.HealthChecks;

public class GCInfo(IOptionsMonitor<GCInfo.Options> options) : IHealthCheck
{
    private readonly IOptionsMonitor<Options> _options = options;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        Options options = _options.Get(context.Registration.Name);
        long allocated = GC.GetTotalMemory(forceFullCollection: false);
        Dictionary<string, object> data =
            new()
            {
                    { "Allocated", allocated },
                    { "Gen0Collections", GC.CollectionCount(0) },
                    { "Gen1Collections", GC.CollectionCount(1) },
                    { "Gen2Collections", GC.CollectionCount(2) },
            };
        HealthStatus result = allocated >= options.Threshold ? context.Registration.FailureStatus : HealthStatus.Healthy;
        return Task.FromResult(new HealthCheckResult(result, description: "Reports degraded status if allocated bytes >= 1gb", data: data));
    }

    public class Options
    {
        public long Threshold { get; set; } = 1024L * 1024L * 1024L;
    }
}