using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Web.Controllers;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class CandidatePhotoControllerTests
{
    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        Assert.NotNull(typeof(CandidatePhotoController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void Profile_DoesNotAcceptUserIdParameter()
    {
        var method = typeof(CandidatePhotoController).GetMethod(nameof(CandidatePhotoController.Profile));
        Assert.NotNull(method);
        Assert.DoesNotContain(method!.GetParameters(), p =>
            p.Name is not null && p.Name.Contains("user", StringComparison.OrdinalIgnoreCase) && p.ParameterType != typeof(CancellationToken));
    }

    [Fact]
    public async Task Profile_Success_ReturnsSecureImageHeaders()
    {
        var photos = new Mock<ICandidatePhotoService>();
        var userId = Guid.NewGuid();
        photos.Setup(p => p.GetCurrentUserPhotoAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<PhotoFileContent>.Ok(new PhotoFileContent
            {
                Content = new byte[] { 0xFF, 0xD8, 0xFF },
                ContentType = "image/jpeg",
                FileName = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg"
            }));

        var controller = new CandidatePhotoController(photos.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(userId)
            }
        };

        var result = await controller.Profile(CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("image/jpeg", file.ContentType);
        Assert.Contains("no-store", controller.Response.Headers.CacheControl.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"].ToString());
    }

    private static DefaultHttpContext CreateHttpContext(Guid userId)
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, DomainConstants.RoleNames.Candidate)
        }, authenticationType: "Test");
        context.User = new ClaimsPrincipal(identity);
        return context;
    }
}
