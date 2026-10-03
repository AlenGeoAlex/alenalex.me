using System.CommandLine;
using AlenAlex.Generator.Cli;
using AlenAlex.Generator.Configuration;
using Microsoft.Extensions.Configuration;
using AlenAlex.Generator.Output;
using AlenAlex.Generator.Sync;

var output = ConsoleOutput.CreateDefault();
var configuration = GeneratorConfiguration.Build();
var posts = configuration.GetSection(PostsOptions.SectionName).Get<PostsOptions>() ?? new PostsOptions();

var postsOption = new Option<string?>("--posts")
{
    Description = "Path to the blogs/ folder. Defaults to the Posts:Directory setting, else the nearest blogs/ above the working directory.",
    Recursive = true,
};

var changedOption = new Option<string?>("--changed")
{
    Description = "Comma-separated post folder names to sync; '*' or empty means all. Defaults to the Posts:Changed setting.",
};

var dryRunOption = new Option<bool>("--dry-run")
{
    Description = "Show what would be uploaded without uploading. Uses read-only HEAD requests when the R2 settings are present, otherwise no network at all.",
};

var offlineOption = new Option<bool>("--offline")
{
    Description = "Dry run without any network access: lists every asset that would be synced.",
};

var validate = new Command("validate", "Validate every post folder (.meta, dates, tags, asset references, unique slugs).");
validate.SetAction(parse =>
{
    var postsDirectory = ResolvePostsDirectory(parse.GetValue(postsOption));
    return postsDirectory is null ? ExitCodes.Configuration : ValidateCommand.Run(postsDirectory, output);
});

var sync = new Command("sync", "Validate, then upload post assets to R2 at assets/hotlink-ok/<folder>/<file>, skipping unchanged files.")
{
    changedOption,
    dryRunOption,
    offlineOption,
};
sync.SetAction(async (parse, cancellationToken) =>
{
    var postsDirectory = ResolvePostsDirectory(parse.GetValue(postsOption));
    if (postsDirectory is null) return ExitCodes.Configuration;

    var changed = ChangedDirs.Parse(parse.GetValue(changedOption) ?? posts.Changed);
    var offline = parse.GetValue(offlineOption);
    var settings = new SyncSettings(postsDirectory, changed, DryRun: parse.GetValue(dryRunOption) || offline, Offline: offline);
    return await SyncCommand.RunAsync(settings, configuration, output, cancellationToken);
});

var root = new RootCommand("alenalex.me blog generator: validates posts in blogs/ and syncs their assets to Cloudflare R2.")
{
    postsOption,
    validate,
    sync,
};

return await root.Parse(args).InvokeAsync();

string? ResolvePostsDirectory(string? fromOption)
{
    var resolved = PostsDirectory.Resolve(fromOption, posts.Directory, Directory.GetCurrentDirectory(), AppContext.BaseDirectory);
    if (resolved is not null && Directory.Exists(resolved)) return resolved;

    output.Error(resolved is null
        ? "could not find a blogs/ folder; pass --posts or set Posts:Directory"
        : $"posts directory does not exist: {resolved}");
    return null;
}
