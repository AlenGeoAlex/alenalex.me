using AlenAlex.Generator.Output;
using AlenAlex.Generator.Validation;

namespace AlenAlex.Generator.Cli;

internal static class ValidateCommand
{
    private static readonly string DiagnosticIndent = new(' ', ConsoleOutput.LabelWidth + 2);

    public static int Run(string postsDirectory, ConsoleOutput output)
    {
        output.Line($"Validating posts in {output.DisplayPath(postsDirectory)}");
        var report = PostValidator.ValidateAll(postsDirectory);
        Print(report, output);
        return report.HasErrors ? ExitCodes.Failure : ExitCodes.Success;
    }

    public static void Print(ValidationReport report, ConsoleOutput output)
    {
        var nameWidth = report.Posts.Select(p => p.Folder.Name.Length)
            .Concat(report.Series.Select(s => s.Folder.Name.Length))
            .Concat(report.Series.SelectMany(s => s.Parts).Select(p => p.Folder.Name.Length + 2))
            .DefaultIfEmpty(0).Max() + 2;

        // Posts and series interleaved by folder name, as on disk.
        var entries = report.Posts.Select(p => (p.Folder.Name, Post: (PostReport?)p, Series: (SeriesReport?)null))
            .Concat(report.Series.Select(s => (s.Folder.Name, Post: (PostReport?)null, Series: (SeriesReport?)s)))
            .OrderBy(e => e.Name, StringComparer.Ordinal);

        foreach (var (_, post, series) in entries)
        {
            if (post is not null) PrintPost(post, output, nameWidth);
            else PrintSeries(series!, output, nameWidth);
        }

        var parts = report.Series.SelectMany(s => s.Parts).ToList();
        var standalone = report.Posts.Count(p => !p.HasErrors && !p.IsDraft);
        var drafts = report.Posts.Count(p => !p.HasErrors && p.IsDraft) + parts.Count(p => !p.HasErrors && p.IsDraft);
        var broken = report.Posts.Count(p => p.HasErrors) + parts.Count(p => p.HasErrors);

        var summary = $"{report.Posts.Count} post(s), {report.Series.Count} series with {parts.Count} part(s): "
                      + $"{standalone + parts.Count(p => !p.HasErrors && !p.IsDraft)} ok, {drafts} draft(s), {broken} with errors; "
                      + $"{report.ErrorCount} error(s), {report.WarningCount} warning(s)";
        if (report.NotReady.Count > 0)
            summary += $"; {report.NotReady.Count} folder(s) without .meta skipped";

        output.Line(output.Paint(summary, report.HasErrors ? Tone.Error : Tone.Success));
    }

    private static void PrintPost(PostReport post, ConsoleOutput output, int nameWidth)
    {
        var label = post switch
        {
            { HasErrors: true } => output.Label("error", Tone.Error),
            { IsDraft: true } => output.Label("draft", Tone.Info),
            _ => output.Label("ok", Tone.Success),
        };

        output.Line($"  {label}{post.Folder.Name.PadRight(nameWidth)}{post.Url}  {output.Paint(Count(post.Assets.Count, "asset"), Tone.Muted)}");
        PrintDiagnostics(post.Diagnostics, output);
    }

    private static void PrintSeries(SeriesReport series, ConsoleOutput output, int nameWidth)
    {
        var tone = series.HasErrors ? Tone.Error : series.IsDraft ? Tone.Info : Tone.Success;
        var label = output.Label(series.IsDraft ? "draft series" : "series", tone);
        var details = Count(series.Parts.Count, "part") + (series.Assets.Count > 0 ? $", {Count(series.Assets.Count, "asset")}" : string.Empty);
        output.Line($"  {label}{series.Folder.Name.PadRight(nameWidth)}{series.Url}  {output.Paint(details, Tone.Muted)}");
        PrintDiagnostics(series.Diagnostics, output);

        foreach (var part in series.Parts)
        {
            var partTone = part.HasErrors ? Tone.Error : part.IsDraft ? Tone.Info : Tone.Success;
            var partLabel = output.Label(part.PartNumber is { } n ? $"part {n}" : "part", partTone);
            var state = part.HasErrors ? ", errors" : part.IsDraft ? ", draft" : string.Empty;
            output.Line($"    {partLabel}{part.Folder.Name.PadRight(nameWidth - 2)}{part.Url}  "
                        + output.Paint(Count(part.Assets.Count, "asset") + state, Tone.Muted));
            PrintDiagnostics(part.Diagnostics, output, extraIndent: "  ");
        }
    }

    private static void PrintDiagnostics(IEnumerable<Diagnostic> diagnostics, ConsoleOutput output, string extraIndent = "")
    {
        foreach (var diagnostic in diagnostics.OrderByDescending(d => d.IsError).ThenBy(d => d.FilePath).ThenBy(d => d.Line))
            output.Diagnostic(diagnostic, indent: extraIndent + DiagnosticIndent);
    }

    internal static string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";
}
