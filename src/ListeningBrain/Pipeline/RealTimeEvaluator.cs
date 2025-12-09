using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;
using ListeningBrain.Evaluation;
using ListeningBrain.Feedback;

namespace ListeningBrain.Pipeline;

/// <summary>
/// Real-time evaluator for live performance feedback.
/// Uses a sliding window approach to provide immediate feedback.
/// </summary>
public class RealTimeEvaluator : IDisposable
{
    private readonly Score _score;
    private readonly PerformanceBuilder _performanceBuilder;
    private readonly DynamicTimeWarping _aligner;
    private readonly NoteAccuracyEvaluator _noteEvaluator;
    private readonly FeedbackGenerator _feedbackGenerator;
    
    private readonly Queue<PerformanceNote> _noteBuffer;
    private readonly List<RealTimeFeedback> _feedbackHistory;
    private readonly object _lock = new();
    
    private int _currentScorePosition;
    private double _performanceStartTime;
    private bool _isStarted;
    private DateTime _lastFeedbackTime;
    
    /// <summary>
    /// Minimum buffer size before evaluation.
    /// </summary>
    public int MinBufferSize { get; init; } = 4;
    
    /// <summary>
    /// Maximum lookahead in score notes.
    /// </summary>
    public int ScoreLookahead { get; init; } = 8;
    
    /// <summary>
    /// Minimum interval between feedback events.
    /// </summary>
    public TimeSpan MinFeedbackInterval { get; init; } = TimeSpan.FromMilliseconds(500);
    
    /// <summary>
    /// Event fired when new feedback is available.
    /// </summary>
    public event Action<RealTimeFeedback>? OnFeedbackAvailable;
    
    /// <summary>
    /// Event fired when a significant error is detected.
    /// </summary>
    public event Action<RealTimeError>? OnErrorDetected;
    
    /// <summary>
    /// Creates a new real-time evaluator for a score.
    /// </summary>
    public RealTimeEvaluator(Score score)
    {
        _score = score;
        _performanceBuilder = new PerformanceBuilder();
        _aligner = new DynamicTimeWarping();
        _noteEvaluator = new NoteAccuracyEvaluator();
        _feedbackGenerator = new FeedbackGenerator();
        _noteBuffer = new Queue<PerformanceNote>();
        _feedbackHistory = [];
    }
    
    /// <summary>
    /// Starts the evaluation session.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            _isStarted = true;
            _currentScorePosition = 0;
            _performanceStartTime = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            _lastFeedbackTime = DateTime.MinValue;
        }
    }
    
    /// <summary>
    /// Processes a note-on event.
    /// </summary>
    /// <returns>Handle to complete the note on note-off.</returns>
    public NoteHandle OnNoteOn(int pitch, int velocity, double? timeMs = null)
    {
        timeMs ??= GetCurrentTimeMs();
        var handle = _performanceBuilder.AddNoteOn(pitch, velocity, timeMs.Value);
        
        // Add to buffer for real-time evaluation
        lock (_lock)
        {
            var note = new PerformanceNote
            {
                Id = Guid.NewGuid(),
                Pitch = pitch,
                Velocity = velocity,
                StartTick = 0,
                DurationTicks = 0,
                StartTimeMs = timeMs.Value,
                DurationMs = 0,
                ReceivedTimestamp = DateTime.UtcNow
            };
            
            _noteBuffer.Enqueue(note);
            
            // Evaluate if we have enough notes
            if (_noteBuffer.Count >= MinBufferSize)
            {
                EvaluateBuffer();
            }
            
            // Quick error detection for wrong notes
            DetectImmediateErrors(note);
        }
        
        return handle;
    }
    
    /// <summary>
    /// Processes a sustain pedal event.
    /// </summary>
    public void OnSustainPedal(int value, double? timeMs = null)
    {
        timeMs ??= GetCurrentTimeMs();
        _performanceBuilder.AddSustainPedal(timeMs.Value, value);
    }
    
    /// <summary>
    /// Gets the final evaluation after the performance ends.
    /// </summary>
    public FullEvaluationResult GetFinalEvaluation()
    {
        var performance = _performanceBuilder.Build();
        var pipeline = new EvaluationPipeline();
        return pipeline.Evaluate(_score, performance);
    }
    
    /// <summary>
    /// Gets current progress information.
    /// </summary>
    public RealTimeProgress GetProgress()
    {
        lock (_lock)
        {
            var currentScoreTime = _score.Notes.ElementAtOrDefault(_currentScorePosition)?.StartTimeMs ?? 0;
            var totalTime = _score.TotalDurationMs;
            
            return new RealTimeProgress
            {
                CurrentMeasure = _score.Notes.ElementAtOrDefault(_currentScorePosition)?.Measure ?? 1,
                CurrentScorePosition = _currentScorePosition,
                TotalScoreNotes = _score.Notes.Count,
                ProgressPercent = totalTime > 0 ? currentScoreTime / totalTime * 100 : 0,
                NotesPlayedSoFar = _noteBuffer.Count,
                FeedbackHistory = _feedbackHistory.TakeLast(10).ToList()
            };
        }
    }
    
    private void EvaluateBuffer()
    {
        if (DateTime.UtcNow - _lastFeedbackTime < MinFeedbackInterval)
            return;
        
        var bufferNotes = _noteBuffer.ToList();
        
        // Get expected notes around current position
        var expectedNotes = GetExpectedNotes();
        
        if (expectedNotes.Count == 0) return;
        
        // Create mini score and performance for local alignment
        var miniScore = new Score { Notes = expectedNotes, PPQ = _score.PPQ };
        var miniPerf = new Performance { Notes = bufferNotes };
        
        // Align
        var alignment = _aligner.Align(miniScore, miniPerf, AlignmentOptions.Default);
        
        // Generate feedback
        var feedback = GenerateRealTimeFeedback(alignment, expectedNotes, bufferNotes);
        
        if (feedback.HasContent)
        {
            _feedbackHistory.Add(feedback);
            _lastFeedbackTime = DateTime.UtcNow;
            OnFeedbackAvailable?.Invoke(feedback);
        }
        
        // Update score position
        UpdateScorePosition(alignment);
        
        // Clear processed notes from buffer
        while (_noteBuffer.Count > MinBufferSize / 2)
        {
            _noteBuffer.Dequeue();
        }
    }
    
    private List<ScoreNote> GetExpectedNotes()
    {
        int start = Math.Max(0, _currentScorePosition - 2);
        int end = Math.Min(_score.Notes.Count, _currentScorePosition + ScoreLookahead);
        
        return _score.Notes.Skip(start).Take(end - start).ToList();
    }
    
    private void DetectImmediateErrors(PerformanceNote note)
    {
        // Quick check: is this note anywhere near what's expected?
        var expected = GetExpectedNotes();
        
        var nearbyExpected = expected
            .Where(e => Math.Abs(e.StartTimeMs - note.StartTimeMs) < 500)
            .ToList();
        
        // If no match at all in nearby notes, it might be wrong
        if (nearbyExpected.Any() && !nearbyExpected.Any(e => e.Pitch == note.Pitch || e.PitchClass == note.PitchClass))
        {
            var closestExpected = nearbyExpected
                .OrderBy(e => Math.Abs(e.StartTimeMs - note.StartTimeMs))
                .First();
            
            var error = new RealTimeError
            {
                Type = RealTimeErrorType.WrongNote,
                Message = $"Expected {closestExpected.NoteName}, played {note.NoteName}",
                ExpectedPitch = closestExpected.Pitch,
                PlayedPitch = note.Pitch,
                Measure = closestExpected.Measure,
                Timestamp = DateTime.UtcNow
            };
            
            OnErrorDetected?.Invoke(error);
        }
    }
    
    private RealTimeFeedback GenerateRealTimeFeedback(
        AlignmentResult alignment,
        List<ScoreNote> expected,
        List<PerformanceNote> played)
    {
        int correctCount = alignment.Pairs.Count(p => p.IsExactPitchMatch);
        int totalExpected = expected.Count;
        double accuracy = totalExpected > 0 ? (double)correctCount / totalExpected * 100 : 100;
        
        var issues = new List<string>();
        
        foreach (var missed in alignment.MissedNotes.Take(2))
        {
            issues.Add($"Missed {missed.ExpectedNote.NoteName}");
        }
        
        foreach (var pair in alignment.Pairs.Where(p => !p.IsExactPitchMatch).Take(2))
        {
            issues.Add($"Wrong: {pair.PerformanceNote.NoteName} vs {pair.ScoreNote.NoteName}");
        }
        
        // Timing feedback
        var avgTiming = alignment.Pairs.Count > 0 
            ? alignment.Pairs.Average(p => p.TimingDeviationMs) 
            : 0;
        
        string timingMessage = "";
        if (avgTiming < -50)
            timingMessage = "Rushing - slow down";
        else if (avgTiming > 50)
            timingMessage = "Dragging - keep up";
        
        return new RealTimeFeedback
        {
            Timestamp = DateTime.UtcNow,
            LocalAccuracy = accuracy,
            CorrectNotes = correctCount,
            TotalNotes = totalExpected,
            Issues = issues,
            TimingMessage = timingMessage,
            CurrentMeasure = expected.FirstOrDefault()?.Measure ?? 1,
            IsPositive = accuracy >= 80 && issues.Count == 0
        };
    }
    
    private void UpdateScorePosition(AlignmentResult alignment)
    {
        if (alignment.Pairs.Count > 0)
        {
            var lastMatched = alignment.Pairs
                .OrderByDescending(p => p.ScoreNote.StartTick)
                .First();
            
            var matchedIndex = _score.Notes.ToList().FindIndex(n => n.Id == lastMatched.ScoreNote.Id);
            if (matchedIndex >= 0 && matchedIndex >= _currentScorePosition)
            {
                _currentScorePosition = matchedIndex + 1;
            }
        }
    }
    
    private double GetCurrentTimeMs()
    {
        return DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond - _performanceStartTime;
    }
    
    public void Dispose()
    {
        _isStarted = false;
    }
}

/// <summary>
/// Real-time feedback event.
/// </summary>
public record RealTimeFeedback
{
    public DateTime Timestamp { get; init; }
    public double LocalAccuracy { get; init; }
    public int CorrectNotes { get; init; }
    public int TotalNotes { get; init; }
    public IReadOnlyList<string> Issues { get; init; } = [];
    public string TimingMessage { get; init; } = "";
    public int CurrentMeasure { get; init; }
    public bool IsPositive { get; init; }
    
    public bool HasContent => Issues.Count > 0 || !string.IsNullOrEmpty(TimingMessage);
}

/// <summary>
/// Real-time error detection.
/// </summary>
public record RealTimeError
{
    public RealTimeErrorType Type { get; init; }
    public string Message { get; init; } = "";
    public int ExpectedPitch { get; init; }
    public int PlayedPitch { get; init; }
    public int Measure { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Types of real-time errors.
/// </summary>
public enum RealTimeErrorType
{
    WrongNote,
    MissedNote,
    TimingError
}

/// <summary>
/// Current progress in real-time evaluation.
/// </summary>
public record RealTimeProgress
{
    public int CurrentMeasure { get; init; }
    public int CurrentScorePosition { get; init; }
    public int TotalScoreNotes { get; init; }
    public double ProgressPercent { get; init; }
    public int NotesPlayedSoFar { get; init; }
    public IReadOnlyList<RealTimeFeedback> FeedbackHistory { get; init; } = [];
}

