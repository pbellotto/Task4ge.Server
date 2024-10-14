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
using System.Security.Cryptography;
using Amazon.S3;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MongoDB.Driver;
using Task4ge.Server.Database;
using Task4ge.Server.Dto.Task;
using Task4ge.Server.Services;
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
            .Where(x => x.Id == id)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();
        if (task is null)
        {
            return NotFound();
        }

        return Ok(
            new GetResponse()
            {
                Id = task.Id ?? string.Empty,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt,
                Priority = task.Priority,
                Name = task.Name,
                Description = task.Description,
                StartDate = task.StartDate,
                EndDate = task.EndDate,
                Images = task.Images.Select(x => x.Url).ToList(),
                Completed = task.Completed
            });
    }

    [HttpGet($"{nameof(this.GetAll)}")]
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
                    Id = x.Id ?? string.Empty,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    Priority = x.Priority,
                    Name = x.Name,
                    Description = x.Description,
                    StartDate = x.StartDate,
                    EndDate = x.EndDate,
                    Images = x.Images.Select(x => x.Url).ToList(),
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

        IList<Database.Model.Task.Image> images = [];
        foreach (IFormFile item in request.Images ?? [])
        {
            if (item.Length <= 0)
            {
                continue;
            }

            using Stream stream = item.OpenReadStream();
            images.Add(
                new Database.Model.Task.Image()
                {
                    Hash = await CalculateImageMd5Async(stream),
                    Url = await AmazonS3.UploadImageAsync(_amazonS3Client, stream, item.ContentType)
                });
        }

        EntityEntry entry = await _context.AddAsync(
            new Database.Model.Task()
            {
                Priority = request.Priority,
                User = this.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
                Name = request.Name,
                Description = request.Description,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Images = images
            });
        await _context.SaveChangesAsync();
        Database.Model.Task savedTask = (Database.Model.Task)entry.Entity;
        return CreatedAtAction(
            nameof(Post),
            new PostResponse()
            {
                Id = savedTask.Id ?? string.Empty,
                CreatedAt = savedTask.CreatedAt,
                UpdatedAt = savedTask.UpdatedAt,
                Images = savedTask.Images.Select(x => x.Url).ToList()
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

        Database.Model.Task? existingTask = await _context.Tasks
            .Where(x => x.Id == request.Id)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();
        if (existingTask is null)
        {
            return NotFound();
        }

        IList<Database.Model.Task.Image> images = [];
        foreach (IFormFile item in request.Images ?? [])
        {
            using Stream stream = item.OpenReadStream();
            images.Add(
                new Database.Model.Task.Image()
                {
                    Hash = await CalculateImageMd5Async(stream),
                    Url = await AmazonS3.UploadImageAsync(_amazonS3Client, stream, item.ContentType)
                });
        }

        existingTask.UpdatedAt = DateTime.Now;
        existingTask.Priority = request.Priority;
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
        var task = await _context.Tasks
            .Where(x => x.Id == id)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();
        if (task is null)
        {
            return NotFound();
        }

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static async Task<string> CalculateImageMd5Async(Stream image)
    {
        using MD5 md5 = MD5.Create();
        return Convert.ToBase64String(await md5.ComputeHashAsync(image));
    }
}