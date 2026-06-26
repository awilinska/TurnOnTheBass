using System.Collections.Generic;

namespace TurnOnTheBass
{
    public readonly struct RhythmChartNote
    {
        public RhythmChartNote(float targetTimeSeconds, int laneIndex, float strength)
        {
            TargetTimeSeconds = targetTimeSeconds;
            LaneIndex = laneIndex;
            Strength = strength;
        }

        public float TargetTimeSeconds { get; }
        public int LaneIndex { get; }
        public float Strength { get; }
    }

    public sealed class RhythmGeneratedChart
    {
        private readonly List<RhythmChartNote> notes;

        public RhythmGeneratedChart(
            float estimatedBpm,
            float firstBeatOffsetSeconds,
            IReadOnlyList<RhythmChartNote> generatedNotes,
            int firstSpinAfterNotes,
            int spinEveryNotes,
            int requiredSpinPresses,
            float spinTimeLimitSeconds)
        {
            EstimatedBpm = estimatedBpm;
            FirstBeatOffsetSeconds = firstBeatOffsetSeconds;
            notes = new List<RhythmChartNote>(generatedNotes);
            FirstSpinAfterNotes = firstSpinAfterNotes;
            SpinEveryNotes = spinEveryNotes;
            RequiredSpinPresses = requiredSpinPresses;
            SpinTimeLimitSeconds = spinTimeLimitSeconds;
        }

        public float EstimatedBpm { get; }
        public float FirstBeatOffsetSeconds { get; }
        public IReadOnlyList<RhythmChartNote> Notes => notes;
        public int FirstSpinAfterNotes { get; }
        public int SpinEveryNotes { get; }
        public int RequiredSpinPresses { get; }
        public float SpinTimeLimitSeconds { get; }

        public float AverageNoteIntervalSeconds
        {
            get
            {
                if (notes.Count < 2)
                {
                    return 60f / EstimatedBpm;
                }

                return (notes[notes.Count - 1].TargetTimeSeconds - notes[0].TargetTimeSeconds) / (notes.Count - 1);
            }
        }
    }
}
