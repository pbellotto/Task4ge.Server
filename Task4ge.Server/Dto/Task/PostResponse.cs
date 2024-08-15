//------------------------------------------------------------------------------
// <copyright file="PostResponse.cs" company="DevConn">
//     Copyright (c) 2023 DevConn Software. All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">pbellotto</owner>
//------------------------------------------------------------------------------

namespace Task4ge.Server.Dto.Task
{
    public class PostResponse
    {
        public required string Id { get; set; }
        public required DateTime CreatedAt { get; set; }
        public required DateTime UpdatedAt { get; set; }
    }
}