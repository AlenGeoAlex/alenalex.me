namespace AlenAlex.Api.Features.Status.Shared;

/// <summary>Written by the Discord gateway, the listening recorder and the homelab poller. Snapshots are immutable and swapped atomically.</summary>
public sealed class LiveStatusStore
{
    private PresenceSnapshot _presence = PresenceSnapshot.Disconnected;
    private IReadOnlyList<ServiceStatus> _homelab = [];
    private IReadOnlyList<RecentTrack> _recentTracks = [];

    public PresenceSnapshot Presence => Volatile.Read(ref _presence);

    public IReadOnlyList<ServiceStatus> Homelab => Volatile.Read(ref _homelab);

    public void SetPresence(PresenceSnapshot snapshot) => Volatile.Write(ref _presence, snapshot);

    public void UpdatePresence(Func<PresenceSnapshot, PresenceSnapshot> update)
    {
        PresenceSnapshot current, next;
        do
        {
            current = Volatile.Read(ref _presence);
            next = update(current);
        }
        while (Interlocked.CompareExchange(ref _presence, next, current) != current);
    }

    public void SetHomelab(IReadOnlyList<ServiceStatus> services) => Volatile.Write(ref _homelab, services);

    /// <summary>The last few Spotify tracks, newest first. Kept here so /api/status never reads the database.</summary>
    public IReadOnlyList<RecentTrack> RecentTracks => Volatile.Read(ref _recentTracks);

    public void SetRecentTracks(IReadOnlyList<RecentTrack> tracks) => Volatile.Write(ref _recentTracks, tracks);
}
