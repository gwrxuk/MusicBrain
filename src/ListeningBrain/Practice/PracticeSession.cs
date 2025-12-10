using ListeningBrain.Core.Models;
using ListeningBrain.Evaluation;
using ListeningBrain.Intelligence;

namespace ListeningBrain.Practice;

/// <summary>
/// Represents a single practice session with all tracked data.
/// </summary>
public class PracticeSession
{
    /// <summary>
    /// Unique session identifier.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// Student ID.
    /// </summary>
    public required string StudentId { get; init; }
    
    /// <summary>
    /// Session start time.
    /// </summary>
    public DateTime StartTime { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Session end time.
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// Total duration in minutes.
    /// </summary>
    public int DurationMinutes => EndTime.HasValue 
        ? (int)(EndTime.Value - StartTime).TotalMinutes 
        : (int)(DateTime.UtcNow - StartTime).TotalMinutes;
    
    /// <summary>
    /// Pieces practiced during this session.
    /// </summary>
    public List<PracticedPiece> PiecesPracticed { get; set; } = [];
    
    /// <summary>
    /// Specific passages worked on.
    /// </summary>
    public List<PracticedPassage> PassagesPracticed { get; set; } = [];
    
    /// <summary>
    /// Session notes from the student.
    /// </summary>
    public string? StudentNotes { get; set; }
    
    /// <summary>
    /// AI-generated session summary.
    /// </summary>
    public string? SessionSummary { get; set; }
    
    /// <summary>
    /// Goals worked on during this session.
    /// </summary>
    public List<Guid> GoalsAddressed { get; set; } = [];
    
    /// <summary>
    /// Session quality rating (1-5).
    /// </summary>
    public int? QualityRating { get; set; }
    
    /// <summary>
    /// Focus areas for this session.
    /// </summary>
    public List<string> FocusAreas { get; set; } = [];
    
    /// <summary>
    /// Techniques practiced.
    /// </summary>
    public List<string> TechniquesPracticed { get; set; } = [];
    
    /// <summary>
    /// Average score across all pieces.
    /// </summary>
    public double AverageScore => PiecesPracticed.Count > 0 
        ? PiecesPracticed.Average(p => p.BestScore) 
        : 0;
    
    /// <summary>
    /// Ends the session and generates summary.
    /// </summary>
    public void EndSession()
    {
        EndTime = DateTime.UtcNow;
        SessionSummary = GenerateSessionSummary();
    }
    
    private string GenerateSessionSummary()
    {
        var parts = new List<string>();
        
        parts.Add($"{DurationMinutes} minute session");
        
        if (PiecesPracticed.Count > 0)
        {
            parts.Add($"practiced {PiecesPracticed.Count} piece(s)");
            double improvement = PiecesPracticed.Average(p => p.ScoreImprovement);
            if (improvement > 0)
                parts.Add($"average improvement: +{improvement:F0}%");
        }
        
        if (PassagesPracticed.Count > 0)
        {
            parts.Add($"focused on {PassagesPracticed.Count} passage(s)");
        }
        
        return string.Join(", ", parts) + ".";
    }
}

/// <summary>
/// A piece practiced during a session.
/// </summary>
public record PracticedPiece
{
    public string PieceId { get; init; } = "";
    public string Title { get; init; } = "";
    public int TimePracticedMinutes { get; init; }
    public int AttemptCount { get; init; }
    public double BestScore { get; init; }
    public double FirstScore { get; init; }
    public double ScoreImprovement => BestScore - FirstScore;
    public List<int> ProblemMeasures { get; init; } = [];
    public List<ErrorPatternType> PatternsDetected { get; init; } = [];
}

/// <summary>
/// A specific passage practiced during a session.
/// </summary>
public record PracticedPassage
{
    public string PieceId { get; init; } = "";
    public int StartMeasure { get; init; }
    public int EndMeasure { get; init; }
    public int RepetitionCount { get; init; }
    public double InitialAccuracy { get; init; }
    public double FinalAccuracy { get; init; }
    public double Improvement => FinalAccuracy - InitialAccuracy;
    public string? Notes { get; init; }
}

/// <summary>
/// Manages practice sessions and history.
/// </summary>
public class PracticeSessionManager
{
    private readonly Dictionary<string, List<PracticeSession>> _sessionsByStudent = new();
    private PracticeSession? _activeSession;
    
    /// <summary>
    /// Starts a new practice session.
    /// </summary>
    public PracticeSession StartSession(string studentId, List<string>? focusAreas = null)
    {
        if (_activeSession != null)
        {
            EndSession();
        }
        
        _activeSession = new PracticeSession
        {
            StudentId = studentId,
            FocusAreas = focusAreas ?? []
        };
        
        return _activeSession;
    }
    
    /// <summary>
    /// Gets the current active session.
    /// </summary>
    public PracticeSession? GetActiveSession() => _activeSession;
    
    /// <summary>
    /// Records a piece practice attempt.
    /// </summary>
    public void RecordPieceAttempt(
        string pieceId,
        string title,
        double score,
        List<int>? problemMeasures = null,
        List<ErrorPatternType>? patterns = null)
    {
        if (_activeSession == null) return;
        
        var existing = _activeSession.PiecesPracticed.FirstOrDefault(p => p.PieceId == pieceId);
        
        if (existing != null)
        {
            var index = _activeSession.PiecesPracticed.IndexOf(existing);
            _activeSession.PiecesPracticed[index] = existing with
            {
                AttemptCount = existing.AttemptCount + 1,
                BestScore = Math.Max(existing.BestScore, score),
                TimePracticedMinutes = existing.TimePracticedMinutes + 2, // Estimate
                ProblemMeasures = problemMeasures ?? existing.ProblemMeasures,
                PatternsDetected = patterns ?? existing.PatternsDetected
            };
        }
        else
        {
            _activeSession.PiecesPracticed.Add(new PracticedPiece
            {
                PieceId = pieceId,
                Title = title,
                AttemptCount = 1,
                FirstScore = score,
                BestScore = score,
                TimePracticedMinutes = 2,
                ProblemMeasures = problemMeasures ?? [],
                PatternsDetected = patterns ?? []
            });
        }
    }
    
    /// <summary>
    /// Records a passage practice.
    /// </summary>
    public void RecordPassagePractice(
        string pieceId,
        int startMeasure,
        int endMeasure,
        double accuracy,
        string? notes = null)
    {
        if (_activeSession == null) return;
        
        var existing = _activeSession.PassagesPracticed.FirstOrDefault(p => 
            p.PieceId == pieceId && p.StartMeasure == startMeasure && p.EndMeasure == endMeasure);
        
        if (existing != null)
        {
            var index = _activeSession.PassagesPracticed.IndexOf(existing);
            _activeSession.PassagesPracticed[index] = existing with
            {
                RepetitionCount = existing.RepetitionCount + 1,
                FinalAccuracy = accuracy,
                Notes = notes ?? existing.Notes
            };
        }
        else
        {
            _activeSession.PassagesPracticed.Add(new PracticedPassage
            {
                PieceId = pieceId,
                StartMeasure = startMeasure,
                EndMeasure = endMeasure,
                RepetitionCount = 1,
                InitialAccuracy = accuracy,
                FinalAccuracy = accuracy,
                Notes = notes
            });
        }
    }
    
    /// <summary>
    /// Ends the current session.
    /// </summary>
    public PracticeSession? EndSession()
    {
        if (_activeSession == null) return null;
        
        _activeSession.EndSession();
        
        if (!_sessionsByStudent.ContainsKey(_activeSession.StudentId))
        {
            _sessionsByStudent[_activeSession.StudentId] = [];
        }
        
        _sessionsByStudent[_activeSession.StudentId].Add(_activeSession);
        
        var completed = _activeSession;
        _activeSession = null;
        
        return completed;
    }
    
    /// <summary>
    /// Gets session history for a student.
    /// </summary>
    public List<PracticeSession> GetSessionHistory(string studentId, int? limit = null)
    {
        if (!_sessionsByStudent.TryGetValue(studentId, out var sessions))
        {
            return [];
        }
        
        var ordered = sessions.OrderByDescending(s => s.StartTime);
        return limit.HasValue ? ordered.Take(limit.Value).ToList() : ordered.ToList();
    }
    
    /// <summary>
    /// Gets practice statistics for a student.
    /// </summary>
    public PracticeStatistics GetStatistics(string studentId, DateTime? since = null)
    {
        var sessions = GetSessionHistory(studentId);
        
        if (since.HasValue)
        {
            sessions = sessions.Where(s => s.StartTime >= since.Value).ToList();
        }
        
        if (sessions.Count == 0)
        {
            return new PracticeStatistics { StudentId = studentId };
        }
        
        return new PracticeStatistics
        {
            StudentId = studentId,
            TotalSessions = sessions.Count,
            TotalMinutes = sessions.Sum(s => s.DurationMinutes),
            AverageSessionLength = sessions.Average(s => s.DurationMinutes),
            TotalPiecesPracticed = sessions.SelectMany(s => s.PiecesPracticed).Select(p => p.PieceId).Distinct().Count(),
            TotalAttempts = sessions.Sum(s => s.PiecesPracticed.Sum(p => p.AttemptCount)),
            AverageScore = sessions.Where(s => s.PiecesPracticed.Any()).Average(s => s.AverageScore),
            MostPracticedPiece = sessions.SelectMany(s => s.PiecesPracticed)
                .GroupBy(p => p.PieceId)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.First().Title,
            PracticeStreak = CalculatePracticeStreak(sessions),
            LastPracticeDate = sessions.Max(s => s.StartTime)
        };
    }
    
    private int CalculatePracticeStreak(List<PracticeSession> sessions)
    {
        if (sessions.Count == 0) return 0;
        
        var dates = sessions
            .Select(s => s.StartTime.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();
        
        if (dates[0] < DateTime.UtcNow.Date.AddDays(-1))
        {
            return 0; // Streak broken
        }
        
        int streak = 1;
        for (int i = 1; i < dates.Count; i++)
        {
            if (dates[i] == dates[i - 1].AddDays(-1))
            {
                streak++;
            }
            else
            {
                break;
            }
        }
        
        return streak;
    }
}

/// <summary>
/// Practice statistics for a student.
/// </summary>
public record PracticeStatistics
{
    public string StudentId { get; init; } = "";
    public int TotalSessions { get; init; }
    public int TotalMinutes { get; init; }
    public double AverageSessionLength { get; init; }
    public int TotalPiecesPracticed { get; init; }
    public int TotalAttempts { get; init; }
    public double AverageScore { get; init; }
    public string? MostPracticedPiece { get; init; }
    public int PracticeStreak { get; init; }
    public DateTime? LastPracticeDate { get; init; }
    
    public int TotalHours => TotalMinutes / 60;
}

