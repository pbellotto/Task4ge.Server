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

using FluentValidation;

namespace Task4ge.Server.Dto.Task;

public class PostRequest
{
    public Database.Model.Task.PriorityEnum Priority { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool Completed { get; set; }
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
                .When(x => x.EndDate.HasValue)
                .WithMessage("End date must be greater than or equal to today.");
        }
    }
}