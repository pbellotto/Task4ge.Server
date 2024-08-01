//------------------------------------------------------------------------------
// <copyright file="Task.cs" company="DevConn">
//     Copyright (c) 2023 DevConn Software. All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">pbellotto</owner>
//------------------------------------------------------------------------------

namespace Task4ge.Server.Database.Model
{
    using System.ComponentModel.DataAnnotations.Schema;
    using MongoDB.Bson;
    using MongoDB.Bson.Serialization.Attributes;

    public class Task
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
        #endregion
    }
}