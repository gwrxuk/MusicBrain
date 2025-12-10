using ListeningBrain.Intelligence;

namespace ListeningBrain.Practice;

/// <summary>
/// Implements spaced repetition for efficient practice scheduling.
/// Uses a modified SM-2 algorithm adapted for musical practice.
/// </summary>
public class SpacedRepetitionScheduler
{
    /// <summary>
    /// Default ease factor for new items.
    /// </summary>
    private const double DefaultEaseFactor = 2.5;
    
    /// <summary>
    /// Minimum ease factor.
    /// </summary>
    private const double MinEaseFactor = 1.3;
    
    /// <summary>
    /// Gets the next review schedule for a passage.
    /// </summary>
    public ReviewSchedule ScheduleReview(PassageReviewItem item, ReviewQuality quality)
    {
        double newEase = CalculateNewEaseFactor(item.EaseFactor, quality);
        int newInterval = CalculateNewInterval(item, quality);
        int newRepetitions = quality >= ReviewQuality.Good 
            ? item.Repetitions + 1 
            : 0;
        
        return new ReviewSchedule
        {
            NextReviewDate = DateTime.UtcNow.AddDays(newInterval),
            IntervalDays = newInterval,
            EaseFactor = newEase,
            Repetitions = newRepetitions,
            Quality = quality
        };
    }
    
    /// <summary>
    /// Creates a new review item for a difficult passage.
    /// </summary>
    public PassageReviewItem CreateReviewItem(
        string pieceId,
        string pieceTitle,
        int startMeasure,
        int endMeasure,
        double initialScore,
        string? notes = null)
    {
        return new PassageReviewItem
        {
            Id = Guid.NewGuid(),
            PieceId = pieceId,
            PieceTitle = pieceTitle,
            StartMeasure = startMeasure,
            EndMeasure = endMeasure,
            EaseFactor = DefaultEaseFactor,
            Interval = 1,
            Repetitions = 0,
            LastReviewDate = DateTime.UtcNow,
            NextReviewDate = DateTime.UtcNow.AddDays(1),
            LastScore = initialScore,
            Notes = notes
        };
    }
    
    /// <summary>
    /// Gets passages due for review today.
    /// </summary>
    public List<PassageReviewItem> GetDueReviews(
        List<PassageReviewItem> items,
        DateTime? asOfDate = null)
    {
        var date = asOfDate ?? DateTime.UtcNow;
        
        return items
            .Where(i => i.NextReviewDate <= date)
            .OrderBy(i => i.NextReviewDate)
            .ThenBy(i => i.LastScore) // Harder passages first
            .ToList();
    }
    
    /// <summary>
    /// Gets upcoming reviews for the next N days.
    /// </summary>
    public Dictionary<DateTime, List<PassageReviewItem>> GetUpcomingReviews(
        List<PassageReviewItem> items,
        int days = 7)
    {
        var result = new Dictionary<DateTime, List<PassageReviewItem>>();
        var startDate = DateTime.UtcNow.Date;
        
        for (int i = 0; i <= days; i++)
        {
            var date = startDate.AddDays(i);
            result[date] = items
                .Where(item => item.NextReviewDate.Date == date)
                .ToList();
        }
        
        return result;
    }
    
    /// <summary>
    /// Processes a review and updates the item.
    /// </summary>
    public void ProcessReview(PassageReviewItem item, double score)
    {
        var quality = ScoreToQuality(score);
        var schedule = ScheduleReview(item, quality);
        
        item.EaseFactor = schedule.EaseFactor;
        item.Interval = schedule.IntervalDays;
        item.Repetitions = schedule.Repetitions;
        item.LastReviewDate = DateTime.UtcNow;
        item.NextReviewDate = schedule.NextReviewDate;
        item.LastScore = score;
        item.ReviewHistory.Add(new ReviewRecord
        {
            Date = DateTime.UtcNow,
            Score = score,
            Quality = quality
        });
    }
    
    /// <summary>
    /// Auto-identifies passages that should be added to spaced repetition.
    /// </summary>
    public List<PassageSuggestion> IdentifyProblemPassages(
        StudentProfile profile,
        List<ErrorPattern> errorPatterns)
    {
        var suggestions = new List<PassageSuggestion>();
        
        // Find recurring errors in specific passages
        var passageErrors = errorPatterns
            .Where(e => e.AffectedMeasures.Count > 0)
            .GroupBy(e => new { e.PieceId, Start = e.AffectedMeasures.Min(), End = e.AffectedMeasures.Max() })
            .Where(g => g.Count() >= 2) // At least 2 occurrences
            .ToList();
        
        foreach (var group in passageErrors)
        {
            var pattern = group.First();
            var pieceTitle = profile.Repertoire
                .FirstOrDefault(r => r.PieceId == group.Key.PieceId)?.Title ?? "Unknown";
            
            suggestions.Add(new PassageSuggestion
            {
                PieceId = group.Key.PieceId,
                PieceTitle = pieceTitle,
                StartMeasure = group.Key.Start,
                EndMeasure = group.Key.End,
                ErrorTypes = group.Select(e => e.Type).Distinct().ToList(),
                OccurrenceCount = group.Count(),
                AverageErrorRate = group.Average(e => e.Severity),
                Reasoning = GeneratePassageReasoning(group.ToList())
            });
        }
        
        return suggestions
            .OrderByDescending(s => s.OccurrenceCount * s.AverageErrorRate)
            .Take(10)
            .ToList();
    }
    
    /// <summary>
    /// Gets statistics about spaced repetition progress.
    /// </summary>
    public SpacedRepetitionStats GetStatistics(List<PassageReviewItem> items)
    {
        if (items.Count == 0)
        {
            return new SpacedRepetitionStats();
        }
        
        var today = DateTime.UtcNow.Date;
        
        return new SpacedRepetitionStats
        {
            TotalPassages = items.Count,
            DueToday = items.Count(i => i.NextReviewDate.Date <= today),
            Overdue = items.Count(i => i.NextReviewDate.Date < today),
            Mastered = items.Count(i => i.Interval >= 30 && i.LastScore >= 90),
            Learning = items.Count(i => i.Interval < 7),
            AverageInterval = items.Average(i => i.Interval),
            AverageEase = items.Average(i => i.EaseFactor),
            TotalReviews = items.Sum(i => i.ReviewHistory.Count),
            ReviewsToday = items.Sum(i => i.ReviewHistory.Count(r => r.Date.Date == today)),
            LongestInterval = items.Max(i => i.Interval),
            HardestPassage = items.OrderBy(i => i.LastScore).FirstOrDefault()
        };
    }
    
    /// <summary>
    /// Generates a practice schedule for the day including spaced repetition.
    /// </summary>
    public DailyPracticeSchedule GenerateDailySchedule(
        List<PassageReviewItem> reviewItems,
        StudentProfile profile,
        int availableMinutes)
    {
        var dueItems = GetDueReviews(reviewItems);
        
        var schedule = new DailyPracticeSchedule
        {
            Date = DateTime.UtcNow.Date,
            TotalMinutes = availableMinutes
        };
        
        // Allocate time for reviews (max 40% of practice time)
        int reviewMinutes = Math.Min(availableMinutes * 40 / 100, dueItems.Count * 5);
        schedule.ReviewTime = reviewMinutes;
        schedule.ReviewItems = dueItems.Take(reviewMinutes / 5).ToList();
        
        // Rest goes to new material
        schedule.NewMaterialTime = availableMinutes - reviewMinutes;
        
        // Prioritize items
        schedule.Schedule = [];
        
        int minutesUsed = 0;
        
        // First: Overdue reviews
        foreach (var item in dueItems.Where(i => i.NextReviewDate < DateTime.UtcNow))
        {
            if (minutesUsed >= reviewMinutes) break;
            
            schedule.Schedule.Add(new ScheduleItem
            {
                Type = ScheduleItemType.OverdueReview,
                PassageReview = item,
                EstimatedMinutes = 5,
                Priority = 1
            });
            minutesUsed += 5;
        }
        
        // Second: Due today
        foreach (var item in dueItems.Where(i => i.NextReviewDate.Date == DateTime.UtcNow.Date))
        {
            if (minutesUsed >= reviewMinutes) break;
            
            schedule.Schedule.Add(new ScheduleItem
            {
                Type = ScheduleItemType.DueReview,
                PassageReview = item,
                EstimatedMinutes = 5,
                Priority = 2
            });
            minutesUsed += 5;
        }
        
        // Third: New material
        var currentPiece = profile.Repertoire
            .FirstOrDefault(r => r.Status == RepertoireStatus.Learning);
        
        if (currentPiece != null && minutesUsed < availableMinutes)
        {
            schedule.Schedule.Add(new ScheduleItem
            {
                Type = ScheduleItemType.NewMaterial,
                PieceTitle = currentPiece.Title,
                EstimatedMinutes = availableMinutes - minutesUsed,
                Priority = 3
            });
        }
        
        return schedule;
    }
    
    private double CalculateNewEaseFactor(double currentEase, ReviewQuality quality)
    {
        // SM-2 ease factor adjustment
        double adjustment = 0.1 - (5 - (int)quality) * (0.08 + (5 - (int)quality) * 0.02);
        double newEase = currentEase + adjustment;
        
        return Math.Max(MinEaseFactor, newEase);
    }
    
    private int CalculateNewInterval(PassageReviewItem item, ReviewQuality quality)
    {
        if (quality < ReviewQuality.Good)
        {
            // Failed review - start over
            return 1;
        }
        
        if (item.Repetitions == 0)
        {
            return 1;
        }
        else if (item.Repetitions == 1)
        {
            return 3;
        }
        else
        {
            return (int)Math.Round(item.Interval * item.EaseFactor);
        }
    }
    
    private ReviewQuality ScoreToQuality(double score)
    {
        if (score >= 95) return ReviewQuality.Perfect;
        if (score >= 90) return ReviewQuality.Excellent;
        if (score >= 80) return ReviewQuality.Good;
        if (score >= 70) return ReviewQuality.Okay;
        if (score >= 60) return ReviewQuality.Difficult;
        return ReviewQuality.Failed;
    }
    
    private string GeneratePassageReasoning(List<ErrorPattern> patterns)
    {
        var types = patterns.Select(p => p.Type).Distinct().ToList();
        
        if (types.Contains(ErrorPatternType.IntervalError))
            return "Consistent note errors - needs slow, careful practice";
        if (types.Contains(ErrorPatternType.RushingPattern))
            return "Tendency to rush - practice with metronome";
        if (types.Contains(ErrorPatternType.DraggingPattern))
            return "Tempo drags - focus on pulse consistency";
        if (types.Contains(ErrorPatternType.HighRegisterError))
            return "Accuracy drops in upper register - practice hands separately";
        
        return "Recurring difficulty - add to review queue";
    }
}

/// <summary>
/// A passage tracked for spaced repetition review.
/// </summary>
public class PassageReviewItem
{
    public Guid Id { get; set; }
    public string PieceId { get; set; } = "";
    public string PieceTitle { get; set; } = "";
    public int StartMeasure { get; set; }
    public int EndMeasure { get; set; }
    
    /// <summary>
    /// SM-2 ease factor (default 2.5).
    /// </summary>
    public double EaseFactor { get; set; } = 2.5;
    
    /// <summary>
    /// Current interval in days.
    /// </summary>
    public int Interval { get; set; } = 1;
    
    /// <summary>
    /// Number of successful repetitions.
    /// </summary>
    public int Repetitions { get; set; }
    
    public DateTime LastReviewDate { get; set; }
    public DateTime NextReviewDate { get; set; }
    public double LastScore { get; set; }
    public string? Notes { get; set; }
    
    public List<ReviewRecord> ReviewHistory { get; set; } = [];
    
    public string PassageDescription => $"mm. {StartMeasure}-{EndMeasure}";
    public bool IsMastered => Interval >= 30 && LastScore >= 90;
}

/// <summary>
/// Record of a single review.
/// </summary>
public record ReviewRecord
{
    public DateTime Date { get; init; }
    public double Score { get; init; }
    public ReviewQuality Quality { get; init; }
}

/// <summary>
/// Quality rating for a review (SM-2 based).
/// </summary>
public enum ReviewQuality
{
    Failed = 0,      // Complete failure
    Difficult = 1,   // Serious difficulty
    Okay = 2,        // Significant difficulty
    Good = 3,        // Correct with difficulty
    Excellent = 4,   // Correct with hesitation
    Perfect = 5      // Perfect response
}

/// <summary>
/// Schedule for next review.
/// </summary>
public record ReviewSchedule
{
    public DateTime NextReviewDate { get; init; }
    public int IntervalDays { get; init; }
    public double EaseFactor { get; init; }
    public int Repetitions { get; init; }
    public ReviewQuality Quality { get; init; }
}

/// <summary>
/// Suggested passage to add to spaced repetition.
/// </summary>
public record PassageSuggestion
{
    public string PieceId { get; init; } = "";
    public string PieceTitle { get; init; } = "";
    public int StartMeasure { get; init; }
    public int EndMeasure { get; init; }
    public List<ErrorPatternType> ErrorTypes { get; init; } = [];
    public int OccurrenceCount { get; init; }
    public double AverageErrorRate { get; init; }
    public string Reasoning { get; init; } = "";
}

/// <summary>
/// Statistics about spaced repetition progress.
/// </summary>
public record SpacedRepetitionStats
{
    public int TotalPassages { get; init; }
    public int DueToday { get; init; }
    public int Overdue { get; init; }
    public int Mastered { get; init; }
    public int Learning { get; init; }
    public double AverageInterval { get; init; }
    public double AverageEase { get; init; }
    public int TotalReviews { get; init; }
    public int ReviewsToday { get; init; }
    public int LongestInterval { get; init; }
    public PassageReviewItem? HardestPassage { get; init; }
}

/// <summary>
/// Daily practice schedule with spaced repetition.
/// </summary>
public record DailyPracticeSchedule
{
    public DateTime Date { get; init; }
    public int TotalMinutes { get; init; }
    public int ReviewTime { get; init; }
    public int NewMaterialTime { get; init; }
    public List<PassageReviewItem> ReviewItems { get; init; } = [];
    public List<ScheduleItem> Schedule { get; init; } = [];
}

/// <summary>
/// An item in the daily schedule.
/// </summary>
public record ScheduleItem
{
    public ScheduleItemType Type { get; init; }
    public PassageReviewItem? PassageReview { get; init; }
    public string? PieceTitle { get; init; }
    public int EstimatedMinutes { get; init; }
    public int Priority { get; init; }
}

/// <summary>
/// Type of scheduled item.
/// </summary>
public enum ScheduleItemType
{
    OverdueReview,
    DueReview,
    NewMaterial,
    WarmUp,
    Technical
}

