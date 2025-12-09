namespace ListeningBrain.Core.Models;

/// <summary>
/// Represents a complete performance recording to be evaluated against a score.
/// Contains all played notes, pedal events, and metadata.
/// </summary>
public class Performance
{
    /// <summary>
    /// All notes played during the performance, ordered by start time.
    /// </summary>
    public IReadOnlyList<PerformanceNote> Notes { get; init; } = [];
    
    /// <summary>
    /// Sustain pedal events during the performance.
    /// </summary>
    public IReadOnlyList<PedalEvent> SustainPedalEvents { get; init; } = [];
    
    /// <summary>
    /// Soft pedal (una corda) events.
    /// </summary>
    public IReadOnlyList<PedalEvent> SoftPedalEvents { get; init; } = [];
    
    /// <summary>
    /// Sostenuto pedal events.
    /// </summary>
    public IReadOnlyList<PedalEvent> SostenutoPedalEvents { get; init; } = [];
    
    /// <summary>
    /// When the performance recording started.
    /// </summary>
    public DateTime StartTimestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Total duration of the performance in milliseconds.
    /// </summary>
    public double TotalDurationMs => Notes.Count > 0 ? Notes.Max(n => n.EndTimeMs) : 0;
    
    /// <summary>
    /// The score this performance is being compared against (if known).
    /// </summary>
    public Guid? ScoreId { get; init; }
    
    /// <summary>
    /// Student/performer identifier.
    /// </summary>
    public string? PerformerId { get; init; }
    
    /// <summary>
    /// Recording session identifier.
    /// </summary>
    public Guid SessionId { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// Source of the MIDI input (e.g., "Yamaha P-125", "MIDI File").
    /// </summary>
    public string? InputSource { get; init; }
    
    /// <summary>
    /// Was this captured in real-time or loaded from a file?
    /// </summary>
    public bool IsRealTimeCapture { get; init; } = true;
    
    /// <summary>
    /// Detected average tempo (BPM) of the performance.
    /// Calculated after alignment.
    /// </summary>
    public double? DetectedTempo { get; init; }
    
    /// <summary>
    /// Gets notes within a time range.
    /// </summary>
    public IEnumerable<PerformanceNote> GetNotesInTimeRange(double startMs, double endMs)
        => Notes.Where(n => n.StartTimeMs >= startMs && n.StartTimeMs < endMs);
    
    /// <summary>
    /// Gets the sustain pedal state at a specific time.
    /// </summary>
    public bool IsSustainPedalActiveAt(double timeMs)
    {
        var lastEvent = SustainPedalEvents
            .Where(e => e.TimeMs <= timeMs)
            .OrderByDescending(e => e.TimeMs)
            .FirstOrDefault();
            
        return lastEvent?.IsPressed ?? false;
    }
    
    /// <summary>
    /// Calculates note density (notes per second) over the performance.
    /// </summary>
    public double AverageNoteDensity
    {
        get
        {
            if (Notes.Count == 0 || TotalDurationMs <= 0)
                return 0;
            return Notes.Count / (TotalDurationMs / 1000.0);
        }
    }
    
    /// <summary>
    /// Gets the velocity distribution across the performance.
    /// </summary>
    public VelocityDistribution GetVelocityDistribution()
    {
        if (Notes.Count == 0)
            return new VelocityDistribution(0, 0, 0, 0, []);
            
        var velocities = Notes.Select(n => n.Velocity).ToList();
        var distribution = velocities
            .GroupBy(v => v / 16) // Group into 8 buckets
            .ToDictionary(g => g.Key, g => g.Count());
            
        return new VelocityDistribution(
            Min: velocities.Min(),
            Max: velocities.Max(),
            Average: velocities.Average(),
            StdDev: CalculateStdDev(velocities),
            Distribution: distribution
        );
    }
    
    private static double CalculateStdDev(List<int> values)
    {
        if (values.Count <= 1) return 0;
        double avg = values.Average();
        double sumSquares = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumSquares / (values.Count - 1));
    }
}

/// <summary>
/// Represents a pedal event (sustain, soft, or sostenuto).
/// </summary>
public record PedalEvent
{
    /// <summary>
    /// Time in milliseconds when the event occurred.
    /// </summary>
    public required double TimeMs { get; init; }
    
    /// <summary>
    /// Tick position (if available from MIDI file).
    /// </summary>
    public long? Tick { get; init; }
    
    /// <summary>
    /// True = pedal pressed down, False = pedal released.
    /// </summary>
    public required bool IsPressed { get; init; }
    
    /// <summary>
    /// Controller value (0-127). Supports half-pedaling.
    /// </summary>
    public int Value { get; init; } = 127;
    
    /// <summary>
    /// Is this a "half pedal" position (for pianos that support it)?
    /// </summary>
    public bool IsHalfPedal => Value is > 32 and < 96;
}

/// <summary>
/// Velocity distribution statistics.
/// </summary>
public record VelocityDistribution(
    int Min,
    int Max,
    double Average,
    double StdDev,
    Dictionary<int, int> Distribution
);

/// <summary>
/// Builder for constructing Performance objects from real-time input.
/// Thread-safe for concurrent note addition.
/// </summary>
public class PerformanceBuilder
{
    private readonly List<PerformanceNote> _notes = [];
    private readonly List<PedalEvent> _sustainEvents = [];
    private readonly List<PedalEvent> _softPedalEvents = [];
    private readonly List<PedalEvent> _sostenutoEvents = [];
    private readonly object _lock = new();
    private readonly DateTime _startTime;
    private int _noteIndex;
    
    public PerformanceBuilder()
    {
        _startTime = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Adds a note from a MIDI note-on event.
    /// Returns a handle to complete the note when note-off is received.
    /// </summary>
    public NoteHandle AddNoteOn(int pitch, int velocity, double timeMs)
    {
        lock (_lock)
        {
            var note = new PerformanceNote
            {
                Id = Guid.NewGuid(),
                Pitch = pitch,
                Velocity = velocity,
                StartTick = 0, // Not available in real-time
                DurationTicks = 0,
                StartTimeMs = timeMs,
                DurationMs = 0, // Will be updated on note-off
                ReceivedTimestamp = DateTime.UtcNow,
                SequenceIndex = _noteIndex++,
                SustainPedalActive = IsSustainPedalActive()
            };
            
            _notes.Add(note);
            return new NoteHandle(note.Id, this);
        }
    }
    
    /// <summary>
    /// Completes a note when note-off is received.
    /// </summary>
    internal void CompleteNote(Guid noteId, double endTimeMs, int? releaseVelocity)
    {
        lock (_lock)
        {
            var index = _notes.FindIndex(n => n.Id == noteId);
            if (index >= 0)
            {
                var note = _notes[index];
                _notes[index] = note with
                {
                    DurationMs = endTimeMs - note.StartTimeMs,
                    ReleaseVelocity = releaseVelocity
                };
            }
        }
    }
    
    /// <summary>
    /// Adds a sustain pedal event.
    /// </summary>
    public void AddSustainPedal(double timeMs, int value)
    {
        lock (_lock)
        {
            _sustainEvents.Add(new PedalEvent
            {
                TimeMs = timeMs,
                IsPressed = value >= 64,
                Value = value
            });
        }
    }
    
    /// <summary>
    /// Adds a soft pedal event.
    /// </summary>
    public void AddSoftPedal(double timeMs, int value)
    {
        lock (_lock)
        {
            _softPedalEvents.Add(new PedalEvent
            {
                TimeMs = timeMs,
                IsPressed = value >= 64,
                Value = value
            });
        }
    }
    
    private bool IsSustainPedalActive()
    {
        if (_sustainEvents.Count == 0) return false;
        return _sustainEvents[^1].IsPressed;
    }
    
    /// <summary>
    /// Builds the final Performance object.
    /// </summary>
    public Performance Build()
    {
        lock (_lock)
        {
            return new Performance
            {
                Notes = _notes.OrderBy(n => n.StartTimeMs).ToList(),
                SustainPedalEvents = _sustainEvents.OrderBy(e => e.TimeMs).ToList(),
                SoftPedalEvents = _softPedalEvents.OrderBy(e => e.TimeMs).ToList(),
                SostenutoPedalEvents = _sostenutoEvents.OrderBy(e => e.TimeMs).ToList(),
                StartTimestamp = _startTime,
                IsRealTimeCapture = true
            };
        }
    }
}

/// <summary>
/// Handle for completing a note when note-off is received.
/// </summary>
public class NoteHandle
{
    private readonly Guid _noteId;
    private readonly PerformanceBuilder _builder;
    private bool _completed;
    
    internal NoteHandle(Guid noteId, PerformanceBuilder builder)
    {
        _noteId = noteId;
        _builder = builder;
    }
    
    /// <summary>
    /// Completes the note with the off event.
    /// </summary>
    public void Complete(double endTimeMs, int? releaseVelocity = null)
    {
        if (_completed) return;
        _completed = true;
        _builder.CompleteNote(_noteId, endTimeMs, releaseVelocity);
    }
}

