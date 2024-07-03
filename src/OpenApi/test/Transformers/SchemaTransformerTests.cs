// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

public class SchemaTransformerTests : OpenApiDocumentServiceTestBase
{
    [Fact]
    public async Task SchemaTransformer_CanAccessTypeAndParameterDescriptionForParameter()
    {
        var builder = CreateBuilder();

        builder.MapPost("/todo", (Todo todo) => { });

        var options = new OpenApiOptions();
        options.UseSchemaTransformer((schema, context, cancellationToken) =>
        {
            if (context.JsonPropertyInfo == null)
            {
                Assert.Equal(typeof(Todo), context.Type);
                Assert.Equal("todo", context.ParameterDescription.Name);
            }
            if (context.JsonPropertyInfo?.Name == "id")
            {
                Assert.Equal(typeof(int), context.Type);
            }
            if (context.JsonPropertyInfo?.Name == "name")
            {
                Assert.Equal(typeof(string), context.Type);
            }
            if (context.JsonPropertyInfo?.Name == "isComplete")
            {
                Assert.Equal(typeof(bool), context.Type);
            }
            if (context.JsonPropertyInfo?.Name == "dueDate")
            {
                Assert.Equal(typeof(DateTime), context.Type);
            }
            return Task.CompletedTask;
        });

        await VerifyOpenApiDocument(builder, options, document => { });
    }

    [Fact]
    public async Task SchemaTransformer_CanAccessTypeForResponse()
    {
        var builder = CreateBuilder();

        builder.MapGet("/todo", () => new Todo(1, "Item1", false, DateTime.Now));

        var options = new OpenApiOptions();
        options.UseSchemaTransformer((schema, context, cancellationToken) =>
        {
            if (context.JsonPropertyInfo == null)
            {
                Assert.Equal(typeof(Todo), context.Type);
            }
            if (context.JsonPropertyInfo?.Name == "id")
            {
                Assert.Equal(typeof(int), context.Type);
            }
            if (context.JsonPropertyInfo?.Name == "name")
            {
                Assert.Equal(typeof(string), context.Type);
            }
            if (context.JsonPropertyInfo?.Name == "isComplete")
            {
                Assert.Equal(typeof(bool), context.Type);
            }
            if (context.JsonPropertyInfo?.Name == "dueDate")
            {
                Assert.Equal(typeof(DateTime), context.Type);
            }
            Assert.Null(context.ParameterDescription);
            return Task.CompletedTask;
        });

        await VerifyOpenApiDocument(builder, options, document => { });
    }

    [Fact]
    public async Task SchemaTransformer_CanAccessApplicationServicesAndDocumentName()
    {
        var builder = CreateBuilder();

        builder.MapGet("/todo", () => new Todo(1, "Item1", false, DateTime.Now));

        var options = new OpenApiOptions();
        options.UseSchemaTransformer((schema, context, cancellationToken) =>
        {
            var service = context.ApplicationServices.GetKeyedService<OpenApiDocumentService>(context.DocumentName);
            Assert.NotNull(service);
            return Task.CompletedTask;
        });

        await VerifyOpenApiDocument(builder, options, document => { });
    }

    [Fact]
    public async Task SchemaTransformer_RespectsCancellationToken()
    {
        var builder = CreateBuilder();

        builder.MapGet("/todo", () => new Todo(1, "Item1", false, DateTime.Now));

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var options = new OpenApiOptions();
        options.UseSchemaTransformer((schema, context, cancellationToken) =>
        {
            Assert.Equal(cts.Token, cancellationToken);
            Assert.True(cancellationToken.IsCancellationRequested);
            return Task.CompletedTask;
        });

        await VerifyOpenApiDocument(builder, options, document => { }, cts.Token);
    }

    [Fact]
    public async Task SchemaTransformer_RunsInRegisteredOrder()
    {
        var builder = CreateBuilder();

        builder.MapPost("/todo", (Todo todo) => { });

        var options = new OpenApiOptions();
        options.UseSchemaTransformer((schema, context, cancellationToken) =>
        {
            schema.Extensions["x-my-extension"] = new OpenApiString("1");
            return Task.CompletedTask;
        });
        options.UseSchemaTransformer((schema, context, cancellationToken) =>
        {
            Assert.Equal("1", ((OpenApiString)schema.Extensions["x-my-extension"]).Value);
            schema.Extensions["x-my-extension"] = new OpenApiString("2");
            return Task.CompletedTask;
        });

        await VerifyOpenApiDocument(builder, options, document =>
        {
            var operation = Assert.Single(document.Paths.Values).Operations.Values.Single();
            var schema = operation.RequestBody.Content["application/json"].Schema;
            Assert.Equal("2", ((OpenApiString)schema.Extensions["x-my-extension"]).Value);
        });
    }

    [Fact]
    public async Task SchemaTransformer_OnTypeModifiesBothRequestAndResponse()
    {
        var builder = CreateBuilder();

        builder.MapPost("/todo", (Todo todo) => { });
        builder.MapGet("/todo", () => new Todo(1, "Item1", false, DateTime.Now));

        var options = new OpenApiOptions();
        options.UseSchemaTransformer((schema, context, cancellationToken) =>
        {
            if (context.Type == typeof(Todo))
            {
                schema.Extensions["x-my-extension"] = new OpenApiString("1");
            }
            return Task.CompletedTask;
        });

        await VerifyOpenApiDocument(builder, options, document =>
        {
            var path = Assert.Single(document.Paths.Values);
            var postOperation = path.Operations[OperationType.Post];
            var requestSchema = postOperation.RequestBody.Content["application/json"].Schema.GetEffective(document);
            Assert.Equal("1", ((OpenApiString)requestSchema.Extensions["x-my-extension"]).Value);
            var getOperation = path.Operations[OperationType.Get];
            var responseSchema = getOperation.Responses["200"].Content["application/json"].Schema.GetEffective(document);
            Assert.Equal("1", ((OpenApiString)responseSchema.Extensions["x-my-extension"]).Value);
        });
    }

    [Fact]
    public async Task SchemaTransformer_WithDescriptionOnlyModifiesParameter()
    {
        var builder = CreateBuilder();

        builder.MapPost("/todo", (Todo todo) => { });
        builder.MapGet("/todo", () => new Todo(1, "Item1", false, DateTime.Now));

        var options = new OpenApiOptions();
        options.UseSchemaTransformer((schema, context, cancellationToken) =>
        {
            if (context.Type == typeof(Todo) && context.ParameterDescription is not null)
            {
                schema.Extensions["x-my-extension"] = new OpenApiString(context.ParameterDescription.Name);
            }
            return Task.CompletedTask;
        });

        await VerifyOpenApiDocument(builder, options, document =>
        {
            var path = Assert.Single(document.Paths.Values);
            var postOperation = path.Operations[OperationType.Post];
            var requestSchema = postOperation.RequestBody.Content["application/json"].Schema;
            Assert.Equal("todo", ((OpenApiString)requestSchema.Extensions["x-my-extension"]).Value);
            var getOperation = path.Operations[OperationType.Get];
            var responseSchema = getOperation.Responses["200"].Content["application/json"].Schema;
            Assert.False(responseSchema.Extensions.TryGetValue("x-my-extension", out var _));
        });
    }

    [Fact]
    public async Task SchemaTransformer_CanModifyAllTypesInADocument()
    {
        var builder = CreateBuilder();

        builder.MapPost("/todo", (Todo todo) => { });
        builder.MapGet("/todo", (int id) => {});

        var options = new OpenApiOptions();
        options.UseSchemaTransformer((schema, context, cancellationToken) =>
        {
            if (context.Type == typeof(int))
            {
                schema.Format = "modified-number-format";
            }
            return Task.CompletedTask;
        });

        await VerifyOpenApiDocument(builder, options, document =>
        {
            // Assert that parameter schema has been update
            var path = Assert.Single(document.Paths.Values);
            var getOperation = path.Operations[OperationType.Get];
            var responseSchema = getOperation.Parameters[0].Schema;
            Assert.Equal("modified-number-format", responseSchema.Format);

            // Assert that property in request body schema has been updated
            var postOperation = path.Operations[OperationType.Post];
            var requestSchema = postOperation.RequestBody.Content["application/json"].Schema;
            Assert.Equal("modified-number-format", requestSchema.Properties["id"].Format);
        });
    }

    [Fact]
    public async Task SchemaTransformer_CanModifyItemTypesInADocument()
    {
        var builder = CreateBuilder();

        builder.MapGet("/list", () => new List<int> { 1, 2, 3, 4 });
        builder.MapGet("/single", () => 1);

        var options = new OpenApiOptions();
        options.UseSchemaTransformer((schema, context, cancellationToken) =>
        {
            if (context.Type == typeof(int))
            {
                schema.Format = "modified-number-format";
            }
            schema = new OpenApiSchema { Type = "array", Items = schema };
            return Task.CompletedTask;
        });

        await VerifyOpenApiDocument(builder, options, document =>
        {
            // Assert that item schema has been updated
            var path = document.Paths["/list"];
            var getOperation = path.Operations[OperationType.Get];
            var responseSchema = getOperation.Responses["200"].Content["application/json"].Schema;
            Assert.Equal("modified-number-format", responseSchema.Items.Format);

            // Assert that top-level schema has been updated
            path = document.Paths["/single"];
            getOperation = path.Operations[OperationType.Get];
            responseSchema = getOperation.Responses["200"].Content["application/json"].Schema;
            Assert.Equal("modified-number-format", responseSchema.Format);
        });
    }

    [Fact]
    public async Task SchemaTransformer_CanModifyPolymorphicChildSchemas()
    {
        var builder = CreateBuilder();

        builder.MapPost("/shape", (Shape todo) => { });
        builder.MapPost("/triangle", (Triangle todo) => { });

        var options = new OpenApiOptions();
        options.UseSchemaTransformer((schema, context, cancellationToken) =>
        {
            if (context.Type == typeof(Triangle))
            {
                schema.Extensions["x-my-extension"] = new OpenApiString("this-is-a-triangle");
            }
            return Task.CompletedTask;
        });

        await VerifyOpenApiDocument(builder, options, document =>
        {
            // Assert that top-level schema has been updated
            var path = document.Paths["/shape"];
            var postOperation = path.Operations[OperationType.Post];
            var requestSchema = postOperation.RequestBody.Content["application/json"].Schema;
            var triangleSubschema = Assert.Single(requestSchema.AnyOf.Where(s => s.Reference.Id == "ShapeTriangle"));
            Assert.True(triangleSubschema.GetEffective(document).Extensions.TryGetValue("x-my-extension", out var _));

            // Assert that the top-level schema has been updated
            path = document.Paths["/triangle"];
            postOperation = path.Operations[OperationType.Post];
            requestSchema = postOperation.RequestBody.Content["application/json"].Schema;
            Assert.Equal("this-is-a-triangle", ((OpenApiString)requestSchema.Extensions["x-my-extension"]).Value);
        });
    }
}
