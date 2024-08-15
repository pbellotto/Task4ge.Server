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

using System.Net.Mime;
using System.Security.Claims;
using Amazon.S3;
using Amazon.S3.Transfer;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MongoDB.Driver;
using Task4ge.Server.Database;
using Task4ge.Server.Dto.Task;
using Task4ge.Server.UserManagement;

namespace Task4ge.Server.Controllers;

[ApiController]
[Route("[controller]")]
[Produces(MediaTypeNames.Application.Json, MediaTypeNames.Application.ProblemJson)]
public class TaskController(ILogger<TaskController> logger, Context context, IAuth0Api auth0Api, IAmazonS3 amazonS3Client) : ControllerBase
{
    private readonly ILogger<TaskController> _logger = logger;
    private readonly Context _context = context;
    private readonly IAuth0Api _auth0Api = auth0Api;
    private readonly IAmazonS3 _amazonS3Client = amazonS3Client;

    public ClaimsIdentity Identity => (ClaimsIdentity)this.User.Identity!;

    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType<GetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get(string id)
    {
        var task = await _context.Tasks
            .Where(x => x.Id == id.Trim())
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();
        if (task is null)
        {
            return NotFound();
        }

        return Ok(
            new GetResponse()
            {
                Id = task.Id,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt,
                Name = task.Name,
                Description = task.Description,
                StartDate = task.StartDate,
                EndDate = task.EndDate,
                Completed = task.Completed
            });
    }

    [HttpGet($"/{nameof(GetAll)}")]
    [Authorize]
    [ProducesResponseType<List<GetAllResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAll()
    {
        var tasks = await _context.Tasks
            .Select(x =>
                new GetAllResponse()
                {
                    Id = x.Id,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    Name = x.Name,
                    Description = x.Description,
                    StartDate = x.StartDate,
                    EndDate = x.EndDate,
                    Completed = x.Completed
                })
            .ToListAsync();
        if (tasks is null)
        {
            return NotFound();
        }

        return Ok(tasks);
    }

    [HttpPost]
    [Authorize]
    [Consumes(MediaTypeNames.Multipart.FormData)]
    [ProducesResponseType<PostResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Post([FromForm] PostRequest request, [FromServices] PostRequest.Validator validator)
    {
        ValidationResult validation = await validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        EntityEntry entry = await _context.AddAsync(
            new Database.Model.Task()
            {
                User = this.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)!.Value,
                Name = request.Name,
                Description = request.Description,
                StartDate = request.StartDate,
                EndDate = request.EndDate
            });
        await _context.SaveChangesAsync();
        Database.Model.Task savedTask = (Database.Model.Task)entry.Entity;
        return CreatedAtAction(
            nameof(Post),
            new PostResponse()
            {
                Id = savedTask.Id,
                CreatedAt = savedTask.CreatedAt,
                UpdatedAt = savedTask.UpdatedAt,
            });
    }

    [HttpPut]
    [Authorize]
    [Consumes(MediaTypeNames.Multipart.FormData)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Put([FromForm] PutRequest request, [FromServices] PutRequest.Validator validator)
    {
        ValidationResult validation = await validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        Database.Model.Task? existingTask = await _context.Tasks.FirstOrDefaultAsync(x => x.Id == request.Id);
        if (existingTask is null)
        {
            return NotFound();
        }

        existingTask.UpdatedAt = DateTime.Now;
        existingTask.Name = request.Name;
        existingTask.Description = request.Description;
        existingTask.StartDate = request.StartDate;
        existingTask.EndDate = request.EndDate;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(string id)
    {
        Database.Model.Task? task = await _context.Tasks.FirstOrDefaultAsync(x => x.Id == id);
        if (task is null)
        {
            return NotFound();
        }

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string> UploadImageToAmazonS3(Stream image)
    {
        TransferUtility fileTransferUtility = new(_amazonS3Client);
        string key = Guid.NewGuid().ToString();
        await fileTransferUtility.UploadAsync(
            new()
            {
                BucketName = "task4gebucket",
                Key = key,
                InputStream = image,
                ContentType = "image/jpeg",
                CannedACL = S3CannedACL.PublicRead,
            });
        return $"https://task4gebucket.s3.amazonaws.com/{key}";
    }
}