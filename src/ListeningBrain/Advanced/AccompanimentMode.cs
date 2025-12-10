using ListeningBrain.Core.Models;
using ListeningBrain.Pipeline;

namespace ListeningBrain.Advanced;

/// <summary>
/// Accompaniment mode for play-along practice.
/// The system plays backing tracks that follow the student's tempo.
/// </summary>
public class AccompanimentMode
{
    private readonly Score _score;
    private readonly AccompanimentOptions _options;
    private readonly RealTimeEvaluator _evaluator;
    
    private bool _isActive;
    private DateTime _startTime;
    private double _currentTempo;
    private double _targetTempo;
    private int _currentMeasure = 1;
    private readonly List<AccompanimentNote> _accompanimentNotes = [];
    private readonly Queue<AccompanimentNote> _pendingNotes = new();
    
    /// <summary>
    /// Fired when an accompaniment note should be played.
    /// </summary>
    public event Action<AccompanimentNote>? OnPlayNote;
    
    /// <summary>
    /// Fired when tempo changes are detected.
    /// </summary>
    public event Action<TempoChange>? OnTempoChange;
    
    /// <summary>
    /// Fired when waiting for the student.
    /// </summary>
    public event Action<WaitingState>? OnWaiting;
    
    /// <summary>
    /// Fired when accompaniment resumes.
    /// </summary>
    public event Action? OnResumed;
    
    /// <summary>
    /// Creates a new accompaniment mode session.
    /// </summary>
    public AccompanimentMode(Score score, AccompanimentOptions? options = null)
    {
        _score = score;
        _options = options ?? new AccompanimentOptions();
        _evaluator = new RealTimeEvaluator(score);
        _currentTempo = score.Tempo;
        _targetTempo = score.Tempo;
        
        LoadAccompanimentParts();
    }
    
    /// <summary>
    /// Starts the accompaniment.
    /// </summary>
    public void Start()
    {
        _isActive = true;
        _startTime = DateTime.UtcNow;
        _currentMeasure = 1;
        _evaluator.Start();
        
        // Queue initial notes
        QueueNotesForMeasure(_currentMeasure);
        QueueNotesForMeasure(_currentMeasure + 1);
        
        // Start playback loop
        StartPlaybackLoop();
    }
    
    /// <summary>
    /// Processes a note from the student (solo part).
    /// </summary>
    public void OnStudentNote(int pitch, int velocity)
    {
        if (!_isActive) return;
        
        var timeMs = (DateTime.UtcNow - _startTime).TotalMilliseconds;
        
        _evaluator.OnNoteOn(pitch, velocity);
        
        // Update tempo estimation based on student's playing
        UpdateTempoFromStudent(pitch, timeMs);
    }
    
    /// <summary>
    /// Stops the accompaniment.
    /// </summary>
    public AccompanimentResult Stop()
    {
        _isActive = false;
        
        var evalResult = _evaluator.GetFinalEvaluation();
        
        return new AccompanimentResult
        {
            Duration = DateTime.UtcNow - _startTime,
            AverageTempo = _currentTempo,
            TempoVariation = CalculateTempoVariation(),
            MeasuresCompleted = _currentMeasure,
            EvaluationResult = evalResult,
            SyncScore = CalculateSyncScore()
        };
    }
    
    /// <summary>
    /// Pauses the accompaniment.
    /// </summary>
    public void Pause()
    {
        _isActive = false;
        OnWaiting?.Invoke(new WaitingState
        {
            Measure = _currentMeasure,
            Reason = "Paused by user"
        });
    }
    
    /// <summary>
    /// Resumes the accompaniment.
    /// </summary>
    public void Resume()
    {
        _isActive = true;
        _startTime = DateTime.UtcNow.AddMilliseconds(-MeasureToTime(_currentMeasure));
        OnResumed?.Invoke();
        StartPlaybackLoop();
    }
    
    /// <summary>
    /// Adjusts the target tempo.
    /// </summary>
    public void SetTempo(double bpm)
    {
        _targetTempo = Math.Clamp(bpm, _options.MinTempo, _options.MaxTempo);
    }
    
    /// <summary>
    /// Gets the current accompaniment parts.
    /// </summary>
    public List<AccompanimentPart> GetParts() => _options.Parts;
    
    /// <summary>
    /// Mutes/unmutes a part.
    /// </summary>
    public void SetPartMuted(string partName, bool muted)
    {
        var part = _options.Parts.FirstOrDefault(p => p.Name == partName);
        if (part != null)
        {
            part.IsMuted = muted;
        }
    }
    
    /// <summary>
    /// Sets part volume.
    /// </summary>
    public void SetPartVolume(string partName, double volume)
    {
        var part = _options.Parts.FirstOrDefault(p => p.Name == partName);
        if (part != null)
        {
            part.Volume = Math.Clamp(volume, 0, 1);
        }
    }
    
    private void LoadAccompanimentParts()
    {
        // In a real implementation, this would load from the score's
        // accompaniment tracks. Here we create placeholder parts.
        
        if (_options.Parts.Count == 0)
        {
            // Default parts based on score
            _options.Parts.Add(new AccompanimentPart
            {
                Name = "Left Hand",
                Channel = 1,
                IsSoloPart = false
            });
            
            _options.Parts.Add(new AccompanimentPart
            {
                Name = "Bass",
                Channel = 2,
                IsSoloPart = false
            });
        }
        
        // Load notes for accompaniment
        foreach (var note in _score.Notes)
        {
            // Determine which part this note belongs to
            // (simplified: lower notes go to bass, etc.)
            var part = note.Pitch < 48 ? "Bass" : "Left Hand";
            
            _accompanimentNotes.Add(new AccompanimentNote
            {
                Pitch = note.Pitch,
                Velocity = note.Velocity,
                Measure = note.Measure,
                Beat = note.Beat,
                Duration = note.Duration,
                PartName = part
            });
        }
    }
    
    private void StartPlaybackLoop()
    {
        Task.Run(async () =>
        {
            while (_isActive)
            {
                var currentTimeMs = (DateTime.UtcNow - _startTime).TotalMilliseconds;
                
                // Check for notes to play
                while (_pendingNotes.Count > 0)
                {
                    var nextNote = _pendingNotes.Peek();
                    var noteTimeMs = NoteToTime(nextNote);
                    
                    if (noteTimeMs <= currentTimeMs)
                    {
                        _pendingNotes.Dequeue();
                        
                        // Check if part is muted
                        var part = _options.Parts.FirstOrDefault(p => p.Name == nextNote.PartName);
                        if (part != null && !part.IsMuted && !part.IsSoloPart)
                        {
                            // Apply volume
                            var adjustedNote = nextNote with
                            {
                                Velocity = (int)(nextNote.Velocity * part.Volume)
                            };
                            OnPlayNote?.Invoke(adjustedNote);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                
                // Update current measure
                var newMeasure = TimeToMeasure(currentTimeMs);
                if (newMeasure > _currentMeasure)
                {
                    _currentMeasure = newMeasure;
                    QueueNotesForMeasure(_currentMeasure + 1);
                }
                
                // Smooth tempo adjustment
                if (Math.Abs(_currentTempo - _targetTempo) > 1)
                {
                    var adjustment = (_targetTempo - _currentTempo) * 0.1;
                    _currentTempo += adjustment;
                    
                    OnTempoChange?.Invoke(new TempoChange
                    {
                        NewTempo = _currentTempo,
                        TargetTempo = _targetTempo,
                        Measure = _currentMeasure
                    });
                }
                
                // Wait mode if student is behind
                if (_options.WaitForStudent && IsStudentBehind())
                {
                    OnWaiting?.Invoke(new WaitingState
                    {
                        Measure = _currentMeasure,
                        Reason = "Waiting for student"
                    });
                    
                    // Wait until student catches up
                    while (_isActive && IsStudentBehind())
                    {
                        await Task.Delay(100);
                    }
                    
                    if (_isActive)
                    {
                        OnResumed?.Invoke();
                    }
                }
                
                await Task.Delay(10); // 100 FPS for timing accuracy
            }
        });
    }
    
    private void QueueNotesForMeasure(int measure)
    {
        var notes = _accompanimentNotes
            .Where(n => n.Measure == measure)
            .OrderBy(n => n.Beat);
        
        foreach (var note in notes)
        {
            _pendingNotes.Enqueue(note);
        }
    }
    
    private readonly List<(double time, int pitch)> _studentNotes = [];
    
    private void UpdateTempoFromStudent(int pitch, double timeMs)
    {
        _studentNotes.Add((timeMs, pitch));
        
        if (_studentNotes.Count < 4) return;
        
        // Keep last 8 notes for tempo estimation
        if (_studentNotes.Count > 8)
        {
            _studentNotes.RemoveAt(0);
        }
        
        // Calculate average inter-onset interval
        var intervals = new List<double>();
        for (int i = 1; i < _studentNotes.Count; i++)
        {
            intervals.Add(_studentNotes[i].time - _studentNotes[i - 1].time);
        }
        
        var avgInterval = intervals.Average();
        
        // Convert to BPM (assuming quarter notes)
        var estimatedTempo = 60000.0 / avgInterval;
        
        // Only adjust if within reasonable range
        if (estimatedTempo >= _options.MinTempo && estimatedTempo <= _options.MaxTempo)
        {
            // Smooth adjustment
            _targetTempo = _targetTempo * 0.7 + estimatedTempo * 0.3;
        }
    }
    
    private bool IsStudentBehind()
    {
        if (_studentNotes.Count == 0) return false;
        
        var lastStudentTime = _studentNotes.Last().time;
        var expectedTime = (DateTime.UtcNow - _startTime).TotalMilliseconds - (_options.WaitThresholdMs);
        
        return lastStudentTime < expectedTime;
    }
    
    private double MeasureToTime(int measure)
    {
        var msPerBeat = 60000.0 / _currentTempo;
        var beatsPerMeasure = _score.TimeSignature?.Numerator ?? 4;
        return (measure - 1) * beatsPerMeasure * msPerBeat;
    }
    
    private int TimeToMeasure(double timeMs)
    {
        var msPerBeat = 60000.0 / _currentTempo;
        var beatsPerMeasure = _score.TimeSignature?.Numerator ?? 4;
        return (int)(timeMs / (beatsPerMeasure * msPerBeat)) + 1;
    }
    
    private double NoteToTime(AccompanimentNote note)
    {
        var msPerBeat = 60000.0 / _currentTempo;
        var beatsPerMeasure = _score.TimeSignature?.Numerator ?? 4;
        return (note.Measure - 1) * beatsPerMeasure * msPerBeat + (note.Beat - 1) * msPerBeat;
    }
    
    private double CalculateTempoVariation()
    {
        // Would track tempo over time in full implementation
        return Math.Abs(_currentTempo - _score.Tempo);
    }
    
    private double CalculateSyncScore()
    {
        // Measure how well student stayed in sync
        // Simplified implementation
        return 85.0; // Placeholder
    }
}

/// <summary>
/// Options for accompaniment mode.
/// </summary>
public class AccompanimentOptions
{
    /// <summary>
    /// Whether to wait for the student if they fall behind.
    /// </summary>
    public bool WaitForStudent { get; set; } = true;
    
    /// <summary>
    /// Threshold before waiting (ms behind).
    /// </summary>
    public double WaitThresholdMs { get; set; } = 1000;
    
    /// <summary>
    /// Whether to follow the student's tempo.
    /// </summary>
    public bool FollowStudentTempo { get; set; } = true;
    
    /// <summary>
    /// Minimum allowed tempo.
    /// </summary>
    public double MinTempo { get; set; } = 40;
    
    /// <summary>
    /// Maximum allowed tempo.
    /// </summary>
    public double MaxTempo { get; set; } = 200;
    
    /// <summary>
    /// Accompaniment parts.
    /// </summary>
    public List<AccompanimentPart> Parts { get; set; } = [];
    
    /// <summary>
    /// Count-in measures.
    /// </summary>
    public int CountInMeasures { get; set; } = 1;
    
    /// <summary>
    /// Master volume.
    /// </summary>
    public double MasterVolume { get; set; } = 0.8;
}

/// <summary>
/// An accompaniment part (voice/instrument).
/// </summary>
public class AccompanimentPart
{
    public string Name { get; set; } = "";
    public int Channel { get; set; }
    public double Volume { get; set; } = 1.0;
    public bool IsMuted { get; set; }
    public bool IsSoloPart { get; set; }
    public string? Instrument { get; set; }
}

/// <summary>
/// A note to be played by the accompaniment.
/// </summary>
public record AccompanimentNote
{
    public int Pitch { get; init; }
    public int Velocity { get; init; }
    public int Measure { get; init; }
    public double Beat { get; init; }
    public double Duration { get; init; }
    public string PartName { get; init; } = "";
}

/// <summary>
/// Tempo change notification.
/// </summary>
public record TempoChange
{
    public double NewTempo { get; init; }
    public double TargetTempo { get; init; }
    public int Measure { get; init; }
}

/// <summary>
/// Waiting state notification.
/// </summary>
public record WaitingState
{
    public int Measure { get; init; }
    public string Reason { get; init; } = "";
}

/// <summary>
/// Results from an accompaniment session.
/// </summary>
public record AccompanimentResult
{
    public TimeSpan Duration { get; init; }
    public double AverageTempo { get; init; }
    public double TempoVariation { get; init; }
    public int MeasuresCompleted { get; init; }
    public EvaluationResult? EvaluationResult { get; init; }
    public double SyncScore { get; init; }
}

