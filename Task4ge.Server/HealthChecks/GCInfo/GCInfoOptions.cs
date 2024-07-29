//------------------------------------------------------------------------------
// <copyright file="GCInfoOptions.cs" company="DevConn">
//     Copyright (c) 2023 DevConn Software. All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">pbellotto</owner>
//------------------------------------------------------------------------------

namespace Task4ge.Server.HealthChecks.GCInfo
{
    public class GCInfoOptions
    {
        #region Construtor
        public long Threshold { get; set; } = 1024L * 1024L * 1024L;
        #endregion
    }
}