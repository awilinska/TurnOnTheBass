using UnityEngine;

namespace TurnOnTheBass
{
    [CreateAssetMenu(fileName = "Rhythm Song", menuName = "Turn On The Bass/Rhythm Song")]
    public sealed class RhythmSongDefinition : ScriptableObject
    {
        [Header("Audio")]
        [SerializeField] private AudioClip audioClip;

        [Header("Auto Chart")]
        [SerializeField] private bool autoGenerateFromAudio = true;
        [Tooltip("0 means the analyzer decides from song length. Use this only as a cap for very long songs.")]
        [SerializeField, Min(0)] private int maximumGeneratedNotes;

        [Header("Manual Fallback")]
        [SerializeField, Min(1f)] private float bpm = 120f;
        [SerializeField, Min(0f)] private float firstBeatOffsetSeconds = 1.5f;

        [Header("Notes")]
        [SerializeField] private bool useBeatSync = true;
        [SerializeField, Min(0f)] private float startBeat;
        [SerializeField, Min(0.25f)] private float noteBeatInterval = 1f;
        [SerializeField, Min(0)] private int maxNotes;
        [SerializeField] private bool randomizeLanesWhenPatternEmpty = true;
        [SerializeField] private int[] lanePattern = { 0, 1, 2, 3 };

        [Header("Spin")]
        [SerializeField] private bool enableSpinPhase = true;
        [SerializeField, Min(1)] private int firstSpinAfterNotes = 16;
        [SerializeField, Min(1)] private int spinEveryNotes = 16;
        [SerializeField, Min(1)] private int requiredSpinPresses = 12;
        [SerializeField] private bool useSpinTimeLimit = true;
        [SerializeField, Min(0.1f)] private float spinTimeLimitSeconds = 3f;

        public AudioClip AudioClip => audioClip;
        public bool AutoGenerateFromAudio => autoGenerateFromAudio;
        public int MaximumGeneratedNotes => maximumGeneratedNotes;
        public float Bpm => bpm;
        public float FirstBeatOffsetSeconds => firstBeatOffsetSeconds;
        public bool UseBeatSync => useBeatSync;
        public float StartBeat => startBeat;
        public float NoteBeatInterval => noteBeatInterval;
        public int MaxNotes => maxNotes;
        public bool RandomizeLanesWhenPatternEmpty => randomizeLanesWhenPatternEmpty;
        public bool EnableSpinPhase => enableSpinPhase;
        public int FirstSpinAfterNotes => firstSpinAfterNotes;
        public int SpinEveryNotes => spinEveryNotes;
        public int RequiredSpinPresses => requiredSpinPresses;
        public bool UseSpinTimeLimit => useSpinTimeLimit;
        public float SpinTimeLimitSeconds => spinTimeLimitSeconds;
        public float BeatDurationSeconds => 60f / Mathf.Max(1f, bpm);
        public float NoteIntervalSeconds => BeatDurationSeconds * Mathf.Max(0.25f, noteBeatInterval);

        public int GetLaneForNote(int noteIndex, int laneCount)
        {
            if (laneCount <= 0)
            {
                return -1;
            }

            if (lanePattern != null && lanePattern.Length > 0)
            {
                int lane = lanePattern[Mathf.Abs(noteIndex) % lanePattern.Length];
                return Mathf.Clamp(lane, 0, laneCount - 1);
            }

            if (randomizeLanesWhenPatternEmpty)
            {
                return Random.Range(0, laneCount);
            }

            return Mathf.Abs(noteIndex) % laneCount;
        }

        public int GetResolvedNoteCount(int fallbackNoteCount)
        {
            if (maxNotes > 0)
            {
                return maxNotes;
            }

            if (audioClip == null)
            {
                return Mathf.Max(1, fallbackNoteCount);
            }

            float playableSeconds = Mathf.Max(0f, audioClip.length - firstBeatOffsetSeconds);
            int count = Mathf.FloorToInt(playableSeconds / Mathf.Max(0.01f, NoteIntervalSeconds));
            return Mathf.Max(1, count);
        }

        private void OnValidate()
        {
            bpm = Mathf.Max(1f, bpm);
            maximumGeneratedNotes = Mathf.Max(0, maximumGeneratedNotes);
            firstBeatOffsetSeconds = Mathf.Max(0f, firstBeatOffsetSeconds);
            noteBeatInterval = Mathf.Max(0.25f, noteBeatInterval);
            maxNotes = Mathf.Max(0, maxNotes);
            firstSpinAfterNotes = Mathf.Max(1, firstSpinAfterNotes);
            spinEveryNotes = Mathf.Max(1, spinEveryNotes);
            requiredSpinPresses = Mathf.Max(1, requiredSpinPresses);
            spinTimeLimitSeconds = Mathf.Max(0.1f, spinTimeLimitSeconds);
        }
    }
}
