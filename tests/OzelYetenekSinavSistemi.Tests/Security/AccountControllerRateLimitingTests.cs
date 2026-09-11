using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Web.Controllers;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class AccountControllerRateLimitingTests
{
    private static readonly Type ControllerType = typeof(AccountController);

    public static TheoryData<string> ExpectedPolicyNames => new()
    {
        "login",
        "password-reset-request",
        "password-reset-submit"
    };

    [Fact]
    public void ForgotPasswordPost_HasPasswordResetRequestPolicy()
    {
        var method = GetPostMethod(nameof(AccountController.ForgotPassword), typeof(ForgotPasswordViewModel));
        Assert.Equal("password-reset-request", GetPolicyName(method));
    }

    [Fact]
    public void ResetPasswordPost_HasPasswordResetSubmitPolicy()
    {
        var method = GetPostMethod(nameof(AccountController.ResetPassword), typeof(ResetPasswordViewModel));
        Assert.Equal("password-reset-submit", GetPolicyName(method));
    }

    [Fact]
    public void ForgotPasswordGet_HasNoRateLimiting()
    {
        var method = ControllerType.GetMethod(
            nameof(AccountController.ForgotPassword),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        Assert.NotNull(method);
        Assert.Null(method!.GetCustomAttribute<EnableRateLimitingAttribute>());
    }

    [Fact]
    public void ResetPasswordGet_HasNoRateLimiting()
    {
        var method = ControllerType.GetMethod(
            nameof(AccountController.ResetPassword),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
            binder: null,
            types: [typeof(string)],
            modifiers: null);

        Assert.NotNull(method);
        Assert.Null(method!.GetCustomAttribute<EnableRateLimitingAttribute>());
    }

    [Fact]
    public void LoginPost_KeepsLoginPolicy()
    {
        var method = GetPostMethod(nameof(AccountController.Login), typeof(LoginViewModel));
        Assert.Equal("login", GetPolicyName(method));
    }

    [Theory]
    [MemberData(nameof(ExpectedPolicyNames))]
    public void Program_RegistersPolicyMatchingControllerAttributes(string policyName)
    {
        var programPath = FindProgramCs();
        var programSource = File.ReadAllText(programPath);

        Assert.Contains($"AddPolicy(\"{policyName}\"", programSource, StringComparison.Ordinal);

        var controllerPolicies = ControllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(m => m.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(policyName, controllerPolicies);
    }

    [Fact]
    public void ControllerRateLimitPolicies_AreAllRegisteredInProgram()
    {
        var programSource = File.ReadAllText(FindProgramCs());

        var controllerPolicies = ControllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(m => m.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal);

        foreach (var policyName in controllerPolicies)
            Assert.Contains($"AddPolicy(\"{policyName}\"", programSource, StringComparison.Ordinal);
    }

    private static string GetPolicyName(MethodInfo method)
    {
        var attribute = method.GetCustomAttribute<EnableRateLimitingAttribute>();
        Assert.NotNull(attribute);
        Assert.False(string.IsNullOrWhiteSpace(attribute!.PolicyName));
        return attribute.PolicyName!;
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

    private static string FindProgramCs()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "OzelYetenekSinavSistemi.Web", "Program.cs");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Program.cs bulunamadı.");
    }
}
