using Microsoft.AspNetCore.Mvc;
using Vocabula.Api.Controllers;

namespace Vocabula.Api.Tests;

public sealed class HealthControllerTests
{
    [Fact]
    public void Get_ReturnsOk()
    {
        var controller = new HealthController();

        var result = controller.Get();

        Assert.IsType<OkObjectResult>(result);
    }
}
