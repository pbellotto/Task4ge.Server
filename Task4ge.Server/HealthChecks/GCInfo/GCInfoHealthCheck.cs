//------------------------------------------------------------------------------
// <copyright file="GCInfoHealthCheck.cs" company="DevConn">
//     Copyright (c) 2023 DevConn Software. All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">pbellotto</owner>
//------------------------------------------------------------------------------

namespace Task4ge.Server.HealthChecks.GCInfo
{
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Options;

    public class GCInfoHealthCheck(IOptionsMonitor<GCInfoOptions> options) : IHealthCheck
    {
        #region Fields
        private readonly IOptionsMonitor<GCInfoOptions> _options = options;
        #endregion

        #region Methods
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            GCInfoOptions options = this._options.Get(context.Registration.Name);
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
        #endregion
    }
}