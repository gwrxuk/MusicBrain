using System.Text.Json;
using System.Text.Json.Serialization;

namespace ListeningBrain.Samples;

/// <summary>
/// Generates sample scores and performances for testing the evaluation engine.
/// Run this to populate the samples folder with test data.
/// </summary>
public static class SampleGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    public static void GenerateAllSamples(string outputPath)
    {
        Directory.CreateDirectory(Path.Combine(outputPath, "scores"));
        Directory.CreateDirectory(Path.Combine(outputPath, "performances"));
        
        // Generate scores
        GenerateScales(outputPath);
        GenerateChords(outputPath);
        GenerateArpeggios(outputPath);
        GenerateMelodies(outputPath);
        GenerateClassicalExcerpts(outputPath);
        
        // Generate performances with varying accuracy
        GeneratePerformances(outputPath);
        
        Console.WriteLine($"Generated samples in {outputPath}");
    }
    
    private static void GenerateScales(string basePath)
    {
        var scaleTypes = new[]
        {
            ("c_major", new[] { 60, 62, 64, 65, 67, 69, 71, 72 }),
            ("g_major", new[] { 67, 69, 71, 72, 74, 76, 78, 79 }),
            ("d_major", new[] { 62, 64, 66, 67, 69, 71, 73, 74 }),
            ("a_minor", new[] { 57, 59, 60, 62, 64, 65, 67, 69 }),
            ("e_minor", new[] { 64, 66, 67, 69, 71, 72, 74, 76 }),
            ("chromatic", new[] { 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72 }),
            ("blues", new[] { 60, 63, 65, 66, 67, 70, 72 }),
            ("pentatonic", new[] { 60, 62, 64, 67, 69, 72 })
        };
        
        foreach (var (name, pitches) in scaleTypes)
        {
            var score = CreateScaleScore(name, pitches, 120);
            SaveScore(basePath, $"scale_{name}", score);
            
            // Also create descending version
            var descending = CreateScaleScore($"{name}_desc", pitches.Reverse().ToArray(), 120);
            SaveScore(basePath, $"scale_{name}_descending", descending);
        }
    }
    
    private static void GenerateChords(string basePath)
    {
        var chordProgressions = new[]
        {
            ("I_IV_V_I_major", new[] { 
                new[] { 60, 64, 67 },      // C major
                new[] { 65, 69, 72 },      // F major
                new[] { 67, 71, 74 },      // G major
                new[] { 60, 64, 67 }       // C major
            }),
            ("i_iv_V_i_minor", new[] {
                new[] { 57, 60, 64 },      // A minor
                new[] { 62, 65, 69 },      // D minor
                new[] { 64, 68, 71 },      // E major
                new[] { 57, 60, 64 }       // A minor
            }),
            ("jazz_ii_V_I", new[] {
                new[] { 62, 65, 69, 72 },  // Dm7
                new[] { 67, 71, 74, 77 },  // G7
                new[] { 60, 64, 67, 71 }   // Cmaj7
            }),
            ("pop_I_V_vi_IV", new[] {
                new[] { 60, 64, 67 },      // C
                new[] { 67, 71, 74 },      // G
                new[] { 57, 60, 64 },      // Am
                new[] { 65, 69, 72 }       // F
            })
        };
        
        foreach (var (name, chords) in chordProgressions)
        {
            var score = CreateChordScore(name, chords, 90);
            SaveScore(basePath, $"chords_{name}", score);
        }
    }
    
    private static void GenerateArpeggios(string basePath)
    {
        var arpeggioPatterns = new[]
        {
            ("c_major_arpeggio", new[] { 48, 52, 55, 60, 64, 67, 72, 76, 79, 84 }),
            ("a_minor_arpeggio", new[] { 45, 48, 52, 57, 60, 64, 69, 72, 76, 81 }),
            ("alberti_bass", new[] { 48, 55, 52, 55, 48, 55, 52, 55 }),
            ("broken_chord", new[] { 60, 64, 67, 64, 60, 64, 67, 64 })
        };
        
        foreach (var (name, pattern) in arpeggioPatterns)
        {
            var score = CreateArpeggioScore(name, pattern, 100);
            SaveScore(basePath, $"arpeggio_{name}", score);
        }
    }
    
    private static void GenerateMelodies(string basePath)
    {
        // Twinkle Twinkle Little Star
        var twinkle = new[]
        {
            (60, 1.0), (60, 1.0), (67, 1.0), (67, 1.0), (69, 1.0), (69, 1.0), (67, 2.0),
            (65, 1.0), (65, 1.0), (64, 1.0), (64, 1.0), (62, 1.0), (62, 1.0), (60, 2.0)
        };
        SaveScore(basePath, "melody_twinkle_twinkle", CreateMelodyScore("Twinkle Twinkle", twinkle, 100));
        
        // Mary Had a Little Lamb
        var mary = new[]
        {
            (64, 1.0), (62, 1.0), (60, 1.0), (62, 1.0), (64, 1.0), (64, 1.0), (64, 2.0),
            (62, 1.0), (62, 1.0), (62, 2.0), (64, 1.0), (67, 1.0), (67, 2.0)
        };
        SaveScore(basePath, "melody_mary_lamb", CreateMelodyScore("Mary Had a Little Lamb", mary, 110));
        
        // Ode to Joy theme
        var ode = new[]
        {
            (64, 1.0), (64, 1.0), (65, 1.0), (67, 1.0), (67, 1.0), (65, 1.0), (64, 1.0), (62, 1.0),
            (60, 1.0), (60, 1.0), (62, 1.0), (64, 1.0), (64, 1.5), (62, 0.5), (62, 2.0)
        };
        SaveScore(basePath, "melody_ode_to_joy", CreateMelodyScore("Ode to Joy", ode, 108));
        
        // Für Elise opening
        var furElise = new[]
        {
            (76, 0.5), (75, 0.5), (76, 0.5), (75, 0.5), (76, 0.5), (71, 0.5), (74, 0.5), (72, 0.5),
            (69, 1.0), (52, 0.5), (57, 0.5), (60, 0.5), (69, 0.5), (71, 1.0)
        };
        SaveScore(basePath, "melody_fur_elise", CreateMelodyScore("Fur Elise", furElise, 72));
        
        // Happy Birthday
        var birthday = new[]
        {
            (60, 0.75), (60, 0.25), (62, 1.0), (60, 1.0), (65, 1.0), (64, 2.0),
            (60, 0.75), (60, 0.25), (62, 1.0), (60, 1.0), (67, 1.0), (65, 2.0)
        };
        SaveScore(basePath, "melody_happy_birthday", CreateMelodyScore("Happy Birthday", birthday, 100));
    }
    
    private static void GenerateClassicalExcerpts(string basePath)
    {
        // Simple Bach-style counterpoint
        var bachTwoVoice = CreateTwoVoiceScore("Bach Style", 
            new[] { (72, 1.0), (71, 1.0), (72, 1.0), (74, 1.0), (76, 2.0), (74, 1.0), (72, 1.0) },
            new[] { (60, 2.0), (64, 2.0), (67, 2.0), (64, 2.0) },
            80);
        SaveScore(basePath, "classical_bach_style", bachTwoVoice);
        
        // Mozart-style Alberti bass with melody
        var mozartStyle = CreateMozartStyle("Mozart Style", 120);
        SaveScore(basePath, "classical_mozart_style", mozartStyle);
        
        // Chopin-style nocturne pattern
        var chopinStyle = CreateChopinStyle("Chopin Nocturne", 60);
        SaveScore(basePath, "classical_chopin_style", chopinStyle);
    }
    
    private static void GeneratePerformances(string basePath)
    {
        // Get all score files
        var scoresPath = Path.Combine(basePath, "scores");
        var scoreFiles = Directory.GetFiles(scoresPath, "*.json");
        
        foreach (var scoreFile in scoreFiles)
        {
            var scoreName = Path.GetFileNameWithoutExtension(scoreFile);
            var scoreJson = File.ReadAllText(scoreFile);
            var score = JsonSerializer.Deserialize<SampleScore>(scoreJson, JsonOptions);
            
            if (score == null) continue;
            
            // Generate performances at different accuracy levels
            var accuracyLevels = new[]
            {
                ("perfect", 1.0, 0, 0),
                ("excellent", 0.98, 10, 5),
                ("good", 0.92, 25, 15),
                ("fair", 0.85, 40, 30),
                ("poor", 0.70, 80, 60),
                ("beginner", 0.60, 120, 100)
            };
            
            foreach (var (level, accuracy, timingJitter, tempoVariation) in accuracyLevels)
            {
                var performance = CreatePerformance(score, accuracy, timingJitter, tempoVariation);
                SavePerformance(basePath, $"{scoreName}_{level}", performance);
            }
        }
    }
    
    // Score creation helpers
    private static SampleScore CreateScaleScore(string name, int[] pitches, double bpm)
    {
        var notes = pitches.Select((p, i) => new SampleNote
        {
            Pitch = p,
            Velocity = 80,
            StartTick = i * 480,
            DurationTicks = 480,
            Measure = (i / 4) + 1,
            Beat = (i % 4) + 1.0
        }).ToList();
        
        return new SampleScore
        {
            Title = $"Scale: {name}",
            Bpm = bpm,
            TimeSignature = "4/4",
            Notes = notes
        };
    }
    
    private static SampleScore CreateChordScore(string name, int[][] chords, double bpm)
    {
        var notes = new List<SampleNote>();
        int tick = 0;
        int measure = 1;
        
        foreach (var chord in chords)
        {
            foreach (var pitch in chord)
            {
                notes.Add(new SampleNote
                {
                    Pitch = pitch,
                    Velocity = 75,
                    StartTick = tick,
                    DurationTicks = 960,
                    Measure = measure,
                    Beat = 1.0
                });
            }
            tick += 960;
            measure++;
        }
        
        return new SampleScore
        {
            Title = $"Chords: {name}",
            Bpm = bpm,
            TimeSignature = "4/4",
            Notes = notes
        };
    }
    
    private static SampleScore CreateArpeggioScore(string name, int[] pattern, double bpm)
    {
        var notes = pattern.Select((p, i) => new SampleNote
        {
            Pitch = p,
            Velocity = 70,
            StartTick = i * 240,
            DurationTicks = 240,
            Measure = (i / 8) + 1,
            Beat = ((i % 8) / 2.0) + 1.0
        }).ToList();
        
        return new SampleScore
        {
            Title = $"Arpeggio: {name}",
            Bpm = bpm,
            TimeSignature = "4/4",
            Notes = notes
        };
    }
    
    private static SampleScore CreateMelodyScore(string name, (int pitch, double beats)[] melody, double bpm)
    {
        var notes = new List<SampleNote>();
        double currentBeat = 1.0;
        int currentMeasure = 1;
        long tick = 0;
        
        foreach (var (pitch, beats) in melody)
        {
            notes.Add(new SampleNote
            {
                Pitch = pitch,
                Velocity = 80,
                StartTick = tick,
                DurationTicks = (long)(beats * 480),
                Measure = currentMeasure,
                Beat = currentBeat
            });
            
            tick += (long)(beats * 480);
            currentBeat += beats;
            while (currentBeat > 4.0)
            {
                currentBeat -= 4.0;
                currentMeasure++;
            }
        }
        
        return new SampleScore
        {
            Title = name,
            Bpm = bpm,
            TimeSignature = "4/4",
            Notes = notes
        };
    }
    
    private static SampleScore CreateTwoVoiceScore(string name, 
        (int pitch, double beats)[] soprano, 
        (int pitch, double beats)[] bass, 
        double bpm)
    {
        var notes = new List<SampleNote>();
        
        // Add soprano voice
        long tick = 0;
        int measure = 1;
        double beat = 1.0;
        foreach (var (pitch, beats) in soprano)
        {
            notes.Add(new SampleNote
            {
                Pitch = pitch,
                Velocity = 85,
                StartTick = tick,
                DurationTicks = (long)(beats * 480),
                Measure = measure,
                Beat = beat,
                Voice = 1
            });
            tick += (long)(beats * 480);
            beat += beats;
            while (beat > 4.0) { beat -= 4.0; measure++; }
        }
        
        // Add bass voice
        tick = 0;
        measure = 1;
        beat = 1.0;
        foreach (var (pitch, beats) in bass)
        {
            notes.Add(new SampleNote
            {
                Pitch = pitch,
                Velocity = 70,
                StartTick = tick,
                DurationTicks = (long)(beats * 480),
                Measure = measure,
                Beat = beat,
                Voice = 2
            });
            tick += (long)(beats * 480);
            beat += beats;
            while (beat > 4.0) { beat -= 4.0; measure++; }
        }
        
        return new SampleScore
        {
            Title = name,
            Bpm = bpm,
            TimeSignature = "4/4",
            Notes = notes.OrderBy(n => n.StartTick).ThenBy(n => n.Pitch).ToList()
        };
    }
    
    private static SampleScore CreateMozartStyle(string name, double bpm)
    {
        var notes = new List<SampleNote>();
        
        // Simple melody
        var melody = new[] { 72, 74, 76, 77, 76, 74, 72, 71 };
        for (int i = 0; i < melody.Length; i++)
        {
            notes.Add(new SampleNote
            {
                Pitch = melody[i],
                Velocity = 85,
                StartTick = i * 480,
                DurationTicks = 480,
                Measure = (i / 4) + 1,
                Beat = (i % 4) + 1.0,
                Voice = 1
            });
        }
        
        // Alberti bass pattern
        var bassPattern = new[] { 60, 64, 67, 64 };
        for (int m = 0; m < 2; m++)
        {
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    notes.Add(new SampleNote
                    {
                        Pitch = bassPattern[j],
                        Velocity = 60,
                        StartTick = (m * 4 + i) * 480 + j * 120,
                        DurationTicks = 120,
                        Measure = m + 1,
                        Beat = i + 1.0 + (j * 0.25),
                        Voice = 2
                    });
                }
            }
        }
        
        return new SampleScore
        {
            Title = name,
            Bpm = bpm,
            TimeSignature = "4/4",
            Notes = notes.OrderBy(n => n.StartTick).ToList()
        };
    }
    
    private static SampleScore CreateChopinStyle(string name, double bpm)
    {
        var notes = new List<SampleNote>();
        
        // Long melody notes
        var melody = new[] { 72, 74, 76, 74, 72, 71, 72 };
        for (int i = 0; i < melody.Length; i++)
        {
            notes.Add(new SampleNote
            {
                Pitch = melody[i],
                Velocity = 75,
                StartTick = i * 960,
                DurationTicks = 900,
                Measure = (i / 2) + 1,
                Beat = (i % 2) * 2 + 1.0,
                Voice = 1
            });
        }
        
        // Rolling arpeggios in bass
        var bassNotes = new[] { 48, 55, 60, 64, 67, 64, 60, 55 };
        for (int m = 0; m < 4; m++)
        {
            for (int i = 0; i < bassNotes.Length; i++)
            {
                notes.Add(new SampleNote
                {
                    Pitch = bassNotes[i],
                    Velocity = 50,
                    StartTick = m * 1920 + i * 240,
                    DurationTicks = 480,
                    Measure = m + 1,
                    Beat = (i / 2.0) + 1.0,
                    Voice = 2
                });
            }
        }
        
        return new SampleScore
        {
            Title = name,
            Bpm = bpm,
            TimeSignature = "4/4",
            Notes = notes.OrderBy(n => n.StartTick).ToList()
        };
    }
    
    private static SamplePerformance CreatePerformance(SampleScore score, double accuracy, int timingJitterMs, int tempoVariationMs)
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var notes = new List<SamplePerformanceNote>();
        
        double msPerTick = (60000.0 / score.Bpm) / 480.0;
        
        foreach (var scoreNote in score.Notes)
        {
            // Decide if note is played correctly
            bool isPlayed = random.NextDouble() < accuracy;
            if (!isPlayed && random.NextDouble() > 0.5)
            {
                continue; // Skip this note (missed)
            }
            
            // Calculate timing with jitter
            double baseTimeMs = scoreNote.StartTick * msPerTick;
            double timingOffset = (random.NextDouble() * 2 - 1) * timingJitterMs;
            double tempoOffset = (random.NextDouble() * 2 - 1) * tempoVariationMs;
            
            // Determine pitch (possibly wrong)
            int pitch = scoreNote.Pitch;
            if (!isPlayed)
            {
                // Wrong note - either semitone off or octave error
                if (random.NextDouble() > 0.7)
                    pitch += 12 * (random.Next(2) * 2 - 1); // Octave error
                else
                    pitch += random.Next(2) * 2 - 1; // Semitone error
            }
            
            notes.Add(new SamplePerformanceNote
            {
                Pitch = Math.Clamp(pitch, 21, 108),
                Velocity = Math.Clamp(scoreNote.Velocity + random.Next(-15, 15), 1, 127),
                StartTimeMs = Math.Max(0, baseTimeMs + timingOffset + tempoOffset),
                DurationMs = scoreNote.DurationTicks * msPerTick * (0.8 + random.NextDouble() * 0.4)
            });
        }
        
        // Occasionally add extra notes
        if (accuracy < 0.9)
        {
            int extraNotes = (int)((1 - accuracy) * 5);
            for (int i = 0; i < extraNotes; i++)
            {
                double randomTime = random.NextDouble() * (score.Notes.Max(n => n.StartTick) * msPerTick);
                notes.Add(new SamplePerformanceNote
                {
                    Pitch = 60 + random.Next(24),
                    Velocity = 50 + random.Next(40),
                    StartTimeMs = randomTime,
                    DurationMs = 100 + random.Next(200)
                });
            }
        }
        
        return new SamplePerformance
        {
            ScoreTitle = score.Title,
            Notes = notes.OrderBy(n => n.StartTimeMs).ToList()
        };
    }
    
    private static void SaveScore(string basePath, string name, SampleScore score)
    {
        var path = Path.Combine(basePath, "scores", $"{name}.json");
        var json = JsonSerializer.Serialize(score, JsonOptions);
        File.WriteAllText(path, json);
    }
    
    private static void SavePerformance(string basePath, string name, SamplePerformance performance)
    {
        var path = Path.Combine(basePath, "performances", $"{name}.json");
        var json = JsonSerializer.Serialize(performance, JsonOptions);
        File.WriteAllText(path, json);
    }
}

// Sample data models
public class SampleScore
{
    public string Title { get; set; } = "";
    public double Bpm { get; set; } = 120;
    public string TimeSignature { get; set; } = "4/4";
    public int Ppq { get; set; } = 480;
    public List<SampleNote> Notes { get; set; } = new();
}

public class SampleNote
{
    public int Pitch { get; set; }
    public int Velocity { get; set; }
    public long StartTick { get; set; }
    public long DurationTicks { get; set; }
    public int Measure { get; set; }
    public double Beat { get; set; }
    public int Voice { get; set; } = 1;
}

public class SamplePerformance
{
    public string ScoreTitle { get; set; } = "";
    public List<SamplePerformanceNote> Notes { get; set; } = new();
}

public class SamplePerformanceNote
{
    public int Pitch { get; set; }
    public int Velocity { get; set; }
    public double StartTimeMs { get; set; }
    public double DurationMs { get; set; }
}

