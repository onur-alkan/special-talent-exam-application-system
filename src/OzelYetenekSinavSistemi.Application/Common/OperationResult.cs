namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// İş servislerinden dönen standart sonuç tipi (payload'suz).
/// </summary>
public class OperationResult
{
    public bool Success { get; protected set; }
    public string? ErrorMessage { get; protected set; }
    public IReadOnlyList<string> ValidationErrors { get; protected set; } = Array.Empty<string>();

    public static OperationResult Ok() => new() { Success = true };

    public static OperationResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };

    public static OperationResult Invalid(IReadOnlyList<string> errors) =>
        new() { Success = false, ErrorMessage = errors.Count > 0 ? errors[0] : "Bilgilerinizi kontrol ediniz.", ValidationErrors = errors };
}

/// <summary>
/// İş servislerinden dönen, veri taşıyan standart sonuç tipi.
/// </summary>
public sealed class OperationResult<T> : OperationResult
{
    public T? Data { get; private set; }

    public static OperationResult<T> Ok(T data) => new() { Success = true, Data = data };

    public static new OperationResult<T> Fail(string message) =>
        new() { Success = false, ErrorMessage = message };

    public static new OperationResult<T> Invalid(IReadOnlyList<string> errors) =>
        new() { Success = false, ErrorMessage = errors.Count > 0 ? errors[0] : "Bilgilerinizi kontrol ediniz.", ValidationErrors = errors };
}
