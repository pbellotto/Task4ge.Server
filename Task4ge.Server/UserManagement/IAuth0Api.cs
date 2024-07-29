//------------------------------------------------------------------------------
// <copyright file="IAuth0Api.cs" company="DevConn">
//     Copyright (c) 2023 DevConn Software. All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">pbellotto</owner>
//------------------------------------------------------------------------------

namespace Task4ge.Server.UserManagement
{
    using Auth0.ManagementApi.Models;

    public interface IAuth0Api
    {
        #region Methods
        Task<User> Get(string id);
        #endregion
    }
}