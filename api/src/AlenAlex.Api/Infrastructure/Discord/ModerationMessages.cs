using AlenAlex.Api.Features.Guestbook.Shared;
using NetCord;
using NetCord.Rest;

namespace AlenAlex.Api.Infrastructure.Discord;

public static class ModerationMessages
{
    public const string ButtonPrefix = "gb";

    public static readonly Color Pending = new(0xFFA500);
    public static readonly Color Accepted = new(0x2ECC71);
    public static readonly Color Rejected = new(0xE74C3C);

    public enum ButtonAction { Approve, Reject }

    private const string ReactPrefix = ButtonPrefix + ":react:";
    private const int ButtonsPerRow = 5;

    public static MessageProperties NewEntry(GuestbookEntry entry, string ipHash) => new()
    {
        Embeds =
        [
            new EmbedProperties
            {
                Title = $"New guestbook entry #{entry.Seq}",
                Color = Pending,
                Fields =
                [
                    new EmbedFieldProperties { Name = "Name", Value = entry.Name },
                    new EmbedFieldProperties { Name = "Message", Value = entry.Message },
                    // Enough to spot repeat posters.
                    new EmbedFieldProperties { Name = "IP hash", Value = ipHash[..Math.Min(12, ipHash.Length)], Inline = true },
                    new EmbedFieldProperties { Name = "Entry ID", Value = entry.Id, Inline = true },
                ],
                Footer = new EmbedFooterProperties { Text = "Use the buttons, or /guestbook-accept and /guestbook-reject with this ID. Reactions show up once it's approved." },
            },
        ],
        Components =
        [
            new ActionRowProperties
            {
                new ButtonProperties($"{ButtonPrefix}:approve:{entry.Id}", "Approve", ButtonStyle.Success),
                new ButtonProperties($"{ButtonPrefix}:reject:{entry.Id}", "Reject", ButtonStyle.Danger),
            },
        ],
    };

    /// <summary>
    /// One toggle button per reaction, five to a row. Reactions already on the entry are highlighted.
    /// Button ids are <c>gb:react:&lt;reaction&gt;:&lt;entry id&gt;</c>.
    /// </summary>
    public static List<ActionRowProperties> ReactionRows(string entryId, IReadOnlyCollection<string> active) =>
        GuestbookReactions.All
            .Select(r => new ButtonProperties($"{ReactPrefix}{r.Key}:{entryId}", r.Label, EmojiProperties.Standard(r.Emoji),
                active.Contains(r.Key) ? ButtonStyle.Primary : ButtonStyle.Secondary))
            .Chunk(ButtonsPerRow)
            .Select(row => new ActionRowProperties(row))
            .ToList();

    /// <summary>Parses <c>gb:react:&lt;reaction&gt;:&lt;id&gt;</c>; the id may contain colons.</summary>
    public static bool TryParseReactionButtonId(string customId, out string reaction, out string entryId)
    {
        reaction = "";
        entryId = "";
        if (!customId.StartsWith(ReactPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = customId[ReactPrefix.Length..].Split(':', 2);
        if (parts.Length != 2 || !GuestbookReactions.IsKnown(parts[0]) || parts[1].Length == 0)
        {
            return false;
        }
        (reaction, entryId) = (parts[0], parts[1]);
        return true;
    }

    /// <summary>"❤️ 🔥", or "none" when there are no reactions.</summary>
    public static string Describe(IReadOnlyCollection<string> reactions)
    {
        var emoji = GuestbookReactions.All.Where(r => reactions.Contains(r.Key)).Select(r => r.Emoji).ToList();
        return emoji.Count > 0 ? string.Join(' ', emoji) : "none";
    }

    /// <summary>Parses <c>gb:approve:&lt;id&gt;</c> / <c>gb:reject:&lt;id&gt;</c>; the id may contain colons.</summary>
    public static bool TryParseButtonId(string customId, out ButtonAction action, out string entryId)
    {
        action = default;
        entryId = "";
        var parts = customId.Split(':', 3);
        if (parts.Length != 3 || parts[0] != ButtonPrefix)
        {
            return false;
        }

        switch (parts[1])
        {
            case "approve": action = ButtonAction.Approve; break;
            case "reject": action = ButtonAction.Reject; break;
            default: return false;
        }
        entryId = parts[2];
        return true;
    }

    public static IReadOnlyList<ApplicationCommandProperties> SlashCommands() =>
    [
        new SlashCommandProperties("guestbook-accept", "Approve a pending guestbook entry")
        {
            Options = [new ApplicationCommandOptionProperties(ApplicationCommandOptionType.String, "id", "The entry ID") { Required = true }],
        },
        new SlashCommandProperties("guestbook-reject", "Reject a pending guestbook entry")
        {
            Options =
            [
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.String, "id", "The entry ID") { Required = true },
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.String, "reason", "Reason for rejection"),
            ],
        },
        new SlashCommandProperties("guestbook-pending", "List pending guestbook entries"),
        new SlashCommandProperties("guestbook-react", "React to an accepted guestbook entry")
        {
            Options =
            [
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.String, "id", "The entry ID") { Required = true },
                new ApplicationCommandOptionProperties(ApplicationCommandOptionType.String, "reaction", "Toggle this reaction (leave empty to get the buttons)")
                {
                    Choices = GuestbookReactions.All.Select(r => new ApplicationCommandOptionChoiceProperties($"{r.Emoji} {r.Label}", r.Key)).ToList(),
                },
            ],
        },
    ];
}
