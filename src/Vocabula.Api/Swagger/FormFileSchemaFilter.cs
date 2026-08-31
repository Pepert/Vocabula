using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Vocabula.Api.Swagger;

public sealed class FormFileSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(IFormFile))
        {
            schema.Type = "string";
            schema.Format = "binary";
        }
    }
}
