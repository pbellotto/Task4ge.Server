//------------------------------------------------------------------------------
// <copyright file="PostRequest.cs" company="DevConn">
//     Copyright (c) 2023 DevConn Software. All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">pbellotto</owner>
//------------------------------------------------------------------------------

namespace Task4ge.Server.Dto.Product
{
    using FluentValidation;

    public class PostRequest
    {
        public required string Name { get; set; }
        public required string Description { get; set; }
        public required string Category { get; set; }
        public required string Unit { get; set; }
        public int Quantity { get; set; }
        public required string[] Images { get; set; }
        public float Price { get; set; }
        public float Proof { get; set; }
        public required string Ingredients { get; set; }

        public class Validator : AbstractValidator<PostRequest>
        {
            #region Constructor
            public Validator()
            {
            }
            #endregion
        }
    }
}