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

namespace Task4ge.Server.Services;

public sealed class Auth0Api(IManagementApiClient client) : IAuth0Api
{
    private readonly IManagementApiClient _client = client;

    public async Task<User> GetUserAsync(string id) => await _client.Users.GetAsync(id);

    public async Task SetUserPicture(string id, string picture) => await _client.Users.UpdateAsync(id, new UserUpdateRequest() { Picture = picture });
}