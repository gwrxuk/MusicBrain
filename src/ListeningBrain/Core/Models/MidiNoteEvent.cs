namespace ListeningBrain.Core.Models;

/// <summary>
/// Represents a single MIDI note event with both tick-based and time-based positioning.
/// This is the fundamental unit of musical data throughout the system.
/// </summary>
public record MidiNoteEvent
{
    /// <summary>
    /// MIDI pitch value (0-127). Middle C = 60.
    /// Octave = Pitch / 12 - 1, Note = Pitch % 12
    /// </summary>
    public required int Pitch { get; init; }
    
    /// <summary>
    /// Velocity (0-127). Higher = louder. 0 typically means note off.
    /// Maps to dynamics: pp (1-31), p (32-47), mp (48-63), mf (64-79), f (80-95), ff (96-127)
    /// </summary>
    public required int Velocity { get; init; }
    
    /// <summary>
    /// Start position in MIDI ticks (PPQ-dependent).
    /// Use StartTimeMs for time-based comparisons.
    /// </summary>
    public required long StartTick { get; init; }
    
    /// <summary>
    /// Duration in MIDI ticks.
    /// </summary>
    public required long DurationTicks { get; init; }
    
    /// <summary>
    /// Absolute start time in milliseconds from the beginning of the piece.
    /// Computed from ticks using tempo map.
    /// </summary>
    public required double StartTimeMs { get; init; }
    
    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public required double DurationMs { get; init; }
    
    /// <summary>
    /// MIDI channel (0-15). Piano typically uses channel 0.
    /// </summary>
    public int Channel { get; init; } = 0;
    
    /// <summary>
    /// Assigned voice for polyphonic separation (0 = unassigned).
    /// Used to track soprano, alto, tenor, bass in multi-voice music.
    /// </summary>
    public int Voice { get; init; } = 0;
    
    /// <summary>
    /// End time in milliseconds (computed).
    /// </summary>
    public double EndTimeMs => StartTimeMs + DurationMs;
    
    /// <summary>
    /// End tick position (computed).
    /// </summary>
    public long EndTick => StartTick + DurationTicks;
    
    /// <summary>
    /// Returns the pitch class (0-11) regardless of octave.
    /// 0=C, 1=C#, 2=D, ..., 11=B
    /// </summary>
    public int PitchClass => Pitch % 12;
    
    /// <summary>
    /// Returns the octave number (-1 to 9 for standard MIDI range).
    /// </summary>
    public int Octave => (Pitch / 12) - 1;
    
    /// <summary>
    /// Returns note name as string (e.g., "C4", "F#5", "Bb3").
    /// </summary>
    public string NoteName => GetNoteName(Pitch);
    
    /// <summary>
    /// Maps the dynamic level based on velocity.
    /// </summary>
    public DynamicLevel DynamicLevel => Velocity switch
    {
        <= 0 => DynamicLevel.Silent,
        <= 31 => DynamicLevel.Pianissimo,
        <= 47 => DynamicLevel.Piano,
        <= 63 => DynamicLevel.MezzoPiano,
        <= 79 => DynamicLevel.MezzoForte,
        <= 95 => DynamicLevel.Forte,
        _ => DynamicLevel.Fortissimo
    };
    
    private static string GetNoteName(int pitch)
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int octave = (pitch / 12) - 1;
        int note = pitch % 12;
        return $"{noteNames[note]}{octave}";
    }
    
    /// <summary>
    /// Calculate the semitone distance between two pitches.
    /// </summary>
    public int SemitoneDistanceTo(MidiNoteEvent other) => Math.Abs(Pitch - other.Pitch);
    
    /// <summary>
    /// Check if this note overlaps temporally with another.
    /// </summary>
    public bool OverlapsWith(MidiNoteEvent other)
    {
        return StartTimeMs < other.EndTimeMs && EndTimeMs > other.StartTimeMs;
    }
    
    /// <summary>
    /// Check if two notes have the same pitch class (ignoring octave).
    /// </summary>
    public bool IsSamePitchClass(MidiNoteEvent other) => PitchClass == other.PitchClass;
}

/// <summary>
/// Dynamic levels mapped from MIDI velocity.
/// </summary>
public enum DynamicLevel
{
    Silent = 0,
    Pianissimo = 1,   // pp: very soft
    Piano = 2,        // p: soft
    MezzoPiano = 3,   // mp: moderately soft
    MezzoForte = 4,   // mf: moderately loud
    Forte = 5,        // f: loud
    Fortissimo = 6    // ff: very loud
}

