using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SalesApp
{
    public class FileUploadOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var fileParams = context.MethodInfo.GetParameters()
                .Where(p => p.ParameterType == typeof(IFormFile))
                .ToList();

            if (!fileParams.Any())
                return;

            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>(),
                            Required = new HashSet<string>()
                        }
                    }
                }
            };

            var schema = operation.RequestBody.Content["multipart/form-data"].Schema;

            // Add file parameter
            foreach (var fileParam in fileParams)
            {
                schema.Properties[fileParam.Name!] = new OpenApiSchema
                {
                    Type = "string",
                    Format = "binary"
                };
                schema.Required.Add(fileParam.Name!);
            }

            // Add other [FromForm] parameters
            var formParams = context.MethodInfo.GetParameters()
                .Where(p => p.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.FromFormAttribute), false).Any())
                .Where(p => p.ParameterType != typeof(IFormFile))
                .ToList();

            foreach (var param in formParams)
            {
                var paramType = param.ParameterType;
                var schemaType = "string";

                if (paramType == typeof(int) || paramType == typeof(int?))
                    schemaType = "integer";
                else if (paramType == typeof(bool) || paramType == typeof(bool?))
                    schemaType = "boolean";

                schema.Properties[param.Name!] = new OpenApiSchema
                {
                    Type = schemaType
                };

                if (!param.HasDefaultValue && Nullable.GetUnderlyingType(paramType) == null)
                {
                    schema.Required.Add(param.Name!);
                }
            }

            // Remove parameters that are now in the request body
            var paramsToRemove = operation.Parameters
                .Where(p => fileParams.Any(fp => fp.Name == p.Name) || 
                           formParams.Any(fp => fp.Name == p.Name))
                .ToList();

            foreach (var param in paramsToRemove)
            {
                operation.Parameters.Remove(param);
            }
        }
    }
}
