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

using System.ComponentModel.DataAnnotations.Schema;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Task4ge.Server.Database.Model;

public class Task
{
    [BsonId]
    [BsonRequired]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string? Id { get; set; }

    [BsonRequired]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = BsonType.DateTime)]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [BsonRequired]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = BsonType.DateTime)]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [BsonRequired]
    public string User { get; set; } = string.Empty;

    [BsonRequired]
    public string Name { get; set; } = string.Empty;

    [BsonRequired]
    public string Description { get; set; } = string.Empty;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = BsonType.DateTime)]
    public DateTime? StartDate { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = BsonType.DateTime)]
    public DateTime? EndDate { get; set; }

    [BsonRequired]
    public IList<string> Images { get; set; } = [];

    [BsonRequired]
    public bool Completed { get; set; }
}
