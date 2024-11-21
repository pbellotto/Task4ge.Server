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

using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;

namespace Task4ge.Server.Database;

public class Context(DbContextOptions options) : DbContext(options)
{
    public DbSet<Model.Image> Images { get; init; }
    public DbSet<Model.Log> Logs { get; init; }
    public DbSet<Model.Task> Tasks { get; init; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Model.Image>().ToCollection("images");
        modelBuilder.Entity<Model.Log>().ToCollection("logs");
        modelBuilder.Entity<Model.Task>().ToCollection("tasks");
    }
}