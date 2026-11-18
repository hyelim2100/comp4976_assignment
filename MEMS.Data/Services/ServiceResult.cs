namespace MEMS.Data.Services;

public enum ServiceStatus
{
    Ok,
    NotFound,
    Forbidden,
    Invalid,
}

// Outcome of a service call. The Admin app shows Errors next to form fields; the API maps the status
// to 404 / 403 / 400 ValidationProblemDetails.
public class ServiceResult
{
    protected ServiceResult(ServiceStatus status, IReadOnlyDictionary<string, string[]>? errors = null, string? message = null)
    {
        Status = status;
        Errors = errors ?? new Dictionary<string, string[]>();
        Message = message;
    }

    public ServiceStatus Status { get; }

    public bool Succeeded => Status == ServiceStatus.Ok;

    // Field name ("" for the whole form) -> messages.
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public string? Message { get; }

    public static ServiceResult Ok() => new(ServiceStatus.Ok);

    public static ServiceResult NotFound(string message = "The requested item was not found.") => new(ServiceStatus.NotFound, message: message);

    public static ServiceResult Forbidden(string message = "You do not have permission to perform this action.") => new(ServiceStatus.Forbidden, message: message);

    public static ServiceResult Invalid(string field, string error) => new(ServiceStatus.Invalid, new Dictionary<string, string[]> { [field] = [error] }, error);

    public static ServiceResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(ServiceStatus.Invalid, errors, errors.Values.SelectMany(e => e).FirstOrDefault());
}

public sealed class ServiceResult<T> : ServiceResult
{
    private ServiceResult(ServiceStatus status, T? value, IReadOnlyDictionary<string, string[]>? errors, string? message)
        : base(status, errors, message)
    {
        Value = value;
    }

    public T? Value { get; }

    public static ServiceResult<T> Ok(T value) => new(ServiceStatus.Ok, value, null, null);

    // Converts a failed non-generic result (same status, errors and message).
    public static ServiceResult<T> From(ServiceResult failure) => new(failure.Status, default, failure.Errors, failure.Message);

    public static implicit operator ServiceResult<T>(T value) => Ok(value);
}

// An uploaded image (Blazor InputFile or API multipart). Validated with ImageRules before it is stored.
public sealed record ImageUpload(string FileName, byte[] Content);
