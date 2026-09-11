using Microsoft.AspNetCore.Http;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class BlockPublicPhotoUploadsMiddlewareTests
{
    [Theory]
    [InlineData("/uploads/photos")]
    [InlineData("/uploads/photos/")]
    [InlineData("/uploads/photos/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg")]
    [InlineData("/Uploads/Photos/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.png")]
    public async Task BlockedPaths_Return404_ForAnonymousAndAuthenticated(string path)
    {
        foreach (var authenticated in new[] { false, true })
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            if (authenticated)
                context.User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim("sub", "1") }, "auth"));

            var calledNext = false;
            var middleware = new BlockPublicPhotoUploadsMiddleware(_ =>
            {
                calledNext = true;
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(context);

            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
            Assert.False(calledNext);
        }
    }

    [Theory]
    [InlineData("/vendor/bootstrap/5.3.8/css/bootstrap.min.css")]
    [InlineData("/vendor/jquery/3.7.1/js/jquery.min.js")]
    [InlineData("/favicon.ico")]
    public async Task NonPhotoStaticPaths_PassThrough(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var calledNext = false;
        var middleware = new BlockPublicPhotoUploadsMiddleware(_ =>
        {
            calledNext = true;
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(calledNext);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("/uploads/photos")]
    [InlineData("/uploads/photos/x.jpg")]
    [InlineData("/vendor/bootstrap/5.3.8/css/bootstrap.min.css")]
    public void IsBlockedPhotoPath_MatchesExpected(string path)
    {
        var blocked = BlockPublicPhotoUploadsMiddleware.IsBlockedPhotoPath(path);
        Assert.Equal(path.StartsWith("/uploads/photos", StringComparison.OrdinalIgnoreCase), blocked);
    }
}
