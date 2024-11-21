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

using Newtonsoft.Json;
using Task4ge.Server.Database;

namespace Task4ge.Server.Services;

public sealed class LogControl(Context context) : ILogControl
{
    private readonly Context _context = context;

    public async Task RegisterAsync(RegisterArgs args)
    {
        await _context.AddAsync(
            new Database.Model.Log()
            {
                Type = args.Type,
                User = args.User,
                UserIp = args.UserIp,
                Model = args.Model,
                PreviousData = args.PreviousObj is not null ? JsonConvert.SerializeObject(args.PreviousObj) : null,
                CurrentData = args.CurrentObj is not null ? JsonConvert.SerializeObject(args.CurrentObj) : null
            });
        if (args.Save)
        {
            await _context.SaveChangesAsync();
        }
    }

    public sealed class RegisterArgs
    {
        public Database.Model.Log.TypeEnum Type { get; set; }
        public string User { get; set; } = string.Empty;
        public string UserIp { get; set; } = string.Empty;
        public string? Model { get; set; }
        public object? PreviousObj { get; set; }
        public object? CurrentObj { get; set; }
        public bool Save { get; set; } = true;
    }
}