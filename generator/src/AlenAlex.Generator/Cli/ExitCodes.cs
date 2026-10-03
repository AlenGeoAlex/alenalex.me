namespace AlenAlex.Generator.Cli;

internal static class ExitCodes
{
    public const int Success = 0;

    /// <summary>Validation errors or failed uploads.</summary>
    public const int Failure = 1;

    /// <summary>Bad usage or configuration (missing posts dir, missing R2 env vars).</summary>
    public const int Configuration = 2;
}
