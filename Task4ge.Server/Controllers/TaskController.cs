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

    public string LoggedUser => this.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType<GetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get(string id)
    {
        Database.Model.Task? task = await _context.Tasks
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
        return Ok(await _context.Tasks
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
            .ToListAsync());
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

        List<ImageControl> imageControls = [];
        if (request.Images?.Length > 0)
        {
            foreach (IFormFile image in request.Images.Where(x => x.Length > 0))
            {
                Stream imageStream = image.OpenReadStream();
                imageControls.Add(
                    new ImageControl()
                    {
                        Hash = await CalculateImageMd5Async(imageStream),
                        Stream = imageStream,
                        ContentType = image.ContentType
                    });
            }
        }

        await this.SaveImagesAsync(imageControls);
        Database.Model.Task taskAdd =
            new()
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

        Database.Model.Task? taskPrevious = await _context.Tasks
            .Where(x => x.User == this.LoggedUser)
            .Where(x => x.Id == request.Id)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();
        if (taskPrevious is null)
        {
            return NotFound();
        }

        List<ImageControl> imageControls = [];
        if (request.Images?.Length > 0)
        {
            foreach (IFormFile image in request.Images.Where(x => x.Length > 0))
            {
                Stream imageStream = image.OpenReadStream();
                imageControls.Add(
                    new ImageControl()
                    {
                        Hash = await CalculateImageMd5Async(imageStream),
                        Stream = imageStream,
                        ContentType = image.ContentType
                    });
            }
        }

        List<ImageControl> savedImages = await _context.Images
            .Where(x => taskPrevious.ImagesIds.Contains(x.Id!))
            .Select(x =>
                new ImageControl()
                {
                    Id = x.Id,
                    Hash = x.Hash,
                    Key = x.Key,
                    Url = x.Url
                })
            .ToListAsync();
        IEnumerable<ImageControl> imagesToDelete = savedImages.Where(x => !imageControls.Select(y => y.Hash).Contains(x.Hash));
        if (imagesToDelete.Any())
        {
            await this.DeleteImagesAsync(imagesToDelete);
        }

        IEnumerable<ImageControl> imagesToAdd = imageControls.Where(x => !savedImages.Select(y => y.Hash).Contains(x.Hash));
        if (imagesToAdd.Any())
        {
            await this.SaveImagesAsync(imagesToAdd);
        }

        IEnumerable<ImageControl> images = savedImages
            .Where(x => !imagesToDelete.Any(y => y.Id == x.Id))
            .Concat(imagesToAdd);
        Database.Model.Task taskUpdate =
            new()
            {
                Id = request.Id,
                User = this.LoggedUser,
                Priority = request.Priority,
                Name = request.Name,
                Description = request.Description,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                ImagesIds = images.Select(x => x.Id!).ToList()
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
        return Ok(
            new PutResponse()
            {
                Images = images.Select(x => x.Url).ToList()
            });
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

        List<ImageControl> imagesToDelete = await _context.Images
            .Where(x => taskDelete.ImagesIds.Contains(x.Id!))
            .Select(x =>
                new ImageControl()
                {
                    Id = x.Id,
                    Key = x.Key
                })
            .ToListAsync();
        await this.DeleteImagesAsync(imagesToDelete);
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

    private async System.Threading.Tasks.Task SaveImagesAsync(IEnumerable<ImageControl> imageControls)
    {
        foreach (ImageControl imageControl in imageControls)
        {
            Image imageAdd;
            using (imageControl.Stream)
            {
                (string, string) uploadedImageData = await _amazonS3Api.UploadImageAsync(imageControl.Stream!, imageControl.ContentType);
                imageControl.Key = uploadedImageData.Item1;
                imageControl.Url = uploadedImageData.Item2;
                imageAdd =
                    new()
                    {
                        Hash = imageControl.Hash,
                        Key = imageControl.Key,
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

    private async System.Threading.Tasks.Task DeleteImagesAsync(IEnumerable<ImageControl> imageControls)
    {
        foreach (ImageControl imageControl in imageControls)
        {
            await _amazonS3Api.DeleteImageAsync(imageControl.Key);
            Image? imagePrevious = await _context.Images.Where(x => x.Id == imageControl.Id).FirstOrDefaultAsync();
            _context.Images.Remove(new Image() { Id = imageControl.Id });
            await _logControl.RegisterAsync(
                new LogControl.RegisterArgs()
                {
                    Type = Log.TypeEnum.Delete,
                    User = this.LoggedUser,
                    UserIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                    Model = nameof(Image),
                    PreviousObj = imagePrevious,
                    Save = false
                });
        }
    }

    private class ImageControl
    {
        public string? Id { get; set; }
        public string Hash { get; set; } = string.Empty;
        public Stream? Stream { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}