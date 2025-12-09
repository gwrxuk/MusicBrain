namespace ListeningBrain.Core.Models;

/// <summary>
/// Represents a complete musical score (ground truth) that performances are compared against.
/// Contains all notes, timing information, and musical structure.
/// </summary>
public class Score
{
    /// <summary>
    /// All notes in the score, ordered by start time.
    /// </summary>
    public IReadOnlyList<ScoreNote> Notes { get; init; } = [];
    
    /// <summary>
    /// Pulses (ticks) per quarter note. Standard is 480 or 960.
    /// </summary>
    public int PPQ { get; init; } = 480;
    
    /// <summary>
    /// Time signatures throughout the piece (at least one required).
    /// </summary>
    public IReadOnlyList<TimeSignature> TimeSignatures { get; init; } = [TimeSignature.Common];
    
    /// <summary>
    /// Tempo markings throughout the piece.
    /// </summary>
    public IReadOnlyList<TempoMarking> TempoMarkings { get; init; } = [TempoMarking.Moderato];
    
    /// <summary>
    /// Key signatures throughout the piece.
    /// </summary>
    public IReadOnlyList<KeySignature> KeySignatures { get; init; } = [KeySignature.CMajor];
    
    /// <summary>
    /// Title of the piece.
    /// </summary>
    public string Title { get; init; } = "Untitled";
    
    /// <summary>
    /// Composer name.
    /// </summary>
    public string? Composer { get; init; }
    
    /// <summary>
    /// Total number of measures.
    /// </summary>
    public int TotalMeasures { get; init; }
    
    /// <summary>
    /// Total duration in milliseconds.
    /// </summary>
    public double TotalDurationMs => Notes.Count > 0 ? Notes.Max(n => n.EndTimeMs) : 0;
    
    /// <summary>
    /// Total duration in ticks.
    /// </summary>
    public long TotalDurationTicks => Notes.Count > 0 ? Notes.Max(n => n.EndTick) : 0;
    
    /// <summary>
    /// Number of pickup beats before measure 1 (anacrusis).
    /// </summary>
    public double PickupBeats { get; init; } = 0;
    
    /// <summary>
    /// The tick position where measure 1, beat 1 begins.
    /// </summary>
    public long FirstDownbeatTick { get; init; } = 0;
    
    /// <summary>
    /// Source file path (if loaded from file).
    /// </summary>
    public string? SourcePath { get; init; }
    
    /// <summary>
    /// Gets notes at a specific measure.
    /// </summary>
    public IEnumerable<ScoreNote> GetNotesInMeasure(int measure)
        => Notes.Where(n => n.Measure == measure);
    
    /// <summary>
    /// Gets notes within a time range.
    /// </summary>
    public IEnumerable<ScoreNote> GetNotesInTimeRange(double startMs, double endMs)
        => Notes.Where(n => n.StartTimeMs >= startMs && n.StartTimeMs < endMs);
    
    /// <summary>
    /// Gets the time signature at a specific tick position.
    /// </summary>
    public TimeSignature GetTimeSignatureAt(long tick)
        => TimeSignatures
            .Where(ts => ts.StartTick <= tick)
            .OrderByDescending(ts => ts.StartTick)
            .FirstOrDefault() ?? TimeSignature.Common;
    
    /// <summary>
    /// Gets the tempo at a specific tick position.
    /// </summary>
    public TempoMarking GetTempoAt(long tick)
        => TempoMarkings
            .Where(t => t.StartTick <= tick)
            .OrderByDescending(t => t.StartTick)
            .FirstOrDefault() ?? TempoMarking.Moderato;
    
    /// <summary>
    /// Gets the key signature at a specific tick position.
    /// </summary>
    public KeySignature GetKeySignatureAt(long tick)
        => KeySignatures
            .Where(k => k.StartTick <= tick)
            .OrderByDescending(k => k.StartTick)
            .FirstOrDefault() ?? KeySignature.CMajor;
    
    /// <summary>
    /// Converts a tick position to absolute time in milliseconds.
    /// Accounts for tempo changes.
    /// </summary>
    public double TickToMs(long tick)
    {
        double ms = 0;
        long currentTick = 0;
        
        foreach (var tempo in TempoMarkings.OrderBy(t => t.StartTick))
        {
            if (tempo.StartTick >= tick)
                break;
                
            long segmentTicks = Math.Min(tick, tempo.StartTick) - currentTick;
            var prevTempo = GetTempoAt(currentTick);
            ms += segmentTicks * prevTempo.MillisecondsPerQuarter / PPQ;
            currentTick = tempo.StartTick;
        }
        
        // Add remaining ticks at current tempo
        long remainingTicks = tick - currentTick;
        var currentTempo = GetTempoAt(currentTick);
        ms += remainingTicks * currentTempo.MillisecondsPerQuarter / PPQ;
        
        return ms;
    }
    
    /// <summary>
    /// Gets notes grouped by voice (for polyphonic pieces).
    /// </summary>
    public Dictionary<int, List<ScoreNote>> GetNotesByVoice()
        => Notes.GroupBy(n => n.Voice)
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.StartTick).ToList());
    
    /// <summary>
    /// Gets all grace notes that decorate a specific parent note.
    /// </summary>
    public IEnumerable<ScoreNote> GetGraceNotesFor(Guid parentNoteId)
        => Notes.Where(n => n.ParentNoteId == parentNoteId);
    
    /// <summary>
    /// Validates the score for internal consistency.
    /// </summary>
    public ScoreValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        
        if (Notes.Count == 0)
            errors.Add("Score contains no notes");
            
        if (PPQ <= 0)
            errors.Add($"Invalid PPQ value: {PPQ}");
            
        if (TimeSignatures.Count == 0)
            warnings.Add("No time signature specified, defaulting to 4/4");
            
        if (TempoMarkings.Count == 0)
            warnings.Add("No tempo specified, defaulting to 110 BPM");
        
        // Check for overlapping notes on same pitch
        var notesByPitch = Notes.GroupBy(n => n.Pitch);
        foreach (var group in notesByPitch)
        {
            var sorted = group.OrderBy(n => n.StartTick).ToList();
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                if (sorted[i].EndTick > sorted[i + 1].StartTick)
                {
                    warnings.Add($"Overlapping notes on pitch {sorted[i].Pitch} at tick {sorted[i].StartTick}");
                }
            }
        }
        
        // Check for grace notes without parents
        foreach (var graceNote in Notes.Where(n => n.IsGraceNote))
        {
            if (!graceNote.ParentNoteId.HasValue)
            {
                warnings.Add($"Grace note at tick {graceNote.StartTick} has no parent note");
            }
            else if (!Notes.Any(n => n.Id == graceNote.ParentNoteId))
            {
                errors.Add($"Grace note references non-existent parent {graceNote.ParentNoteId}");
            }
        }
        
        return new ScoreValidationResult(errors, warnings);
    }
}

/// <summary>
/// Result of score validation.
/// </summary>
public record ScoreValidationResult(
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings
)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Builder pattern for constructing Score objects.
/// </summary>
public class ScoreBuilder
{
    private readonly List<ScoreNote> _notes = [];
    private readonly List<TimeSignature> _timeSignatures = [];
    private readonly List<TempoMarking> _tempoMarkings = [];
    private readonly List<KeySignature> _keySignatures = [];
    private int _ppq = 480;
    private string _title = "Untitled";
    private string? _composer;
    private double _pickupBeats;
    private int _totalMeasures;
    
    public ScoreBuilder WithPPQ(int ppq)
    {
        _ppq = ppq;
        return this;
    }
    
    public ScoreBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }
    
    public ScoreBuilder WithComposer(string composer)
    {
        _composer = composer;
        return this;
    }
    
    public ScoreBuilder WithPickupBeats(double beats)
    {
        _pickupBeats = beats;
        return this;
    }
    
    public ScoreBuilder WithTotalMeasures(int measures)
    {
        _totalMeasures = measures;
        return this;
    }
    
    public ScoreBuilder AddNote(ScoreNote note)
    {
        _notes.Add(note);
        return this;
    }
    
    public ScoreBuilder AddNotes(IEnumerable<ScoreNote> notes)
    {
        _notes.AddRange(notes);
        return this;
    }
    
    public ScoreBuilder AddTimeSignature(TimeSignature ts)
    {
        _timeSignatures.Add(ts);
        return this;
    }
    
    public ScoreBuilder AddTempo(TempoMarking tempo)
    {
        _tempoMarkings.Add(tempo);
        return this;
    }
    
    public ScoreBuilder AddKeySignature(KeySignature key)
    {
        _keySignatures.Add(key);
        return this;
    }
    
    public Score Build()
    {
        var orderedNotes = _notes.OrderBy(n => n.StartTick).ThenBy(n => n.Pitch).ToList();
        
        return new Score
        {
            Notes = orderedNotes,
            PPQ = _ppq,
            TimeSignatures = _timeSignatures.Count > 0 ? _timeSignatures : [TimeSignature.Common],
            TempoMarkings = _tempoMarkings.Count > 0 ? _tempoMarkings : [TempoMarking.Moderato],
            KeySignatures = _keySignatures.Count > 0 ? _keySignatures : [KeySignature.CMajor],
            Title = _title,
            Composer = _composer,
            PickupBeats = _pickupBeats,
            TotalMeasures = _totalMeasures > 0 ? _totalMeasures : CalculateTotalMeasures(orderedNotes)
        };
    }
    
    private int CalculateTotalMeasures(List<ScoreNote> notes)
    {
        if (notes.Count == 0) return 0;
        return notes.Max(n => n.Measure);
    }
}

