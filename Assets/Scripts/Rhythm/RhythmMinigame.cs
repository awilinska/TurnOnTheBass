using System;
using System.Collections.Generic;
using UnityEngine;

namespace TurnOnTheBass
{
    [Serializable]
    public sealed class RhythmGameSettings
    {
        [Header("Speed")]
        [SerializeField, Range(0.5f, 2.5f)] private float speedMultiplier = 1.35f;
        [SerializeField, Range(0.5f, 2f)] private float chartDensityMultiplier = 1.15f;
        [SerializeField, Range(0.35f, 2.5f)] private float noteTravelTime = 0.95f;
        [SerializeField, Range(0f, 8f)] private float introLeadTime = 0.35f;

        [Header("Hit Windows")]
        [SerializeField, Range(0.02f, 0.2f)] private float perfectWindow = 0.08f;
        [SerializeField, Range(0.04f, 0.3f)] private float goodWindow = 0.16f;
        [SerializeField, Range(0.08f, 0.4f)] private float missWindow = 0.24f;

        [Header("Difficulty")]
        [SerializeField, Range(0.4f, 0.9f)] private float baseRequiredAccuracy = 0.55f;
        [SerializeField, Range(0f, 0.4f)] private float requiredAccuracyDifficultyBonus = 0.17f;

        public float SpeedMultiplier => speedMultiplier;
        public float ChartDensityMultiplier => chartDensityMultiplier;
        public float NoteTravelTime => noteTravelTime;
        public float IntroLeadTime => introLeadTime;
        public float PerfectWindow => perfectWindow;
        public float GoodWindow => goodWindow;
        public float MissWindow => missWindow;
        public float BaseRequiredAccuracy => baseRequiredAccuracy;
        public float RequiredAccuracyDifficultyBonus => requiredAccuracyDifficultyBonus;

        public RhythmGameSettings Clone()
        {
            return new RhythmGameSettings
            {
                speedMultiplier = speedMultiplier,
                chartDensityMultiplier = chartDensityMultiplier,
                noteTravelTime = noteTravelTime,
                introLeadTime = introLeadTime,
                perfectWindow = perfectWindow,
                goodWindow = goodWindow,
                missWindow = missWindow,
                baseRequiredAccuracy = baseRequiredAccuracy,
                requiredAccuracyDifficultyBonus = requiredAccuracyDifficultyBonus
            };
        }

        public void Sanitize()
        {
            speedMultiplier = Mathf.Clamp(speedMultiplier, 0.5f, 2.5f);
            chartDensityMultiplier = Mathf.Clamp(chartDensityMultiplier, 0.5f, 2f);
            noteTravelTime = Mathf.Clamp(noteTravelTime, 0.35f, 2.5f);
            introLeadTime = Mathf.Clamp(introLeadTime, 0f, 8f);

            perfectWindow = Mathf.Clamp(perfectWindow, 0.02f, 0.2f);
            goodWindow = Mathf.Clamp(goodWindow, perfectWindow, 0.3f);
            missWindow = Mathf.Clamp(missWindow, goodWindow, 0.4f);

            baseRequiredAccuracy = Mathf.Clamp(baseRequiredAccuracy, 0.4f, 0.9f);
            requiredAccuracyDifficultyBonus = Mathf.Clamp(requiredAccuracyDifficultyBonus, 0f, 0.4f);
        }

        public void EnsureMinimumIntroLead(float minimumSeconds)
        {
            introLeadTime = Mathf.Max(introLeadTime, Mathf.Max(0f, minimumSeconds));
            introLeadTime = Mathf.Clamp(introLeadTime, 0f, 8f);
        }
    }

    public enum RhythmJudgement
    {
        Pending = 0,
        Perfect = 1,
        Good = 2,
        Miss = 3
    }

    [Serializable]
    public sealed class RhythmNote
    {
        public int Lane;
        public float HitTime;
        public RhythmJudgement Judgement = RhythmJudgement.Pending;
    }

    public struct HitFeedback
    {
        public bool ConsumedInput;
        public string Label;
    }

    public struct RhythmResult
    {
        public bool Completed;
        public bool Success;
        public float Accuracy;
        public int Score;
        public int MaxCombo;
        public int TotalNotes;
    }

    public sealed class RhythmMinigame
    {
        public const int LaneCount = 4;

        private readonly List<RhythmNote> notes = new List<RhythmNote>();
        private readonly List<RhythmNote> readonlyNotes;
        private RhythmGameSettings settings = new RhythmGameSettings();

        private float elapsedTime;
        private int score;
        private int combo;
        private int maxCombo;
        private int resolvedNotes;
        private float requiredAccuracy;
        private bool running;
        private bool complete;

        public RhythmMinigame()
        {
            readonlyNotes = notes;
            settings.Sanitize();
        }

        public IReadOnlyList<RhythmNote> Notes => readonlyNotes;
        public float ElapsedTime => elapsedTime;
        public int Score => score;
        public int Combo => combo;
        public int MaxCombo => maxCombo;
        public float RequiredAccuracy => requiredAccuracy;
        public bool IsRunning => running;
        public bool IsComplete => complete;
        public float NoteTravelTime => settings.NoteTravelTime;

        public float Accuracy
        {
            get
            {
                if (notes.Count == 0)
                {
                    return 0f;
                }

                return score / (notes.Count * 100f);
            }
        }

        public void Configure(RhythmGameSettings runtimeSettings)
        {
            settings = runtimeSettings == null ? new RhythmGameSettings() : runtimeSettings.Clone();
            settings.Sanitize();
        }

        public void Begin(FishDefinition fish)
        {
            if (fish == null)
            {
                throw new ArgumentNullException(nameof(fish));
            }

            notes.Clear();
            GenerateChart(fish);

            elapsedTime = 0f;
            score = 0;
            combo = 0;
            maxCombo = 0;
            resolvedNotes = 0;
            requiredAccuracy = Mathf.Clamp01(
                settings.BaseRequiredAccuracy + (fish.Difficulty * settings.RequiredAccuracyDifficultyBonus));
            running = true;
            complete = false;
        }

        public void Tick(float deltaTime)
        {
            if (!running || complete)
            {
                return;
            }

            elapsedTime += deltaTime;
            MarkExpiredNotesAsMissed();

            if (notes.Count == 0)
            {
                complete = true;
                running = false;
                return;
            }

            float songEndTime = notes[notes.Count - 1].HitTime + settings.MissWindow + 0.3f;
            if (resolvedNotes >= notes.Count || elapsedTime >= songEndTime)
            {
                complete = true;
                running = false;
            }
        }

        public HitFeedback TryHitLane(int lane)
        {
            if (!running || complete)
            {
                return new HitFeedback { ConsumedInput = false, Label = string.Empty };
            }

            RhythmNote candidate = null;
            float candidateTimeUntilHit = float.MaxValue;

            for (int index = 0; index < notes.Count; index++)
            {
                RhythmNote note = notes[index];
                if (note.Judgement != RhythmJudgement.Pending || note.Lane != lane)
                {
                    continue;
                }

                float timeUntilHit = note.HitTime - elapsedTime;
                if (timeUntilHit < 0f)
                {
                    continue;
                }

                if (timeUntilHit >= candidateTimeUntilHit)
                {
                    continue;
                }

                candidate = note;
                candidateTimeUntilHit = timeUntilHit;
            }

            if (candidate == null)
            {
                combo = 0;
                return new HitFeedback { ConsumedInput = true, Label = "Miss" };
            }

            if (candidateTimeUntilHit <= settings.PerfectWindow)
            {
                ApplyHit(candidate, RhythmJudgement.Perfect, 100);
                return new HitFeedback { ConsumedInput = true, Label = "Perfect" };
            }

            if (candidateTimeUntilHit <= settings.GoodWindow)
            {
                ApplyHit(candidate, RhythmJudgement.Good, 70);
                return new HitFeedback { ConsumedInput = true, Label = "Good" };
            }

            combo = 0;
            return new HitFeedback { ConsumedInput = true, Label = "Miss" };
        }

        public RhythmResult GetResult()
        {
            float accuracy = Accuracy;
            return new RhythmResult
            {
                Completed = complete,
                Success = complete && accuracy >= requiredAccuracy,
                Accuracy = accuracy,
                Score = score,
                MaxCombo = maxCombo,
                TotalNotes = notes.Count
            };
        }

        private void ApplyHit(RhythmNote note, RhythmJudgement judgement, int points)
        {
            note.Judgement = judgement;
            score += points;
            combo++;
            resolvedNotes++;
            maxCombo = Mathf.Max(maxCombo, combo);
        }

        private void MarkExpiredNotesAsMissed()
        {
            for (int index = 0; index < notes.Count; index++)
            {
                RhythmNote note = notes[index];
                if (note.Judgement != RhythmJudgement.Pending)
                {
                    continue;
                }

                if (elapsedTime <= note.HitTime)
                {
                    continue;
                }

                note.Judgement = RhythmJudgement.Miss;
                combo = 0;
                resolvedNotes++;
            }
        }

        private void GenerateChart(FishDefinition fish)
        {
            float bpm = Mathf.Max(70f, fish.Bpm * settings.SpeedMultiplier);
            float beatDuration = 60f / bpm;
            int totalBeats = Mathf.Max(16, fish.SongLengthBeats);
            float density = Mathf.Clamp01(Mathf.Lerp(0.42f, 0.82f, fish.Difficulty) * settings.ChartDensityMultiplier);
            int previousLane = 0;

            System.Random random = new System.Random(fish.FishId.GetHashCode());
            for (int beat = 0; beat < totalBeats; beat++)
            {
                float beatTime = settings.IntroLeadTime + (beat * beatDuration);
                if (random.NextDouble() <= density)
                {
                    previousLane = PickLane(random, previousLane);
                    notes.Add(new RhythmNote
                    {
                        Lane = previousLane,
                        HitTime = beatTime
                    });
                }

                if (fish.Difficulty > 0.55f && random.NextDouble() <= density * 0.45f)
                {
                    previousLane = PickLane(random, previousLane);
                    notes.Add(new RhythmNote
                    {
                        Lane = previousLane,
                        HitTime = beatTime + (beatDuration * 0.5f)
                    });
                }
            }

            if (notes.Count < 12)
            {
                notes.Clear();
                for (int beat = 0; beat < 16; beat++)
                {
                    notes.Add(new RhythmNote
                    {
                        Lane = beat % LaneCount,
                        HitTime = settings.IntroLeadTime + (beat * beatDuration)
                    });
                }
            }

            notes.Sort((left, right) => left.HitTime.CompareTo(right.HitTime));
        }

        private static int PickLane(System.Random random, int previousLane)
        {
            int lane = random.Next(0, LaneCount);
            if (lane == previousLane)
            {
                lane = (lane + 1 + random.Next(0, LaneCount - 1)) % LaneCount;
            }

            return lane;
        }
    }
}
