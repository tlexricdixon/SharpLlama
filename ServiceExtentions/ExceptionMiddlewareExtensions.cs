// ***********************************************************************
// Assembly         : Web
// Author           : ricdi
// Created          : 03-16-2024
//
// Last Modified By : ricdi
// Last Modified On : 03-16-2024
// ***********************************************************************
// <copyright file="ExceptionMiddlewareExtensions.cs" company="Web">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
using Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using SharpLlama.Contracts;
using System.Net;

namespace SharpLlama.ServiceExtentions
{
    /// <summary>
    /// Class ExceptionMiddlewareExtensions.
    /// </summary>
    public static class ExceptionMiddlewareExtensions
    {
        /// <summary>
        /// Configures the exception handler.
        /// </summary>
        /// <param name="app">The application.</param>
        /// <param name="logger">The logger.</param>
        public static void ConfigureExceptionHandler(this IApplicationBuilder app, ILoggerManager logger)
        {
            app.UseExceptionHandler(appError =>
            {
                appError.Run(async context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    context.Response.ContentType = "application/json";

                    var contextFeature = context.Features.Get<IExceptionHandlerFeature>();
                    if (contextFeature != null)
                    {
                        logger.LogError($"Something went wrong: {contextFeature.Error}");
                        await context.Response.WriteAsync(new ErrorDetails()
                        {
                            StatusCode = context.Response.StatusCode,
                            Message = "Internal Server Error"
                        }.ToString());
                    }
                });
            });
        }
    }
}
