using ListeningBrain.Core.Models;
using ListeningBrain.Pipeline;

namespace ListeningBrain.Advanced;

/// <summary>
/// Recording and playback system with annotation support.
/// Records performances and allows review with teacher/AI annotations.
/// </summary>
public class RecordingManager
{
    private readonly Dictionary<Guid, Recording> _recordings = new();
    
    private Recording? _activeRecording;
    private DateTime _recordingStartTime;
    
    /// <summary>
    /// Starts a new recording.
    /// </summary>
    public Recording StartRecording(string studentId, Score score, string? title = null)
    {
        _activeRecording = new Recording
        {
            StudentId = studentId,
            ScoreId = score.Id,
            ScoreTitle = score.Title ?? "Untitled",
            Title = title ?? $"Recording {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
            Tempo = score.Tempo
        };
        
        _recordingStartTime = DateTime.UtcNow;
        
        return _activeRecording;
    }
    
    /// <summary>
    /// Records a note event.
    /// </summary>
    public void RecordNoteOn(int pitch, int velocity)
    {
        if (_activeRecording == null) return;
        
        var timeMs = (DateTime.UtcNow - _recordingStartTime).TotalMilliseconds;
        
        _activeRecording.Events.Add(new RecordedEvent
        {
            Type = RecordedEventType.NoteOn,
            TimeMs = timeMs,
            Pitch = pitch,
            Velocity = velocity
        });
    }
    
    /// <summary>
    /// Records a note off event.
    /// </summary>
    public void RecordNoteOff(int pitch)
    {
        if (_activeRecording == null) return;
        
        var timeMs = (DateTime.UtcNow - _recordingStartTime).TotalMilliseconds;
        
        _activeRecording.Events.Add(new RecordedEvent
        {
            Type = RecordedEventType.NoteOff,
            TimeMs = timeMs,
            Pitch = pitch
        });
    }
    
    /// <summary>
    /// Records a pedal event.
    /// </summary>
    public void RecordPedal(PedalType pedal, bool isDown)
    {
        if (_activeRecording == null) return;
        
        var timeMs = (DateTime.UtcNow - _recordingStartTime).TotalMilliseconds;
        
        _activeRecording.Events.Add(new RecordedEvent
        {
            Type = isDown ? RecordedEventType.PedalDown : RecordedEventType.PedalUp,
            TimeMs = timeMs,
            PedalType = pedal
        });
    }
    
    /// <summary>
    /// Stops the recording and saves it.
    /// </summary>
    public Recording? StopRecording(EvaluationResult? evaluation = null)
    {
        if (_activeRecording == null) return null;
        
        _activeRecording.Duration = DateTime.UtcNow - _recordingStartTime;
        _activeRecording.EvaluationResult = evaluation;
        
        _recordings[_activeRecording.Id] = _activeRecording;
        
        var completed = _activeRecording;
        _activeRecording = null;
        
        return completed;
    }
    
    /// <summary>
    /// Gets a recording by ID.
    /// </summary>
    public Recording? GetRecording(Guid id)
    {
        return _recordings.GetValueOrDefault(id);
    }
    
    /// <summary>
    /// Gets all recordings for a student.
    /// </summary>
    public List<Recording> GetRecordingsForStudent(string studentId)
    {
        return _recordings.Values
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }
    
    /// <summary>
    /// Gets recordings for a specific piece.
    /// </summary>
    public List<Recording> GetRecordingsForPiece(string studentId, string scoreId)
    {
        return _recordings.Values
            .Where(r => r.StudentId == studentId && r.ScoreId == scoreId)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }
    
    /// <summary>
    /// Adds an annotation to a recording.
    /// </summary>
    public void AddAnnotation(
        Guid recordingId,
        double timeMs,
        string text,
        string authorId,
        AnnotationType type = AnnotationType.Comment)
    {
        if (!_recordings.TryGetValue(recordingId, out var recording)) return;
        
        recording.Annotations.Add(new Annotation
        {
            TimeMs = timeMs,
            Text = text,
            AuthorId = authorId,
            Type = type
        });
    }
    
    /// <summary>
    /// Adds a measure-level annotation.
    /// </summary>
    public void AddMeasureAnnotation(
        Guid recordingId,
        int measure,
        string text,
        string authorId,
        AnnotationType type = AnnotationType.Comment)
    {
        if (!_recordings.TryGetValue(recordingId, out var recording)) return;
        
        recording.Annotations.Add(new Annotation
        {
            Measure = measure,
            Text = text,
            AuthorId = authorId,
            Type = type
        });
    }
    
    /// <summary>
    /// Deletes a recording.
    /// </summary>
    public bool DeleteRecording(Guid id)
    {
        return _recordings.Remove(id);
    }
    
    /// <summary>
    /// Exports a recording to MIDI format.
    /// </summary>
    public byte[] ExportToMidi(Guid recordingId)
    {
        if (!_recordings.TryGetValue(recordingId, out var recording))
        {
            return [];
        }
        
        // In a real implementation, this would create a proper MIDI file
        // using DryWetMidi. Here we return a placeholder.
        return BuildMidiBytes(recording);
    }
    
    /// <summary>
    /// Compares two recordings.
    /// </summary>
    public RecordingComparison CompareRecordings(Guid recordingId1, Guid recordingId2)
    {
        var r1 = _recordings.GetValueOrDefault(recordingId1);
        var r2 = _recordings.GetValueOrDefault(recordingId2);
        
        if (r1 == null || r2 == null)
        {
            return new RecordingComparison { IsValid = false };
        }
        
        return new RecordingComparison
        {
            IsValid = true,
            Recording1 = r1,
            Recording2 = r2,
            ScoreImprovement = (r2.EvaluationResult?.OverallScore ?? 0) - 
                             (r1.EvaluationResult?.OverallScore ?? 0),
            TempoChange = r2.AverageTempo - r1.AverageTempo,
            DurationChange = r2.Duration - r1.Duration,
            NoteCountChange = r2.NoteCount - r1.NoteCount,
            Summary = GenerateComparisonSummary(r1, r2)
        };
    }
    
    private byte[] BuildMidiBytes(Recording recording)
    {
        // Placeholder - would use DryWetMidi to build actual MIDI file
        var header = new byte[] { 0x4D, 0x54, 0x68, 0x64 }; // "MThd"
        return header;
    }
    
    private string GenerateComparisonSummary(Recording r1, Recording r2)
    {
        var parts = new List<string>();
        
        var scoreChange = (r2.EvaluationResult?.OverallScore ?? 0) - 
                         (r1.EvaluationResult?.OverallScore ?? 0);
        
        if (scoreChange > 5)
            parts.Add($"Score improved by {scoreChange:F0}%!");
        else if (scoreChange < -5)
            parts.Add($"Score decreased by {Math.Abs(scoreChange):F0}%");
        else
            parts.Add("Score remained similar");
        
        if (Math.Abs(r2.AverageTempo - r1.AverageTempo) > 5)
        {
            parts.Add(r2.AverageTempo > r1.AverageTempo
                ? "Played faster"
                : "Played slower");
        }
        
        return string.Join(". ", parts) + ".";
    }
}

/// <summary>
/// Playback controller for recordings.
/// </summary>
public class PlaybackController
{
    private Recording? _recording;
    private bool _isPlaying;
    private double _currentTimeMs;
    private double _playbackSpeed = 1.0;
    private DateTime _playbackStartTime;
    private int _currentEventIndex;
    
    /// <summary>
    /// Fired when a note should be played.
    /// </summary>
    public event Action<RecordedEvent>? OnPlayEvent;
    
    /// <summary>
    /// Fired when playback position changes.
    /// </summary>
    public event Action<PlaybackPosition>? OnPositionChanged;
    
    /// <summary>
    /// Fired when an annotation is reached.
    /// </summary>
    public event Action<Annotation>? OnAnnotationReached;
    
    /// <summary>
    /// Fired when playback completes.
    /// </summary>
    public event Action? OnPlaybackComplete;
    
    /// <summary>
    /// Loads a recording for playback.
    /// </summary>
    public void LoadRecording(Recording recording)
    {
        _recording = recording;
        _currentTimeMs = 0;
        _currentEventIndex = 0;
    }
    
    /// <summary>
    /// Starts playback.
    /// </summary>
    public void Play()
    {
        if (_recording == null) return;
        
        _isPlaying = true;
        _playbackStartTime = DateTime.UtcNow.AddMilliseconds(-_currentTimeMs / _playbackSpeed);
        
        StartPlaybackLoop();
    }
    
    /// <summary>
    /// Pauses playback.
    /// </summary>
    public void Pause()
    {
        _isPlaying = false;
    }
    
    /// <summary>
    /// Stops playback and resets position.
    /// </summary>
    public void Stop()
    {
        _isPlaying = false;
        _currentTimeMs = 0;
        _currentEventIndex = 0;
    }
    
    /// <summary>
    /// Seeks to a specific position.
    /// </summary>
    public void Seek(double timeMs)
    {
        _currentTimeMs = Math.Clamp(timeMs, 0, _recording?.Duration.TotalMilliseconds ?? 0);
        
        // Find the correct event index
        if (_recording != null)
        {
            _currentEventIndex = _recording.Events
                .TakeWhile(e => e.TimeMs < _currentTimeMs)
                .Count();
        }
        
        if (_isPlaying)
        {
            _playbackStartTime = DateTime.UtcNow.AddMilliseconds(-_currentTimeMs / _playbackSpeed);
        }
        
        OnPositionChanged?.Invoke(GetCurrentPosition());
    }
    
    /// <summary>
    /// Sets playback speed (0.25 to 2.0).
    /// </summary>
    public void SetSpeed(double speed)
    {
        _playbackSpeed = Math.Clamp(speed, 0.25, 2.0);
        
        if (_isPlaying)
        {
            _playbackStartTime = DateTime.UtcNow.AddMilliseconds(-_currentTimeMs / _playbackSpeed);
        }
    }
    
    /// <summary>
    /// Gets current playback position.
    /// </summary>
    public PlaybackPosition GetCurrentPosition()
    {
        return new PlaybackPosition
        {
            TimeMs = _currentTimeMs,
            TotalMs = _recording?.Duration.TotalMilliseconds ?? 0,
            PercentComplete = _recording != null
                ? _currentTimeMs / _recording.Duration.TotalMilliseconds * 100
                : 0,
            IsPlaying = _isPlaying,
            Speed = _playbackSpeed
        };
    }
    
    /// <summary>
    /// Gets annotations at current position.
    /// </summary>
    public List<Annotation> GetAnnotationsAtPosition(double toleranceMs = 100)
    {
        if (_recording == null) return [];
        
        return _recording.Annotations
            .Where(a => a.TimeMs.HasValue && 
                       Math.Abs(a.TimeMs.Value - _currentTimeMs) < toleranceMs)
            .ToList();
    }
    
    private void StartPlaybackLoop()
    {
        Task.Run(async () =>
        {
            while (_isPlaying && _recording != null)
            {
                _currentTimeMs = (DateTime.UtcNow - _playbackStartTime).TotalMilliseconds * _playbackSpeed;
                
                // Play events
                while (_currentEventIndex < _recording.Events.Count)
                {
                    var evt = _recording.Events[_currentEventIndex];
                    
                    if (evt.TimeMs <= _currentTimeMs)
                    {
                        OnPlayEvent?.Invoke(evt);
                        _currentEventIndex++;
                    }
                    else
                    {
                        break;
                    }
                }
                
                // Check for annotations
                foreach (var annotation in _recording.Annotations
                    .Where(a => a.TimeMs.HasValue && 
                               Math.Abs(a.TimeMs.Value - _currentTimeMs) < 50))
                {
                    OnAnnotationReached?.Invoke(annotation);
                }
                
                OnPositionChanged?.Invoke(GetCurrentPosition());
                
                // Check if complete
                if (_currentTimeMs >= _recording.Duration.TotalMilliseconds)
                {
                    _isPlaying = false;
                    OnPlaybackComplete?.Invoke();
                    break;
                }
                
                await Task.Delay(10);
            }
        });
    }
}

/// <summary>
/// A recorded performance.
/// </summary>
public class Recording
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string StudentId { get; init; } = "";
    public string ScoreId { get; init; } = "";
    public string ScoreTitle { get; init; } = "";
    public string Title { get; set; } = "";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
    public double Tempo { get; init; }
    public double AverageTempo { get; set; }
    
    public List<RecordedEvent> Events { get; set; } = [];
    public List<Annotation> Annotations { get; set; } = [];
    public EvaluationResult? EvaluationResult { get; set; }
    
    public int NoteCount => Events.Count(e => e.Type == RecordedEventType.NoteOn);
    public bool HasAnnotations => Annotations.Count > 0;
}

/// <summary>
/// A recorded MIDI event.
/// </summary>
public record RecordedEvent
{
    public RecordedEventType Type { get; init; }
    public double TimeMs { get; init; }
    public int Pitch { get; init; }
    public int Velocity { get; init; }
    public PedalType? PedalType { get; init; }
}

/// <summary>
/// Type of recorded event.
/// </summary>
public enum RecordedEventType
{
    NoteOn,
    NoteOff,
    PedalDown,
    PedalUp,
    ControlChange
}

/// <summary>
/// Pedal type.
/// </summary>
public enum PedalType
{
    Sustain,
    Soft,
    Sostenuto
}

/// <summary>
/// An annotation on a recording.
/// </summary>
public record Annotation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public double? TimeMs { get; init; }
    public int? Measure { get; init; }
    public string Text { get; init; } = "";
    public string AuthorId { get; init; } = "";
    public AnnotationType Type { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Type of annotation.
/// </summary>
public enum AnnotationType
{
    Comment,
    Correction,
    Praise,
    Suggestion,
    Technique,
    Dynamics,
    Rhythm,
    Fingering
}

/// <summary>
/// Current playback position.
/// </summary>
public record PlaybackPosition
{
    public double TimeMs { get; init; }
    public double TotalMs { get; init; }
    public double PercentComplete { get; init; }
    public bool IsPlaying { get; init; }
    public double Speed { get; init; }
}

/// <summary>
/// Comparison between two recordings.
/// </summary>
public record RecordingComparison
{
    public bool IsValid { get; init; }
    public Recording? Recording1 { get; init; }
    public Recording? Recording2 { get; init; }
    public double ScoreImprovement { get; init; }
    public double TempoChange { get; init; }
    public TimeSpan DurationChange { get; init; }
    public int NoteCountChange { get; init; }
    public string Summary { get; init; } = "";
}

