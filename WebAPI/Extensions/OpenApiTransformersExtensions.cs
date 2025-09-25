using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace WebAPI.Extensions
{
    public static class OpenApiTransformersExtensions
    {
        public static OpenApiOptions UseJwtBearerAuthentication(this OpenApiOptions options)
        {
            var scheme = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Name = JwtBearerDefaults.AuthenticationScheme,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                }
            };

            options.AddDocumentTransformer((document, context, ct) =>
            {
                document.Components ??= new();
                document.Components.SecuritySchemes.Add(JwtBearerDefaults.AuthenticationScheme, scheme);
                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, ct) =>
            {
                if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
                {
                    operation.Security = [new() { [scheme] = [] }];
                }
                return Task.CompletedTask;
            });

            return options;

        }
    }
}
