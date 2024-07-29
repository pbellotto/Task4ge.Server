//------------------------------------------------------------------------------
// <copyright file="Auth0Api.cs" company="DevConn">
//     Copyright (c) 2023 DevConn Software. All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">pbellotto</owner>
//------------------------------------------------------------------------------

namespace Task4ge.Server.UserManagement
{
    using Auth0.ManagementApi;
    using Auth0.ManagementApi.Models;

    public sealed class Auth0Api : IAuth0Api
    {
        #region Fields
        private ManagementApiClient? client;
        #endregion

        #region Properties
        private ManagementApiClient Client
        {
            get => this.client ??= new ManagementApiClient(Environment.GetEnvironmentVariable("AUTH0_MANAGEMENT_API_TOKEN")!, new Uri(Environment.GetEnvironmentVariable("AUTH0_AUDIENCE")!));
            set => this.client = value;
        }
        #endregion

        #region Methods
        public async Task<User> Get(string id) => await this.Client.Users.GetAsync(id);
        #endregion
    }
}