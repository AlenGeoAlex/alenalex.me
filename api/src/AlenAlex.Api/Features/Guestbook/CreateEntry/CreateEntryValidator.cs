using AlenAlex.Api.Features.Guestbook.Shared;

namespace AlenAlex.Api.Features.Guestbook.CreateEntry;

public static class CreateEntryValidator
{
    public const int NameMin = 1;
    public const int NameMax = 40;
    public const int MessageMin = 3;
    public const int MessageMax = 200;

    /// <summary>
    /// Lengths are counted in runes rather than UTF-16 units, so emoji and non-Latin scripts
    /// aren't penalised. The site's form uses the same limits.
    /// </summary>
    /// <exception cref="GuestbookValidationException">A value is out of range.</exception>
    public static (string Name, string Message) Validate(string? name, string? message)
    {
        var trimmedName = (name ?? "").Trim();
        var trimmedMessage = (message ?? "").Trim();

        var nameLength = trimmedName.EnumerateRunes().Count();
        if (nameLength is < NameMin or > NameMax)
        {
            throw new GuestbookValidationException($"name must be {NameMin}–{NameMax} characters");
        }

        var messageLength = trimmedMessage.EnumerateRunes().Count();
        if (messageLength is < MessageMin or > MessageMax)
        {
            throw new GuestbookValidationException($"message must be {MessageMin}–{MessageMax} characters");
        }

        return (trimmedName, trimmedMessage);
    }
}
