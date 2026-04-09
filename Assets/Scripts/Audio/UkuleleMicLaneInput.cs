using System;
using UnityEngine;

namespace TurnOnTheBass
{
    [Serializable]
    public struct UkuleleStringMapping
    {
        [Tooltip("Name shown in debug logs.")]
        public string stringName;
        [Range(0, RhythmMinigame.LaneCount - 1)]
        public int lane;
        [Min(80f)]
        public float targetFrequencyHz;
        [Range(10f, 180f)]
        public float toleranceCents;
        [Range(0.02f, 0.4f)]
        public float laneCooldownSeconds;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Turn On The Bass/Ukulele Mic Lane Input")]
    public sealed class UkuleleMicLaneInput : MonoBehaviour
    {
        private static readonly string[] LaneLabels = { "A", "S", "D", "F" };
        private static readonly Color[] LaneColors =
        {
            new Color(0.96f, 0.41f, 0.33f, 1f),
            new Color(0.99f, 0.75f, 0.27f, 1f),
            new Color(0.32f, 0.82f, 0.61f, 1f),
            new Color(0.37f, 0.57f, 0.98f, 1f)
        };

        [Header("Microphone")]
        [SerializeField] private bool autoStartOnEnable = true;
        [SerializeField] private bool useDefaultMicrophone = true;
        [SerializeField] private string microphoneDeviceName = string.Empty;
        [SerializeField, Range(1, 8)] private int recordingLengthSeconds = 2;
        [SerializeField, Range(16000, 48000)] private int requestedSampleRate = 44100;

        [Header("Analysis")]
        [SerializeField, Range(0.01f, 0.08f)] private float analysisInterval = 0.02f;
        [SerializeField, Range(512, 4096)] private int analysisWindowSize = 2048;
        [SerializeField, Min(80f)] private float minDetectFrequencyHz = 180f;
        [SerializeField, Min(220f)] private float maxDetectFrequencyHz = 520f;
        [SerializeField, Range(0.08f, 0.95f)] private float minAutocorrelationScore = 0.26f;

        [Header("Pluck Detection")]
        [SerializeField, Range(0.001f, 0.15f)] private float minRmsForDetection = 0.018f;
        [SerializeField, Range(0.0005f, 0.09f)] private float onsetRmsDelta = 0.011f;
        [SerializeField, Range(0.02f, 0.35f)] private float globalTriggerCooldown = 0.08f;
        [SerializeField, Range(0.01f, 0.6f)] private float rmsSmoothing = 0.22f;

        [Header("String Mapping (0=A lane, 1=S lane, 2=D lane, 3=F lane)")]
        [SerializeField] private UkuleleStringMapping[] stringMappings = new UkuleleStringMapping[0];

        [Header("Debug")]
        [SerializeField] private bool debugLogHits;
        [SerializeField] private bool drawDebugOverlay = true;
        [SerializeField] private bool drawLaneDebugOutsideMinigame = true;
        [SerializeField, Range(0.1f, 1f)] private float laneFlashDuration = 0.35f;
        [SerializeField, Range(0.2f, 1f)] private float idleLaneAlpha = 0.35f;

        private string activeMicrophoneDevice;
        private AudioClip microphoneClip;
        private float[] rawSampleBuffer;
        private float[] monoSampleBuffer;
        private float[] laneLastHitTime;
        private int[] laneHitCounts;
        private float[] laneFlashUntil;

        private bool microphoneReady;
        private float previousRms;
        private float lastAnalysisTime;
        private float lastGlobalHitTime = -1000f;

        private float lastDetectedFrequency;
        private float lastAutocorrelationScore;
        private float lastRms;
        private string lastMatchedString = "--";

        public bool IsMicrophoneReady => microphoneReady;
        public string ActiveMicrophoneDevice => activeMicrophoneDevice;
        public float LastDetectedFrequency => lastDetectedFrequency;

        private void Reset()
        {
            stringMappings = CreateDefaultMappings();
            EnsureLaneTimesArray();
            EnsureDebugArrays();
            ValidateConfig();
        }

        private void OnValidate()
        {
            if (stringMappings == null || stringMappings.Length == 0)
            {
                stringMappings = CreateDefaultMappings();
            }

            ValidateConfig();
            EnsureLaneTimesArray();
            EnsureDebugArrays();
        }

        private void OnEnable()
        {
            EnsureLaneTimesArray();
            EnsureDebugArrays();
            ValidateConfig();

            if (autoStartOnEnable)
            {
                StartMicrophoneCapture();
            }
        }

        private void OnDisable()
        {
            StopMicrophoneCapture();
        }

        private void Update()
        {
            if (!microphoneReady || microphoneClip == null)
            {
                return;
            }

            if ((Time.unscaledTime - lastAnalysisTime) < analysisInterval)
            {
                return;
            }

            lastAnalysisTime = Time.unscaledTime;
            AnalyzeCurrentAudioFrame();
        }

        private void OnGUI()
        {
            if (!drawDebugOverlay)
            {
                return;
            }

            Rect panel = new Rect(Screen.width - 370f, 12f, 358f, 120f);
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label("Ukulele Mic Input");
            GUILayout.Label("Mic: " + (microphoneReady ? activeMicrophoneDevice : "Not Ready"));
            GUILayout.Label("RMS: " + lastRms.ToString("0.0000"));
            GUILayout.Label("Freq: " + lastDetectedFrequency.ToString("0.0") + " Hz");
            GUILayout.Label("Matched: " + lastMatchedString + " (corr " + lastAutocorrelationScore.ToString("0.00") + ")");
            GUILayout.EndArea();

            if (drawLaneDebugOutsideMinigame)
            {
                DrawLaneDebugBoard();
            }
        }

        public void StartMicrophoneCapture()
        {
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.LogWarning("No microphone devices detected.");
                microphoneReady = false;
                return;
            }

            string selectedDevice = ResolveMicrophoneDevice();
            if (string.IsNullOrEmpty(selectedDevice))
            {
                Debug.LogWarning("Could not resolve a microphone device.");
                microphoneReady = false;
                return;
            }

            StopMicrophoneCapture();
            activeMicrophoneDevice = selectedDevice;
            microphoneClip = Microphone.Start(activeMicrophoneDevice, true, recordingLengthSeconds, requestedSampleRate);
            if (microphoneClip == null)
            {
                Debug.LogWarning("Microphone.Start returned null for device: " + activeMicrophoneDevice);
                microphoneReady = false;
                return;
            }

            microphoneReady = true;
            previousRms = 0f;
            lastDetectedFrequency = 0f;
            lastAutocorrelationScore = 0f;
            lastMatchedString = "--";
            lastGlobalHitTime = -1000f;
            InputRouter.ClearExternalLanePresses();
        }

        public void StopMicrophoneCapture()
        {
            if (!string.IsNullOrEmpty(activeMicrophoneDevice))
            {
                if (Microphone.IsRecording(activeMicrophoneDevice))
                {
                    Microphone.End(activeMicrophoneDevice);
                }
            }

            microphoneReady = false;
            activeMicrophoneDevice = string.Empty;
            microphoneClip = null;
            InputRouter.ClearExternalLanePresses();
        }

        private void AnalyzeCurrentAudioFrame()
        {
            if (!TryReadLatestMonoSamples())
            {
                return;
            }

            float rms = ComputeRms(monoSampleBuffer);
            lastRms = rms;

            float rmsDelta = rms - previousRms;
            previousRms = Mathf.Lerp(previousRms, rms, rmsSmoothing);

            if (rms < minRmsForDetection || rmsDelta < onsetRmsDelta)
            {
                return;
            }

            if ((Time.unscaledTime - lastGlobalHitTime) < globalTriggerCooldown)
            {
                return;
            }

            float frequency = EstimateFundamentalFrequency(monoSampleBuffer, microphoneClip.frequency, out float bestScore);
            lastDetectedFrequency = frequency;
            lastAutocorrelationScore = bestScore;

            if (frequency <= 0f || bestScore < minAutocorrelationScore)
            {
                return;
            }

            if (!TryMapFrequencyToLane(frequency, out UkuleleStringMapping mappedString))
            {
                lastMatchedString = "--";
                return;
            }

            int lane = mappedString.lane;
            if ((Time.unscaledTime - laneLastHitTime[lane]) < mappedString.laneCooldownSeconds)
            {
                return;
            }

            laneLastHitTime[lane] = Time.unscaledTime;
            lastGlobalHitTime = Time.unscaledTime;
            lastMatchedString = mappedString.stringName;
            laneHitCounts[lane]++;
            laneFlashUntil[lane] = Time.unscaledTime + laneFlashDuration;
            InputRouter.QueueExternalLanePress(lane);

            if (debugLogHits)
            {
                Debug.Log(
                    "Ukulele hit -> " + mappedString.stringName +
                    " (lane " + lane + ") " +
                    frequency.ToString("0.0") + " Hz, corr " + bestScore.ToString("0.00"));
            }
        }

        private bool TryReadLatestMonoSamples()
        {
            if (microphoneClip == null)
            {
                return false;
            }

            int currentPosition = Microphone.GetPosition(activeMicrophoneDevice);
            if (currentPosition <= 0 || currentPosition < analysisWindowSize)
            {
                return false;
            }

            int channels = Mathf.Max(1, microphoneClip.channels);
            int requiredRawLength = analysisWindowSize * channels;

            if (rawSampleBuffer == null || rawSampleBuffer.Length != requiredRawLength)
            {
                rawSampleBuffer = new float[requiredRawLength];
            }

            if (monoSampleBuffer == null || monoSampleBuffer.Length != analysisWindowSize)
            {
                monoSampleBuffer = new float[analysisWindowSize];
            }

            int sampleOffset = currentPosition - analysisWindowSize;
            while (sampleOffset < 0)
            {
                sampleOffset += microphoneClip.samples;
            }

            microphoneClip.GetData(rawSampleBuffer, sampleOffset);

            for (int sampleIndex = 0; sampleIndex < analysisWindowSize; sampleIndex++)
            {
                float summed = 0f;
                int frameIndex = sampleIndex * channels;
                for (int channel = 0; channel < channels; channel++)
                {
                    summed += rawSampleBuffer[frameIndex + channel];
                }

                monoSampleBuffer[sampleIndex] = summed / channels;
            }

            return true;
        }

        private bool TryMapFrequencyToLane(float frequency, out UkuleleStringMapping mapping)
        {
            mapping = default;
            bool found = false;
            float bestCentsDistance = float.MaxValue;

            for (int index = 0; index < stringMappings.Length; index++)
            {
                UkuleleStringMapping candidate = stringMappings[index];
                if (candidate.targetFrequencyHz <= 0f || candidate.lane < 0 || candidate.lane >= RhythmMinigame.LaneCount)
                {
                    continue;
                }

                float cents = Mathf.Abs(1200f * Mathf.Log(frequency / candidate.targetFrequencyHz, 2f));
                if (cents > candidate.toleranceCents || cents >= bestCentsDistance)
                {
                    continue;
                }

                bestCentsDistance = cents;
                mapping = candidate;
                found = true;
            }

            return found;
        }

        private float EstimateFundamentalFrequency(float[] samples, int sampleRate, out float bestScore)
        {
            bestScore = 0f;

            int lagMin = Mathf.Max(1, Mathf.FloorToInt(sampleRate / maxDetectFrequencyHz));
            int lagMax = Mathf.Min(samples.Length - 2, Mathf.CeilToInt(sampleRate / minDetectFrequencyHz));
            if (lagMax <= lagMin)
            {
                return 0f;
            }

            int bestLag = -1;
            for (int lag = lagMin; lag <= lagMax; lag++)
            {
                float corr = 0f;
                float energyA = 0f;
                float energyB = 0f;
                int sampleCount = samples.Length - lag;

                for (int index = 0; index < sampleCount; index++)
                {
                    float a = samples[index];
                    float b = samples[index + lag];
                    corr += a * b;
                    energyA += a * a;
                    energyB += b * b;
                }

                float normalized = corr / Mathf.Sqrt((energyA * energyB) + 1e-8f);
                if (normalized <= bestScore)
                {
                    continue;
                }

                bestScore = normalized;
                bestLag = lag;
            }

            if (bestLag <= 0)
            {
                return 0f;
            }

            return sampleRate / (float)bestLag;
        }

        private static float ComputeRms(float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return 0f;
            }

            float sum = 0f;
            for (int index = 0; index < samples.Length; index++)
            {
                sum += samples[index] * samples[index];
            }

            return Mathf.Sqrt(sum / samples.Length);
        }

        private string ResolveMicrophoneDevice()
        {
            if (useDefaultMicrophone || string.IsNullOrWhiteSpace(microphoneDeviceName))
            {
                return Microphone.devices[0];
            }

            for (int index = 0; index < Microphone.devices.Length; index++)
            {
                if (Microphone.devices[index] == microphoneDeviceName)
                {
                    return microphoneDeviceName;
                }
            }

            return Microphone.devices[0];
        }

        private void EnsureLaneTimesArray()
        {
            bool needsReset = laneLastHitTime == null || laneLastHitTime.Length != RhythmMinigame.LaneCount;
            if (needsReset)
            {
                laneLastHitTime = new float[RhythmMinigame.LaneCount];
                for (int lane = 0; lane < laneLastHitTime.Length; lane++)
                {
                    laneLastHitTime[lane] = -1000f;
                }
            }
        }

        private void EnsureDebugArrays()
        {
            bool missingCounts = laneHitCounts == null || laneHitCounts.Length != RhythmMinigame.LaneCount;
            if (missingCounts)
            {
                laneHitCounts = new int[RhythmMinigame.LaneCount];
            }

            bool missingFlashes = laneFlashUntil == null || laneFlashUntil.Length != RhythmMinigame.LaneCount;
            if (missingFlashes)
            {
                laneFlashUntil = new float[RhythmMinigame.LaneCount];
                for (int lane = 0; lane < laneFlashUntil.Length; lane++)
                {
                    laneFlashUntil[lane] = -1000f;
                }
            }
        }

        private void ValidateConfig()
        {
            recordingLengthSeconds = Mathf.Clamp(recordingLengthSeconds, 1, 8);
            requestedSampleRate = Mathf.Clamp(requestedSampleRate, 16000, 48000);
            analysisInterval = Mathf.Clamp(analysisInterval, 0.01f, 0.08f);
            analysisWindowSize = Mathf.Clamp(analysisWindowSize, 512, 4096);
            minDetectFrequencyHz = Mathf.Clamp(minDetectFrequencyHz, 80f, 1200f);
            maxDetectFrequencyHz = Mathf.Clamp(maxDetectFrequencyHz, minDetectFrequencyHz + 10f, 2000f);
            minAutocorrelationScore = Mathf.Clamp01(minAutocorrelationScore);
            minRmsForDetection = Mathf.Clamp(minRmsForDetection, 0.001f, 0.15f);
            onsetRmsDelta = Mathf.Clamp(onsetRmsDelta, 0.0005f, 0.09f);
            globalTriggerCooldown = Mathf.Clamp(globalTriggerCooldown, 0.02f, 0.35f);
            rmsSmoothing = Mathf.Clamp(rmsSmoothing, 0.01f, 0.6f);
            laneFlashDuration = Mathf.Clamp(laneFlashDuration, 0.1f, 1f);
            idleLaneAlpha = Mathf.Clamp(idleLaneAlpha, 0.2f, 1f);

            for (int index = 0; index < stringMappings.Length; index++)
            {
                UkuleleStringMapping mapping = stringMappings[index];
                mapping.lane = Mathf.Clamp(mapping.lane, 0, RhythmMinigame.LaneCount - 1);
                mapping.targetFrequencyHz = Mathf.Max(80f, mapping.targetFrequencyHz);
                mapping.toleranceCents = Mathf.Clamp(mapping.toleranceCents, 10f, 180f);
                mapping.laneCooldownSeconds = Mathf.Clamp(mapping.laneCooldownSeconds, 0.02f, 0.4f);
                if (string.IsNullOrWhiteSpace(mapping.stringName))
                {
                    mapping.stringName = "String " + (index + 1);
                }

                stringMappings[index] = mapping;
            }
        }

        private void DrawLaneDebugBoard()
        {
            Rect boardRect = new Rect(Screen.width - 370f, 138f, 358f, 84f);
            DrawRect(boardRect, new Color(0f, 0f, 0f, 0.6f));
            GUI.Box(boardRect, string.Empty);

            float laneWidth = (boardRect.width - 10f) / RhythmMinigame.LaneCount;
            for (int lane = 0; lane < RhythmMinigame.LaneCount; lane++)
            {
                Rect laneRect = new Rect(
                    boardRect.x + 4f + (lane * laneWidth),
                    boardRect.y + 4f,
                    laneWidth - 4f,
                    boardRect.height - 8f);

                Color fill = LaneColors[lane];
                bool flash = Time.unscaledTime <= laneFlashUntil[lane];
                fill.a = flash ? 0.9f : idleLaneAlpha;
                DrawRect(laneRect, fill);

                string lastAge = laneLastHitTime[lane] <= -999f
                    ? "--"
                    : (Time.unscaledTime - laneLastHitTime[lane]).ToString("0.00") + "s";

                GUI.Label(
                    new Rect(laneRect.x + 6f, laneRect.y + 6f, laneRect.width - 8f, 20f),
                    LaneLabels[lane] + " lane");
                GUI.Label(
                    new Rect(laneRect.x + 6f, laneRect.y + 26f, laneRect.width - 8f, 20f),
                    "hits " + laneHitCounts[lane]);
                GUI.Label(
                    new Rect(laneRect.x + 6f, laneRect.y + 46f, laneRect.width - 8f, 20f),
                    "last " + lastAge);
            }
        }

        private static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static UkuleleStringMapping[] CreateDefaultMappings()
        {
            return new[]
            {
                new UkuleleStringMapping
                {
                    stringName = "G4 String",
                    lane = 0,
                    targetFrequencyHz = 392f,
                    toleranceCents = 55f,
                    laneCooldownSeconds = 0.1f
                },
                new UkuleleStringMapping
                {
                    stringName = "C4 String",
                    lane = 1,
                    targetFrequencyHz = 261.63f,
                    toleranceCents = 55f,
                    laneCooldownSeconds = 0.1f
                },
                new UkuleleStringMapping
                {
                    stringName = "E4 String",
                    lane = 2,
                    targetFrequencyHz = 329.63f,
                    toleranceCents = 55f,
                    laneCooldownSeconds = 0.1f
                },
                new UkuleleStringMapping
                {
                    stringName = "A4 String",
                    lane = 3,
                    targetFrequencyHz = 440f,
                    toleranceCents = 55f,
                    laneCooldownSeconds = 0.1f
                }
            };
        }
    }
}
