using NUnit.Framework;
using FluentAssertions;
using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;
using ListeningBrain.Evaluation;

namespace ListeningBrain.Tests.Evaluation;

[TestFixture]
public class NoteAccuracyTests
{
    private NoteAccuracyEvaluator _evaluator = null!;
    private DynamicTimeWarping _aligner = null!;
    
    [SetUp]
    public void Setup()
    {
        _evaluator = new NoteAccuracyEvaluator();
        _aligner = new DynamicTimeWarping();
    }
    
    [Test]
    public void Evaluate_PerfectPerformance_Returns100Percent()
    {
        // Arrange
        var score = CreateSimpleScale();
        var performance = CreateMatchingPerformance(score);
        var alignment = _aligner.Align(score, performance);
        
        // Act
        var result = _evaluator.Evaluate(alignment, score, performance);
        
        // Assert
        result.Score.Should().BeGreaterOrEqualTo(95);
        result.CorrectNotes.Should().Be(5);
        result.WrongNotes.Should().Be(0);
        result.MissedNotes.Should().Be(0);
        result.Grade.Should().Be("A+");
    }
    
    [Test]
    public void Evaluate_OneWrongNote_ReducesScore()
    {
        // Arrange
        var score = CreateSimpleScale();
        var performance = CreatePerformanceWithWrongNote(score, 2, 66); // F# instead of E
        var alignment = _aligner.Align(score, performance);
        
        // Act
        var result = _evaluator.Evaluate(alignment, score, performance);
        
        // Assert
        result.Score.Should().BeLessThan(95);
        result.WrongNotes.Should().BeGreaterThan(0);
        result.Issues.Should().Contain(i => i.Type == IssueType.WrongNote);
    }
    
    [Test]
    public void Evaluate_OctaveError_GivesPartialCredit()
    {
        // Arrange
        var score = CreateSimpleScale();
        var performance = CreatePerformanceWithOctaveError(score, 2); // E5 instead of E4
        var alignment = _aligner.Align(score, performance);
        
        // Act
        var result = _evaluator.Evaluate(alignment, score, performance);
        
        // Assert
        result.OctaveErrors.Should().BeGreaterThan(0);
        result.Score.Should().BeGreaterThan(70); // Partial credit
        result.Issues.Should().Contain(i => i.Type == IssueType.OctaveError);
    }
    
    [Test]
    public void Evaluate_MissedNotes_PenalizesCorrectly()
    {
        // Arrange
        var score = CreateSimpleScale();
        var performance = CreatePerformanceWithMissingNotes(score, new[] { 2, 3 }); // Missing E and F
        var alignment = _aligner.Align(score, performance);
        
        // Act
        var result = _evaluator.Evaluate(alignment, score, performance);
        
        // Assert
        result.MissedNotes.Should().Be(2);
        result.AccuracyPercent.Should().BeLessThan(70);
        result.Issues.Should().Contain(i => i.Type == IssueType.MissedNote);
    }
    
    [Test]
    public void Evaluate_ExtraNotes_AppliesMinorPenalty()
    {
        // Arrange
        var score = CreateSimpleScale();
        var performance = CreatePerformanceWithExtraNotes(score);
        var alignment = _aligner.Align(score, performance);
        
        // Act
        var result = _evaluator.Evaluate(alignment, score, performance);
        
        // Assert
        result.ExtraNotes.Should().BeGreaterThan(0);
        result.Score.Should().BeGreaterThan(80); // Extra notes are minor penalty
    }
    
    [Test]
    public void Evaluate_MeasureBreakdown_IdentifiesProblemAreas()
    {
        // Arrange
        var score = CreateTwoMeasureScale();
        var performance = CreatePerformanceWithMeasure1Errors(score);
        var alignment = _aligner.Align(score, performance);
        
        // Act
        var result = _evaluator.Evaluate(alignment, score, performance);
        
        // Assert
        result.MeasureBreakdown.Should().HaveCountGreaterThan(0);
        var measure1 = result.MeasureBreakdown.FirstOrDefault(m => m.Measure == 1);
        measure1.Should().NotBeNull();
    }
    
    [Test]
    public void Evaluate_StrictWeights_MorePunishing()
    {
        // Arrange
        _evaluator = new NoteAccuracyEvaluator { Weights = NoteAccuracyWeights.Strict };
        var score = CreateSimpleScale();
        var performance = CreatePerformanceWithWrongNote(score, 2, 66);
        var alignment = _aligner.Align(score, performance);
        
        // Act
        var result = _evaluator.Evaluate(alignment, score, performance);
        
        // Assert
        result.Score.Should().BeLessThan(85);
    }
    
    [Test]
    public void Evaluate_LenientWeights_MoreForgiving()
    {
        // Arrange
        _evaluator = new NoteAccuracyEvaluator { Weights = NoteAccuracyWeights.Lenient };
        var score = CreateSimpleScale();
        var performance = CreatePerformanceWithWrongNote(score, 2, 66);
        var alignment = _aligner.Align(score, performance);
        
        // Act
        var result = _evaluator.Evaluate(alignment, score, performance);
        
        // Assert
        result.Score.Should().BeGreaterThan(70);
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
        
        return new Score { Notes = notes, PPQ = 480, TotalMeasures = 1 };
    }
    
    private Score CreateTwoMeasureScale()
    {
        var notes = new List<ScoreNote>();
        int[] pitches = { 60, 62, 64, 65, 67, 69, 71, 72 };
        
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
                Measure = i < 4 ? 1 : 2,
                Beat = (i % 4) + 1,
                RhythmicValue = RhythmicValue.Quarter
            });
        }
        
        return new Score { Notes = notes, PPQ = 480, TotalMeasures = 2 };
    }
    
    private Performance CreateMatchingPerformance(Score score)
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
    
    private Performance CreatePerformanceWithWrongNote(Score score, int wrongIndex, int wrongPitch)
    {
        var notes = score.Notes.Select((s, i) => new PerformanceNote
        {
            Id = Guid.NewGuid(),
            Pitch = i == wrongIndex ? wrongPitch : s.Pitch,
            Velocity = s.Velocity,
            StartTick = s.StartTick,
            DurationTicks = s.DurationTicks,
            StartTimeMs = s.StartTimeMs,
            DurationMs = s.DurationMs,
            ReceivedTimestamp = DateTime.UtcNow
        }).ToList();
        
        return new Performance { Notes = notes };
    }
    
    private Performance CreatePerformanceWithOctaveError(Score score, int errorIndex)
    {
        var notes = score.Notes.Select((s, i) => new PerformanceNote
        {
            Id = Guid.NewGuid(),
            Pitch = i == errorIndex ? s.Pitch + 12 : s.Pitch, // One octave up
            Velocity = s.Velocity,
            StartTick = s.StartTick,
            DurationTicks = s.DurationTicks,
            StartTimeMs = s.StartTimeMs,
            DurationMs = s.DurationMs,
            ReceivedTimestamp = DateTime.UtcNow
        }).ToList();
        
        return new Performance { Notes = notes };
    }
    
    private Performance CreatePerformanceWithMissingNotes(Score score, int[] skipIndices)
    {
        var notes = score.Notes
            .Select((s, i) => (note: s, index: i))
            .Where(x => !skipIndices.Contains(x.index))
            .Select(x => new PerformanceNote
            {
                Id = Guid.NewGuid(),
                Pitch = x.note.Pitch,
                Velocity = x.note.Velocity,
                StartTick = x.note.StartTick,
                DurationTicks = x.note.DurationTicks,
                StartTimeMs = x.note.StartTimeMs,
                DurationMs = x.note.DurationMs,
                ReceivedTimestamp = DateTime.UtcNow
            }).ToList();
        
        return new Performance { Notes = notes };
    }
    
    private Performance CreatePerformanceWithExtraNotes(Score score)
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
        
        // Add extra note
        notes.Add(new PerformanceNote
        {
            Id = Guid.NewGuid(),
            Pitch = 70,
            Velocity = 60,
            StartTick = 2400,
            DurationTicks = 240,
            StartTimeMs = 2500,
            DurationMs = 250,
            ReceivedTimestamp = DateTime.UtcNow
        });
        
        return new Performance { Notes = notes };
    }
    
    private Performance CreatePerformanceWithMeasure1Errors(Score score)
    {
        var notes = score.Notes.Select((s, i) => new PerformanceNote
        {
            Id = Guid.NewGuid(),
            Pitch = (s.Measure == 1 && i == 1) ? s.Pitch + 1 : s.Pitch, // Wrong note in measure 1
            Velocity = s.Velocity,
            StartTick = s.StartTick,
            DurationTicks = s.DurationTicks,
            StartTimeMs = s.StartTimeMs,
            DurationMs = s.DurationMs,
            ReceivedTimestamp = DateTime.UtcNow
        }).ToList();
        
        return new Performance { Notes = notes };
    }
}

