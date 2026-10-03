using System.Security.Cryptography;

namespace AlenAlex.Api.Features.Guestbook.CreateEntry;

/// <summary>
/// nanoid-style ids (21 URL-safe characters), the format of the ids already in the database.
/// </summary>
public static class NanoId
{
    private const string Alphabet = "_-0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static string New(int length = 21) => RandomNumberGenerator.GetString(Alphabet, length);
}
