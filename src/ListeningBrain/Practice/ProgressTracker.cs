using ListeningBrain.Intelligence;

namespace ListeningBrain.Practice;

/// <summary>
/// Tracks and visualizes progress over time.
/// </summary>
public class ProgressTracker
{
    /// <summary>
    /// Gets progress data for visualization.
    /// </summary>
    public ProgressVisualization GetProgressVisualization(
        StudentProfile profile,
        TimeSpan period)
    {
        var startDate = DateTime.UtcNow - period;
        var history = profile.PerformanceHistory
            .Where(p => p.Timestamp >= startDate)
            .OrderBy(p => p.Timestamp)
            .ToList();
        
        if (history.Count == 0)
        {
            return new ProgressVisualization
            {
                HasData = false,
                Message = "No practice data in the selected period"
            };
        }
        
        return new ProgressVisualization
        {
            HasData = true,
            Period = period,
            StartDate = startDate,
            EndDate = DateTime.UtcNow,
            
            // Overall score over time
            ScoreTimeline = history.Select(h => new TimelinePoint
            {
                Date = h.Timestamp,
                Value = h.OverallScore,
                Label = h.PieceTitle
            }).ToList(),
            
            // Skill progress
            SkillProgress = CalculateSkillProgress(profile, history),
            
            // Practice frequency
            PracticeFrequency = CalculatePracticeFrequency(history, period),
            
            // Milestones achieved
            Milestones = DetectMilestones(history, profile),
            
            // Summary statistics
            Statistics = CalculateProgressStatistics(history, profile),
            
            // Trend analysis
            TrendAnalysis = AnalyzeTrends(history)
        };
    }
    
    /// <summary>
    /// Gets skill radar chart data.
    /// </summary>
    public SkillRadarData GetSkillRadarData(StudentProfile profile)
    {
        return new SkillRadarData
        {
            Labels = new[]
            {
                "Note Accuracy",
                "Rhythm",
                "Tempo Control",
                "Dynamics",
                "Articulation",
                "Phrasing",
                "Pedal",
                "Sight Reading"
            },
            CurrentValues = new[]
            {
                profile.Skills.NoteAccuracy,
                profile.Skills.Rhythm,
                profile.Skills.TempoControl,
                profile.Skills.Dynamics,
                profile.Skills.Articulation,
                profile.Skills.Phrasing,
                profile.Skills.PedalTechnique,
                profile.Skills.SightReading
            },
            TargetValues = Enumerable.Repeat(80.0, 8).ToArray() // Target for each skill
        };
    }
    
    /// <summary>
    /// Gets piece progress data.
    /// </summary>
    public PieceProgressData GetPieceProgress(StudentProfile profile, string pieceId)
    {
        var pieceHistory = profile.PerformanceHistory
            .Where(p => p.PieceId == pieceId)
            .OrderBy(p => p.Timestamp)
            .ToList();
        
        if (pieceHistory.Count == 0)
        {
            return new PieceProgressData { HasData = false };
        }
        
        var repertoireItem = profile.Repertoire.FirstOrDefault(r => r.PieceId == pieceId);
        
        return new PieceProgressData
        {
            HasData = true,
            PieceId = pieceId,
            Title = pieceHistory.First().PieceTitle,
            TotalAttempts = pieceHistory.Count,
            FirstScore = pieceHistory.First().OverallScore,
            BestScore = pieceHistory.Max(p => p.OverallScore),
            LatestScore = pieceHistory.Last().OverallScore,
            Improvement = pieceHistory.Last().OverallScore - pieceHistory.First().OverallScore,
            ScoreHistory = pieceHistory.Select(p => new TimelinePoint
            {
                Date = p.Timestamp,
                Value = p.OverallScore
            }).ToList(),
            Status = repertoireItem?.Status ?? RepertoireStatus.Learning,
            TimeToMastery = EstimateTimeToMastery(pieceHistory)
        };
    }
    
    /// <summary>
    /// Gets weekly practice report.
    /// </summary>
    public WeeklyReport GetWeeklyReport(StudentProfile profile)
    {
        var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
        var weekHistory = profile.PerformanceHistory
            .Where(p => p.Timestamp >= weekStart)
            .ToList();
        
        var previousWeek = profile.PerformanceHistory
            .Where(p => p.Timestamp >= weekStart.AddDays(-7) && p.Timestamp < weekStart)
            .ToList();
        
        return new WeeklyReport
        {
            WeekStarting = weekStart,
            TotalPracticeMinutes = weekHistory.Sum(p => p.DurationMinutes),
            SessionCount = weekHistory.Count,
            PiecesPracticed = weekHistory.Select(p => p.PieceTitle).Distinct().Count(),
            AverageScore = weekHistory.Count > 0 ? weekHistory.Average(p => p.OverallScore) : 0,
            
            // Comparison with previous week
            MinutesChange = weekHistory.Sum(p => p.DurationMinutes) - previousWeek.Sum(p => p.DurationMinutes),
            ScoreChange = weekHistory.Count > 0 && previousWeek.Count > 0
                ? weekHistory.Average(p => p.OverallScore) - previousWeek.Average(p => p.OverallScore)
                : 0,
            
            // Daily breakdown
            DailyBreakdown = Enumerable.Range(0, 7)
                .Select(i => new DailyPractice
                {
                    Date = weekStart.AddDays(i),
                    DayOfWeek = weekStart.AddDays(i).DayOfWeek,
                    MinutesPracticed = weekHistory
                        .Where(p => p.Timestamp.Date == weekStart.AddDays(i).Date)
                        .Sum(p => p.DurationMinutes),
                    SessionCount = weekHistory
                        .Count(p => p.Timestamp.Date == weekStart.AddDays(i).Date)
                })
                .ToList(),
            
            // Goals progress
            GoalsProgress = profile.Goals
                .Where(g => !g.IsComplete)
                .Select(g => new GoalProgress
                {
                    GoalTitle = g.Title,
                    Progress = g.Progress,
                    RemainingValue = g.TargetValue - g.CurrentValue
                })
                .ToList(),
            
            // Achievements
            Achievements = DetectWeeklyAchievements(weekHistory, previousWeek, profile)
        };
    }
    
    private Dictionary<string, SkillProgressData> CalculateSkillProgress(
        StudentProfile profile,
        List<PerformanceRecord> history)
    {
        var result = new Dictionary<string, SkillProgressData>();
        
        void AddSkillProgress(string skill, Func<PerformanceRecord, double> selector, double current)
        {
            if (history.Count < 2)
            {
                result[skill] = new SkillProgressData
                {
                    SkillName = skill,
                    CurrentLevel = current,
                    StartLevel = current,
                    Change = 0,
                    Trend = TrendDirection.Stable
                };
                return;
            }
            
            var first = history.Take(history.Count / 2).Average(selector);
            var second = history.Skip(history.Count / 2).Average(selector);
            
            result[skill] = new SkillProgressData
            {
                SkillName = skill,
                CurrentLevel = current,
                StartLevel = history.First().NoteAccuracyScore,
                Change = current - first,
                Trend = second > first + 3 ? TrendDirection.Improving
                    : second < first - 3 ? TrendDirection.Declining
                    : TrendDirection.Stable
            };
        }
        
        AddSkillProgress("Note Accuracy", p => p.NoteAccuracyScore, profile.Skills.NoteAccuracy);
        AddSkillProgress("Rhythm", p => p.RhythmScore, profile.Skills.Rhythm);
        AddSkillProgress("Tempo", p => p.TempoScore, profile.Skills.TempoControl);
        AddSkillProgress("Dynamics", p => p.DynamicsScore, profile.Skills.Dynamics);
        AddSkillProgress("Expression", p => p.ExpressionScore, profile.Skills.Phrasing);
        
        return result;
    }
    
    private List<FrequencyPoint> CalculatePracticeFrequency(
        List<PerformanceRecord> history,
        TimeSpan period)
    {
        if (period.TotalDays <= 7)
        {
            // Daily frequency
            return history
                .GroupBy(h => h.Timestamp.Date)
                .Select(g => new FrequencyPoint
                {
                    Date = g.Key,
                    Count = g.Count(),
                    TotalMinutes = g.Sum(p => p.DurationMinutes)
                })
                .OrderBy(f => f.Date)
                .ToList();
        }
        else
        {
            // Weekly frequency
            return history
                .GroupBy(h => GetWeekStart(h.Timestamp))
                .Select(g => new FrequencyPoint
                {
                    Date = g.Key,
                    Count = g.Count(),
                    TotalMinutes = g.Sum(p => p.DurationMinutes)
                })
                .OrderBy(f => f.Date)
                .ToList();
        }
    }
    
    private DateTime GetWeekStart(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }
    
    private List<Milestone> DetectMilestones(
        List<PerformanceRecord> history,
        StudentProfile profile)
    {
        var milestones = new List<Milestone>();
        
        // First 90+ score
        var first90 = history.FirstOrDefault(h => h.OverallScore >= 90);
        if (first90 != null)
        {
            milestones.Add(new Milestone
            {
                Title = "First Excellent Score",
                Description = $"Achieved 90%+ on {first90.PieceTitle}",
                Date = first90.Timestamp,
                Type = MilestoneType.Score
            });
        }
        
        // Mastered pieces
        foreach (var piece in profile.Repertoire.Where(r => r.Status == RepertoireStatus.Mastered))
        {
            milestones.Add(new Milestone
            {
                Title = "Piece Mastered",
                Description = $"Mastered {piece.Title}",
                Date = piece.MasteredAt ?? DateTime.UtcNow,
                Type = MilestoneType.Mastery
            });
        }
        
        // Practice streaks
        if (profile.PracticeStreak >= 7)
        {
            milestones.Add(new Milestone
            {
                Title = "Week Streak",
                Description = $"Practiced for {profile.PracticeStreak} days in a row!",
                Date = DateTime.UtcNow,
                Type = MilestoneType.Streak
            });
        }
        
        // Skill improvements
        if (profile.Skills.Average >= 70)
        {
            milestones.Add(new Milestone
            {
                Title = "Intermediate Level",
                Description = "Reached intermediate skill level overall",
                Date = DateTime.UtcNow,
                Type = MilestoneType.Level
            });
        }
        
        return milestones.OrderByDescending(m => m.Date).ToList();
    }
    
    private ProgressStatistics CalculateProgressStatistics(
        List<PerformanceRecord> history,
        StudentProfile profile)
    {
        if (history.Count == 0)
        {
            return new ProgressStatistics();
        }
        
        var firstHalf = history.Take(history.Count / 2).ToList();
        var secondHalf = history.Skip(history.Count / 2).ToList();
        
        return new ProgressStatistics
        {
            TotalSessions = history.Count,
            TotalMinutes = history.Sum(h => h.DurationMinutes),
            AverageScore = history.Average(h => h.OverallScore),
            HighestScore = history.Max(h => h.OverallScore),
            ImprovementRate = secondHalf.Count > 0 && firstHalf.Count > 0
                ? secondHalf.Average(h => h.OverallScore) - firstHalf.Average(h => h.OverallScore)
                : 0,
            ConsistencyScore = 100 - CalculateStdDev(history.Select(h => h.OverallScore).ToList()),
            UniquesPiecePracticed = history.Select(h => h.PieceId).Distinct().Count()
        };
    }
    
    private TrendAnalysis AnalyzeTrends(List<PerformanceRecord> history)
    {
        if (history.Count < 3)
        {
            return new TrendAnalysis { HasEnoughData = false };
        }
        
        // Linear regression on scores over time
        var xValues = history.Select((h, i) => (double)i).ToList();
        var yValues = history.Select(h => h.OverallScore).ToList();
        
        double slope = CalculateSlope(xValues, yValues);
        
        return new TrendAnalysis
        {
            HasEnoughData = true,
            OverallTrend = slope > 0.5 ? TrendDirection.Improving
                : slope < -0.5 ? TrendDirection.Declining
                : TrendDirection.Stable,
            TrendStrength = Math.Abs(slope),
            ProjectedScore30Days = history.Last().OverallScore + (slope * 30),
            Recommendation = slope > 0.5
                ? "Great progress! Keep up the consistent practice."
                : slope < -0.5
                    ? "Scores are declining. Consider focusing on fundamentals."
                    : "Maintaining steady progress. Consider new challenges."
        };
    }
    
    private List<string> DetectWeeklyAchievements(
        List<PerformanceRecord> thisWeek,
        List<PerformanceRecord> lastWeek,
        StudentProfile profile)
    {
        var achievements = new List<string>();
        
        if (thisWeek.Count > lastWeek.Count)
        {
            achievements.Add($"Practiced more sessions than last week! ({thisWeek.Count} vs {lastWeek.Count})");
        }
        
        if (thisWeek.Any(p => p.OverallScore >= 95))
        {
            achievements.Add("Achieved a near-perfect score!");
        }
        
        var newBests = thisWeek
            .Where(t => !lastWeek.Any(l => l.PieceId == t.PieceId && l.OverallScore >= t.OverallScore));
        
        if (newBests.Any())
        {
            achievements.Add($"Set new personal best on {newBests.Count()} piece(s)!");
        }
        
        if (profile.PracticeStreak >= 7)
        {
            achievements.Add($"🔥 {profile.PracticeStreak}-day practice streak!");
        }
        
        return achievements;
    }
    
    private string? EstimateTimeToMastery(List<PerformanceRecord> history)
    {
        if (history.Count < 3) return "Need more data";
        
        var latestScore = history.Last().OverallScore;
        if (latestScore >= 90) return "Already at mastery level!";
        
        var improvement = CalculateSlope(
            history.Select((h, i) => (double)i).ToList(),
            history.Select(h => h.OverallScore).ToList());
        
        if (improvement <= 0) return "Focus on improvement strategies";
        
        double sessionsNeeded = (90 - latestScore) / improvement;
        return $"~{Math.Ceiling(sessionsNeeded)} more sessions";
    }
    
    private double CalculateSlope(List<double> x, List<double> y)
    {
        if (x.Count != y.Count || x.Count < 2) return 0;
        
        double avgX = x.Average();
        double avgY = y.Average();
        
        double numerator = x.Zip(y, (xi, yi) => (xi - avgX) * (yi - avgY)).Sum();
        double denominator = x.Sum(xi => Math.Pow(xi - avgX, 2));
        
        return denominator != 0 ? numerator / denominator : 0;
    }
    
    private double CalculateStdDev(List<double> values)
    {
        if (values.Count <= 1) return 0;
        double avg = values.Average();
        return Math.Sqrt(values.Sum(v => Math.Pow(v - avg, 2)) / (values.Count - 1));
    }
}

// Visualization data structures
public record ProgressVisualization
{
    public bool HasData { get; init; }
    public string? Message { get; init; }
    public TimeSpan Period { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public List<TimelinePoint> ScoreTimeline { get; init; } = [];
    public Dictionary<string, SkillProgressData> SkillProgress { get; init; } = new();
    public List<FrequencyPoint> PracticeFrequency { get; init; } = [];
    public List<Milestone> Milestones { get; init; } = [];
    public ProgressStatistics Statistics { get; init; } = new();
    public TrendAnalysis TrendAnalysis { get; init; } = new();
}

public record TimelinePoint
{
    public DateTime Date { get; init; }
    public double Value { get; init; }
    public string? Label { get; init; }
}

public record SkillProgressData
{
    public string SkillName { get; init; } = "";
    public double CurrentLevel { get; init; }
    public double StartLevel { get; init; }
    public double Change { get; init; }
    public TrendDirection Trend { get; init; }
}

public record FrequencyPoint
{
    public DateTime Date { get; init; }
    public int Count { get; init; }
    public int TotalMinutes { get; init; }
}

public record Milestone
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public DateTime Date { get; init; }
    public MilestoneType Type { get; init; }
}

public enum MilestoneType { Score, Mastery, Streak, Level, Goal }

public record ProgressStatistics
{
    public int TotalSessions { get; init; }
    public int TotalMinutes { get; init; }
    public double AverageScore { get; init; }
    public double HighestScore { get; init; }
    public double ImprovementRate { get; init; }
    public double ConsistencyScore { get; init; }
    public int UniquesPiecePracticed { get; init; }
}

public record TrendAnalysis
{
    public bool HasEnoughData { get; init; }
    public TrendDirection OverallTrend { get; init; }
    public double TrendStrength { get; init; }
    public double ProjectedScore30Days { get; init; }
    public string Recommendation { get; init; } = "";
}

public record SkillRadarData
{
    public string[] Labels { get; init; } = [];
    public double[] CurrentValues { get; init; } = [];
    public double[] TargetValues { get; init; } = [];
}

public record PieceProgressData
{
    public bool HasData { get; init; }
    public string PieceId { get; init; } = "";
    public string Title { get; init; } = "";
    public int TotalAttempts { get; init; }
    public double FirstScore { get; init; }
    public double BestScore { get; init; }
    public double LatestScore { get; init; }
    public double Improvement { get; init; }
    public List<TimelinePoint> ScoreHistory { get; init; } = [];
    public RepertoireStatus Status { get; init; }
    public string? TimeToMastery { get; init; }
}

public record WeeklyReport
{
    public DateTime WeekStarting { get; init; }
    public int TotalPracticeMinutes { get; init; }
    public int SessionCount { get; init; }
    public int PiecesPracticed { get; init; }
    public double AverageScore { get; init; }
    public int MinutesChange { get; init; }
    public double ScoreChange { get; init; }
    public List<DailyPractice> DailyBreakdown { get; init; } = [];
    public List<GoalProgress> GoalsProgress { get; init; } = [];
    public List<string> Achievements { get; init; } = [];
}

public record DailyPractice
{
    public DateTime Date { get; init; }
    public DayOfWeek DayOfWeek { get; init; }
    public int MinutesPracticed { get; init; }
    public int SessionCount { get; init; }
}

public record GoalProgress
{
    public string GoalTitle { get; init; } = "";
    public double Progress { get; init; }
    public double RemainingValue { get; init; }
}

