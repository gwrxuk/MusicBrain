using ListeningBrain.Core.Models;

namespace ListeningBrain.Alignment;

/// <summary>
/// Dynamic Time Warping (DTW) alignment algorithm.
/// Optimal for handling tempo variations and rubato playing.
/// 
/// DTW finds the optimal alignment between two time series by warping the time axis.
/// It allows for non-linear time stretching to find the minimum cost alignment path.
/// 
/// Complexity: O(n × m) time and space where n = score notes, m = performance notes.
/// </summary>
public class DynamicTimeWarping : IAlignmentStrategy
{
    public string Name => "Dynamic Time Warping (DTW)";
    
    /// <summary>
    /// Aligns a performance to a score using DTW.
    /// </summary>
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
        
        // Step 1: Build DTW cost matrix
        var (costMatrix, pathMatrix) = BuildCostMatrix(scoreNotes, perfNotes, options);
        
        // Step 2: Backtrack to find optimal path
        var warpingPath = BacktrackPath(costMatrix, pathMatrix, scoreNotes.Count, perfNotes.Count);
        
        // Step 3: Extract aligned pairs from path
        var (pairs, usedScoreIndices, usedPerfIndices) = ExtractPairs(
            warpingPath, scoreNotes, perfNotes, score, options);
        
        // Step 4: Identify missed and extra notes
        var missedNotes = FindMissedNotes(scoreNotes, usedScoreIndices, perfNotes);
        var extraNotes = FindExtraNotes(perfNotes, usedPerfIndices);
        
        // Step 5: Calculate tempo ratio
        double tempoRatio = EstimateTempoRatio(warpingPath, scoreNotes, perfNotes);
        
        stopwatch.Stop();
        
        return new AlignmentResult
        {
            Pairs = pairs,
            MissedNotes = missedNotes,
            ExtraNotes = extraNotes,
            TotalCost = costMatrix[scoreNotes.Count, perfNotes.Count],
            NormalizedScore = CalculateNormalizedScore(pairs, missedNotes.Count, extraNotes.Count),
            WarpingPath = warpingPath,
            EstimatedTempoRatio = tempoRatio,
            TimeOffsetMs = perfNotes.Count > 0 && scoreNotes.Count > 0 
                ? perfNotes[0].StartTimeMs - scoreNotes[0].StartTimeMs 
                : 0,
            AlgorithmUsed = Name,
            ComputeTime = stopwatch.Elapsed
        };
    }
    
    /// <summary>
    /// Builds the DTW cost matrix using dynamic programming.
    /// </summary>
    private (double[,] cost, int[,] path) BuildCostMatrix(
        List<ScoreNote> scoreNotes, 
        List<PerformanceNote> perfNotes,
        AlignmentOptions options)
    {
        int n = scoreNotes.Count;
        int m = perfNotes.Count;
        
        // cost[i,j] = minimum cost to align score[0..i-1] with perf[0..j-1]
        var cost = new double[n + 1, m + 1];
        // path[i,j] = previous cell (0=diagonal, 1=left, 2=up)
        var path = new int[n + 1, m + 1];
        
        // Initialize boundaries
        for (int i = 0; i <= n; i++)
            cost[i, 0] = i * options.GapPenalty;
        for (int j = 0; j <= m; j++)
            cost[0, j] = j * options.GapPenalty * 0.5; // Lower penalty for extra notes
        
        // Fill the matrix
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                double matchCost = CalculateMatchCost(
                    scoreNotes[i - 1], perfNotes[j - 1], options);
                
                // Three possible moves:
                double diagonal = cost[i - 1, j - 1] + matchCost; // Match
                double left = cost[i, j - 1] + options.GapPenalty * 0.5; // Extra note
                double up = cost[i - 1, j] + options.GapPenalty; // Missed note
                
                if (diagonal <= left && diagonal <= up)
                {
                    cost[i, j] = diagonal;
                    path[i, j] = 0; // Diagonal (match)
                }
                else if (left <= up)
                {
                    cost[i, j] = left;
                    path[i, j] = 1; // Left (extra note in performance)
                }
                else
                {
                    cost[i, j] = up;
                    path[i, j] = 2; // Up (missed note in score)
                }
            }
        }
        
        return (cost, path);
    }
    
    /// <summary>
    /// Calculates the cost of matching a score note with a performance note.
    /// </summary>
    private double CalculateMatchCost(ScoreNote scoreNote, PerformanceNote perfNote, AlignmentOptions options)
    {
        double cost = 0;
        
        // Pitch cost
        if (scoreNote.Pitch == perfNote.Pitch)
        {
            cost += 0; // Perfect pitch match
        }
        else if (scoreNote.PitchClass == perfNote.PitchClass)
        {
            // Octave error
            cost += options.WrongOctavePenalty * options.PitchWeight;
        }
        else
        {
            // Wrong pitch - use semitone distance
            int semitones = Math.Abs(scoreNote.Pitch - perfNote.Pitch);
            cost += Math.Min(semitones / 12.0, 1.0) * options.PitchWeight;
        }
        
        // Timing cost (normalized by max deviation)
        double timingDeviation = Math.Abs(perfNote.StartTimeMs - scoreNote.StartTimeMs);
        double normalizedTiming = Math.Min(timingDeviation / options.MaxTimingDeviationMs, 1.0);
        
        // Apply grace note relaxation
        if (scoreNote.IsGraceNote && options.RelaxGraceNoteTiming)
        {
            normalizedTiming *= 0.3; // Grace notes have much more timing flexibility
        }
        
        cost += normalizedTiming * options.TimingWeight;
        
        // Velocity cost
        int velocityDiff = Math.Abs(perfNote.Velocity - scoreNote.Velocity);
        cost += (velocityDiff / 127.0) * options.VelocityWeight;
        
        return cost;
    }
    
    /// <summary>
    /// Backtracks through the path matrix to find the optimal alignment path.
    /// </summary>
    private List<WarpingPoint> BacktrackPath(double[,] cost, int[,] path, int n, int m)
    {
        var warpingPath = new List<WarpingPoint>();
        int i = n, j = m;
        
        while (i > 0 || j > 0)
        {
            warpingPath.Add(new WarpingPoint(i - 1, j - 1, cost[i, j]));
            
            if (i == 0)
            {
                j--;
            }
            else if (j == 0)
            {
                i--;
            }
            else
            {
                switch (path[i, j])
                {
                    case 0: // Diagonal
                        i--;
                        j--;
                        break;
                    case 1: // Left (extra perf note)
                        j--;
                        break;
                    case 2: // Up (missed score note)
                        i--;
                        break;
                }
            }
        }
        
        warpingPath.Reverse();
        return warpingPath;
    }
    
    /// <summary>
    /// Extracts aligned note pairs from the warping path.
    /// </summary>
    private (List<AlignedNotePair> pairs, HashSet<int> usedScore, HashSet<int> usedPerf) ExtractPairs(
        List<WarpingPoint> path,
        List<ScoreNote> scoreNotes,
        List<PerformanceNote> perfNotes,
        Score score,
        AlignmentOptions options)
    {
        var pairs = new List<AlignedNotePair>();
        var usedScoreIndices = new HashSet<int>();
        var usedPerfIndices = new HashSet<int>();
        
        int prevScoreIdx = -1, prevPerfIdx = -1;
        
        foreach (var point in path)
        {
            // Only create pair when both indices advance (diagonal move)
            if (point.ScoreIndex != prevScoreIdx && point.PerformanceIndex != prevPerfIdx
                && point.ScoreIndex >= 0 && point.ScoreIndex < scoreNotes.Count
                && point.PerformanceIndex >= 0 && point.PerformanceIndex < perfNotes.Count)
            {
                var scoreNote = scoreNotes[point.ScoreIndex];
                var perfNote = perfNotes[point.PerformanceIndex];
                
                // Only pair if not already used and match cost is reasonable
                if (!usedScoreIndices.Contains(point.ScoreIndex) && 
                    !usedPerfIndices.Contains(point.PerformanceIndex))
                {
                    double matchCost = CalculateMatchCost(scoreNote, perfNote, options);
                    
                    if (matchCost < options.GapPenalty) // Only pair if better than gap
                    {
                        var tempo = score.GetTempoAt(scoreNote.StartTick);
                        double msPerBeat = tempo.MillisecondsPerQuarter;
                        double timingDeviationMs = perfNote.StartTimeMs - scoreNote.StartTimeMs;
                        
                        pairs.Add(new AlignedNotePair
                        {
                            ScoreNote = scoreNote,
                            PerformanceNote = perfNote,
                            Confidence = 1.0 - matchCost,
                            TimingDeviationMs = timingDeviationMs,
                            TimingDeviationBeats = timingDeviationMs / msPerBeat
                        });
                        
                        usedScoreIndices.Add(point.ScoreIndex);
                        usedPerfIndices.Add(point.PerformanceIndex);
                    }
                }
            }
            
            prevScoreIdx = point.ScoreIndex;
            prevPerfIdx = point.PerformanceIndex;
        }
        
        return (pairs, usedScoreIndices, usedPerfIndices);
    }
    
    /// <summary>
    /// Finds score notes that were not matched (missed).
    /// </summary>
    private List<MissedNote> FindMissedNotes(
        List<ScoreNote> scoreNotes, 
        HashSet<int> usedIndices,
        List<PerformanceNote> perfNotes)
    {
        var missed = new List<MissedNote>();
        
        for (int i = 0; i < scoreNotes.Count; i++)
        {
            if (!usedIndices.Contains(i))
            {
                var scoreNote = scoreNotes[i];
                
                // Find nearby played notes for context
                var nearby = perfNotes
                    .Where(p => Math.Abs(p.StartTimeMs - scoreNote.StartTimeMs) < 500)
                    .ToList();
                
                missed.Add(new MissedNote
                {
                    ExpectedNote = scoreNote,
                    NearbyPlayedNotes = nearby,
                    InferredReason = InferMissReason(scoreNote, nearby)
                });
            }
        }
        
        return missed;
    }
    
    /// <summary>
    /// Finds performance notes that don't match any score note (extra).
    /// </summary>
    private List<PerformanceNote> FindExtraNotes(
        List<PerformanceNote> perfNotes,
        HashSet<int> usedIndices)
    {
        var extra = new List<PerformanceNote>();
        
        for (int i = 0; i < perfNotes.Count; i++)
        {
            if (!usedIndices.Contains(i))
            {
                extra.Add(perfNotes[i].AsExtra());
            }
        }
        
        return extra;
    }
    
    /// <summary>
    /// Tries to infer why a note was missed.
    /// </summary>
    private MissReason InferMissReason(ScoreNote scoreNote, List<PerformanceNote> nearby)
    {
        if (scoreNote.IsGraceNote)
            return MissReason.OptionalOrnament;
            
        if (nearby.Count == 0)
            return MissReason.Skipped;
            
        // Check if a different note was played at the same time
        var sameTime = nearby.Where(n => Math.Abs(n.StartTimeMs - scoreNote.StartTimeMs) < 100).ToList();
        if (sameTime.Any())
        {
            // Check for octave error
            if (sameTime.Any(n => n.PitchClass == scoreNote.PitchClass))
                return MissReason.Substituted;
            return MissReason.Substituted;
        }
        
        return MissReason.TimingMismatch;
    }
    
    /// <summary>
    /// Estimates the tempo ratio from the warping path.
    /// </summary>
    private double EstimateTempoRatio(
        List<WarpingPoint> path,
        List<ScoreNote> scoreNotes,
        List<PerformanceNote> perfNotes)
    {
        if (path.Count < 2 || scoreNotes.Count < 2 || perfNotes.Count < 2)
            return 1.0;
        
        // Sample tempo ratios at different points along the path
        var ratios = new List<double>();
        
        for (int i = 1; i < path.Count; i++)
        {
            var prev = path[i - 1];
            var curr = path[i];
            
            if (prev.ScoreIndex >= 0 && prev.ScoreIndex < scoreNotes.Count &&
                curr.ScoreIndex >= 0 && curr.ScoreIndex < scoreNotes.Count &&
                prev.PerformanceIndex >= 0 && prev.PerformanceIndex < perfNotes.Count &&
                curr.PerformanceIndex >= 0 && curr.PerformanceIndex < perfNotes.Count &&
                prev.ScoreIndex != curr.ScoreIndex &&
                prev.PerformanceIndex != curr.PerformanceIndex)
            {
                double scoreInterval = scoreNotes[curr.ScoreIndex].StartTimeMs - 
                                       scoreNotes[prev.ScoreIndex].StartTimeMs;
                double perfInterval = perfNotes[curr.PerformanceIndex].StartTimeMs - 
                                      perfNotes[prev.PerformanceIndex].StartTimeMs;
                
                if (scoreInterval > 0)
                {
                    ratios.Add(perfInterval / scoreInterval);
                }
            }
        }
        
        if (ratios.Count == 0) return 1.0;
        
        // Return median to be robust to outliers
        ratios.Sort();
        return ratios[ratios.Count / 2];
    }
    
    private double CalculateNormalizedScore(
        List<AlignedNotePair> pairs, 
        int missedCount, 
        int extraCount)
    {
        int total = pairs.Count + missedCount;
        if (total == 0) return 1.0;
        
        double correctPitches = pairs.Count(p => p.IsExactPitchMatch);
        double partialCredit = pairs.Count(p => p.IsOctaveError) * 0.5;
        double extraPenalty = extraCount * 0.1;
        
        double score = (correctPitches + partialCredit - extraPenalty) / total;
        return Math.Max(0, Math.Min(1, score));
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

