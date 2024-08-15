//------------------------------------------------------------------------------
// <copyright file="GetAllResponse.cs" company="DevConn">
//     Copyright (c) 2023 DevConn Software. All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">pbellotto</owner>
//------------------------------------------------------------------------------

namespace Task4ge.Server.Dto.Task
{
    public class GetAllResponse
    {
        public required string Id { get; set; }
        public required DateTime CreatedAt { get; set; }
        public required DateTime UpdatedAt { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public DateTime? StartDate { get; set; }
        public required DateTime EndDate { get; set; }
        public required bool Completed { get; set; }
    }
}