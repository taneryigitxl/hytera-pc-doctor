namespace PCDoctor.Core.Models.Results;

public sealed class OperationResult<T>
{
    public bool Succeeded { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }
    public string? TechnicalDetails { get; }
    public IReadOnlyList<string> Warnings { get; }

    private OperationResult(bool succeeded, T? value, string? errorMessage, string? technicalDetails, IReadOnlyList<string>? warnings)
    {
        Succeeded = succeeded;
        Value = value;
        ErrorMessage = errorMessage;
        TechnicalDetails = technicalDetails;
        Warnings = warnings ?? [];
    }

    public static OperationResult<T> Ok(T value, IReadOnlyList<string>? warnings = null)
        => new(true, value, null, null, warnings);

    public static OperationResult<T> Fail(string errorMessage, string? technicalDetails = null)
        => new(false, default, errorMessage, technicalDetails, null);
}
