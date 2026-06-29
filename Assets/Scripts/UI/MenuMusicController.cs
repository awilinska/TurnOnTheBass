using UnityEngine;

namespace TurnOnTheBass.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class MenuMusicController : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private CanvasRhythmGame rhythmGame;
        [SerializeField] private AudioClip musicClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.45f;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool pauseDuringRhythmGame = true;

        private void Reset()
        {
            audioSource = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            CacheAudioSource();
            ApplyAudioSettings();
        }

        private void Start()
        {
            if (rhythmGame == null)
            {
                rhythmGame = FindRhythmGameIncludingInactive();
            }

            if (playOnStart)
            {
                Play();
            }
        }

        private void Update()
        {
            if (!pauseDuringRhythmGame || rhythmGame == null)
            {
                EnsurePlaying();
                return;
            }

            if (rhythmGame.gameObject.activeInHierarchy && rhythmGame.IsPlaying)
            {
                if (audioSource.isPlaying)
                {
                    audioSource.Pause();
                }

                return;
            }

            EnsurePlaying();
        }

        private void OnValidate()
        {
            CacheAudioSource();
            ApplyAudioSettings();
        }

        public void Play()
        {
            CacheAudioSource();
            ApplyAudioSettings();
            EnsurePlaying();
        }

        public void Stop()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
            }
        }

        private void EnsurePlaying()
        {
            if (audioSource == null || audioSource.clip == null || audioSource.isPlaying)
            {
                return;
            }

            audioSource.UnPause();
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }

        private void CacheAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        private void ApplyAudioSettings()
        {
            if (audioSource == null)
            {
                return;
            }

            if (musicClip != null)
            {
                audioSource.clip = musicClip;
            }

            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.volume = volume;
            audioSource.spatialBlend = 0f;
        }

        private static CanvasRhythmGame FindRhythmGameIncludingInactive()
        {
            CanvasRhythmGame[] rhythmGames = Resources.FindObjectsOfTypeAll<CanvasRhythmGame>();
            for (int index = 0; index < rhythmGames.Length; index++)
            {
                CanvasRhythmGame game = rhythmGames[index];
                if (game != null && game.gameObject.scene.IsValid())
                {
                    return game;
                }
            }

            return null;
        }
    }
}
