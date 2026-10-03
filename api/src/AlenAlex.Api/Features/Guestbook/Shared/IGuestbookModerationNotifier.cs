namespace AlenAlex.Api.Features.Guestbook.Shared;

public interface IGuestbookModerationNotifier
{
    /// <summary>
    /// Returns immediately so a Discord outage can't fail or slow down the visitor's request.
    /// Failures are logged.
    /// </summary>
    void QueueForReview(GuestbookEntry entry, string ipHash);
}
