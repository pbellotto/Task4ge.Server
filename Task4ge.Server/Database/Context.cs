//------------------------------------------------------------------------------
// <copyright file="Context.cs" company="DevConn">
//     Copyright (c) 2023 DevConn Software. All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">pbellotto</owner>
//------------------------------------------------------------------------------

namespace Task4ge.Server.Database
{
    using Microsoft.EntityFrameworkCore;
    using MongoDB.EntityFrameworkCore.Extensions;
    using Task4ge.Server.Database.Model;

    public class Context(DbContextOptions options) : DbContext(options)
    {
        #region Properties
        public DbSet<Task> Tasks { get; init; }
        #endregion

        #region Methods
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Base
            base.OnModelCreating(modelBuilder);

            // Tasks
            modelBuilder.Entity<Task>().ToCollection("tasks");
        }
        #endregion
    }
}