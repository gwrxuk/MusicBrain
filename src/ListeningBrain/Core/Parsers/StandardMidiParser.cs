using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using ListeningBrain.Core.Models;

namespace ListeningBrain.Core.Parsers;

/// <summary>
/// Parses Standard MIDI Files (SMF) into Score objects using DryWetMidi.
/// Supports Type 0 (single track) and Type 1 (multi-track) MIDI files.
/// </summary>
public class StandardMidiParser
{
    /// <summary>
    /// Parses a MIDI file into a Score.
    /// </summary>
    public Score ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"MIDI file not found: {filePath}");
        }
        
        var midiFile = MidiFile.Read(filePath);
        return ParseMidiFile(midiFile, filePath);
    }
    
    /// <summary>
    /// Parses MIDI data from a stream.
    /// </summary>
    public Score ParseStream(Stream stream, string? sourcePath = null)
    {
        var midiFile = MidiFile.Read(stream);
        return ParseMidiFile(midiFile, sourcePath);
    }
    
    /// <summary>
    /// Parses MIDI data from bytes.
    /// </summary>
    public Score ParseBytes(byte[] data, string? sourcePath = null)
    {
        using var stream = new MemoryStream(data);
        return ParseStream(stream, sourcePath);
    }
    
    private Score ParseMidiFile(MidiFile midiFile, string? sourcePath)
    {
        var builder = new ScoreBuilder();
        
        // Get timing information
        var timeDivision = midiFile.TimeDivision;
        int ppq = 480; // Default
        
        if (timeDivision is TicksPerQuarterNoteTimeDivision tpqn)
        {
            ppq = tpqn.TicksPerQuarterNote;
        }
        
        builder.WithPPQ(ppq);
        
        if (sourcePath != null)
        {
            builder.WithTitle(Path.GetFileNameWithoutExtension(sourcePath));
        }
        
        // Extract tempo map
        var tempoMap = midiFile.GetTempoMap();
        
        // Extract time signatures
        var timeSignatures = ExtractTimeSignatures(midiFile, tempoMap);
        foreach (var ts in timeSignatures)
        {
            builder.AddTimeSignature(ts);
        }
        
        // Extract tempo markings
        var tempoMarkings = ExtractTempoMarkings(midiFile, tempoMap);
        foreach (var tempo in tempoMarkings)
        {
            builder.AddTempo(tempo);
        }
        
        // Extract key signatures
        var keySignatures = ExtractKeySignatures(midiFile);
        foreach (var key in keySignatures)
        {
            builder.AddKeySignature(key);
        }
        
        // Extract notes
        var notes = ExtractNotes(midiFile, tempoMap, ppq, timeSignatures.FirstOrDefault() ?? TimeSignature.Common);
        builder.AddNotes(notes);
        
        // Calculate total measures
        if (notes.Any())
        {
            builder.WithTotalMeasures(notes.Max(n => n.Measure));
        }
        
        return builder.Build();
    }
    
    private List<TimeSignature> ExtractTimeSignatures(MidiFile midiFile, TempoMap tempoMap)
    {
        var result = new List<TimeSignature>();
        
        foreach (var tsChange in tempoMap.GetTimeSignatureChanges())
        {
            var ts = tsChange.Value;
            result.Add(new TimeSignature
            {
                Numerator = ts.Numerator,
                Denominator = (int)Math.Pow(2, ts.DenominatorPower),
                StartTick = tsChange.Time
            });
        }
        
        if (result.Count == 0)
        {
            result.Add(TimeSignature.Common);
        }
        
        return result;
    }
    
    private List<TempoMarking> ExtractTempoMarkings(MidiFile midiFile, TempoMap tempoMap)
    {
        var result = new List<TempoMarking>();
        
        foreach (var tempoChange in tempoMap.GetTempoChanges())
        {
            var tempo = tempoChange.Value;
            double bpm = tempo.BeatsPerMinute;
            
            result.Add(new TempoMarking
            {
                BPM = bpm,
                StartTick = tempoChange.Time
            });
        }
        
        if (result.Count == 0)
        {
            result.Add(TempoMarking.Moderato);
        }
        
        return result;
    }
    
    private List<KeySignature> ExtractKeySignatures(MidiFile midiFile)
    {
        var result = new List<KeySignature>();
        
        // Key signatures are in meta events
        foreach (var trackChunk in midiFile.GetTrackChunks())
        {
            long currentTick = 0;
            
            foreach (var midiEvent in trackChunk.Events)
            {
                currentTick += midiEvent.DeltaTime;
                
                if (midiEvent is KeySignatureEvent keySigEvent)
                {
                    result.Add(new KeySignature
                    {
                        Accidentals = keySigEvent.Key,
                        IsMinor = keySigEvent.Scale == 1,
                        StartTick = currentTick
                    });
                }
            }
        }
        
        if (result.Count == 0)
        {
            result.Add(KeySignature.CMajor);
        }
        
        return result;
    }
    
    private List<ScoreNote> ExtractNotes(
        MidiFile midiFile, 
        TempoMap tempoMap, 
        int ppq,
        TimeSignature firstTimeSignature)
    {
        var notes = new List<ScoreNote>();
        var midiNotes = midiFile.GetNotes();
        
        foreach (var note in midiNotes)
        {
            // Get timing
            long startTick = note.Time;
            long durationTicks = note.Length;
            
            // Convert to milliseconds
            var startMetric = TimeConverter.ConvertTo<MetricTimeSpan>(startTick, tempoMap);
            var endMetric = TimeConverter.ConvertTo<MetricTimeSpan>(startTick + durationTicks, tempoMap);
            
            double startMs = startMetric.TotalMilliseconds;
            double durationMs = endMetric.TotalMilliseconds - startMs;
            
            // Calculate measure and beat
            var (measure, beat) = CalculateMeasureBeat(startTick, ppq, firstTimeSignature);
            
            // Determine rhythmic value
            var rhythmicValue = DetermineRhythmicValue(durationTicks, ppq);
            
            // Assign voice based on channel and pitch
            int voice = AssignVoice(note.Channel, note.NoteNumber);
            
            notes.Add(new ScoreNote
            {
                Id = Guid.NewGuid(),
                Pitch = note.NoteNumber,
                Velocity = note.Velocity,
                StartTick = startTick,
                DurationTicks = durationTicks,
                StartTimeMs = startMs,
                DurationMs = durationMs,
                Channel = note.Channel,
                Voice = voice,
                Measure = measure,
                Beat = beat,
                RhythmicValue = rhythmicValue,
                Staff = note.NoteNumber >= 60 ? 1 : 2 // Simple split at middle C
            });
        }
        
        return notes.OrderBy(n => n.StartTick).ThenBy(n => n.Pitch).ToList();
    }
    
    private (int measure, double beat) CalculateMeasureBeat(long tick, int ppq, TimeSignature ts)
    {
        long ticksPerMeasure = ts.TicksPerMeasure(ppq);
        long ticksPerBeat = ts.TicksPerBeat(ppq);
        
        int measure = (int)(tick / ticksPerMeasure) + 1;
        long tickInMeasure = tick % ticksPerMeasure;
        double beat = (double)tickInMeasure / ticksPerBeat + 1;
        
        return (measure, beat);
    }
    
    private RhythmicValue DetermineRhythmicValue(long durationTicks, int ppq)
    {
        double quarters = (double)durationTicks / ppq;
        
        return quarters switch
        {
            >= 3.8 => RhythmicValue.Whole,
            >= 2.8 => RhythmicValue.HalfDotted,
            >= 1.8 => RhythmicValue.Half,
            >= 1.4 => RhythmicValue.QuarterDotted,
            >= 0.9 => RhythmicValue.Quarter,
            >= 0.7 => RhythmicValue.EighthDotted,
            >= 0.45 => RhythmicValue.Eighth,
            >= 0.2 => RhythmicValue.Sixteenth,
            >= 0.1 => RhythmicValue.ThirtySecond,
            _ => RhythmicValue.SixtyFourth
        };
    }
    
    private int AssignVoice(int channel, int pitch)
    {
        // Simple voice assignment based on pitch
        // Can be overridden with more sophisticated analysis
        return pitch switch
        {
            >= 72 => 1, // Soprano
            >= 60 => 2, // Alto
            >= 48 => 3, // Tenor
            _ => 4      // Bass
        };
    }
}

/// <summary>
/// Factory for creating parsers.
/// </summary>
public static class MidiParserFactory
{
    /// <summary>
    /// Creates a parser appropriate for the file type.
    /// </summary>
    public static StandardMidiParser Create()
    {
        return new StandardMidiParser();
    }
    
    /// <summary>
    /// Parses a MIDI file directly.
    /// </summary>
    public static Score ParseFile(string filePath)
    {
        return new StandardMidiParser().ParseFile(filePath);
    }
}

