//------------------------------------------------------------------------------
// <copyright file="ProductController.cs" company="DevConn">
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
    using Task4ge.Server.Dto.Product;
    using Task4ge.Server.UserManagement;

    [ApiController]
    [Route("[controller]")]
    [Produces(MediaTypeNames.Application.Json, "application/problem+json")]
    public class ProductController(ILogger<ProductController> logger, Context context, IValidator<Product> validator, IAuth0Api auth0Api) : ControllerBase
    {
        #region Fields
        private readonly ILogger<ProductController> _logger = logger;
        private readonly Context _context = context;
        private readonly IValidator<Product> _validator = validator;
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
            var product = await this._context.Products
                .Where(x => x.Id == id.Trim())
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();
            if (product is null)
            {
                return this.NotFound();
            }

            return this.Ok(
                new
                {
                    product.Id,
                    product.CreatedAt,
                    product.UpdatedAt,
                    product.Name,
                    product.Description,
                    product.Category,
                    product.Unit,
                    product.Quantity,
                    product.Images,
                    product.Price,
                    product.Proof,
                    product.Ingredients,
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

            Product product =
                new()
                {
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    Name = request.Name,
                    Description = request.Description,
                    Category = request.Category,
                    Unit = request.Unit,
                    Quantity = request.Quantity,
                    Images = request.Images,
                    Price = request.Price,
                    Proof = request.Proof,
                    Ingredients = request.Ingredients,
                };
            ValidationResult validation = await this._validator.ValidateAsync(product);
            if (!validation.IsValid)
            {
                return await Task.FromResult(this.ValidationProblem(new ValidationProblemDetails(validation.ToDictionary())));
            }

            EntityEntry entry = await this._context.AddAsync(product);
            await this._context.SaveChangesAsync();
            Product savedProduct = (Product)entry.Entity;
            return this.Ok(
                new
                {
                    savedProduct.Id,
                    savedProduct.CreatedAt,
                    savedProduct.UpdatedAt,
                });
        }
        #endregion
    }
}