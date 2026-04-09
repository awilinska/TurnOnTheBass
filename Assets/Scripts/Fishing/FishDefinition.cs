using System;
using UnityEngine;

namespace TurnOnTheBass
{
    [Serializable]
    public sealed class FishDefinition
    {
        [SerializeField] private string fishId = "fish";
        [SerializeField] private string displayName = "Unknown Fish";
        [SerializeField] private WaterBodyType habitat = WaterBodyType.Lake;
        [SerializeField, Min(0.1f)] private float baseSizeKg = 1f;
        [SerializeField, Range(0f, 1f)] private float difficulty = 0.35f;
        [SerializeField, Min(60f)] private float bpm = 110f;
        [SerializeField, Min(8)] private int songLengthBeats = 24;
        [SerializeField] private string songResourceName = string.Empty;
        [SerializeField, Range(-0.2f, 0.2f)] private float qualityBias;

        public string FishId => fishId;
        public string DisplayName => displayName;
        public WaterBodyType Habitat => habitat;
        public float BaseSizeKg => baseSizeKg;
        public float Difficulty => difficulty;
        public float Bpm => bpm;
        public int SongLengthBeats => songLengthBeats;
        public string SongResourceName => songResourceName;
        public float QualityBias => qualityBias;

        public FishDefinition(
            string fishId,
            string displayName,
            WaterBodyType habitat,
            float baseSizeKg,
            float difficulty,
            float bpm,
            int songLengthBeats,
            string songResourceName,
            float qualityBias = 0f)
        {
            this.fishId = fishId;
            this.displayName = displayName;
            this.habitat = habitat;
            this.baseSizeKg = baseSizeKg;
            this.difficulty = Mathf.Clamp01(difficulty);
            this.bpm = Mathf.Max(60f, bpm);
            this.songLengthBeats = Mathf.Max(8, songLengthBeats);
            this.songResourceName = songResourceName;
            this.qualityBias = Mathf.Clamp(qualityBias, -0.2f, 0.2f);
        }

        public AudioClip LoadSongClip()
        {
            if (string.IsNullOrWhiteSpace(songResourceName))
            {
                return null;
            }

            return Resources.Load<AudioClip>("Songs/" + songResourceName);
        }
    }
}
