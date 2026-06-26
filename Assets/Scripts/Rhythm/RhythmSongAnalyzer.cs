using System;
using System.Collections.Generic;
using UnityEngine;

namespace TurnOnTheBass
{
    public static class RhythmSongAnalyzer
    {
        private const int AnalysisWindowSamples = 1024;
        private const float MinimumPreliminaryPeakGapSeconds = 0.12f;
        private const float MinimumPlayableNoteGapSeconds = 0.55f;
        private const float MinimumBpm = 70f;
        private const float MaximumBpm = 180f;

        public static bool TryGenerateChart(
            RhythmSongDefinition song,
            int laneCount,
            int fallbackNoteCount,
            out RhythmGeneratedChart chart)
        {
            chart = null;

            if (song == null || song.AudioClip == null || laneCount <= 0)
            {
                return false;
            }

            AudioClip clip = song.AudioClip;
            float[] samples = new float[clip.samples * clip.channels];
            try
            {
                if (!clip.GetData(samples, 0))
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Could not analyze '" + clip.name + "'. Set the audio import Load Type to Decompress On Load if GetData is unavailable. " +
                    exception.Message);
                return false;
            }

            float frameDuration = AnalysisWindowSamples / (float)clip.frequency;
            float[] envelope = BuildEnvelope(samples, clip.channels, frameDuration);
            if (envelope.Length < 8)
            {
                return false;
            }

            Smooth(envelope, 2);
            float[] novelty = BuildNovelty(envelope);
            List<Peak> preliminaryPeaks = FindPeaks(novelty, frameDuration, MinimumPreliminaryPeakGapSeconds);
            float estimatedBpm = EstimateBpm(preliminaryPeaks);
            float beatDuration = 60f / estimatedBpm;
            float firstBeatOffset = preliminaryPeaks.Count > 0 ? preliminaryPeaks[0].TimeSeconds : Mathf.Min(0.5f, clip.length * 0.1f);
            float minNoteGap = Mathf.Clamp(beatDuration, MinimumPlayableNoteGapSeconds, 0.9f);
            List<Peak> selectedPeaks = SelectPlayablePeaks(preliminaryPeaks, minNoteGap, clip.length, song.MaximumGeneratedNotes);

            if (selectedPeaks.Count < 4)
            {
                selectedPeaks = BuildBeatFallback(firstBeatOffset, beatDuration, clip.length, fallbackNoteCount);
            }

            List<RhythmChartNote> notes = BuildNotes(selectedPeaks, laneCount);
            int phraseNotes = estimatedBpm >= 145f ? 24 : 16;
            int firstSpinAfterNotes = Mathf.Clamp(phraseNotes, 4, Mathf.Max(4, notes.Count));
            int spinEveryNotes = Mathf.Clamp(phraseNotes, 4, Mathf.Max(4, notes.Count));
            int requiredSpinPresses = Mathf.Clamp(Mathf.RoundToInt(estimatedBpm / 12f), 8, 18);
            float spinTimeLimitSeconds = Mathf.Clamp(beatDuration * 4f, 1.6f, 4f);

            chart = new RhythmGeneratedChart(
                estimatedBpm,
                firstBeatOffset,
                notes,
                firstSpinAfterNotes,
                spinEveryNotes,
                requiredSpinPresses,
                spinTimeLimitSeconds);
            return true;
        }

        private static float[] BuildEnvelope(float[] samples, int channels, float frameDuration)
        {
            int samplesPerChannel = samples.Length / Mathf.Max(1, channels);
            int frameCount = Mathf.Max(1, samplesPerChannel / AnalysisWindowSamples);
            float[] envelope = new float[frameCount];

            for (int frame = 0; frame < frameCount; frame++)
            {
                int firstSample = frame * AnalysisWindowSamples;
                float sum = 0f;
                int count = 0;

                for (int sample = 0; sample < AnalysisWindowSamples; sample++)
                {
                    int sampleIndex = firstSample + sample;
                    if (sampleIndex >= samplesPerChannel)
                    {
                        break;
                    }

                    for (int channel = 0; channel < channels; channel++)
                    {
                        int index = (sampleIndex * channels) + channel;
                        if (index < samples.Length)
                        {
                            sum += Mathf.Abs(samples[index]);
                            count++;
                        }
                    }
                }

                envelope[frame] = count > 0 ? sum / count : 0f;
            }

            return envelope;
        }

        private static float[] BuildNovelty(float[] envelope)
        {
            float[] novelty = new float[envelope.Length];
            float max = 0.0001f;

            for (int index = 1; index < envelope.Length; index++)
            {
                float value = Mathf.Max(0f, envelope[index] - envelope[index - 1]);
                novelty[index] = value;
                max = Mathf.Max(max, value);
            }

            for (int index = 0; index < novelty.Length; index++)
            {
                novelty[index] /= max;
            }

            return novelty;
        }

        private static List<Peak> FindPeaks(float[] novelty, float frameDuration, float minimumGapSeconds)
        {
            List<Peak> peaks = new List<Peak>();
            float globalMean = GetMean(novelty);
            float globalDeviation = GetStandardDeviation(novelty, globalMean);
            float lastPeakTime = -999f;
            const int localRadius = 10;

            for (int index = 1; index < novelty.Length - 1; index++)
            {
                float value = novelty[index];
                if (value <= novelty[index - 1] || value < novelty[index + 1])
                {
                    continue;
                }

                GetLocalStats(novelty, index, localRadius, out float localMean, out float localDeviation);
                float threshold = Mathf.Max(globalMean + (globalDeviation * 0.5f), localMean + (localDeviation * 0.35f));
                if (value < threshold || value < 0.08f)
                {
                    continue;
                }

                float time = index * frameDuration;
                if (time - lastPeakTime < minimumGapSeconds)
                {
                    if (peaks.Count > 0 && value > peaks[peaks.Count - 1].Strength)
                    {
                        peaks[peaks.Count - 1] = new Peak(time, value);
                        lastPeakTime = time;
                    }

                    continue;
                }

                peaks.Add(new Peak(time, value));
                lastPeakTime = time;
            }

            return peaks;
        }

        private static float EstimateBpm(IReadOnlyList<Peak> peaks)
        {
            if (peaks.Count < 2)
            {
                return 120f;
            }

            float[] histogram = new float[Mathf.RoundToInt(MaximumBpm) + 1];
            for (int index = 0; index < peaks.Count; index++)
            {
                int maxCompare = Mathf.Min(peaks.Count, index + 12);
                for (int next = index + 1; next < maxCompare; next++)
                {
                    float interval = peaks[next].TimeSeconds - peaks[index].TimeSeconds;
                    if (interval <= 0.05f || interval > 2.5f)
                    {
                        continue;
                    }

                    float bpm = 60f / interval;
                    while (bpm < MinimumBpm)
                    {
                        bpm *= 2f;
                    }

                    while (bpm > MaximumBpm)
                    {
                        bpm *= 0.5f;
                    }

                    int bucket = Mathf.Clamp(Mathf.RoundToInt(bpm), Mathf.RoundToInt(MinimumBpm), Mathf.RoundToInt(MaximumBpm));
                    histogram[bucket] += peaks[index].Strength + peaks[next].Strength;
                }
            }

            int bestBucket = 120;
            float bestScore = 0f;
            for (int bucket = Mathf.RoundToInt(MinimumBpm); bucket <= Mathf.RoundToInt(MaximumBpm); bucket++)
            {
                float score = histogram[bucket];
                if (bucket > 1)
                {
                    score += histogram[bucket - 1] * 0.5f;
                }

                if (bucket < histogram.Length - 1)
                {
                    score += histogram[bucket + 1] * 0.5f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestBucket = bucket;
                }
            }

            return Mathf.Clamp(bestBucket, MinimumBpm, MaximumBpm);
        }

        private static List<Peak> SelectPlayablePeaks(
            IReadOnlyList<Peak> peaks,
            float minimumGapSeconds,
            float clipLength,
            int maximumGeneratedNotes)
        {
            List<Peak> selected = new List<Peak>();
            float lastSelectedTime = -999f;
            int maxNotes = maximumGeneratedNotes > 0 ? maximumGeneratedNotes : Mathf.Max(8, Mathf.RoundToInt(clipLength * 1.5f));

            for (int index = 0; index < peaks.Count && selected.Count < maxNotes; index++)
            {
                Peak peak = peaks[index];
                if (peak.TimeSeconds < 0.15f || peak.TimeSeconds > clipLength - 0.1f)
                {
                    continue;
                }

                if (peak.TimeSeconds - lastSelectedTime < minimumGapSeconds)
                {
                    continue;
                }

                selected.Add(peak);
                lastSelectedTime = peak.TimeSeconds;
            }

            return selected;
        }

        private static List<Peak> BuildBeatFallback(float firstBeatOffset, float beatDuration, float clipLength, int fallbackNoteCount)
        {
            List<Peak> peaks = new List<Peak>();
            float noteInterval = Mathf.Max(beatDuration, MinimumPlayableNoteGapSeconds);
            int count = Mathf.Max(8, fallbackNoteCount);
            for (int index = 0; index < count; index++)
            {
                float time = firstBeatOffset + (index * noteInterval);
                if (time >= clipLength)
                {
                    break;
                }

                peaks.Add(new Peak(time, 0.5f));
            }

            return peaks;
        }

        private static List<RhythmChartNote> BuildNotes(IReadOnlyList<Peak> peaks, int laneCount)
        {
            List<RhythmChartNote> notes = new List<RhythmChartNote>();
            int previousLane = -1;

            for (int index = 0; index < peaks.Count; index++)
            {
                Peak peak = peaks[index];
                int lane = Mathf.Abs(Mathf.RoundToInt((peak.Strength * 1000f) + (index * 1.618f))) % laneCount;
                if (lane == previousLane && laneCount > 1)
                {
                    lane = (lane + 1) % laneCount;
                }

                notes.Add(new RhythmChartNote(peak.TimeSeconds, lane, peak.Strength));
                previousLane = lane;
            }

            return notes;
        }

        private static void Smooth(float[] values, int radius)
        {
            if (radius <= 0 || values.Length == 0)
            {
                return;
            }

            float[] copy = new float[values.Length];
            Array.Copy(values, copy, values.Length);

            for (int index = 0; index < values.Length; index++)
            {
                float sum = 0f;
                int count = 0;
                for (int offset = -radius; offset <= radius; offset++)
                {
                    int sample = index + offset;
                    if (sample < 0 || sample >= copy.Length)
                    {
                        continue;
                    }

                    sum += copy[sample];
                    count++;
                }

                values[index] = count > 0 ? sum / count : copy[index];
            }
        }

        private static float GetMean(float[] values)
        {
            float sum = 0f;
            for (int index = 0; index < values.Length; index++)
            {
                sum += values[index];
            }

            return values.Length > 0 ? sum / values.Length : 0f;
        }

        private static float GetStandardDeviation(float[] values, float mean)
        {
            float sum = 0f;
            for (int index = 0; index < values.Length; index++)
            {
                float delta = values[index] - mean;
                sum += delta * delta;
            }

            return values.Length > 0 ? Mathf.Sqrt(sum / values.Length) : 0f;
        }

        private static void GetLocalStats(float[] values, int center, int radius, out float mean, out float deviation)
        {
            float sum = 0f;
            int count = 0;
            int min = Mathf.Max(0, center - radius);
            int max = Mathf.Min(values.Length - 1, center + radius);

            for (int index = min; index <= max; index++)
            {
                sum += values[index];
                count++;
            }

            mean = count > 0 ? sum / count : 0f;
            float variance = 0f;
            for (int index = min; index <= max; index++)
            {
                float delta = values[index] - mean;
                variance += delta * delta;
            }

            deviation = count > 0 ? Mathf.Sqrt(variance / count) : 0f;
        }

        private readonly struct Peak
        {
            public Peak(float timeSeconds, float strength)
            {
                TimeSeconds = timeSeconds;
                Strength = strength;
            }

            public float TimeSeconds { get; }
            public float Strength { get; }
        }
    }
}
