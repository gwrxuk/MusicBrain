namespace ListeningBrain.Core.Models;

/// <summary>
/// Represents a time signature in musical notation.
/// </summary>
public record TimeSignature
{
    /// <summary>
    /// Numerator: beats per measure (e.g., 4 in 4/4).
    /// </summary>
    public required int Numerator { get; init; }
    
    /// <summary>
    /// Denominator: note value that gets one beat (e.g., 4 = quarter note).
    /// </summary>
    public required int Denominator { get; init; }
    
    /// <summary>
    /// MIDI tick where this time signature takes effect.
    /// </summary>
    public long StartTick { get; init; } = 0;
    
    /// <summary>
    /// Measure number where this time signature starts (1-indexed).
    /// </summary>
    public int StartMeasure { get; init; } = 1;
    
    /// <summary>
    /// Display string (e.g., "4/4", "3/4", "6/8").
    /// </summary>
    public string Display => $"{Numerator}/{Denominator}";
    
    /// <summary>
    /// Number of quarter notes per measure.
    /// For 4/4: 4, for 3/4: 3, for 6/8: 1.5
    /// </summary>
    public double QuarterNotesPerMeasure => Numerator * (4.0 / Denominator);
    
    /// <summary>
    /// Duration of one measure in MIDI ticks (at given PPQ).
    /// </summary>
    public long TicksPerMeasure(int ppq) => (long)(ppq * QuarterNotesPerMeasure);
    
    /// <summary>
    /// Duration of one beat in MIDI ticks (at given PPQ).
    /// </summary>
    public long TicksPerBeat(int ppq) => ppq * 4 / Denominator;
    
    /// <summary>
    /// Is this a compound time signature (6/8, 9/8, 12/8)?
    /// </summary>
    public bool IsCompound => Denominator == 8 && Numerator % 3 == 0;
    
    /// <summary>
    /// Number of strong beats per measure.
    /// For compound time, counts groups (6/8 = 2 groups of 3).
    /// </summary>
    public int StrongBeatsPerMeasure => IsCompound ? Numerator / 3 : Numerator;
    
    /// <summary>
    /// Common time signatures.
    /// </summary>
    public static TimeSignature Common => new() { Numerator = 4, Denominator = 4 };
    public static TimeSignature CutTime => new() { Numerator = 2, Denominator = 2 };
    public static TimeSignature ThreeFour => new() { Numerator = 3, Denominator = 4 };
    public static TimeSignature SixEight => new() { Numerator = 6, Denominator = 8 };
    public static TimeSignature TwoFour => new() { Numerator = 2, Denominator = 4 };
}

/// <summary>
/// Represents a tempo marking (BPM) at a specific point in the score.
/// </summary>
public record TempoMarking
{
    /// <summary>
    /// Beats per minute.
    /// </summary>
    public required double BPM { get; init; }
    
    /// <summary>
    /// MIDI tick where this tempo takes effect.
    /// </summary>
    public long StartTick { get; init; } = 0;
    
    /// <summary>
    /// Microseconds per quarter note (MIDI format).
    /// </summary>
    public int MicrosecondsPerQuarter => (int)(60_000_000 / BPM);
    
    /// <summary>
    /// Milliseconds per quarter note.
    /// </summary>
    public double MillisecondsPerQuarter => 60_000 / BPM;
    
    /// <summary>
    /// Milliseconds per beat for a given time signature.
    /// </summary>
    public double MillisecondsPerBeat(TimeSignature timeSignature)
    {
        double beatsPerQuarter = timeSignature.Denominator / 4.0;
        return MillisecondsPerQuarter / beatsPerQuarter;
    }
    
    /// <summary>
    /// Common tempo markings.
    /// </summary>
    public static TempoMarking Largo => new() { BPM = 50 };
    public static TempoMarking Adagio => new() { BPM = 70 };
    public static TempoMarking Andante => new() { BPM = 90 };
    public static TempoMarking Moderato => new() { BPM = 110 };
    public static TempoMarking Allegro => new() { BPM = 130 };
    public static TempoMarking Vivace => new() { BPM = 160 };
    public static TempoMarking Presto => new() { BPM = 180 };
}

/// <summary>
/// Key signature representation.
/// </summary>
public record KeySignature
{
    /// <summary>
    /// Number of sharps (positive) or flats (negative).
    /// Range: -7 to +7
    /// </summary>
    public required int Accidentals { get; init; }
    
    /// <summary>
    /// Is this a minor key?
    /// </summary>
    public bool IsMinor { get; init; } = false;
    
    /// <summary>
    /// MIDI tick where this key signature takes effect.
    /// </summary>
    public long StartTick { get; init; } = 0;
    
    /// <summary>
    /// Gets the root note pitch class (0-11).
    /// </summary>
    public int RootPitchClass
    {
        get
        {
            // Circle of fifths: F C G D A E B F# C# G# D# A#
            // Flats go counterclockwise, sharps clockwise
            int root = (Accidentals * 7 + 12) % 12; // Major key root
            if (IsMinor)
            {
                root = (root + 9) % 12; // Relative minor is 3 semitones down
            }
            return root;
        }
    }
    
    /// <summary>
    /// Gets the key name (e.g., "C Major", "A Minor").
    /// </summary>
    public string KeyName
    {
        get
        {
            string[] sharpKeys = { "C", "G", "D", "A", "E", "B", "F#", "C#" };
            string[] flatKeys = { "C", "F", "Bb", "Eb", "Ab", "Db", "Gb", "Cb" };
            
            string root = Accidentals >= 0 
                ? sharpKeys[Accidentals] 
                : flatKeys[-Accidentals];
                
            if (IsMinor)
            {
                // Get relative minor
                int minorIndex = Accidentals >= 0
                    ? (Accidentals + 5) % 7
                    : (7 + Accidentals + 5) % 7;
                string[] minorSharpKeys = { "A", "E", "B", "F#", "C#", "G#", "D#", "A#" };
                string[] minorFlatKeys = { "A", "D", "G", "C", "F", "Bb", "Eb", "Ab" };
                root = Accidentals >= 0 ? minorSharpKeys[minorIndex] : minorFlatKeys[Math.Abs(Accidentals)];
            }
            
            return $"{root} {(IsMinor ? "Minor" : "Major")}";
        }
    }
    
    /// <summary>
    /// Common key signatures.
    /// </summary>
    public static KeySignature CMajor => new() { Accidentals = 0 };
    public static KeySignature GMajor => new() { Accidentals = 1 };
    public static KeySignature DMajor => new() { Accidentals = 2 };
    public static KeySignature FMajor => new() { Accidentals = -1 };
    public static KeySignature BbMajor => new() { Accidentals = -2 };
    public static KeySignature AMinor => new() { Accidentals = 0, IsMinor = true };
}

