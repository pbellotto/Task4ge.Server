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

using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;
using Task4ge.Server.Utils.Secrets;

namespace Task4ge.Server.UserManagement;

public sealed class Auth0Api(IConfiguration configuration) : IAuth0Api
{
    private readonly IConfiguration _configuration = configuration;
    private ManagementApiClient? _client;

    private ManagementApiClient Client
    {
        get
        {
            Auth0Settings? auth0Settings = _configuration.GetSection("Auth0").Get<Auth0Settings>();
            return _client ??= new ManagementApiClient(auth0Settings?.ManagementApiToken, new Uri(Environment.GetEnvironmentVariable("AUTH0_AUDIENCE")!));
        }

        set => _client = value;
    }

    public async Task<User> Get(string id) => await this.Client.Users.GetAsync(id);
}