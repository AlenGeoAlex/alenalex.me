CREATE TABLE guestbook_entries (
                                   id         TEXT PRIMARY KEY,
                                   name       TEXT NOT NULL,
                                   message    TEXT NOT NULL,
                                   status     TEXT NOT NULL DEFAULT 'pending',
                                   likes      INTEGER NOT NULL DEFAULT 0,
                                   ip_hash    TEXT NOT NULL,
                                   created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE guestbook_likes (
                                 entry_id   TEXT NOT NULL REFERENCES guestbook_entries(id) ON DELETE CASCADE,
                                 ip_hash    TEXT NOT NULL,
                                 created_at TEXT NOT NULL DEFAULT (datetime('now')),
                                 PRIMARY KEY (entry_id, ip_hash)
);