namespace ListeningBrain.Core.Models;

/// <summary>
/// Represents a note in the ground-truth score with full musical context.
/// Extends MidiNoteEvent with musical semantics (measure, beat, articulation).
/// </summary>
public record ScoreNote : MidiNoteEvent
{
    /// <summary>
    /// Unique identifier for this note in the score.
    /// </summary>
    public required Guid Id { get; init; }
    
    /// <summary>
    /// Measure number (1-indexed) where this note occurs.
    /// </summary>
    public required int Measure { get; init; }
    
    /// <summary>
    /// Beat position within the measure (1-indexed, can be fractional).
    /// e.g., 1.0 = beat 1, 2.5 = beat 2 and a half
    /// </summary>
    public required double Beat { get; init; }
    
    /// <summary>
    /// The rhythmic value of the note.
    /// </summary>
    public required RhythmicValue RhythmicValue { get; init; }
    
    /// <summary>
    /// Is this a grace note (acciaccatura, appoggiatura)?
    /// </summary>
    public bool IsGraceNote { get; init; } = false;
    
    /// <summary>
    /// Type of grace note, if applicable.
    /// </summary>
    public GraceNoteType? GraceNoteType { get; init; }
    
    /// <summary>
    /// The main note this grace note decorates (null if not a grace note).
    /// </summary>
    public Guid? ParentNoteId { get; init; }
    
    /// <summary>
    /// Is this note part of a tuplet (triplet, quintuplet, etc.)?
    /// </summary>
    public bool IsTuplet { get; init; } = false;
    
    /// <summary>
    /// Tuplet grouping info if applicable.
    /// </summary>
    public TupletInfo? TupletInfo { get; init; }
    
    /// <summary>
    /// Is this note tied to the next note (sustain continues)?
    /// </summary>
    public bool IsTiedToNext { get; init; } = false;
    
    /// <summary>
    /// Is this note the continuation of a tied note?
    /// </summary>
    public bool IsTiedFromPrevious { get; init; } = false;
    
    /// <summary>
    /// Articulation marking on this note.
    /// </summary>
    public Articulation Articulation { get; init; } = Articulation.Normal;
    
    /// <summary>
    /// Expected dynamic marking at this point in the score.
    /// </summary>
    public DynamicLevel ExpectedDynamic { get; init; } = DynamicLevel.MezzoForte;
    
    /// <summary>
    /// Is this note part of a pickup measure (anacrusis)?
    /// </summary>
    public bool IsPickupNote { get; init; } = false;
    
    /// <summary>
    /// Staff number for grand staff notation (1 = treble/RH, 2 = bass/LH).
    /// </summary>
    public int Staff { get; init; } = 1;
    
    /// <summary>
    /// Finger number suggestion (1-5, 0 = unspecified).
    /// </summary>
    public int SuggestedFinger { get; init; } = 0;
    
    /// <summary>
    /// Gets the tolerance window for timing evaluation based on note characteristics.
    /// Grace notes and tuplets have different timing expectations.
    /// </summary>
    public TimingTolerance GetTimingTolerance()
    {
        if (IsGraceNote)
        {
            return new TimingTolerance(
                EarlyMs: 100,    // Grace notes can be early
                LateMs: 50,     // Should not be too late
                EarlyBeats: 0.5,
                LateBeats: 0.25
            );
        }
        
        if (IsTuplet)
        {
            return new TimingTolerance(
                EarlyMs: 60,    // Slightly more tolerance for tuplets
                LateMs: 60,
                EarlyBeats: 0.15,
                LateBeats: 0.15
            );
        }
        
        // Standard tolerance
        return new TimingTolerance(
            EarlyMs: 50,
            LateMs: 50,
            EarlyBeats: 0.125,
            LateBeats: 0.125
        );
    }
}

/// <summary>
/// Standard rhythmic note values.
/// </summary>
public enum RhythmicValue
{
    Whole = 1,          // 4 beats (in 4/4)
    HalfDotted = 2,     // 3 beats
    Half = 3,           // 2 beats
    QuarterDotted = 4,  // 1.5 beats
    Quarter = 5,        // 1 beat
    EighthDotted = 6,   // 0.75 beats
    Eighth = 7,         // 0.5 beats
    Sixteenth = 8,      // 0.25 beats
    ThirtySecond = 9,   // 0.125 beats
    SixtyFourth = 10,   // 0.0625 beats
    TripletQuarter = 11,  // 2/3 beat
    TripletEighth = 12,   // 1/3 beat
    TripletSixteenth = 13 // 1/6 beat
}

/// <summary>
/// Types of grace notes.
/// </summary>
public enum GraceNoteType
{
    /// <summary>
    /// Crushed note - played very quickly, "steals" time from main note.
    /// </summary>
    Acciaccatura,
    
    /// <summary>
    /// Leaning note - takes time from previous note, played on the beat.
    /// </summary>
    Appoggiatura,
    
    /// <summary>
    /// Multi-note ornament.
    /// </summary>
    GraceNoteGroup
}

/// <summary>
/// Tuplet grouping information.
/// </summary>
public record TupletInfo(
    /// <summary>
    /// Number of notes played (e.g., 3 for triplet).
    /// </summary>
    int ActualNotes,
    
    /// <summary>
    /// Normal number of notes in the same time (e.g., 2 for triplet).
    /// </summary>
    int NormalNotes,
    
    /// <summary>
    /// Position within the tuplet group (1-indexed).
    /// </summary>
    int PositionInGroup,
    
    /// <summary>
    /// Total notes in this tuplet group.
    /// </summary>
    int GroupSize
);

/// <summary>
/// Articulation markings.
/// </summary>
public enum Articulation
{
    Normal = 0,
    Staccato = 1,      // Short, detached
    Staccatissimo = 2, // Very short
    Tenuto = 3,        // Held full value
    Accent = 4,        // Emphasized
    Marcato = 5,       // Strongly accented
    Legato = 6,        // Smooth, connected
    Portato = 7        // Slightly separated
}

/// <summary>
/// Timing tolerance window for a note.
/// </summary>
public record TimingTolerance(
    double EarlyMs,
    double LateMs,
    double EarlyBeats,
    double LateBeats
);

