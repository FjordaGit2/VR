using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Counterbalanced, shuffled trial orders for SC3 street and practice scenes.</summary>
public static class StudyTrialSequence
{
    public const int RoadLeft = 0;
    public const int RoadRight = 1;

    public const int SeedSaltSc3aStreet = 391_001;
    public const int SeedSaltSc3bStreet = 392_001;
    public const int SeedSaltSc3aPractice = 391_011;
    public const int SeedSaltSc3bPractice = 392_011;

    public struct Sc3Sequences
    {
        public List<int> RoadSides;
        public List<int> CarIndices;
        /// <summary>1 = police distractor on opposite road; 0 = target only. ~50% overall, balanced per road side.</summary>
        public List<int> DistractorPresent;
        public int Seed;
    }

    /// <summary>Independent per scene: fixed inspector seed is offset by <paramref name="sceneSalt"/>.</summary>
    public static int ResolveSeed(int configuredSeed, int sceneSalt)
    {
        if (configuredSeed >= 0)
            return configuredSeed + sceneSalt;
        int r = UnityEngine.Random.Range(int.MinValue / 4, int.MaxValue / 4);
        return r ^ sceneSalt;
    }

    public static Sc3Sequences BuildSc3TrialSequences(
        int trialsPerRoadSide,
        int totalTrials,
        int prefabCount,
        int configuredSeed,
        int sceneSalt,
        bool avoidConsecutiveSameRoadSide,
        bool avoidConsecutiveSameCarWithinRoadSide)
    {
        int seed = ResolveSeed(configuredSeed, sceneSalt);
        var rng = new System.Random(seed);

        var roadSides = BuildBalancedMultiset(RoadLeft, trialsPerRoadSide, RoadRight, trialsPerRoadSide);
        Shuffle(roadSides, rng);
        if (avoidConsecutiveSameRoadSide)
            TryRemoveConsecutiveDuplicates(roadSides, rng);

        var carIndices = BuildMergedCarIndices(
            roadSides,
            prefabCount,
            trialsPerRoadSide,
            totalTrials,
            rng,
            avoidConsecutiveSameCarWithinRoadSide);

        var distractorPresent = BuildDistractorPresenceFlags(roadSides, rng);

        return new Sc3Sequences
        {
            RoadSides = roadSides,
            CarIndices = carIndices,
            DistractorPresent = distractorPresent,
            Seed = seed
        };
    }

    public static Sc3Sequences BuildPracticeSequences(int totalTrials, int prefabCount, int sceneSalt)
    {
        int seed = ResolveSeed(-1, sceneSalt);
        var rng = new System.Random(seed);

        int leftCount = totalTrials / 2;
        int rightCount = totalTrials - leftCount;
        var roadSides = BuildBalancedMultiset(RoadLeft, leftCount, RoadRight, rightCount);
        Shuffle(roadSides, rng);

        int carPrefabCount = Mathf.Max(1, prefabCount);
        var carIndices = BuildMergedCarIndices(
            roadSides,
            carPrefabCount,
            leftCount,
            totalTrials,
            rng,
            avoidConsecutiveSameCarWithinRoadSide: false);

        var distractorPresent = BuildDistractorPresenceFlags(roadSides, rng);

        return new Sc3Sequences
        {
            RoadSides = roadSides,
            CarIndices = carIndices,
            DistractorPresent = distractorPresent,
            Seed = seed
        };
    }

    /// <summary>
    /// Marks half of trials on each road side as distractor-present (shuffled),
    /// so overall ~50% and balanced across left/right targets.
    /// </summary>
    public static List<int> BuildDistractorPresenceFlags(IList<int> roadSides, System.Random rng)
    {
        var flags = new List<int>(roadSides != null ? roadSides.Count : 0);
        if (roadSides == null || roadSides.Count == 0)
            return flags;

        for (int i = 0; i < roadSides.Count; i++)
            flags.Add(0);

        var leftIdx = new List<int>();
        var rightIdx = new List<int>();
        for (int i = 0; i < roadSides.Count; i++)
        {
            if (roadSides[i] == RoadLeft)
                leftIdx.Add(i);
            else
                rightIdx.Add(i);
        }

        MarkHalfOfIndices(leftIdx, flags, rng);
        MarkHalfOfIndices(rightIdx, flags, rng);
        return flags;
    }

    static void MarkHalfOfIndices(List<int> indices, IList<int> flags, System.Random rng)
    {
        if (indices == null || indices.Count == 0)
            return;

        Shuffle(indices, rng);
        int n = indices.Count / 2;
        for (int i = 0; i < n; i++)
            flags[indices[i]] = 1;
    }

    static List<int> BuildMergedCarIndices(
        IList<int> roadSides,
        int prefabCount,
        int trialsPerRoadSide,
        int totalTrials,
        System.Random rng,
        bool avoidConsecutiveSameCarWithinRoadSide)
    {
        var merged = new List<int>(roadSides.Count);
        if (prefabCount < 1 || roadSides == null)
            return merged;

        var leftCars = BuildBalancedCarMultiset(prefabCount, trialsPerRoadSide);
        var rightCars = BuildBalancedCarMultiset(prefabCount, trialsPerRoadSide);
        Shuffle(leftCars, rng);
        Shuffle(rightCars, rng);
        if (avoidConsecutiveSameCarWithinRoadSide)
        {
            TryRemoveConsecutiveDuplicates(leftCars, rng);
            TryRemoveConsecutiveDuplicates(rightCars, rng);
        }

        int leftUsed = 0;
        int rightUsed = 0;
        for (int t = 0; t < roadSides.Count; t++)
        {
            if (roadSides[t] == RoadLeft)
            {
                if (leftUsed >= leftCars.Count)
                    break;
                merged.Add(leftCars[leftUsed++]);
            }
            else
            {
                if (rightUsed >= rightCars.Count)
                    break;
                merged.Add(rightCars[rightUsed++]);
            }
        }

        while (merged.Count < totalTrials)
            merged.Add(rng.Next(prefabCount));
        while (merged.Count > totalTrials)
            merged.RemoveAt(merged.Count - 1);

        return merged;
    }

    static List<int> BuildBalancedCarMultiset(int prefabCount, int trialCount)
    {
        var list = new List<int>(trialCount);
        if (prefabCount < 1 || trialCount < 1)
            return list;

        int baseCount = trialCount / prefabCount;
        int remainder = trialCount % prefabCount;
        for (int c = 0; c < prefabCount; c++)
        {
            int n = baseCount + (c < remainder ? 1 : 0);
            for (int i = 0; i < n; i++)
                list.Add(c);
        }

        return list;
    }

    static List<int> BuildBalancedMultiset(int a, int countA, int b, int countB)
    {
        var list = new List<int>(countA + countB);
        for (int i = 0; i < countA; i++)
            list.Add(a);
        for (int i = 0; i < countB; i++)
            list.Add(b);
        return list;
    }

    static void Shuffle(IList<int> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    static bool HasConsecutiveDuplicates(IList<int> list)
    {
        for (int i = 1; i < list.Count; i++)
        {
            if (list[i] == list[i - 1])
                return true;
        }

        return false;
    }

    static void TryRemoveConsecutiveDuplicates(IList<int> list, System.Random rng)
    {
        for (int attempt = 0; attempt < 5000 && HasConsecutiveDuplicates(list); attempt++)
            Shuffle(list, rng);
    }
}
