﻿//------------------------------------------------------------------------------
// <copyright file="PostRequest.cs" company="DevConn">
//     Copyright (c) 2023 DevConn Software. All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">pbellotto</owner>
//------------------------------------------------------------------------------

namespace Task4ge.Server.Dto.Task
{
    using FluentValidation;

    public class PostRequest
    {
        public required string Name { get; set; }
        public required string Description { get; set; }

        public class Validator : AbstractValidator<PostRequest>
        {
            public Validator()
            {
            }
        }
    }
}