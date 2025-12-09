# 🎹 Listening Brain - AI Piano Coach Evaluation Engine

## Executive Summary

The Listening Brain is a real-time polyphonic MIDI performance evaluation engine that compares a user's piano performance against a ground-truth score. It generates actionable feedback on **note accuracy**, **rhythmic precision**, **tempo stability**, and (future) **dynamics**.

---

## 🏗️ System Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           LISTENING BRAIN                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────────────────────┐  │
│  │  MIDI Input  │    │ Score Loader │    │     Alignment Engine         │  │
│  │  (Live/File) │    │  (SMF/MXL)   │    │  ┌─────────┐  ┌──────────┐  │  │
│  └──────┬───────┘    └──────┬───────┘    │  │   DTW   │  │Needleman │  │  │
│         │                   │            │  │         │  │ -Wunsch  │  │  │
│         ▼                   ▼            │  └────┬────┘  └────┬─────┘  │  │
│  ┌──────────────────────────────────┐    │       │            │        │  │
│  │      Event Normalizer            │    │       ▼            ▼        │  │
│  │  • Tick → Absolute Time          │    │  ┌──────────────────────┐   │  │
│  │  • Velocity Quantization         │────▶  │   Hybrid Aligner     │   │  │
│  │  • Note On/Off Pairing           │    │  │  (Multi-Voice Aware) │   │  │
│  └──────────────────────────────────┘    │  └──────────┬───────────┘   │  │
│                                          └─────────────┼───────────────┘  │
│                                                        │                   │
│                                                        ▼                   │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │                    EVALUATION PIPELINE                               │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌────────────┐  │  │
│  │  │    Note     │  │   Rhythm    │  │    Tempo    │  │  Dynamics  │  │  │
│  │  │  Accuracy   │  │  Precision  │  │  Stability  │  │  (Future)  │  │  │
│  │  │  Evaluator  │  │  Evaluator  │  │  Evaluator  │  │  Evaluator │  │  │
│  │  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘  └─────┬──────┘  │  │
│  │         │                │                │                │        │  │
│  │         └────────────────┼────────────────┼────────────────┘        │  │
│  │                          ▼                                          │  │
│  │              ┌───────────────────────┐                              │  │
│  │              │   Score Aggregator    │                              │  │
│  │              └───────────┬───────────┘                              │  │
│  └──────────────────────────┼──────────────────────────────────────────┘  │
│                             │                                              │
│                             ▼                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │                    FEEDBACK GENERATOR                                │  │
│  │  • Human-readable explanations                                       │  │
│  │  • Measure-by-measure breakdown                                      │  │
│  │  • Visual alignment data (for UI)                                    │  │
│  │  • Practice suggestions                                              │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 📁 Project Structure

```
music-brain/
├── src/
│   └── ListeningBrain/
│       ├── ListeningBrain.csproj          # Main library
│       ├── Core/
│       │   ├── Models/
│       │   │   ├── MidiNoteEvent.cs       # Unified note representation
│       │   │   ├── ScoreNote.cs           # Ground-truth note with metadata
│       │   │   ├── PerformanceNote.cs     # Played note with timing
│       │   │   ├── Score.cs               # Full score representation
│       │   │   ├── Performance.cs         # Full performance capture
│       │   │   └── TimeSignature.cs       # Musical time context
│       │   │
│       │   ├── Parsers/
│       │   │   ├── IMidiParser.cs         # Parser interface
│       │   │   ├── StandardMidiParser.cs  # SMF (Type 0/1) parser
│       │   │   └── LiveMidiStream.cs      # Real-time MIDI capture
│       │   │
│       │   └── Normalization/
│       │       ├── EventNormalizer.cs     # Tick→time, note pairing
│       │       ├── QuantizationGrid.cs    # Snap to musical grid
│       │       └── VelocityBuckets.cs     # Dynamic level mapping
│       │
│       ├── Alignment/
│       │   ├── IAlignmentStrategy.cs      # Strategy interface
│       │   ├── DynamicTimeWarping.cs      # DTW implementation
│       │   ├── NeedlemanWunsch.cs         # Sequence alignment
│       │   ├── HybridAligner.cs           # Combined approach
│       │   ├── AlignmentResult.cs         # Aligned note pairs
│       │   └── CostFunctions/
│       │       ├── PitchCost.cs           # Note matching cost
│       │       ├── TimingCost.cs          # Temporal distance
│       │       └── VoiceSeparation.cs     # Polyphonic voice tracking
│       │
│       ├── Evaluation/
│       │   ├── IEvaluator.cs              # Evaluator interface
│       │   ├── NoteAccuracyEvaluator.cs   # Pitch/note correctness
│       │   ├── RhythmEvaluator.cs         # Rhythmic precision
│       │   ├── TempoEvaluator.cs          # Tempo consistency
│       │   ├── DynamicsEvaluator.cs       # Velocity/expression (future)
│       │   ├── EvaluationResult.cs        # Per-note evaluation
│       │   └── AggregateScore.cs          # Overall performance score
│       │
│       ├── Feedback/
│       │   ├── FeedbackGenerator.cs       # Human-readable output
│       │   ├── FeedbackItem.cs            # Single feedback point
│       │   ├── MeasureReport.cs           # Per-measure breakdown
│       │   └── PracticeSuggestion.cs      # AI practice recommendations
│       │
│       └── Pipeline/
│           ├── EvaluationPipeline.cs      # Orchestrates full flow
│           ├── RealTimeEvaluator.cs       # Live performance mode
│           └── BatchEvaluator.cs          # Post-performance analysis
│
├── tests/
│   └── ListeningBrain.Tests/
│       ├── Alignment/
│       │   ├── DTWTests.cs
│       │   └── NeedlemanWunschTests.cs
│       ├── Evaluation/
│       │   ├── NoteAccuracyTests.cs
│       │   ├── RhythmTests.cs
│       │   └── TempoTests.cs
│       └── Integration/
│           └── FullPipelineTests.cs
│
├── samples/
│   ├── scores/                            # Test MIDI scores
│   └── performances/                      # Sample performances
│
├── docs/
│   ├── MIDI_PROTOCOL.md                   # MIDI reference
│   ├── ALIGNMENT_ALGORITHMS.md            # Algorithm deep-dive
│   └── MUSIC_THEORY_CONCEPTS.md           # Grace notes, triplets, etc.
│
├── ListeningBrain.sln                     # Visual Studio solution
└── ARCHITECTURE.md                        # This file
```

---

## 🎼 Core Concepts

### MIDI Protocol Fundamentals

```
┌─────────────────────────────────────────────────────────────────┐
│                    MIDI TIME STRUCTURE                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  PPQ (Pulses Per Quarter Note) = 480 (typical)                 │
│                                                                 │
│  Quarter Note = 480 ticks                                       │
│  Half Note    = 960 ticks                                       │
│  Eighth Note  = 240 ticks                                       │
│  16th Note    = 120 ticks                                       │
│  Triplet 8th  = 160 ticks (480 ÷ 3)                            │
│                                                                 │
│  Tempo: µs per quarter = 500000 (120 BPM default)              │
│  BPM = 60,000,000 ÷ µs_per_quarter                             │
│                                                                 │
│  Absolute Time (ms) = (ticks ÷ PPQ) × (µs_per_quarter ÷ 1000)  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Note Event Structure

```csharp
// Core MIDI note representation
public record MidiNoteEvent
{
    public int Pitch { get; init; }           // 0-127 (60 = Middle C)
    public int Velocity { get; init; }        // 0-127 (0 = note off)
    public long StartTick { get; init; }      // Absolute tick position
    public long DurationTicks { get; init; }  // Note length in ticks
    public double StartTimeMs { get; init; }  // Computed absolute time
    public double DurationMs { get; init; }   // Computed duration
    public int Channel { get; init; }         // MIDI channel 0-15
    public int Voice { get; init; }           // Assigned voice (for polyphony)
}
```

---

## 🔬 Alignment Algorithms

### Why Alignment is Critical

A student doesn't play in perfect sync with a score. They may:
- Start late (pickup measure confusion)
- Speed up or slow down (tempo drift)
- Add notes (embellishments)
- Miss notes (errors)
- Hold notes longer/shorter

We need to **align** the performance to the score to know which played note corresponds to which expected note.

### Algorithm Comparison

| Algorithm | Best For | Complexity | Handles Tempo Changes |
|-----------|----------|------------|----------------------|
| **DTW** (Dynamic Time Warping) | Continuous tempo drift | O(n×m) | ✅ Excellent |
| **Needleman-Wunsch** | Note insertion/deletion | O(n×m) | ⚠️ Moderate |
| **LCS** (Longest Common Subsequence) | Finding correct notes | O(n×m) | ❌ Poor |
| **Hybrid** (Our approach) | Real-world performance | O(n×m) | ✅ Excellent |

### Hybrid Alignment Strategy

```
┌────────────────────────────────────────────────────────────────────┐
│                    HYBRID ALIGNMENT FLOW                           │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  1. VOICE SEPARATION                                               │
│     ─────────────────                                              │
│     • Separate score into voices (soprano, alto, tenor, bass)      │
│     • Use pitch range + temporal overlap detection                 │
│     • Handle crossed voices (e.g., tenor above alto)               │
│                                                                    │
│  2. COARSE DTW ALIGNMENT                                           │
│     ───────────────────────                                        │
│     • Align performance time to score time                         │
│     • Create warping path for tempo mapping                        │
│     • Use pitch-class chroma features (octave-invariant)           │
│                                                                    │
│  3. FINE-GRAINED NEEDLEMAN-WUNSCH                                  │
│     ─────────────────────────────────                              │
│     • Per-voice alignment using DTW time mapping                   │
│     • Match individual notes with gap penalties                    │
│     • Score = pitch_match × timing_proximity × velocity_similarity │
│                                                                    │
│  4. RESULT CONSOLIDATION                                           │
│     ───────────────────────                                        │
│     • Merge voice alignments                                       │
│     • Resolve conflicts (one played note → one score note)         │
│     • Mark unmatched notes as extra/missed                         │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

### DTW Cost Matrix Example

```
Score:      C4  E4  G4  C5
            ──────────────────►
Performance ┌────┬────┬────┬────┐
    C4      │  0 │ 4  │ 7  │ 12 │ ◄─ pitch distance
    E4      │  4 │ 0  │ 3  │  8 │
    F4      │  5 │ 1  │ 2  │  7 │ ◄─ wrong note!
    G4      │  7 │ 3  │ 0  │  5 │
    C5      │ 12 │ 8  │ 5  │  0 │
            └────┴────┴────┴────┘
                        │
                        ▼
            Optimal path: C4→C4, E4→E4, F4→(miss G4), G4→G4, C5→C5
```

---

## 📊 Evaluation Metrics

### 1. Note Accuracy Evaluator

**Measures**: Did the student play the correct pitches?

```csharp
public class NoteAccuracyResult
{
    public double OverallAccuracy { get; }      // 0.0 - 1.0
    public int CorrectNotes { get; }            // Exact pitch matches
    public int WrongNotes { get; }              // Wrong pitch played
    public int MissedNotes { get; }             // Score note not played
    public int ExtraNotes { get; }              // Played note not in score
    public List<NoteError> Errors { get; }      // Detailed error list
}

public enum NoteErrorType
{
    Correct,           // Perfect match
    WrongPitch,        // Different note played
    WrongOctave,       // Right note class, wrong octave
    Missed,            // Note not played
    Extra,             // Note played but not expected
    EnharmonicMatch    // C# played as Db (correct, different name)
}
```

**Scoring Formula**:
```
accuracy = correct_notes / total_expected_notes

penalty_weights:
  - wrong_pitch: -1.0 (full penalty)
  - wrong_octave: -0.3 (partial credit)
  - missed: -1.0 (full penalty)
  - extra: -0.2 (minor penalty)
```

### 2. Rhythm Evaluator

**Measures**: Did the student play at the right time relative to the beat?

```csharp
public class RhythmResult
{
    public double RhythmScore { get; }          // 0.0 - 1.0
    public double MeanTimingError { get; }      // Average ms deviation
    public double TimingStdDev { get; }         // Consistency measure
    public List<TimingError> Errors { get; }    // Per-note timing
}

public record TimingError(
    ScoreNote Expected,
    PerformanceNote Actual,
    double DeviationMs,           // Positive = late, Negative = early
    double DeviationBeats,        // Deviation in musical beats
    RhythmErrorSeverity Severity  // Early/Late/VeryEarly/VeryLate
);
```

**Timing Tolerance Thresholds** (configurable):
```
                    ◄──────────── Beat ────────────►
                              │
   Very Early   Early   On Time   Late    Very Late
   ────────────────────────────────────────────────
    < -100ms   -100    -30│+30   +100     > +100ms
                  │       │       │
              -0.25    0.00    +0.25 beats (at 120 BPM)
```

### 3. Tempo Evaluator

**Measures**: Did the student maintain a consistent tempo?

```csharp
public class TempoResult
{
    public double TempoStability { get; }       // 0.0 - 1.0
    public double AverageBPM { get; }           // Detected avg tempo
    public double ExpectedBPM { get; }          // Score tempo
    public double TempoDeviation { get; }       // % difference
    public List<TempoSegment> Segments { get; } // Tempo over time
}

public record TempoSegment(
    int MeasureStart,
    int MeasureEnd,
    double BPM,
    double Deviation,           // vs expected
    TempoTrend Trend            // Accelerating/Decelerating/Steady
);
```

**Tempo Stability Calculation**:
```
For each pair of consecutive notes:
  inter_onset_interval = note[i+1].time - note[i].time
  expected_interval = score[i+1].time - score[i].time
  
tempo_ratio = performance_IOI / expected_IOI
stability = 1 - std_dev(tempo_ratios)
```

### 4. Dynamics Evaluator (Future)

**Measures**: Did the student play with appropriate dynamics/expression?

```csharp
public class DynamicsResult
{
    public double DynamicsScore { get; }
    public Dictionary<DynamicLevel, int> Distribution { get; }
    public List<DynamicsError> Errors { get; }
}

public enum DynamicLevel
{
    Pianissimo,    // pp: 1-31
    Piano,         // p:  32-47
    MezzoPiano,    // mp: 48-63
    MezzoForte,    // mf: 64-79
    Forte,         // f:  80-95
    Fortissimo     // ff: 96-127
}
```

---

## 🎯 Handling Musical Edge Cases

### Grace Notes

Grace notes are ornamental notes played quickly before the main note. They don't have strict timing.

```csharp
public record ScoreNote
{
    // ... other properties
    public bool IsGraceNote { get; init; }
    public GraceNoteType GraceType { get; init; }  // Acciaccatura, Appoggiatura
    public ScoreNote? ParentNote { get; init; }    // The "real" note it decorates
}

// Evaluation strategy:
// - Grace notes have relaxed timing (±50% of their duration)
// - If missed, penalty is reduced (0.3x normal penalty)
// - If played but parent missed, no grace note credit
```

### Triplets and Tuplets

Triplets divide the beat into 3 instead of 2. Must handle irrational timing.

```csharp
public record TupletGroup
{
    public int ActualNotes { get; }     // e.g., 3 (triplet)
    public int NormalNotes { get; }     // e.g., 2 (normal division)
    public List<ScoreNote> Notes { get; }
}

// Time calculation:
// triplet_eighth_duration = quarter_note_duration / 3
// = 480 ticks / 3 = 160 ticks (at PPQ=480)
```

### Pickup Measures (Anacrusis)

Pieces often start before beat 1 of measure 1.

```csharp
public class Score
{
    public int PickupBeats { get; }       // e.g., 1 beat before downbeat
    public long FirstDownbeatTick { get; } // When measure 1, beat 1 actually is
}

// Alignment strategy:
// 1. Detect if student started at pickup or downbeat
// 2. Adjust time offset accordingly before alignment
// 3. Don't penalize waiting for downbeat if pickup is optional
```

### Pedal Events

Sustain pedal affects note duration perception.

```csharp
public record PedalEvent
{
    public long Tick { get; init; }
    public bool IsPressed { get; init; }  // true = down, false = up
    public int Value { get; init; }       // 0-127 (half-pedal support)
}

// When pedal is down:
// - Note durations extend to pedal release
// - Overlapping notes are acceptable
// - Evaluation should account for sustained harmonies
```

---

## 🔄 Real-Time vs Batch Processing

### Real-Time Mode

For live feedback during practice:

```csharp
public class RealTimeEvaluator
{
    private readonly Queue<PerformanceNote> _buffer = new();
    private readonly Score _score;
    private int _currentPosition = 0;
    
    public void OnNoteReceived(MidiNoteEvent note)
    {
        _buffer.Enqueue(ToPerformanceNote(note));
        
        if (_buffer.Count >= MinBufferSize)
        {
            var feedback = EvaluateWindow();
            OnFeedbackAvailable?.Invoke(feedback);
        }
    }
    
    // Uses sliding window + local alignment
    // Latency target: < 100ms from note played to feedback
}
```

### Batch Mode

For post-performance analysis:

```csharp
public class BatchEvaluator
{
    public FullEvaluationResult Evaluate(Score score, Performance performance)
    {
        // 1. Full alignment (can use expensive algorithms)
        var alignment = _aligner.Align(score, performance);
        
        // 2. Comprehensive evaluation
        var noteResult = _noteEvaluator.Evaluate(alignment);
        var rhythmResult = _rhythmEvaluator.Evaluate(alignment);
        var tempoResult = _tempoEvaluator.Evaluate(alignment);
        
        // 3. Generate detailed feedback
        return _feedbackGenerator.Generate(noteResult, rhythmResult, tempoResult);
    }
}
```

---

## 🛠️ Integration Points

### Unity Integration (C#)

```csharp
// Unity MonoBehaviour wrapper
public class PianoCoachBrain : MonoBehaviour
{
    private RealTimeEvaluator _evaluator;
    private Score _currentScore;
    
    void Start()
    {
        _evaluator = new RealTimeEvaluator();
        _evaluator.OnFeedbackAvailable += HandleFeedback;
    }
    
    // Called from MIDI input handler
    public void OnMidiNoteOn(int pitch, int velocity)
    {
        _evaluator.OnNoteReceived(new MidiNoteEvent
        {
            Pitch = pitch,
            Velocity = velocity,
            StartTimeMs = Time.timeAsDouble * 1000
        });
    }
    
    private void HandleFeedback(FeedbackItem feedback)
    {
        // Update UI, play sounds, show visual indicators
        UIManager.ShowFeedback(feedback);
    }
}
```

### MIDI Input (Platform-Specific)

```csharp
// Windows: Use NAudio or Windows.Devices.Midi
// macOS: Use CoreMIDI via P/Invoke
// Cross-platform: Consider RtMidi wrapper

public interface IMidiInput
{
    event Action<MidiNoteEvent> OnNoteOn;
    event Action<MidiNoteEvent> OnNoteOff;
    event Action<int> OnSustainPedal;
    void Start();
    void Stop();
}
```

---

## 📈 Performance Considerations

| Operation | Target Latency | Strategy |
|-----------|---------------|----------|
| Note reception | < 1ms | Direct callback, no allocation |
| Window evaluation | < 50ms | Pre-allocated buffers, incremental alignment |
| Full alignment | < 500ms | Background thread, chunked processing |
| Feedback generation | < 20ms | Template-based, string pooling |

### Memory Optimization

```csharp
// Use object pooling for frequent allocations
private readonly ObjectPool<PerformanceNote> _notePool = new();

// Pre-allocate alignment matrices
private readonly float[,] _dtwMatrix = new float[MaxScoreLength, MaxPerfLength];

// Use Span<T> for hot paths
public void ProcessBuffer(Span<MidiNoteEvent> events) { ... }
```

---

## 🧪 Testing Strategy

### Unit Tests

```csharp
[TestFixture]
public class DTWTests
{
    [Test]
    public void Align_IdenticalSequences_ReturnsExactMatch()
    {
        var score = CreateSimpleScale();        // C D E F G
        var performance = CreateSimpleScale();  // C D E F G
        
        var result = _aligner.Align(score, performance);
        
        Assert.That(result.Pairs, Has.Count.EqualTo(5));
        Assert.That(result.Pairs.All(p => p.IsExactMatch));
    }
    
    [Test]
    public void Align_MissedNote_DetectsGap()
    {
        var score = CreateSimpleScale();        // C D E F G
        var performance = CreateNotes("C", "D", "F", "G");  // Missing E
        
        var result = _aligner.Align(score, performance);
        
        Assert.That(result.MissedNotes, Has.Count.EqualTo(1));
        Assert.That(result.MissedNotes[0].Pitch, Is.EqualTo(64)); // E
    }
}
```

### Integration Tests

```csharp
[TestFixture]
public class FullPipelineTests
{
    [Test]
    public void Evaluate_RealWorldPerformance_GeneratesMeaningfulFeedback()
    {
        var score = LoadMidiFile("scores/twinkle_twinkle.mid");
        var performance = LoadMidiFile("performances/twinkle_student.mid");
        
        var result = _pipeline.Evaluate(score, performance);
        
        Assert.That(result.NoteAccuracy.OverallAccuracy, Is.GreaterThan(0.7));
        Assert.That(result.Feedback, Is.Not.Empty);
        Assert.That(result.Feedback[0].Message, Contains.Substring("measure"));
    }
}
```

---

## 🚀 Roadmap

### Phase 1: Core Engine (MVP)
- [x] Architecture design
- [ ] MIDI parsing (DryWetMidi integration)
- [ ] Basic DTW alignment
- [ ] Note accuracy evaluation
- [ ] Simple feedback generation

### Phase 2: Rhythm & Tempo
- [ ] Rhythm evaluator with beat tracking
- [ ] Tempo stability analysis
- [ ] Real-time mode implementation
- [ ] Unity integration prototype

### Phase 3: Advanced Features
- [ ] Polyphonic voice separation
- [ ] Grace note handling
- [ ] Tuplet detection
- [ ] Dynamics evaluation

### Phase 4: Intelligence
- [ ] ML-based alignment refinement
- [ ] Personalized feedback based on history
- [ ] Practice suggestion engine
- [ ] Difficulty progression tracking

---

## 📚 References

- [DryWetMidi Documentation](https://melanchall.github.io/drywetmidi/)
- [Dynamic Time Warping Paper](https://www.cs.ucr.edu/~eamonn/DTW_myths.pdf)
- [Music Information Retrieval](https://musicinformationretrieval.com/)
- [MIDI Association Specifications](https://www.midi.org/specifications)

