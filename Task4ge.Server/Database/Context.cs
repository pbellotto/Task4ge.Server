//------------------------------------------------------------------------------
// <copyright file="Context.cs" company="DevConn">
//     Copyright (c) 2023 DevConn Software. All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">pbellotto</owner>
//------------------------------------------------------------------------------

namespace Task4ge.Server.Database
{
    using Task4ge.Server.Database.Model;
    using Microsoft.EntityFrameworkCore;
    using MongoDB.EntityFrameworkCore.Extensions;

    public class Context(DbContextOptions options) : DbContext(options)
    {
        #region Properties
        public DbSet<Product> Products { get; init; }
        #endregion

        #region Methods
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Base
            base.OnModelCreating(modelBuilder);

            // Products
            modelBuilder.Entity<Product>().ToCollection("products");
        }
        #endregion
    }
}