namespace AlenAlex.Api.Features.Guestbook.Shared;

/// <summary>A reaction the owner can put on an entry from Discord.</summary>
/// <param name="Key">Stored in the database and sent to the site, which maps it to its 3D object and effect.</param>
/// <param name="Emoji">Shown on the Discord buttons.</param>
public sealed record GuestbookReaction(string Key, string Emoji, string Label);

/// <summary>
/// Every reaction there is. To add one, add it here and in the site's reaction registry
/// (site/src/app/core/constants/reactions.constants.ts) with its object in site/public/objects/reactions/.
/// Keys are lowercase letters only. Discord shows at most 25 buttons per message.
/// </summary>
public static class GuestbookReactions
{
    public static readonly IReadOnlyList<GuestbookReaction> All =
    [
        new("heart", "❤️", "Heart"),
        new("fire", "🔥", "Fire"),
        new("laugh", "😂", "Laugh"),
        new("clap", "👏", "Clap"),
        new("coffee", "☕", "Coffee"),
        new("bug", "🐛", "Bug"),
        new("hundred", "💯", "Hundred"),
        new("thanks", "🙏", "Thanks"),
        new("eyes", "👀", "Eyes"),
        new("sparkles", "✨", "Sparkles"),
    ];

    private static readonly Dictionary<string, GuestbookReaction> ByKey = All.ToDictionary(r => r.Key, StringComparer.Ordinal);

    public static bool TryGet(string key, out GuestbookReaction reaction) => ByKey.TryGetValue(key, out reaction!);

    public static bool IsKnown(string key) => ByKey.ContainsKey(key);
}
