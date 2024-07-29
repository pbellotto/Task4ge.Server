//------------------------------------------------------------------------------
// <copyright file="Product.cs" company="DevConn">
//     Copyright (c) 2023 DevConn Software. All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">pbellotto</owner>
//------------------------------------------------------------------------------

namespace Task4ge.Server.Database.Model
{
    using System.ComponentModel.DataAnnotations.Schema;
    using MongoDB.Bson;
    using MongoDB.Bson.Serialization.Attributes;

    public class Product
    {
        #region Properties
        [BsonId]
        [BsonRequired]
        [BsonRepresentation(BsonType.ObjectId)]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string? Id { get; set; }

        [BsonRequired]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = BsonType.Document)]
        public DateTime CreatedAt { get; set; }

        [BsonRequired]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = BsonType.Document)]
        public DateTime UpdatedAt { get; set; }

        [BsonRequired]
        public required string Name { get; set; }

        [BsonRequired]
        public required string Description { get; set; }

        [BsonRequired]
        public required string Category { get; set; }

        [BsonRequired]
        public required string Unit { get; set; }

        [BsonRequired]
        public int Quantity { get; set; }

        [BsonRequired]
        public required string[] Images { get; set; }

        [BsonRequired]
        public float Price { get; set; }

        [BsonRequired]
        public float Proof { get; set; }

        [BsonRequired]
        public required string Ingredients { get; set; }
        #endregion
    }
}