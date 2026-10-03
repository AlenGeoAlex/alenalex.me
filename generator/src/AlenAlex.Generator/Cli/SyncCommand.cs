using AlenAlex.Generator.Output;
using Microsoft.Extensions.Configuration;
using AlenAlex.Generator.Posts;
using AlenAlex.Generator.Storage;
using AlenAlex.Generator.Sync;
using AlenAlex.Generator.Validation;

namespace AlenAlex.Generator.Cli;

internal sealed record SyncSettings(string PostsDirectory, ChangedDirs Changed, bool DryRun, bool Offline);

internal static class SyncCommand
{
    public static async Task<int> RunAsync(
        SyncSettings settings, IConfiguration configuration, ConsoleOutput output, CancellationToken cancellationToken)
    {
        output.Line($"Validating posts in {output.DisplayPath(settings.PostsDirectory)}");
        var report = PostValidator.ValidateAll(settings.PostsDirectory);
        ValidateCommand.Print(report, output);
        if (report.HasErrors)
        {
            output.Error("validation failed; nothing was uploaded");
            return ExitCodes.Failure;
        }

        var options = R2Options.FromConfiguration(configuration, out var missing);
        var mode = settings switch
        {
            { Offline: true } => SyncMode.Offline,
            { DryRun: true } => options is null ? SyncMode.Offline : SyncMode.DryRun,
            _ => SyncMode.Live,
        };

        if (options is null && mode != SyncMode.Offline)
        {
            output.Error($"missing setting(s): {string.Join(", ", missing)}; set them in appsettings.Development.json or as R2__* environment variables (or use --dry-run / --offline)");
            return ExitCodes.Configuration;
        }

        if (settings.DryRun && !settings.Offline && options is null)
            output.Notice($"R2 settings not set ({string.Join(", ", missing)}); dry run will not contact R2.");

        using var store = mode == SyncMode.Offline ? null : S3ObjectStore.ForR2(options!);
        var syncer = new AssetSyncService(store, mode);

        var target = options is null ? "R2" : $"r2://{options.Bucket}";
        var modeText = mode switch
        {
            SyncMode.DryRun => " (dry run: read-only, nothing is uploaded)",
            SyncMode.Offline => " (offline dry run: no network, remote state unknown)",
            _ => string.Empty,
        };
        output.Line();
        output.Line($"Syncing assets to {target}/{AssetKey.Prefix}/ for posts: {settings.Changed}{modeText}");

        var tally = new Dictionary<SyncOutcome, int>();
        var postsByName = report.Posts.ToDictionary(p => p.Folder.Name, StringComparer.Ordinal);
        var seriesByName = report.Series.ToDictionary(s => s.Folder.Name, StringComparer.Ordinal);
        var nameWidth = Math.Max(16, postsByName.Keys.Concat(seriesByName.Keys).Select(k => k.Length).DefaultIfEmpty(0).Max() + 2);

        async Task SyncFolderAsync(IAssetFolder folder, IReadOnlyList<string> assets)
        {
            foreach (var result in await syncer.SyncAssetsAsync(folder, assets, cancellationToken))
            {
                tally[result.Outcome] = tally.GetValueOrDefault(result.Outcome) + 1;
                PrintResult(result, options?.PublicUrlBase, output);
            }
        }

        void Skip(string name, string reason, string indent = "  ") =>
            output.Line($"{indent}{output.Label("skip", Tone.Muted)}{name.PadRight(nameWidth)}{output.Paint(reason, Tone.Muted)}");

        foreach (var name in SelectFolders(settings, report))
        {
            if (syncer.Aborted)
            {
                Skip(name, "aborted after a fatal storage error");
                continue;
            }

            if (postsByName.TryGetValue(name, out var post))
            {
                var draft = post.IsDraft ? ", draft" : string.Empty;
                output.Line($"  {output.Label("post", Tone.Info)}{name.PadRight(nameWidth)}{output.Paint(ValidateCommand.Count(post.Assets.Count, "asset") + draft, Tone.Muted)}");
                await SyncFolderAsync(post.Folder, post.Assets);
            }
            else if (seriesByName.TryGetValue(name, out var series))
            {
                var draft = series.IsDraft ? ", draft" : string.Empty;
                var details = $"{ValidateCommand.Count(series.Parts.Count, "part")}, {ValidateCommand.Count(series.Assets.Count, "series asset")}{draft}";
                output.Line($"  {output.Label("series", Tone.Info)}{name.PadRight(nameWidth)}{output.Paint(details, Tone.Muted)}");
                await SyncFolderAsync(series.Folder, series.Assets);

                foreach (var part in series.Parts)
                {
                    if (syncer.Aborted) break;
                    var partDraft = part.IsDraft ? ", draft" : string.Empty;
                    var label = part.PartNumber is { } n ? $"part {n}" : "part";
                    output.Line($"    {output.Label(label, Tone.Info)}{part.Folder.Name.PadRight(nameWidth - 2)}"
                                + output.Paint(ValidateCommand.Count(part.Assets.Count, "asset") + partDraft, Tone.Muted));
                    await SyncFolderAsync(part.Folder, part.Assets);
                }
            }
            else
            {
                Skip(name, Directory.Exists(Path.Combine(settings.PostsDirectory, name))
                    ? "not ready (no .meta or series.meta)"
                    : "folder not found (deleted or renamed?)");
            }
        }

        int Count(SyncOutcome o) => tally.GetValueOrDefault(o);
        var failed = Count(SyncOutcome.Error) + Count(SyncOutcome.Skipped);
        var summary = mode switch
        {
            SyncMode.Live => $"{Count(SyncOutcome.Uploaded)} uploaded, {Count(SyncOutcome.Updated)} updated, "
                             + $"{Count(SyncOutcome.Unchanged)} unchanged, {failed} failed",
            SyncMode.DryRun => $"{Count(SyncOutcome.WouldUpload)} would upload, {Count(SyncOutcome.WouldUpdate)} would update, "
                               + $"{Count(SyncOutcome.Unchanged)} unchanged, {failed} failed",
            _ => $"{Count(SyncOutcome.Planned)} file(s) would be uploaded if missing or changed, {failed} failed",
        };
        output.Line(output.Paint(summary, failed > 0 ? Tone.Error : Tone.Success));
        return failed > 0 ? ExitCodes.Failure : ExitCodes.Success;
    }

    /// <summary>
    /// Every ready post and series, or the requested names (which may not exist). Names are top-level
    /// folders, so a change anywhere inside a series selects the whole series.
    /// </summary>
    internal static IEnumerable<string> SelectFolders(SyncSettings settings, ValidationReport report) =>
        settings.Changed.IsAll
            ? report.Posts.Select(p => p.Folder.Name).Concat(report.Series.Select(s => s.Folder.Name)).Order(StringComparer.Ordinal)
            : settings.Changed.Folders!.Order(StringComparer.Ordinal);

    private static void PrintResult(AssetSyncResult result, string? publicUrlBase, ConsoleOutput output)
    {
        var (label, tone) = result.Outcome switch
        {
            SyncOutcome.Uploaded => ("uploaded", Tone.Success),
            SyncOutcome.Updated => ("updated", Tone.Success),
            SyncOutcome.Unchanged => ("unchanged", Tone.Muted),
            SyncOutcome.WouldUpload => ("would upload", Tone.Warning),
            SyncOutcome.WouldUpdate => ("would update", Tone.Warning),
            SyncOutcome.Planned => ("would sync", Tone.Warning),
            SyncOutcome.Skipped => ("skipped", Tone.Error),
            _ => ("error", Tone.Error),
        };

        var size = result.Outcome == SyncOutcome.Error ? string.Empty : $"  {FormatSize(result.Asset.Length)}";
        var detail = result.Detail is null ? string.Empty : $"  ({result.Detail})";
        var url = publicUrlBase is not null && result.Outcome is SyncOutcome.Uploaded or SyncOutcome.Updated
            ? $"  {publicUrlBase}/{result.Asset.Key}"
            : string.Empty;

        output.Line($"    {output.Label(label, tone)}{result.Asset.Key}{output.Paint(size + detail, Tone.Muted)}{url}");
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes / (1024.0 * 1024):0.0} MB",
    };
}
