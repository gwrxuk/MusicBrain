using ListeningBrain.Core.Models;
using ListeningBrain.Pipeline;
using ListeningBrain.Intelligence;

namespace ListeningBrain.Advanced;

/// <summary>
/// Sight-reading mode with real-time score display synchronization.
/// Provides a "follow-along" experience where the score display tracks
/// the student's current position.
/// </summary>
public class SightReadingMode
{
    private readonly Score _score;
    private readonly RealTimeEvaluator _evaluator;
    private readonly SightReadingOptions _options;
    
    private int _currentMeasure = 1;
    private int _currentBeat = 1;
    private double _currentTimeMs;
    private bool _isActive;
    private DateTime _startTime;
    private readonly List<SightReadingEvent> _events = [];
    
    /// <summary>
    /// Fired when the score position should update.
    /// </summary>
    public event Action<ScorePosition>? OnPositionChanged;
    
    /// <summary>
    /// Fired when sight-reading feedback is available.
    /// </summary>
    public event Action<SightReadingFeedback>? OnFeedbackAvailable;
    
    /// <summary>
    /// Fired when the student is ahead/behind the expected position.
    /// </summary>
    public event Action<TempoGuidance>? OnTempoGuidance;
    
    /// <summary>
    /// Creates a new sight-reading mode session.
    /// </summary>
    public SightReadingMode(Score score, SightReadingOptions? options = null)
    {
        _score = score;
        _options = options ?? new SightReadingOptions();
        _evaluator = new RealTimeEvaluator(score);
    }
    
    /// <summary>
    /// Starts the sight-reading session.
    /// </summary>
    public void Start()
    {
        _isActive = true;
        _startTime = DateTime.UtcNow;
        _currentMeasure = 1;
        _currentBeat = 1;
        _currentTimeMs = 0;
        _events.Clear();
        
        _evaluator.Start();
        
        // Start the position ticker
        if (_options.AutoScroll)
        {
            StartPositionTicker();
        }
        
        OnPositionChanged?.Invoke(new ScorePosition
        {
            Measure = _currentMeasure,
            Beat = _currentBeat,
            TimeMs = 0,
            IsCountIn = _options.CountInMeasures > 0
        });
    }
    
    /// <summary>
    /// Processes a note event from the student.
    /// </summary>
    public void OnNoteOn(int pitch, int velocity)
    {
        if (!_isActive) return;
        
        var timeMs = (DateTime.UtcNow - _startTime).TotalMilliseconds;
        
        _events.Add(new SightReadingEvent
        {
            Type = SightReadingEventType.NoteOn,
            Pitch = pitch,
            Velocity = velocity,
            TimeMs = timeMs,
            Measure = _currentMeasure,
            Beat = _currentBeat
        });
        
        _evaluator.OnNoteOn(pitch, velocity);
        
        // Update position based on played notes
        if (_options.FollowPlayer)
        {
            UpdatePositionFromPlayedNote(pitch, timeMs);
        }
        
        // Check tempo guidance
        CheckTempoGuidance(timeMs);
    }
    
    /// <summary>
    /// Processes a note off event.
    /// </summary>
    public void OnNoteOff(int pitch)
    {
        if (!_isActive) return;
        
        var timeMs = (DateTime.UtcNow - _startTime).TotalMilliseconds;
        
        _events.Add(new SightReadingEvent
        {
            Type = SightReadingEventType.NoteOff,
            Pitch = pitch,
            TimeMs = timeMs,
            Measure = _currentMeasure,
            Beat = _currentBeat
        });
        
        _evaluator.OnNoteOff(pitch);
    }
    
    /// <summary>
    /// Pauses the sight-reading session.
    /// </summary>
    public void Pause()
    {
        _isActive = false;
    }
    
    /// <summary>
    /// Resumes the sight-reading session.
    /// </summary>
    public void Resume()
    {
        _isActive = true;
        _startTime = DateTime.UtcNow.AddMilliseconds(-_currentTimeMs);
    }
    
    /// <summary>
    /// Stops the session and returns results.
    /// </summary>
    public SightReadingResult Stop()
    {
        _isActive = false;
        
        var evalResult = _evaluator.GetFinalEvaluation();
        
        return new SightReadingResult
        {
            Duration = DateTime.UtcNow - _startTime,
            TotalNotes = _events.Count(e => e.Type == SightReadingEventType.NoteOn),
            EvaluationResult = evalResult,
            PositionAccuracy = CalculatePositionAccuracy(),
            ReadingSpeed = CalculateReadingSpeed(),
            HesitationCount = CountHesitations(),
            Events = _events.ToList()
        };
    }
    
    /// <summary>
    /// Jumps to a specific measure.
    /// </summary>
    public void JumpToMeasure(int measure)
    {
        _currentMeasure = Math.Clamp(measure, 1, _score.TotalMeasures);
        _currentBeat = 1;
        
        // Calculate time for this measure
        _currentTimeMs = MeasureToTime(measure);
        _startTime = DateTime.UtcNow.AddMilliseconds(-_currentTimeMs);
        
        OnPositionChanged?.Invoke(new ScorePosition
        {
            Measure = _currentMeasure,
            Beat = _currentBeat,
            TimeMs = _currentTimeMs
        });
    }
    
    /// <summary>
    /// Gets the look-ahead notes (upcoming notes to prepare for).
    /// </summary>
    public List<ScoreNote> GetLookAheadNotes(int measures = 2)
    {
        return _score.Notes
            .Where(n => n.Measure >= _currentMeasure && n.Measure < _currentMeasure + measures)
            .OrderBy(n => n.Measure)
            .ThenBy(n => n.Beat)
            .ToList();
    }
    
    /// <summary>
    /// Gets current score position.
    /// </summary>
    public ScorePosition GetCurrentPosition()
    {
        return new ScorePosition
        {
            Measure = _currentMeasure,
            Beat = _currentBeat,
            TimeMs = _currentTimeMs,
            PercentComplete = (double)_currentMeasure / _score.TotalMeasures * 100
        };
    }
    
    private void StartPositionTicker()
    {
        Task.Run(async () =>
        {
            var msPerBeat = 60000.0 / _score.Tempo;
            var beatsPerMeasure = _score.TimeSignature?.Numerator ?? 4;
            
            // Count-in
            if (_options.CountInMeasures > 0)
            {
                for (int m = 0; m < _options.CountInMeasures; m++)
                {
                    for (int b = 1; b <= beatsPerMeasure; b++)
                    {
                        if (!_isActive) return;
                        
                        OnPositionChanged?.Invoke(new ScorePosition
                        {
                            Measure = -(_options.CountInMeasures - m),
                            Beat = b,
                            IsCountIn = true,
                            TimeMs = -(_options.CountInMeasures - m) * beatsPerMeasure * msPerBeat + (b - 1) * msPerBeat
                        });
                        
                        await Task.Delay((int)msPerBeat);
                    }
                }
            }
            
            // Main playthrough
            while (_isActive && _currentMeasure <= _score.TotalMeasures)
            {
                _currentTimeMs = (DateTime.UtcNow - _startTime).TotalMilliseconds;
                
                // Calculate current beat
                var totalBeats = _currentTimeMs / msPerBeat;
                _currentMeasure = (int)(totalBeats / beatsPerMeasure) + 1;
                _currentBeat = (int)(totalBeats % beatsPerMeasure) + 1;
                
                OnPositionChanged?.Invoke(new ScorePosition
                {
                    Measure = _currentMeasure,
                    Beat = _currentBeat,
                    TimeMs = _currentTimeMs,
                    PercentComplete = (double)_currentMeasure / _score.TotalMeasures * 100
                });
                
                await Task.Delay(50); // 20 FPS update rate
            }
        });
    }
    
    private void UpdatePositionFromPlayedNote(int pitch, double timeMs)
    {
        // Find the expected note closest to current position with matching pitch
        var matchingNotes = _score.Notes
            .Where(n => n.Pitch == pitch && n.Measure >= _currentMeasure - 1)
            .OrderBy(n => n.Measure)
            .ThenBy(n => n.Beat)
            .Take(3)
            .ToList();
        
        if (matchingNotes.Count > 0)
        {
            var closestNote = matchingNotes.First();
            
            // Update position if significantly different
            if (closestNote.Measure > _currentMeasure ||
                (closestNote.Measure == _currentMeasure && closestNote.Beat > _currentBeat + 1))
            {
                _currentMeasure = closestNote.Measure;
                _currentBeat = (int)closestNote.Beat;
                
                OnPositionChanged?.Invoke(new ScorePosition
                {
                    Measure = _currentMeasure,
                    Beat = _currentBeat,
                    TimeMs = timeMs,
                    WasAdjusted = true
                });
            }
        }
    }
    
    private void CheckTempoGuidance(double currentTimeMs)
    {
        if (!_options.ShowTempoGuidance) return;
        
        var expectedTimeMs = MeasureToTime(_currentMeasure);
        var deviationMs = currentTimeMs - expectedTimeMs;
        
        if (Math.Abs(deviationMs) > _options.TempoToleranceMs)
        {
            OnTempoGuidance?.Invoke(new TempoGuidance
            {
                DeviationMs = deviationMs,
                IsAhead = deviationMs < 0,
                IsBehind = deviationMs > 0,
                Message = deviationMs < 0
                    ? "Slow down - you're ahead of the beat"
                    : "Speed up - you're falling behind"
            });
        }
    }
    
    private double MeasureToTime(int measure)
    {
        var msPerBeat = 60000.0 / _score.Tempo;
        var beatsPerMeasure = _score.TimeSignature?.Numerator ?? 4;
        return (measure - 1) * beatsPerMeasure * msPerBeat;
    }
    
    private double CalculatePositionAccuracy()
    {
        if (_events.Count == 0) return 0;
        
        int accurateNotes = 0;
        foreach (var evt in _events.Where(e => e.Type == SightReadingEventType.NoteOn))
        {
            var expectedNotes = _score.Notes
                .Where(n => n.Measure == evt.Measure && Math.Abs(n.Beat - evt.Beat) < 0.5);
            
            if (expectedNotes.Any(n => n.Pitch == evt.Pitch))
            {
                accurateNotes++;
            }
        }
        
        return (double)accurateNotes / _events.Count(e => e.Type == SightReadingEventType.NoteOn) * 100;
    }
    
    private double CalculateReadingSpeed()
    {
        if (_events.Count < 2) return 0;
        
        var firstNote = _events.First(e => e.Type == SightReadingEventType.NoteOn);
        var lastNote = _events.Last(e => e.Type == SightReadingEventType.NoteOn);
        
        var durationMs = lastNote.TimeMs - firstNote.TimeMs;
        var measuresRead = lastNote.Measure - firstNote.Measure + 1;
        
        // Measures per minute
        return measuresRead / (durationMs / 60000.0);
    }
    
    private int CountHesitations()
    {
        int hesitations = 0;
        var noteEvents = _events.Where(e => e.Type == SightReadingEventType.NoteOn).ToList();
        
        for (int i = 1; i < noteEvents.Count; i++)
        {
            var gap = noteEvents[i].TimeMs - noteEvents[i - 1].TimeMs;
            var expectedGap = 60000.0 / _score.Tempo; // One beat
            
            // If gap is more than 2 beats, count as hesitation
            if (gap > expectedGap * 2)
            {
                hesitations++;
            }
        }
        
        return hesitations;
    }
}

/// <summary>
/// Options for sight-reading mode.
/// </summary>
public class SightReadingOptions
{
    /// <summary>
    /// Number of count-in measures before starting.
    /// </summary>
    public int CountInMeasures { get; set; } = 1;
    
    /// <summary>
    /// Whether to auto-scroll with the tempo.
    /// </summary>
    public bool AutoScroll { get; set; } = true;
    
    /// <summary>
    /// Whether to follow the player's position.
    /// </summary>
    public bool FollowPlayer { get; set; } = true;
    
    /// <summary>
    /// Whether to show tempo guidance.
    /// </summary>
    public bool ShowTempoGuidance { get; set; } = true;
    
    /// <summary>
    /// Tolerance for tempo guidance in ms.
    /// </summary>
    public double TempoToleranceMs { get; set; } = 500;
    
    /// <summary>
    /// Number of measures to show ahead (look-ahead).
    /// </summary>
    public int LookAheadMeasures { get; set; } = 2;
    
    /// <summary>
    /// Highlight upcoming notes.
    /// </summary>
    public bool HighlightUpcoming { get; set; } = true;
    
    /// <summary>
    /// Metronome click during playback.
    /// </summary>
    public bool MetronomeEnabled { get; set; } = false;
}

/// <summary>
/// Current position in the score.
/// </summary>
public record ScorePosition
{
    public int Measure { get; init; }
    public int Beat { get; init; }
    public double TimeMs { get; init; }
    public bool IsCountIn { get; init; }
    public bool WasAdjusted { get; init; }
    public double PercentComplete { get; init; }
}

/// <summary>
/// Tempo guidance for the player.
/// </summary>
public record TempoGuidance
{
    public double DeviationMs { get; init; }
    public bool IsAhead { get; init; }
    public bool IsBehind { get; init; }
    public string Message { get; init; } = "";
}

/// <summary>
/// Event recorded during sight-reading.
/// </summary>
public record SightReadingEvent
{
    public SightReadingEventType Type { get; init; }
    public int Pitch { get; init; }
    public int Velocity { get; init; }
    public double TimeMs { get; init; }
    public int Measure { get; init; }
    public int Beat { get; init; }
}

/// <summary>
/// Type of sight-reading event.
/// </summary>
public enum SightReadingEventType
{
    NoteOn,
    NoteOff,
    Pause,
    Resume,
    Jump
}

/// <summary>
/// Feedback during sight-reading.
/// </summary>
public record SightReadingFeedback
{
    public int Measure { get; init; }
    public double Accuracy { get; init; }
    public string Message { get; init; } = "";
    public List<string> Hints { get; init; } = [];
}

/// <summary>
/// Results from a sight-reading session.
/// </summary>
public record SightReadingResult
{
    public TimeSpan Duration { get; init; }
    public int TotalNotes { get; init; }
    public EvaluationResult? EvaluationResult { get; init; }
    public double PositionAccuracy { get; init; }
    public double ReadingSpeed { get; init; }
    public int HesitationCount { get; init; }
    public List<SightReadingEvent> Events { get; init; } = [];
    
    public string Grade => EvaluationResult?.Grade ?? "N/A";
    public double OverallScore => EvaluationResult?.OverallScore ?? 0;
}

