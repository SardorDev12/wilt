using System;
using System.Collections.Generic;

namespace Wilt.Models;

public class CompletedSession
{
    public DateTime CompletedAtUtc { get; set; }
    public Phase Phase { get; set; }
    public int DurationMinutes { get; set; }
    public bool FinishedEarly { get; set; }
}

/// <summary>Local-only session history, persisted as JSON in %AppData%\Wilt\history.json.</summary>
public class HistoryData
{
    public List<CompletedSession> Sessions { get; set; } = new();
}
