using System.Collections.Concurrent;
using AlenAlex.Api.Features.Guestbook.Shared;

namespace AlenAlex.Api.IntegrationTests.Support;

/// <summary>Records queued entries, since the Discord gateway can't be emulated.</summary>
public sealed class FakeModerationNotifier : IGuestbookModerationNotifier
{
    public ConcurrentQueue<(GuestbookEntry Entry, string IpHash)> Queued { get; } = new();

    public void QueueForReview(GuestbookEntry entry, string ipHash) => Queued.Enqueue((entry, ipHash));
}
