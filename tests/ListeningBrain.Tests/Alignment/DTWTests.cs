using NUnit.Framework;
using FluentAssertions;
using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;

namespace ListeningBrain.Tests.Alignment;

[TestFixture]
public class DTWTests
{
    private DynamicTimeWarping _aligner = null!;
    
    [SetUp]
    public void Setup()
    {
        _aligner = new DynamicTimeWarping();
    }
    
    [Test]
    public void Align_IdenticalSequences_ReturnsExactMatches()
    {
        // Arrange
        var score = CreateSimpleScale(); // C4 D4 E4 F4 G4
        var performance = CreatePerformanceFromScore(score);
        
        // Act
        var result = _aligner.Align(score, performance);
        
        // Assert
        result.Pairs.Should().HaveCount(5);
        result.Pairs.All(p => p.IsExactPitchMatch).Should().BeTrue();
        result.MissedNotes.Should().BeEmpty();
        result.ExtraNotes.Should().BeEmpty();
        result.NormalizedScore.Should().BeGreaterThan(0.9);
    }
    
    [Test]
    public void Align_MissedNote_DetectsGap()
    {
        // Arrange
        var score = CreateSimpleScale(); // C4 D4 E4 F4 G4
        var performance = CreatePerformanceNotes(60, 62, 65, 67); // C4 D4 F4 G4 (missing E4)
        
        // Act
        var result = _aligner.Align(score, performance);
        
        // Assert
        result.MissedNotes.Should().HaveCount(1);
        result.MissedNotes[0].ExpectedNote.Pitch.Should().Be(64); // E4
    }
    
    [Test]
    public void Align_ExtraNote_IdentifiesAdditionalNotes()
    {
        // Arrange
        var score = CreateSimpleScale(); // C4 D4 E4 F4 G4
        var performance = CreatePerformanceNotes(60, 62, 63, 64, 65, 67); // Added Eb4
        
        // Act
        var result = _aligner.Align(score, performance);
        
        // Assert
        result.ExtraNotes.Should().NotBeEmpty();
    }
    
    [Test]
    public void Align_OctaveError_RecognizesPartialMatch()
    {
        // Arrange
        var score = CreateSimpleScale(); // C4 D4 E4 F4 G4
        var performance = CreatePerformanceNotes(60, 62, 76, 65, 67); // E5 instead of E4
        
        // Act
        var result = _aligner.Align(score, performance);
        
        // Assert
        var octaveErrorPair = result.Pairs.FirstOrDefault(p => p.IsOctaveError);
        octaveErrorPair.Should().NotBeNull();
        octaveErrorPair!.ScoreNote.PitchClass.Should().Be(octaveErrorPair.PerformanceNote.PitchClass);
    }
    
    [Test]
    public void Align_WrongNote_IdentifiesMismatch()
    {
        // Arrange
        var score = CreateSimpleScale(); // C4 D4 E4 F4 G4
        var performance = CreatePerformanceNotes(60, 62, 66, 65, 67); // F#4 instead of E4
        
        // Act
        var result = _aligner.Align(score, performance);
        
        // Assert
        result.Pairs.Should().Contain(p => 
            !p.IsExactPitchMatch && !p.IsOctaveError);
    }
    
    [Test]
    public void Align_TempoVariation_HandlesRubato()
    {
        // Arrange
        var score = CreateSimpleScale();
        var performance = CreatePerformanceWithVariableTiming(score, 1.2); // 20% slower
        
        // Act
        var result = _aligner.Align(score, performance, AlignmentOptions.Default);
        
        // Assert
        result.Pairs.Should().HaveCountGreaterThan(3);
        result.EstimatedTempoRatio.Should().BeGreaterThan(1.0);
    }
    
    [Test]
    public void Align_EmptyPerformance_ReturnsAllMissed()
    {
        // Arrange
        var score = CreateSimpleScale();
        var performance = new Performance { Notes = [] };
        
        // Act
        var result = _aligner.Align(score, performance);
        
        // Assert
        result.Pairs.Should().BeEmpty();
        result.MissedNotes.Should().HaveCount(5);
        result.NormalizedScore.Should().Be(0);
    }
    
    [Test]
    public void Align_EmptyScore_ReturnsAllExtra()
    {
        // Arrange
        var score = new Score { Notes = [] };
        var performance = CreatePerformanceNotes(60, 62, 64);
        
        // Act
        var result = _aligner.Align(score, performance);
        
        // Assert
        result.Pairs.Should().BeEmpty();
        result.ExtraNotes.Should().HaveCount(3);
    }
    
    [Test]
    public void Align_ChordAlignment_MatchesSimultaneousNotes()
    {
        // Arrange
        var score = CreateChord(0, 60, 64, 67); // C major chord
        var performance = CreatePerformanceChord(0, 60, 64, 67);
        
        // Act
        var result = _aligner.Align(score, performance);
        
        // Assert
        result.Pairs.Should().HaveCount(3);
        result.Pairs.All(p => p.IsExactPitchMatch).Should().BeTrue();
    }
    
    // Helper methods
    private Score CreateSimpleScale()
    {
        var notes = new List<ScoreNote>();
        int[] pitches = { 60, 62, 64, 65, 67 }; // C D E F G
        
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
        
        return new Score { Notes = notes, PPQ = 480 };
    }
    
    private Performance CreatePerformanceFromScore(Score score)
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
    
    private Performance CreatePerformanceNotes(params int[] pitches)
    {
        var notes = pitches.Select((pitch, i) => new PerformanceNote
        {
            Id = Guid.NewGuid(),
            Pitch = pitch,
            Velocity = 80,
            StartTick = i * 480,
            DurationTicks = 480,
            StartTimeMs = i * 500,
            DurationMs = 500,
            ReceivedTimestamp = DateTime.UtcNow,
            SequenceIndex = i
        }).ToList();
        
        return new Performance { Notes = notes };
    }
    
    private Performance CreatePerformanceWithVariableTiming(Score score, double tempoRatio)
    {
        var notes = score.Notes.Select(s => new PerformanceNote
        {
            Id = Guid.NewGuid(),
            Pitch = s.Pitch,
            Velocity = s.Velocity,
            StartTick = s.StartTick,
            DurationTicks = s.DurationTicks,
            StartTimeMs = s.StartTimeMs * tempoRatio,
            DurationMs = s.DurationMs * tempoRatio,
            ReceivedTimestamp = DateTime.UtcNow
        }).ToList();
        
        return new Performance { Notes = notes };
    }
    
    private Score CreateChord(double startMs, params int[] pitches)
    {
        var notes = pitches.Select(pitch => new ScoreNote
        {
            Id = Guid.NewGuid(),
            Pitch = pitch,
            Velocity = 80,
            StartTick = 0,
            DurationTicks = 480,
            StartTimeMs = startMs,
            DurationMs = 500,
            Measure = 1,
            Beat = 1,
            RhythmicValue = RhythmicValue.Quarter
        }).ToList();
        
        return new Score { Notes = notes, PPQ = 480 };
    }
    
    private Performance CreatePerformanceChord(double startMs, params int[] pitches)
    {
        var notes = pitches.Select((pitch, i) => new PerformanceNote
        {
            Id = Guid.NewGuid(),
            Pitch = pitch,
            Velocity = 80,
            StartTick = 0,
            DurationTicks = 480,
            StartTimeMs = startMs + (i * 5), // Slight spread (realistic)
            DurationMs = 500,
            ReceivedTimestamp = DateTime.UtcNow,
            SequenceIndex = i
        }).ToList();
        
        return new Performance { Notes = notes };
    }
}

