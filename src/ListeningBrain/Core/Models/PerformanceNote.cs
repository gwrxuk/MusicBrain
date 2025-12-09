namespace ListeningBrain.Core.Models;

/// <summary>
/// Represents a note played by the student during a performance.
/// Captures real-world timing imperfections and velocity variations.
/// </summary>
public record PerformanceNote : MidiNoteEvent
{
    /// <summary>
    /// Unique identifier for this played note.
    /// </summary>
    public required Guid Id { get; init; }
    
    /// <summary>
    /// The system timestamp when note-on was received.
    /// Used for latency calculations and real-time processing.
    /// </summary>
    public required DateTime ReceivedTimestamp { get; init; }
    
    /// <summary>
    /// The release velocity (if available from the keyboard).
    /// </summary>
    public int? ReleaseVelocity { get; init; }
    
    /// <summary>
    /// Whether the sustain pedal was active when this note was played.
    /// </summary>
    public bool SustainPedalActive { get; init; } = false;
    
    /// <summary>
    /// The soft pedal (una corda) state.
    /// </summary>
    public bool SoftPedalActive { get; init; } = false;
    
    /// <summary>
    /// The sostenuto pedal state.
    /// </summary>
    public bool SostenutoPedalActive { get; init; } = false;
    
    /// <summary>
    /// Aftertouch/pressure value if the keyboard supports it (0-127).
    /// </summary>
    public int? Aftertouch { get; init; }
    
    /// <summary>
    /// Sequential index of this note in the performance (0-indexed).
    /// </summary>
    public int SequenceIndex { get; init; }
    
    /// <summary>
    /// The matched score note (assigned after alignment).
    /// Null if this was an extra note not in the score.
    /// </summary>
    public Guid? MatchedScoreNoteId { get; init; }
    
    /// <summary>
    /// Match confidence from the alignment algorithm (0.0 - 1.0).
    /// Higher = more confident this is the correct match.
    /// </summary>
    public double? MatchConfidence { get; init; }
    
    /// <summary>
    /// Classification of this note after alignment.
    /// </summary>
    public NoteClassification Classification { get; init; } = NoteClassification.Unclassified;
    
    /// <summary>
    /// Timing deviation from expected in milliseconds (positive = late, negative = early).
    /// Assigned after alignment with score.
    /// </summary>
    public double? TimingDeviationMs { get; init; }
    
    /// <summary>
    /// Timing deviation in musical beats.
    /// </summary>
    public double? TimingDeviationBeats { get; init; }
    
    /// <summary>
    /// Velocity deviation from expected dynamic level.
    /// </summary>
    public int? VelocityDeviation { get; init; }
    
    /// <summary>
    /// Creates a copy with alignment results applied.
    /// </summary>
    public PerformanceNote WithAlignment(
        Guid matchedScoreNoteId,
        double confidence,
        NoteClassification classification,
        double timingDeviationMs,
        double timingDeviationBeats,
        int velocityDeviation)
    {
        return this with
        {
            MatchedScoreNoteId = matchedScoreNoteId,
            MatchConfidence = confidence,
            Classification = classification,
            TimingDeviationMs = timingDeviationMs,
            TimingDeviationBeats = timingDeviationBeats,
            VelocityDeviation = velocityDeviation
        };
    }
    
    /// <summary>
    /// Marks this note as an extra (not in score).
    /// </summary>
    public PerformanceNote AsExtra()
    {
        return this with
        {
            Classification = NoteClassification.Extra,
            MatchedScoreNoteId = null,
            MatchConfidence = 0
        };
    }
}

/// <summary>
/// Classification of a performance note after alignment.
/// </summary>
public enum NoteClassification
{
    /// <summary>
    /// Not yet classified (before alignment).
    /// </summary>
    Unclassified = 0,
    
    /// <summary>
    /// Correct pitch and acceptable timing.
    /// </summary>
    Correct = 1,
    
    /// <summary>
    /// Correct pitch but played too early.
    /// </summary>
    CorrectButEarly = 2,
    
    /// <summary>
    /// Correct pitch but played too late.
    /// </summary>
    CorrectButLate = 3,
    
    /// <summary>
    /// Wrong pitch played instead of expected note.
    /// </summary>
    WrongPitch = 4,
    
    /// <summary>
    /// Right pitch class but wrong octave.
    /// </summary>
    WrongOctave = 5,
    
    /// <summary>
    /// Note was played but not in the score at this position.
    /// </summary>
    Extra = 6,
    
    /// <summary>
    /// Enharmonic equivalent (C# vs Db).
    /// </summary>
    EnharmonicMatch = 7,
    
    /// <summary>
    /// Correct note but significantly wrong duration.
    /// </summary>
    WrongDuration = 8
}

/// <summary>
/// Represents a note that was expected but not played.
/// </summary>
public record MissedNote
{
    /// <summary>
    /// The score note that was missed.
    /// </summary>
    public required ScoreNote ExpectedNote { get; init; }
    
    /// <summary>
    /// The performance note that was played instead (if any).
    /// </summary>
    public PerformanceNote? SubstitutedBy { get; init; }
    
    /// <summary>
    /// Possible reason for the miss (inferred).
    /// </summary>
    public MissReason InferredReason { get; init; } = MissReason.Unknown;
    
    /// <summary>
    /// Notes played around the time this note was expected.
    /// Useful for understanding what went wrong.
    /// </summary>
    public IReadOnlyList<PerformanceNote> NearbyPlayedNotes { get; init; } = [];
}

/// <summary>
/// Possible reasons for missing a note.
/// </summary>
public enum MissReason
{
    Unknown = 0,
    
    /// <summary>
    /// Student skipped this note entirely.
    /// </summary>
    Skipped = 1,
    
    /// <summary>
    /// Played a different note instead.
    /// </summary>
    Substituted = 2,
    
    /// <summary>
    /// Part of a passage that was entirely skipped.
    /// </summary>
    PassageSkipped = 3,
    
    /// <summary>
    /// Timing was so off that the match failed.
    /// </summary>
    TimingMismatch = 4,
    
    /// <summary>
    /// Grace note that was omitted (often acceptable).
    /// </summary>
    OptionalOrnament = 5
}

