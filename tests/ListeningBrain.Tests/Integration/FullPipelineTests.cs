using NUnit.Framework;
using FluentAssertions;
using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;
using ListeningBrain.Pipeline;

namespace ListeningBrain.Tests.Integration;

[TestFixture]
public class FullPipelineTests
{
    [Test]
    public void FullPipeline_SimplePiece_GeneratesComprehensiveFeedback()
    {
        // Arrange
        var score = CreateTwinkleTwinkle();
        var performance = CreateTwinklePerformance(score, accuracyLevel: 0.9);
        var pipeline = new EvaluationPipeline();
        
        // Act
        var result = pipeline.Evaluate(score, performance);
        
        // Assert
        result.Should().NotBeNull();
        result.NoteAccuracy.Should().NotBeNull();
        result.Rhythm.Should().NotBeNull();
        result.Tempo.Should().NotBeNull();
        result.Feedback.Should().NotBeNull();
        
        result.Feedback.Summary.Should().NotBeNullOrEmpty();
        result.OverallScore.Should().BeGreaterThan(0);
        result.Grade.Should().NotBeNullOrEmpty();
    }
    
    [Test]
    public void FullPipeline_PerfectPerformance_AchievesHighScore()
    {
        // Arrange
        var score = CreateSimpleScale();
        var performance = CreatePerfectPerformance(score);
        var pipeline = new EvaluationPipeline();
        
        // Act
        var result = pipeline.Evaluate(score, performance);
        
        // Assert
        result.OverallScore.Should().BeGreaterOrEqualTo(90);
        result.Feedback.Strengths.Should().NotBeEmpty();
        result.Feedback.AreasForImprovement.Should().BeEmpty();
    }
    
    [Test]
    public void FullPipeline_PoorPerformance_IdentifiesIssues()
    {
        // Arrange
        var score = CreateSimpleScale();
        var performance = CreatePoorPerformance(score);
        var pipeline = new EvaluationPipeline();
        
        // Act
        var result = pipeline.Evaluate(score, performance);
        
        // Assert
        result.OverallScore.Should().BeLessThan(70);
        result.Feedback.PrioritizedIssues.Should().NotBeEmpty();
        result.Feedback.PracticeSuggestions.Should().NotBeEmpty();
    }
    
    [Test]
    public void BeginnerPipeline_MoreLenient_HigherScores()
    {
        // Arrange
        var score = CreateSimpleScale();
        var performance = CreateModeratePerformance(score);
        var beginnerPipeline = EvaluationPipeline.ForBeginners();
        var standardPipeline = new EvaluationPipeline();
        
        // Act
        var beginnerResult = beginnerPipeline.Evaluate(score, performance);
        var standardResult = standardPipeline.Evaluate(score, performance);
        
        // Assert
        beginnerResult.OverallScore.Should().BeGreaterOrEqualTo(standardResult.OverallScore);
    }
    
    [Test]
    public void AdvancedPipeline_MoreStrict_LowerScores()
    {
        // Arrange
        var score = CreateSimpleScale();
        var performance = CreateModeratePerformance(score);
        var advancedPipeline = EvaluationPipeline.ForAdvanced();
        var standardPipeline = new EvaluationPipeline();
        
        // Act
        var advancedResult = advancedPipeline.Evaluate(score, performance);
        var standardResult = standardPipeline.Evaluate(score, performance);
        
        // Assert
        advancedResult.OverallScore.Should().BeLessThanOrEqualTo(standardResult.OverallScore);
    }
    
    [Test]
    public void QuickEvaluation_ReturnsBasicMetrics()
    {
        // Arrange
        var score = CreateSimpleScale();
        var performance = CreatePerfectPerformance(score);
        var pipeline = new EvaluationPipeline();
        
        // Act
        var result = pipeline.EvaluateNotesOnly(score, performance);
        
        // Assert
        result.Should().NotBeNull();
        result.AccuracyPercent.Should().BeGreaterOrEqualTo(90);
        result.CorrectNotes.Should().Be(result.TotalNotes);
    }
    
    [Test]
    public void ProcessingTime_ReasonableForSmallPiece()
    {
        // Arrange
        var score = CreateSimpleScale();
        var performance = CreatePerfectPerformance(score);
        var pipeline = new EvaluationPipeline();
        
        // Act
        var result = pipeline.Evaluate(score, performance);
        
        // Assert
        result.TotalProcessingTime.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }
    
    [Test]
    public void MeasureReports_GeneratedForEachMeasure()
    {
        // Arrange
        var score = CreateTwinkleTwinkle();
        var performance = CreateTwinklePerformance(score, 0.85);
        var pipeline = new EvaluationPipeline();
        
        // Act
        var result = pipeline.Evaluate(score, performance);
        
        // Assert
        result.Feedback.MeasureReports.Should().NotBeEmpty();
        result.Feedback.MeasureReports.All(m => m.Measure >= 1).Should().BeTrue();
    }
    
    // Helper methods
    private Score CreateSimpleScale()
    {
        var notes = new List<ScoreNote>();
        int[] pitches = { 60, 62, 64, 65, 67 };
        
        for (int i = 0; i < pitches.Length; i++)
        {
            notes.Add(new ScoreNote
            {
                Id = Guid.NewGuid(),
                Pitch = pitches[i],
                Velocity = 80,
                StartTick = i * 480,
                DurationTicks = 480,
                StartTimeMs = i * 500,
                DurationMs = 500,
                Measure = 1,
                Beat = i + 1,
                RhythmicValue = RhythmicValue.Quarter
            });
        }
        
        return new Score 
        { 
            Notes = notes, 
            PPQ = 480,
            TotalMeasures = 1,
            Title = "Simple Scale"
        };
    }
    
    private Score CreateTwinkleTwinkle()
    {
        // First phrase of Twinkle Twinkle
        var notes = new List<ScoreNote>();
        int[] pitches = { 60, 60, 67, 67, 69, 69, 67, 65, 65, 64, 64, 62, 62, 60 };
        
        for (int i = 0; i < pitches.Length; i++)
        {
            notes.Add(new ScoreNote
            {
                Id = Guid.NewGuid(),
                Pitch = pitches[i],
                Velocity = 80,
                StartTick = i * 240,
                DurationTicks = 240,
                StartTimeMs = i * 250,
                DurationMs = 250,
                Measure = (i / 4) + 1,
                Beat = (i % 4) + 1,
                RhythmicValue = RhythmicValue.Eighth
            });
        }
        
        return new Score
        {
            Notes = notes,
            PPQ = 480,
            TotalMeasures = 4,
            Title = "Twinkle Twinkle"
        };
    }
    
    private Performance CreatePerfectPerformance(Score score)
    {
        var notes = score.Notes.Select(s => new PerformanceNote
        {
            Id = Guid.NewGuid(),
            Pitch = s.Pitch,
            Velocity = s.Velocity,
            StartTick = s.StartTick,
            DurationTicks = s.DurationTicks,
            StartTimeMs = s.StartTimeMs,
            DurationMs = s.DurationMs,
            ReceivedTimestamp = DateTime.UtcNow
        }).ToList();
        
        return new Performance { Notes = notes };
    }
    
    private Performance CreatePoorPerformance(Score score)
    {
        var random = new Random(42);
        var notes = score.Notes.Take(score.Notes.Count - 2).Select((s, i) => new PerformanceNote
        {
            Id = Guid.NewGuid(),
            Pitch = i % 3 == 0 ? s.Pitch + 2 : s.Pitch, // Some wrong notes
            Velocity = s.Velocity,
            StartTick = s.StartTick,
            DurationTicks = s.DurationTicks,
            StartTimeMs = s.StartTimeMs + random.Next(-100, 200), // Variable timing
            DurationMs = s.DurationMs,
            ReceivedTimestamp = DateTime.UtcNow
        }).ToList();
        
        return new Performance { Notes = notes };
    }
    
    private Performance CreateModeratePerformance(Score score)
    {
        var random = new Random(42);
        var notes = score.Notes.Select((s, i) => new PerformanceNote
        {
            Id = Guid.NewGuid(),
            Pitch = i == 2 ? s.Pitch + 1 : s.Pitch, // One wrong note
            Velocity = s.Velocity,
            StartTick = s.StartTick,
            DurationTicks = s.DurationTicks,
            StartTimeMs = s.StartTimeMs + random.Next(-30, 60), // Slight timing variation
            DurationMs = s.DurationMs,
            ReceivedTimestamp = DateTime.UtcNow
        }).ToList();
        
        return new Performance { Notes = notes };
    }
    
    private Performance CreateTwinklePerformance(Score score, double accuracyLevel)
    {
        var random = new Random(42);
        var notes = new List<PerformanceNote>();
        
        foreach (var (scoreNote, index) in score.Notes.Select((n, i) => (n, i)))
        {
            bool shouldMiss = random.NextDouble() > accuracyLevel;
            
            if (shouldMiss && random.NextDouble() > 0.5)
            {
                continue; // Skip this note (missed)
            }
            
            notes.Add(new PerformanceNote
            {
                Id = Guid.NewGuid(),
                Pitch = shouldMiss ? scoreNote.Pitch + (random.Next(2) * 2 - 1) : scoreNote.Pitch,
                Velocity = scoreNote.Velocity + random.Next(-10, 10),
                StartTick = scoreNote.StartTick,
                DurationTicks = scoreNote.DurationTicks,
                StartTimeMs = scoreNote.StartTimeMs + random.Next(-40, 60),
                DurationMs = scoreNote.DurationMs,
                ReceivedTimestamp = DateTime.UtcNow,
                SequenceIndex = index
            });
        }
        
        return new Performance { Notes = notes };
    }
}

