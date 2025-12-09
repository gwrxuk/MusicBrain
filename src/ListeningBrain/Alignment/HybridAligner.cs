using ListeningBrain.Core.Models;

namespace ListeningBrain.Alignment;

/// <summary>
/// Hybrid alignment strategy that combines DTW and Needleman-Wunsch for optimal results.
/// 
/// Strategy:
/// 1. Use DTW for coarse tempo alignment (handles rubato/tempo drift)
/// 2. Use time-warped performance to refine with Needleman-Wunsch (handles insertions/deletions)
/// 3. Separate voices for polyphonic alignment
/// 
/// This approach gets the best of both worlds:
/// - DTW's ability to handle continuous tempo changes
/// - Needleman-Wunsch's superior gap handling
/// </summary>
public class HybridAligner : IAlignmentStrategy
{
    public string Name => "Hybrid DTW + Needleman-Wunsch";
    
    private readonly DynamicTimeWarping _dtw = new();
    private readonly NeedlemanWunsch _nw = new();
    
    public AlignmentResult Align(Score score, Performance performance, AlignmentOptions? options = null)
    {
        options ??= AlignmentOptions.Default;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        var scoreNotes = score.Notes.ToList();
        var perfNotes = performance.Notes.ToList();
        
        if (scoreNotes.Count == 0 || perfNotes.Count == 0)
        {
            return CreateEmptyResult(scoreNotes, perfNotes, stopwatch.Elapsed);
        }
        
        // For small sequences, just use Needleman-Wunsch directly
        if (scoreNotes.Count <= 20 && perfNotes.Count <= 20)
        {
            var nwResult = _nw.Align(score, performance, options);
            return nwResult with { AlgorithmUsed = $"{Name} (NW-only for small sequence)" };
        }
        
        // Step 1: Check if piece has multiple voices
        var voiceGroups = SeparateVoices(scoreNotes);
        
        if (voiceGroups.Count > 1)
        {
            // Multi-voice alignment
            return AlignPolyphonic(score, performance, voiceGroups, options, stopwatch);
        }
        
        // Step 2: Single voice - use DTW for tempo estimation
        var dtwResult = _dtw.Align(score, performance, options);
        
        // Step 3: Apply tempo warping to performance times
        var warpedPerformance = WarpPerformanceTime(
            performance, 
            dtwResult.WarpingPath ?? [], 
            dtwResult.EstimatedTempoRatio);
        
        // Step 4: Re-align with Needleman-Wunsch on warped performance
        var nwOptions = options with { AllowTempoFlexibility = false };
        var finalResult = _nw.Align(score, warpedPerformance, nwOptions);
        
        // Restore original performance notes in pairs
        var restoredPairs = RestoreOriginalNotes(finalResult.Pairs, performance.Notes);
        
        stopwatch.Stop();
        
        return new AlignmentResult
        {
            Pairs = restoredPairs,
            MissedNotes = finalResult.MissedNotes,
            ExtraNotes = RestoreOriginalExtraNotes(finalResult.ExtraNotes, performance.Notes),
            TotalCost = finalResult.TotalCost,
            NormalizedScore = finalResult.NormalizedScore,
            WarpingPath = dtwResult.WarpingPath,
            EstimatedTempoRatio = dtwResult.EstimatedTempoRatio,
            TimeOffsetMs = dtwResult.TimeOffsetMs,
            AlgorithmUsed = Name,
            ComputeTime = stopwatch.Elapsed
        };
    }
    
    /// <summary>
    /// Separates score notes into voice groups based on pitch range and temporal overlap.
    /// </summary>
    private Dictionary<int, List<ScoreNote>> SeparateVoices(List<ScoreNote> notes)
    {
        // If notes already have voice assignments, use them
        var assignedVoices = notes.GroupBy(n => n.Voice)
            .Where(g => g.Key != 0)
            .ToDictionary(g => g.Key, g => g.ToList());
            
        if (assignedVoices.Count > 1)
        {
            return assignedVoices;
        }
        
        // Auto-detect voices based on pitch and timing
        return AutoDetectVoices(notes);
    }
    
    /// <summary>
    /// Automatically detects voice separation based on pitch clustering and temporal overlap.
    /// Uses a simple heuristic: notes playing simultaneously are split by pitch.
    /// </summary>
    private Dictionary<int, List<ScoreNote>> AutoDetectVoices(List<ScoreNote> notes)
    {
        var voices = new Dictionary<int, List<ScoreNote>>();
        var currentChords = new List<List<ScoreNote>>();
        
        // Group notes into chords (simultaneous notes)
        var sorted = notes.OrderBy(n => n.StartTick).ToList();
        var currentChord = new List<ScoreNote>();
        long currentTime = -1;
        
        foreach (var note in sorted)
        {
            if (currentTime < 0 || Math.Abs(note.StartTick - currentTime) < 10)
            {
                currentChord.Add(note);
                currentTime = note.StartTick;
            }
            else
            {
                if (currentChord.Count > 0)
                    currentChords.Add(currentChord.OrderBy(n => n.Pitch).ToList());
                currentChord = [note];
                currentTime = note.StartTick;
            }
        }
        if (currentChord.Count > 0)
            currentChords.Add(currentChord.OrderBy(n => n.Pitch).ToList());
        
        // Determine max simultaneous notes
        int maxVoices = currentChords.Count > 0 ? currentChords.Max(c => c.Count) : 1;
        
        if (maxVoices == 1)
        {
            // Monophonic - single voice
            voices[1] = notes;
            return voices;
        }
        
        // Initialize voice lists
        for (int i = 1; i <= maxVoices; i++)
            voices[i] = [];
        
        // Assign notes to voices
        foreach (var chord in currentChords)
        {
            // Distribute chord notes across voices (lowest pitch = highest voice number)
            for (int i = 0; i < chord.Count; i++)
            {
                int voiceNum = maxVoices - i;
                voices[voiceNum].Add(chord[i]);
            }
        }
        
        return voices.Where(kv => kv.Value.Count > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }
    
    /// <summary>
    /// Aligns polyphonic music by separating voices and aligning each independently.
    /// </summary>
    private AlignmentResult AlignPolyphonic(
        Score score,
        Performance performance,
        Dictionary<int, List<ScoreNote>> voiceGroups,
        AlignmentOptions options,
        System.Diagnostics.Stopwatch stopwatch)
    {
        var allPairs = new List<AlignedNotePair>();
        var allMissed = new List<MissedNote>();
        var usedPerfNotes = new HashSet<Guid>();
        
        // Separate performance notes by pitch range to match voices
        var perfNotes = performance.Notes.ToList();
        var perfByVoice = SeparatePerformanceByPitch(perfNotes, voiceGroups);
        
        foreach (var (voiceNum, scoreVoiceNotes) in voiceGroups.OrderBy(kv => kv.Key))
        {
            var perfVoiceNotes = perfByVoice.GetValueOrDefault(voiceNum, []);
            
            if (scoreVoiceNotes.Count == 0) continue;
            
            // Create mini score and performance for this voice
            var voiceScore = new Score { Notes = scoreVoiceNotes, PPQ = score.PPQ };
            var voicePerf = new Performance { Notes = perfVoiceNotes };
            
            // Align this voice
            var voiceResult = _nw.Align(voiceScore, voicePerf, options);
            
            allPairs.AddRange(voiceResult.Pairs);
            allMissed.AddRange(voiceResult.MissedNotes);
            
            foreach (var pair in voiceResult.Pairs)
            {
                usedPerfNotes.Add(pair.PerformanceNote.Id);
            }
        }
        
        // Find extra notes (not used in any voice alignment)
        var extraNotes = perfNotes
            .Where(n => !usedPerfNotes.Contains(n.Id))
            .Select(n => n.AsExtra())
            .ToList();
        
        stopwatch.Stop();
        
        double totalCost = allPairs.Sum(p => 1 - p.Confidence) + allMissed.Count + extraNotes.Count * 0.5;
        
        return new AlignmentResult
        {
            Pairs = allPairs.OrderBy(p => p.ScoreNote.StartTick).ToList(),
            MissedNotes = allMissed,
            ExtraNotes = extraNotes,
            TotalCost = totalCost,
            NormalizedScore = CalculateNormalizedScore(allPairs, allMissed.Count, extraNotes.Count),
            EstimatedTempoRatio = EstimateOverallTempoRatio(allPairs),
            AlgorithmUsed = $"{Name} (polyphonic, {voiceGroups.Count} voices)",
            ComputeTime = stopwatch.Elapsed
        };
    }
    
    /// <summary>
    /// Separates performance notes into voice groups based on pitch ranges.
    /// </summary>
    private Dictionary<int, List<PerformanceNote>> SeparatePerformanceByPitch(
        List<PerformanceNote> perfNotes,
        Dictionary<int, List<ScoreNote>> voiceGroups)
    {
        var result = new Dictionary<int, List<PerformanceNote>>();
        foreach (var key in voiceGroups.Keys)
            result[key] = [];
        
        // Calculate pitch ranges for each voice
        var voiceRanges = voiceGroups.ToDictionary(
            kv => kv.Key,
            kv => (Min: kv.Value.Min(n => n.Pitch), Max: kv.Value.Max(n => n.Pitch))
        );
        
        foreach (var note in perfNotes)
        {
            // Find best matching voice based on pitch
            var bestVoice = voiceRanges
                .OrderBy(kv => Math.Min(
                    Math.Abs(note.Pitch - kv.Value.Min),
                    Math.Abs(note.Pitch - kv.Value.Max)))
                .First().Key;
            
            result[bestVoice].Add(note);
        }
        
        return result;
    }
    
    /// <summary>
    /// Creates a warped copy of the performance with adjusted timing.
    /// </summary>
    private Performance WarpPerformanceTime(
        Performance original,
        IReadOnlyList<WarpingPoint> warpingPath,
        double tempoRatio)
    {
        if (warpingPath.Count == 0 || Math.Abs(tempoRatio - 1.0) < 0.01)
        {
            return original; // No warping needed
        }
        
        // Simple linear tempo adjustment
        var warpedNotes = original.Notes.Select(n => n with
        {
            StartTimeMs = n.StartTimeMs / tempoRatio,
            DurationMs = n.DurationMs / tempoRatio
        }).ToList();
        
        return new Performance
        {
            Notes = warpedNotes,
            SustainPedalEvents = original.SustainPedalEvents,
            SoftPedalEvents = original.SoftPedalEvents,
            StartTimestamp = original.StartTimestamp,
            IsRealTimeCapture = original.IsRealTimeCapture
        };
    }
    
    /// <summary>
    /// Restores original performance note references in aligned pairs.
    /// </summary>
    private List<AlignedNotePair> RestoreOriginalNotes(
        IReadOnlyList<AlignedNotePair> pairs,
        IReadOnlyList<PerformanceNote> originalNotes)
    {
        var originalById = originalNotes.ToDictionary(n => n.Id);
        
        return pairs.Select(p =>
        {
            if (originalById.TryGetValue(p.PerformanceNote.Id, out var original))
            {
                return p with { PerformanceNote = original };
            }
            return p;
        }).ToList();
    }
    
    private List<PerformanceNote> RestoreOriginalExtraNotes(
        IReadOnlyList<PerformanceNote> extras,
        IReadOnlyList<PerformanceNote> originalNotes)
    {
        var originalById = originalNotes.ToDictionary(n => n.Id);
        
        return extras.Select(e =>
        {
            if (originalById.TryGetValue(e.Id, out var original))
            {
                return original.AsExtra();
            }
            return e;
        }).ToList();
    }
    
    private double CalculateNormalizedScore(List<AlignedNotePair> pairs, int missedCount, int extraCount)
    {
        int total = pairs.Count + missedCount;
        if (total == 0) return 1.0;
        
        double correctPitches = pairs.Count(p => p.IsExactPitchMatch);
        double partialCredit = pairs.Count(p => p.IsOctaveError) * 0.5;
        double extraPenalty = extraCount * 0.1;
        
        double score = (correctPitches + partialCredit - extraPenalty) / total;
        return Math.Max(0, Math.Min(1, score));
    }
    
    private double EstimateOverallTempoRatio(List<AlignedNotePair> pairs)
    {
        if (pairs.Count < 2) return 1.0;
        
        var ratios = new List<double>();
        for (int i = 1; i < pairs.Count; i++)
        {
            var prev = pairs[i - 1];
            var curr = pairs[i];
            
            double scoreInterval = curr.ScoreNote.StartTimeMs - prev.ScoreNote.StartTimeMs;
            double perfInterval = curr.PerformanceNote.StartTimeMs - prev.PerformanceNote.StartTimeMs;
            
            if (scoreInterval > 10)
            {
                ratios.Add(perfInterval / scoreInterval);
            }
        }
        
        if (ratios.Count == 0) return 1.0;
        ratios.Sort();
        return ratios[ratios.Count / 2];
    }
    
    private AlignmentResult CreateEmptyResult(
        List<ScoreNote> scoreNotes,
        List<PerformanceNote> perfNotes,
        TimeSpan computeTime)
    {
        return new AlignmentResult
        {
            Pairs = [],
            MissedNotes = scoreNotes.Select(s => new MissedNote 
            { 
                ExpectedNote = s, 
                InferredReason = MissReason.Skipped 
            }).ToList(),
            ExtraNotes = perfNotes.Select(p => p.AsExtra()).ToList(),
            TotalCost = double.MaxValue,
            NormalizedScore = 0,
            AlgorithmUsed = Name,
            ComputeTime = computeTime
        };
    }
}

