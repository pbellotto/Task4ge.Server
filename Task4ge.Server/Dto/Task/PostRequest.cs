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
        public DateTime? StartDate { get; set; }
        public required DateTime EndDate { get; set; }
        public IFormFile[]? Images { get; set; }

        public class Validator : AbstractValidator<PostRequest>
        {
            public Validator()
            {
                this.RuleFor(x => x.Name)
                    .NotEmpty()
                    .WithMessage("Invalid name.");
                this.RuleFor(x => x.Description)
                    .NotEmpty()
                    .WithMessage("Invalid description.");
                this.RuleFor(x => x.StartDate)
                    .LessThanOrEqualTo(x => x.EndDate)
                    .When(x => x.StartDate.HasValue)
                    .WithMessage("Start date must be less than or equal to start date.");
                this.RuleFor(x => x.EndDate)
                    .GreaterThanOrEqualTo(DateTime.Today)
                    .WithMessage("End date must be greater than or equal to today.");
            }
        }
    }
}