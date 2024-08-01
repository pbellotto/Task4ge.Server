//------------------------------------------------------------------------------
// <copyright file="TaskController.cs" company="DevConn">
//     Copyright (c) 2023 DevConn Software. All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">pbellotto</owner>
//------------------------------------------------------------------------------

namespace Task4ge.Server.Controllers
{
    using System.Net.Mime;
    using System.Security.Claims;
    using FluentValidation;
    using FluentValidation.Results;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using Task4ge.Server.Database;
    using Task4ge.Server.Database.Model;
    using Task4ge.Server.Dto.Task;
    using Task4ge.Server.UserManagement;

    [ApiController]
    [Route("[controller]")]
    [Produces(MediaTypeNames.Application.Json, "application/problem+json")]
    public class TaskController(ILogger<TaskController> logger, Context context, IValidator<Task> validator, IAuth0Api auth0Api) : ControllerBase
    {
        #region Fields
        private readonly ILogger<TaskController> _logger = logger;
        private readonly Context _context = context;
        private readonly IValidator<Task> _validator = validator;
        private readonly IAuth0Api _auth0Api = auth0Api;
        #endregion

        #region Properties
        public ClaimsIdentity Identity => (ClaimsIdentity)this.User.Identity!;
        #endregion

        #region Methods
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get(string id)
        {
            var task = await this._context.Tasks
                .Where(x => x.Id == id.Trim())
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();
            if (task is null)
            {
                return this.NotFound();
            }

            return this.Ok(
                new
                {
                    task.Id,
                    task.CreatedAt,
                    task.UpdatedAt,
                    task.Name,
                    task.Description,
                });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Post([FromBody] PostRequest request)
        {
            if (request is null)
            {
                return this.BadRequest();
            }

            Task task =
                new()
                {
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    Name = request.Name,
                    Description = request.Description,
                };
            ValidationResult validation = await this._validator.ValidateAsync(task);
            if (!validation.IsValid)
            {
                return this.ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
            }

            EntityEntry entry = await this._context.AddAsync(task);
            await this._context.SaveChangesAsync();
            Task savedTask = (Task)entry.Entity;
            return this.Ok(
                new
                {
                    savedTask.Id,
                    savedTask.CreatedAt,
                    savedTask.UpdatedAt,
                });
        }
        #endregion
    }
}