namespace AlenAlex.Generator.Validation;

internal enum Severity
{
    Warning,
    Error,
}

/// <summary>Printed compiler-style: <c>blogs/my-post/.meta:3: error: ...</c>.</summary>
/// <param name="FilePath">Absolute path.</param>
/// <param name="Line">1-based, or <c>null</c> when the finding is about the whole file.</param>
internal sealed record Diagnostic(Severity Severity, string FilePath, int? Line, string Message)
{
    public static Diagnostic Error(string filePath, string message, int? line = null) =>
        new(Severity.Error, filePath, line, message);

    public static Diagnostic Warning(string filePath, string message, int? line = null) =>
        new(Severity.Warning, filePath, line, message);

    public bool IsError => Severity == Severity.Error;
}
