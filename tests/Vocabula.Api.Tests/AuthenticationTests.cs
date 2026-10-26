using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Vocabula.Api;
using Xunit;

namespace Vocabula.Api.Tests;

public sealed class AuthenticationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthenticationTests()
    {
        _factory = new CustomWebApplicationFactory();
    }

    [Fact]
    public async Task KnowledgeExtract_WithoutAuthentication_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/knowledge/extract", null!);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StoriesGenerate_WithoutAuthentication_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/stories/generate", null!);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UsersMe_WithoutAuthentication_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_Get_WithoutAuthentication_Returns200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:Cognito:Authority"] = "https://cognito-idp.test.amazonaws.com/test",
                    ["Authentication:Cognito:AppClientId"] = "test-client-id"
                });
            });
            
            return base.CreateHost(builder);
        }
    }
}
