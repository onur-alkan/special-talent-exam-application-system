using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Web.Controllers;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class AccountControllerAuthorizationTests
{
    private static readonly Type ControllerType = typeof(AccountController);

    [Fact]
    public void Controller_DoesNotHaveClassLevelAllowAnonymous()
    {
        Assert.Null(ControllerType.GetCustomAttribute<AllowAnonymousAttribute>(inherit: false));
    }

    [Theory]
    [InlineData(nameof(AccountController.Captcha))]
    [InlineData(nameof(AccountController.AccessDenied))]
    public void AnonymousGetActions_HaveAllowAnonymous(string actionName)
    {
        var method = ControllerType.GetMethod(actionName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(method.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void RegisterGet_HasAllowAnonymous()
    {
        var method = GetParameterlessOrTokenAsyncGet(nameof(AccountController.Register));
        Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(method.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void RegisterPost_HasAllowAnonymousAndAntiForgery()
    {
        var method = GetPostMethod(nameof(AccountController.Register), typeof(RegisterViewModel));
        Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Null(method.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void LoginGet_HasAllowAnonymous()
    {
        var method = ControllerType.GetMethod(
            nameof(AccountController.Login),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
            binder: null,
            types: [typeof(string)],
            modifiers: null);

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(method.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void LoginPost_HasAllowAnonymousAndAntiForgery()
    {
        var method = GetPostMethod(nameof(AccountController.Login), typeof(LoginViewModel));
        Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Null(method.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void ForgotPasswordGet_HasAllowAnonymous()
    {
        var method = ControllerType.GetMethod(
            nameof(AccountController.ForgotPassword),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void ForgotPasswordPost_HasAllowAnonymousAndAntiForgery()
    {
        var method = GetPostMethod(nameof(AccountController.ForgotPassword), typeof(ForgotPasswordViewModel));
        Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void ResetPasswordGet_HasAllowAnonymous()
    {
        var method = ControllerType.GetMethod(
            nameof(AccountController.ResetPassword),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
            binder: null,
            types: [typeof(string)],
            modifiers: null);

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void ResetPasswordPost_HasAllowAnonymousAndAntiForgery()
    {
        var method = GetPostMethod(nameof(AccountController.ResetPassword), typeof(ResetPasswordViewModel));
        Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void Logout_HasAuthorizeAndAntiForgery()
    {
        var method = ControllerType.GetMethod(
            nameof(AccountController.Logout),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void ChangePasswordGet_HasAuthorize()
    {
        var method = ControllerType.GetMethod(
            nameof(AccountController.ChangePassword),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void ChangePasswordPost_HasAuthorizeAndAntiForgery()
    {
        var method = GetPostMethod(nameof(AccountController.ChangePassword), typeof(ChangePasswordViewModel));
        Assert.NotNull(method.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    private static MethodInfo GetParameterlessOrTokenAsyncGet(string name)
    {
        var methods = ControllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == name)
            .ToArray();

        var get = methods.FirstOrDefault(m => m.GetCustomAttribute<HttpPostAttribute>() is null);
        Assert.NotNull(get);
        return get!;
    }

    private static MethodInfo GetPostMethod(string name, Type firstParameterType)
    {
        var method = ControllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .FirstOrDefault(m =>
                m.Name == name
                && m.GetCustomAttribute<HttpPostAttribute>() is not null
                && m.GetParameters().Length > 0
                && m.GetParameters()[0].ParameterType == firstParameterType);

        Assert.NotNull(method);
        return method!;
    }
}
