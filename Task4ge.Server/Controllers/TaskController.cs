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
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using Task4ge.Server.Database;
using Task4ge.Server.Database.Model;
using Task4ge.Server.Dto.Task;
using Task4ge.Server.Services;

namespace Task4ge.Server.Controllers;

[ApiController]
[Route("[controller]")]
[Produces(MediaTypeNames.Application.Json, MediaTypeNames.Application.ProblemJson)]
public class TaskController(ILogger<TaskController> logger, Context context, IAmazonS3Api amazonS3Api, IAuth0Api auth0Api, ILogControl logControl) : ControllerBase
{
    private readonly ILogger<TaskController> _logger = logger;
    private readonly Context _context = context;
    private readonly IAmazonS3Api _amazonS3Api = amazonS3Api;
    private readonly IAuth0Api _auth0Api = auth0Api;
    private readonly ILogControl _logControl = logControl;

    public ClaimsIdentity Identity => (ClaimsIdentity)this.User.Identity!;
    public string LoggedUser => this.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType<GetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get(string id)
    {
        var task = await _context.Tasks
            .Where(x => x.User == this.LoggedUser)
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
                Images = await _context.Images
                    .Where(x => task.ImagesIds.Any(y => y == x.Id))
                    .Select(x => x.Url)
                    .ToListAsync(),
                Completed = task.Completed
            });
    }

    [HttpGet(nameof(GetAll))]
    [Authorize]
    [ProducesResponseType<List<GetAllResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAll()
    {
        var tasks = await _context.Tasks
            .Where(x => x.User == this.LoggedUser)
            .Select(x =>
                new GetAllResponse()
                {
                    Id = x.Id ?? string.Empty,
                    Name = x.Name,
                    Description = x.Description,
                    StartDate = x.StartDate,
                    EndDate = x.EndDate
                })
            .ToListAsync();
        if (tasks.Count <= 0)
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

        IList<ImageControl> imageControls = [];
        if (request.Images?.Length > 0)
        {
            foreach (IFormFile item in request.Images.Where(x => x.Length > 0))
            {
                Stream itemStream = item.OpenReadStream();
                imageControls.Add(
                    new ImageControl()
                    {
                        Hash = await CalculateImageMd5Async(itemStream),
                        Stream = itemStream,
                        ContentType = item.ContentType
                    });
            }
        }

        if (imageControls.Count > 0)
        {
            var hashes = imageControls.Select(x => x.Hash).Distinct();
            var existentImagesLookup = _context.Images
                .Where(x => hashes.Any(y => y == x.Hash))
                .Select(x =>
                    new
                    {
                        x.Id,
                        x.Hash,
                        x.Url
                    })
                .ToLookup(x => x.Hash);
            foreach (ImageControl imageControl in imageControls)
            {
                var existentImage = existentImagesLookup[imageControl.Hash].FirstOrDefault();
                if (existentImage is not null)
                {
                    imageControl.Id = existentImage.Id;
                    imageControl.Url = existentImage.Url;
                    continue;
                }

                Image imageAdd;
                using (imageControl.Stream)
                {
                    imageControl.Url = await _amazonS3Api.UploadImageAsync(imageControl.Stream!, imageControl.ContentType);
                    imageAdd =
                        new()
                        {
                            Hash = imageControl.Hash,
                            Url = imageControl.Url
                        };
                }

                await _context.AddAsync(imageAdd);
                await _logControl.RegisterAsync(
                    new LogControl.RegisterArgs()
                    {
                        Type = Log.TypeEnum.Insert,
                        User = this.LoggedUser,
                        UserIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                        Model = nameof(Image),
                        CurrentObj = imageAdd,
                        Save = false
                    });
                imageControl.Id = imageAdd.Id;
            }
        }

        var taskAdd =
            new Database.Model.Task()
            {
                Priority = request.Priority,
                User = this.LoggedUser,
                Name = request.Name,
                Description = request.Description,
                StartDate = request.StartDate?.ToUniversalTime(),
                EndDate = request.EndDate?.ToUniversalTime(),
                ImagesIds = imageControls.Select(x => x.Id!).ToList()
            };
        await _context.AddAsync(taskAdd);
        await _logControl.RegisterAsync(
            new LogControl.RegisterArgs()
            {
                Type = Log.TypeEnum.Insert,
                User = this.LoggedUser,
                UserIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                Model = nameof(Database.Model.Task),
                CurrentObj = taskAdd,
                Save = false
            });
        await _context.SaveChangesAsync();
        return CreatedAtAction(
            nameof(Post),
            new PostResponse()
            {
                Id = taskAdd.Id ?? string.Empty,
                CreatedAt = taskAdd.CreatedAt,
                UpdatedAt = taskAdd.UpdatedAt,
                Images = imageControls.Select(x => x.Url).ToList()
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

        var taskPrevious = await _context.Tasks
            .Where(x => x.User == this.LoggedUser)
            .Where(x => x.Id == request.Id)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();
        if (taskPrevious == null)
        {
            return NotFound();
        }

        IList<Image> images = [];
        foreach (IFormFile item in request.Images ?? [])
        {
            using Stream stream = item.OpenReadStream();
            images.Add(
                new Image()
                {
                    Hash = await CalculateImageMd5Async(stream),
                    Url = await _amazonS3Api.UploadImageAsync(stream, item.ContentType)
                });
        }

        var taskUpdate =
            new Database.Model.Task()
            {
                Id = request.Id,
                Priority = request.Priority,
                Name = request.Name,
                Description = request.Description,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                //Images = images
            };
        _context.Update(taskUpdate);
        await _logControl.RegisterAsync(
            new LogControl.RegisterArgs()
            {
                Type = Log.TypeEnum.Update,
                User = this.LoggedUser,
                UserIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                Model = nameof(Database.Model.Task),
                PreviousObj = taskPrevious,
                CurrentObj = taskUpdate,
                Save = false
            });
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
        var taskDelete = await _context.Tasks
            .Where(x => x.User == this.LoggedUser)
            .Where(x => x.Id == id)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();
        if (taskDelete is null)
        {
            return NotFound();
        }

        _context.Tasks.Remove(taskDelete);
        await _logControl.RegisterAsync(
            new LogControl.RegisterArgs()
            {
                Type = Log.TypeEnum.Delete,
                User = this.LoggedUser,
                UserIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                Model = nameof(Database.Model.Task),
                PreviousObj = taskDelete,
                Save = false
            });
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static async Task<string> CalculateImageMd5Async(Stream image)
    {
        using MD5 md5 = MD5.Create();
        return Convert.ToBase64String(await md5.ComputeHashAsync(image));
    }

    private class ImageControl
    {
        public string? Id { get; set; }
        public string Hash { get; set; } = string.Empty;
        public Stream? Stream { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}