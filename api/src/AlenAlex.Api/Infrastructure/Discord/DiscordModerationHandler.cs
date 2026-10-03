using AlenAlex.Api.Features.Guestbook.ListPendingEntries;
using AlenAlex.Api.Features.Guestbook.ModerateEntry;
using AlenAlex.Api.Features.Guestbook.Shared;
using AlenAlex.Api.Options;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;

namespace AlenAlex.Api.Infrastructure.Discord;

/// <summary>
/// Slash commands and Approve/Reject buttons. Only <c>Discord:UserId</c> may moderate
/// (anyone, if it's unset).
/// </summary>
public sealed class DiscordModerationHandler(
    ModerateEntryHandler moderate,
    ListPendingEntriesHandler listPending,
    IOptions<DiscordOptions> options,
    ILogger<DiscordModerationHandler> logger)
{
    private const string NotAllowed = "You are not allowed to moderate the guestbook.";

    public async Task HandleAsync(Interaction interaction)
    {
        try
        {
            switch (interaction)
            {
                case SlashCommandInteraction command:
                    await OnCommandAsync(command);
                    break;
                case ButtonInteraction button:
                    await OnButtonAsync(button);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle Discord interaction");
        }
    }

    private bool MayModerate(User user) => options.Value.UserId is not { } owner || owner == user.Id;

    private async Task OnCommandAsync(SlashCommandInteraction command)
    {
        if (!MayModerate(command.User))
        {
            await ReplyAsync(command, NotAllowed);
            return;
        }

        switch (command.Data.Name)
        {
            case "guestbook-accept":
            {
                if (StringOption(command, "id") is not { } id)
                {
                    await ReplyAsync(command, "Missing `id` option.");
                    return;
                }
                await ReplyAsync(command, (await ModerateAsync(id, "approved", () => moderate.ApproveAsync(id))).Text);
                break;
            }
            case "guestbook-reject":
            {
                if (StringOption(command, "id") is not { } id)
                {
                    await ReplyAsync(command, "Missing `id` option.");
                    return;
                }
                var reason = StringOption(command, "reason");
                await ReplyAsync(command, (await ModerateAsync(id, "rejected", () => moderate.RejectAsync(id, reason))).Text);
                break;
            }
            case "guestbook-pending":
                await ListPendingAsync(command);
                break;
            default:
                logger.LogWarning("Unknown command: {Command}", command.Data.Name);
                break;
        }
    }

    private async Task ListPendingAsync(SlashCommandInteraction command)
    {
        IReadOnlyList<GuestbookEntry> entries;
        try
        {
            entries = await listPending.HandleAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Listing pending entries failed");
            await ReplyAsync(command, "Failed to list entries.");
            return;
        }

        if (entries.Count == 0)
        {
            await ReplyAsync(command, "No pending entries.");
            return;
        }

        // Discord allows at most 25 fields per embed.
        var embed = new EmbedProperties
        {
            Title = $"Pending entries ({entries.Count})",
            Fields = entries.Take(25).Select(e => new EmbedFieldProperties { Name = $"{e.Id} — {e.Name}", Value = e.Message }).ToList(),
            Footer = new EmbedFooterProperties { Text = "Approve or reject with /guestbook-accept or /guestbook-reject" },
        };
        await command.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties { Embeds = [embed] }));
    }

    // Rewrites the embed to show the outcome and removes the buttons.
    private async Task OnButtonAsync(ButtonInteraction button)
    {
        if (!ModerationMessages.TryParseButtonId(button.Data.CustomId, out var action, out var id))
        {
            return;
        }

        if (!MayModerate(button.User))
        {
            await button.SendResponseAsync(InteractionCallback.Message(
                new InteractionMessageProperties { Content = NotAllowed, Flags = MessageFlags.Ephemeral }));
            return;
        }

        var (outcome, color) = action == ModerationMessages.ButtonAction.Approve
            ? (await ModerateAsync(id, "approved", () => moderate.ApproveAsync(id)), ModerationMessages.Accepted)
            : (await ModerateAsync(id, "rejected", () => moderate.RejectAsync(id, null)), ModerationMessages.Rejected);

        var original = button.Message.Embeds.FirstOrDefault();
        await button.SendResponseAsync(InteractionCallback.ModifyMessage(message =>
        {
            message.Components = [];
            if (original is null)
            {
                message.Content = outcome.Text;
                return;
            }

            message.Embeds =
            [
                new EmbedProperties
                {
                    Title = original.Title,
                    Description = original.Description,
                    Color = outcome.Succeeded ? color : original.Color ?? default,
                    Fields = original.Fields.Select(f => new EmbedFieldProperties { Name = f.Name, Value = f.Value, Inline = f.Inline }).ToList(),
                    Footer = new EmbedFooterProperties { Text = $"{outcome.Text} by {button.User.Username}" },
                },
            ];
        }));
    }

    private readonly record struct Outcome(bool Succeeded, string Text);

    private async Task<Outcome> ModerateAsync(string id, string verb, Func<Task> moderate)
    {
        try
        {
            await moderate();
            return new Outcome(true, $"Entry `{id}` {verb}");
        }
        catch (GuestbookEntryNotFoundException)
        {
            return new Outcome(false, $"Entry `{id}` not found or no longer pending");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Moderation of {EntryId} failed", id);
            return new Outcome(false, $"Failed to update entry `{id}`");
        }
    }

    private static string? StringOption(SlashCommandInteraction command, string name) =>
        command.Data.Options.FirstOrDefault(o => o.Name == name)?.Value;

    private async Task ReplyAsync(SlashCommandInteraction command, string message)
    {
        try
        {
            await command.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties { Content = message }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to respond to interaction");
        }
    }
}
