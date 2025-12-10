# 🎹 Listening Brain - AI Piano Coach Evaluation Engine

**The "ears" of your AI Piano Coach** - a C# evaluation engine that compares live polyphonic MIDI performances against ground-truth scores to generate feedback on note accuracy, rhythmic precision, and tempo stability.

## 🌟 Features

### Core Evaluation
- **Note Accuracy Evaluation** - Detects wrong notes, missed notes, extra notes, and octave errors
- **Rhythm Precision Analysis** - Measures timing accuracy relative to the beat with configurable thresholds
- **Tempo Stability Tracking** - Monitors tempo consistency and detects rushing/dragging
- **Dynamics & Expression** - Velocity curves, articulation, pedaling, phrase shaping
- **Real-Time Feedback** - Live evaluation during performance with immediate feedback
- **Polyphonic Alignment** - Handles multi-voice piano music with voice separation

### Intelligence & Learning
- **Error Pattern Recognition** - Identifies recurring mistakes (intervals, rhythm, leaps, chords)
- **Student Skill Profiling** - Tracks 10 skill dimensions with progress history
- **Personalized Feedback** - Tailored suggestions based on learning history
- **Adaptive Difficulty** - Automatic assessment and piece recommendations

### Practice Management
- **Session Tracking** - Records practice sessions with detailed statistics
- **Progress Visualization** - Timelines, skill radar charts, weekly reports
- **Goal Management** - Smart goals with achievement tracking and milestones
- **Repertoire System** - Piece tracking, suggestions, and practice priorities
- **Spaced Repetition** - SM-2 algorithm for efficient passage review scheduling
- **Difficulty Progression** - Structured curriculum paths with level assessments

## 🏗️ Architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│                    EVALUATION PIPELINE                                    │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  MIDI Input ──► Alignment Engine ──► Evaluators ──► Feedback Generator  │
│                 (DTW + Needleman-    (Accuracy,      (Reports,          │
│                  Wunsch Hybrid)       Rhythm,        Suggestions)        │
│                                       Tempo)                             │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

See [ARCHITECTURE.md](./ARCHITECTURE.md) for detailed system design.

## 📦 Installation

### Prerequisites
- .NET 8.0 SDK or later
- Visual Studio 2022 / VS Code / JetBrains Rider

### Build from Source
```bash
cd music-brain
dotnet restore
dotnet build
```

### Run Tests
```bash
dotnet test
```

## 🚀 Quick Start

### Basic Usage

```csharp
using ListeningBrain.Pipeline;
using ListeningBrain.Core.Parsers;

// Load a MIDI score
var parser = new StandardMidiParser();
var score = parser.ParseFile("path/to/score.mid");

// Create a performance (from live input or MIDI file)
var performance = // ... your performance data

// Evaluate
var pipeline = new EvaluationPipeline();
var result = pipeline.Evaluate(score, performance);

// Get results
Console.WriteLine($"Overall Score: {result.OverallScore:F1}% ({result.Grade})");
Console.WriteLine($"Note Accuracy: {result.NoteAccuracy.Score:F1}%");
Console.WriteLine($"Rhythm: {result.Rhythm.Score:F1}%");
Console.WriteLine($"Tempo: {result.Tempo.Score:F1}%");
Console.WriteLine();
Console.WriteLine(result.Feedback.Summary);
```

### Real-Time Evaluation

```csharp
using ListeningBrain.Pipeline;

// Create real-time evaluator with score
var evaluator = new RealTimeEvaluator(score);
evaluator.OnFeedbackAvailable += feedback => 
{
    Console.WriteLine($"[M{feedback.CurrentMeasure}] {feedback.LocalAccuracy:F0}%");
    foreach (var issue in feedback.Issues)
        Console.WriteLine($"  ⚠ {issue}");
};
evaluator.OnErrorDetected += error =>
{
    Console.WriteLine($"❌ {error.Message}");
};

// Start evaluation session
evaluator.Start();

// Process MIDI events as they arrive
midiInput.OnNoteOn += (pitch, velocity) =>
{
    evaluator.OnNoteOn(pitch, velocity);
};

// Get final evaluation
var finalResult = evaluator.GetFinalEvaluation();
```

### Difficulty Presets

```csharp
// For beginners - lenient thresholds
var beginnerPipeline = EvaluationPipeline.ForBeginners();

// For advanced players - strict thresholds
var advancedPipeline = EvaluationPipeline.ForAdvanced();

// Custom configuration
var customPipeline = new EvaluationPipeline
{
    AlignmentOptions = new AlignmentOptions
    {
        MaxTimingDeviationMs = 200,
        GapPenalty = 0.8,
        AllowOctaveErrors = true
    }
};
```

## 📊 Evaluation Metrics

### Note Accuracy
- **Correct Notes** - Exact pitch matches
- **Wrong Notes** - Different pitch played
- **Missed Notes** - Score note not played
- **Extra Notes** - Played but not in score
- **Octave Errors** - Right note, wrong octave (partial credit)

### Rhythm
- **Mean Timing Error** - Average deviation from expected timing
- **Standard Deviation** - Consistency of timing
- **On-Time Percentage** - Notes within timing tolerance
- **Rush/Drag Detection** - Systematic timing bias

### Tempo
- **Tempo Deviation** - Difference from marked tempo
- **Tempo Stability** - Consistency over time
- **Drift Detection** - Accelerando/ritardando trends

## 🔧 Alignment Algorithms

The Listening Brain uses a **hybrid alignment approach**:

1. **Dynamic Time Warping (DTW)** - Handles tempo rubato and continuous tempo changes
2. **Needleman-Wunsch** - Optimal for detecting insertions/deletions (missed/extra notes)
3. **Voice Separation** - Separates polyphonic music into voices for independent alignment

## 🧪 Testing

```bash
# Run all tests
dotnet test

# Run specific test category
dotnet test --filter "FullyQualifiedName~Alignment"
dotnet test --filter "FullyQualifiedName~Evaluation"
dotnet test --filter "FullyQualifiedName~Integration"
```

## 📁 Project Structure

```
music-brain/
├── src/ListeningBrain/
│   ├── Core/
│   │   ├── Models/          # Data models (MidiNoteEvent, Score, Performance)
│   │   └── Parsers/         # MIDI file parsing (DryWetMidi)
│   ├── Alignment/           # DTW, Needleman-Wunsch, Hybrid aligner
│   ├── Evaluation/          # Note accuracy, rhythm, tempo, dynamics, expression
│   ├── Feedback/            # Human-readable feedback generation
│   ├── Intelligence/        # Error patterns, student profiles, adaptive learning
│   ├── Practice/            # Session tracking, progress, goals, spaced repetition
│   └── Pipeline/            # Orchestration (batch & real-time)
├── tests/ListeningBrain.Tests/
│   ├── Alignment/
│   ├── Evaluation/
│   └── Integration/
└── docs/
```

## 🎼 Music Theory Concepts Handled

- **Grace Notes** (acciaccatura, appoggiatura) - Relaxed timing tolerance
- **Triplets & Tuplets** - Correct tick calculation
- **Pickup Measures** (anacrusis) - Proper alignment offset
- **Pedal Events** - Sustain affects note duration perception
- **Polyphonic Voices** - SATB-style voice separation

## 🔌 Unity Integration

```csharp
// Unity MonoBehaviour example
public class PianoCoachBrain : MonoBehaviour
{
    private RealTimeEvaluator _evaluator;
    
    void Start()
    {
        var score = LoadCurrentScore();
        _evaluator = new RealTimeEvaluator(score);
        _evaluator.OnFeedbackAvailable += HandleFeedback;
        _evaluator.Start();
    }
    
    // Called from your MIDI input handler
    public void OnMidiNoteOn(int pitch, int velocity)
    {
        _evaluator.OnNoteOn(pitch, velocity);
    }
    
    private void HandleFeedback(RealTimeFeedback feedback)
    {
        // Update UI, play sound effects, show visual indicators
        UIManager.Instance.ShowFeedback(feedback);
    }
}
```

## 📈 Performance

| Operation | Target | Notes |
|-----------|--------|-------|
| Note reception | < 1ms | Direct callback |
| Real-time eval | < 50ms | Sliding window |
| Full alignment | < 500ms | Background thread |
| Feedback gen | < 20ms | Template-based |

## 🗺️ Roadmap

### ✅ Phase 1: Core Engine (Complete)
- [x] Core alignment algorithms (DTW, Needleman-Wunsch, Hybrid)
- [x] Note accuracy evaluation with octave error detection
- [x] Rhythm evaluation with configurable thresholds
- [x] Tempo stability analysis with drift detection
- [x] Real-time evaluation mode with sliding window
- [x] Human-readable feedback generation
- [x] Practice suggestions engine
- [x] MIDI file parsing (DryWetMidi integration)
- [x] Polyphonic voice separation
- [x] Grace note and tuplet handling
- [x] Sample data generation (260+ test files)
- [x] Classic orchestration samples (Bach, Mozart, Chopin, Beethoven, Debussy)

### ✅ Phase 2: Dynamics & Expression (Complete)
- [x] Dynamics evaluation (velocity curves, crescendo/diminuendo detection)
- [x] Articulation detection (staccato, legato, accents, duration analysis)
- [x] Pedal usage analysis (timing, clarity, harmony-aware)
- [x] Phrase shaping evaluation (dynamic arcs, breathing, musicality)
- [x] Expression marking compliance (dynamic level adherence)

### ✅ Phase 3: Intelligence & Learning (Complete)
- [x] ML-enhanced alignment (neural sequence matching with trainable weights)
- [x] Error pattern recognition (interval, rhythm, leap, chord, passage patterns)
- [x] Personalized feedback based on learning history
- [x] Adaptive difficulty assessment with piece recommendations
- [x] Student skill profiling (10 skill dimensions, progress tracking)

### ✅ Phase 4: Practice Management (Complete)
- [x] Practice session history tracking (sessions, attempts, statistics)
- [x] Progress visualization over time (timelines, skill radar, weekly reports)
- [x] Difficulty progression system (curriculum paths, level assessment)
- [x] Repertoire management (piece tracking, suggestions, priorities)
- [x] Goal setting and achievement tracking (smart goals, milestones)
- [x] Spaced repetition for problem passages (SM-2 algorithm, scheduling)

### 🎯 Phase 5: Advanced Features
- [ ] Sight-reading mode (score display sync)
- [ ] Accompaniment mode (play-along)
- [ ] Recording and playback with annotations
- [ ] Multi-user/teacher review system
- [ ] Cloud sync and backup
- [ ] Mobile companion app integration

## 📄 License

MIT License - see LICENSE file for details.

## 🙏 Acknowledgments

- [DryWetMidi](https://github.com/melanchall/drywetmidi) - Excellent MIDI library
- [MathNet.Numerics](https://github.com/mathnet/mathnet-numerics) - Numerical computing
- Music Information Retrieval research community

