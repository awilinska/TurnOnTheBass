using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TurnOnTheBass.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TurnOnTheBass
{
    public enum RhythmHitRating
    {
        Miss = 0,
        Bad = 1,
        Good = 2,
        Perfect = 3
    }

    [Serializable]
    public sealed class CanvasRhythmLane
    {
        [SerializeField] private string laneName = "Lane";
        [SerializeField] private GameObject notePrefab;

        public string LaneName => laneName;
        public GameObject NotePrefab => notePrefab;

        public void SetDefaultName(string value)
        {
            if (string.IsNullOrWhiteSpace(laneName) || laneName == "Lane")
            {
                laneName = value;
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class CanvasRhythmGame : MonoBehaviour
    {
        private sealed class ActiveNote
        {
            public int Id;
            public int LaneIndex;
            public RectTransform RectTransform;
            public Vector2 TargetPosition;
            public Vector2 MoveDirection;
            public bool Judged;
        }

        [Header("Canvas References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private RectTransform playArea;
        [SerializeField] private RectTransform noteParent;
        [SerializeField] private RectTransform targetNote;

        [Header("Panel Visibility")]
        [SerializeField] private bool activatePanelOnBegin = true;
        [SerializeField] private bool hidePanelOnComplete = true;
        [SerializeField] private bool hidePanelOnStop = true;

        [Header("Completion Screen")]
        [SerializeField] private UiScreenSwitcher screenSwitcher;
        [SerializeField] private GameObject finishedScreen;
        [SerializeField] private RhythmResultsScreen resultsScreen;
        [SerializeField] private bool showFinishedScreenOnComplete = true;

        [Header("Lanes")]
        [SerializeField] private CanvasRhythmLane[] lanes =
        {
            new CanvasRhythmLane(),
            new CanvasRhythmLane(),
            new CanvasRhythmLane(),
            new CanvasRhythmLane()
        };
        [SerializeField, Min(0f)] private float laneSpacingPixels = 140f;
        [SerializeField, Range(0.1f, 1f)] private float autoLaneWidthPercent = 0.72f;
        [SerializeField] private bool centerLanesOnTarget = true;

        [Header("Game Flow")]
        [SerializeField] private bool playOnStart;
        [SerializeField, Min(0f)] private float startDelaySeconds = 0.5f;
        [SerializeField, Min(0.05f)] private float spawnIntervalSeconds = 0.75f;
        [SerializeField, Min(0)] private int totalNotesToSpawn = 32;
        [SerializeField] private bool randomizeLanes = true;
        [SerializeField] private bool loopWhenFinished;

        [Header("Song Playback")]
        [SerializeField] private RhythmSongDefinition songDefinition;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private bool stopAudioOnComplete = true;
        [SerializeField, Min(0f)] private float maxSongSpawnLateSeconds = 0.08f;

#if ENABLE_INPUT_SYSTEM
        [Header("Temporary Keyboard Controls")]
        [SerializeField] private bool enableKeyboardInput = true;
        [SerializeField] private Key greenLaneKey = Key.A;
        [SerializeField] private Key blueLaneKey = Key.S;
        [SerializeField] private Key blackLaneKey = Key.D;
        [SerializeField] private Key whiteLaneKey = Key.F;
#endif

        [Header("Motion")]
        [SerializeField, Min(1f)] private float noteSpeedPixelsPerSecond = 720f;
        [SerializeField, Min(0f)] private float bottomSpawnOffsetPixels = 64f;
        [SerializeField, Min(0f)] private float defaultTargetOffsetFromTopPixels = 180f;
        [SerializeField, Min(0f)] private float despawnPastTargetPixels = 140f;
        [SerializeField] private bool useUnscaledTime;

        [Header("Note Size")]
        [SerializeField] private bool overrideNoteSize;
        [SerializeField] private Vector2 noteSize = new Vector2(64f, 64f);

        [Header("Hit Windows")]
        [SerializeField, Min(0f)] private float perfectWindowPixels = 24f;
        [SerializeField, Min(0f)] private float goodWindowPixels = 52f;
        [SerializeField, Min(0f)] private float badWindowPixels = 90f;
        [SerializeField, Min(0f)] private float autoMissWindowPixels = 110f;
        [SerializeField] private bool destroyNoteOnBadClick = true;

        [Header("Scoring")]
        [SerializeField, Min(0)] private int perfectScore = 100;
        [SerializeField, Min(0)] private int goodScore = 70;
        [SerializeField, Min(0)] private int badScore = 35;

        [Header("Spin Phase")]
        [SerializeField] private bool enableSpinPhase = true;
        [SerializeField] private TextMeshProUGUI spinText;
        [SerializeField] private string spinLabel = "SPIN!";
        [SerializeField] private string completedSpinFormat = "SPIN x{0}";
        [SerializeField, Min(1)] private int requiredSpinPresses = 12;
        [SerializeField, Min(1)] private int firstSpinAfterNotes = 6;
        [SerializeField, Min(1)] private int spinEveryNotes = 8;
        [SerializeField] private bool useSpinTimeLimit;
        [SerializeField, Min(0.1f)] private float spinTimeLimitSeconds = 3f;
        [SerializeField, Min(1f)] private float targetPulseScale = 1.25f;
        [SerializeField, Min(0.1f)] private float targetPulseSpeed = 6f;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private Key spinKey = Key.R;
#endif

        [Header("Optional UI")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI comboText;
        [SerializeField] private TextMeshProUGUI feedbackText;
        [SerializeField] private TextMeshProUGUI accuracyText;
        [SerializeField] private TextMeshProUGUI noteHitText;
        [SerializeField] private string accuracyFormat = "Accuracy: {0}%";
        [SerializeField] private string perfectLabel = "PERFECT";
        [SerializeField] private string goodLabel = "GOOD";
        [SerializeField] private string badLabel = "MEH";
        [SerializeField] private string missLabel = "MISSED";
        [SerializeField] private bool logHitsToConsole;

        private readonly List<ActiveNote> activeNotes = new List<ActiveNote>();
        private Canvas rootCanvas;
        private Camera uiCamera;
        private float startTimer;
        private float spawnTimer;
        private int nextNoteId;
        private int spawnedNotes;
        private int laneSequenceIndex;
        private int nextSongNoteIndex;
        private RhythmGeneratedChart generatedSongChart;
        private bool isPlaying;
        private bool isComplete;
        private bool songPlaybackStarted;
        private float songTimer;
        private bool spinPhaseActive;
        private int completedSpinCount;
        private int currentSpinPromptPresses;
        private int nextSpinPromptAtNote;
        private int spinPresses;
        private float spinTimer;
        private Vector3 targetBaseScale = Vector3.one;
        private bool hasTargetBaseScale;

        public bool IsPlaying => isPlaying;
        public bool IsComplete => isComplete;
        public RhythmSongDefinition CurrentSong => songDefinition;
        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int PerfectHits { get; private set; }
        public int GoodHits { get; private set; }
        public int BadHits { get; private set; }
        public int Misses { get; private set; }
        public int TotalJudgedNotes => PerfectHits + GoodHits + BadHits + Misses;
        public bool IsSpinPhaseActive => spinPhaseActive;
        public int SpinPresses => spinPresses;
        public int RequiredSpinPresses => requiredSpinPresses;
        public int CompletedSpinCount => completedSpinCount;

        public float Accuracy
        {
            get
            {
                int totalPossibleNotes = totalNotesToSpawn > 0 ? totalNotesToSpawn : Mathf.Max(1, TotalJudgedNotes);
                float maxScore = Mathf.Max(1, totalPossibleNotes * perfectScore);
                return Mathf.Clamp01(Score / maxScore);
            }
        }

        private RectTransform NoteParent => noteParent != null ? noteParent : playArea;

        private void Awake()
        {
            CacheCanvas();
            ValidateConfig();
            EnsureEventSystemExists();
        }

        private void Start()
        {
            RefreshUi();

            if (playOnStart)
            {
                BeginGame();
            }
        }

        private void OnValidate()
        {
            ValidateConfig();
            ApplyDefaultLaneNames();
        }

        private void Update()
        {
            if (!isPlaying)
            {
                return;
            }

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (startTimer > 0f)
            {
                startTimer -= deltaTime;
                if (startTimer > 0f)
                {
                    return;
                }
            }

            StartSongPlaybackIfNeeded();
            if (songPlaybackStarted)
            {
                songTimer += deltaTime;
            }

            if (spinPhaseActive)
            {
                HandleSpinInput();
                UpdateSpinPrompt(deltaTime);
                return;
            }

            HandleKeyboardInput();
            UpdateActiveNotes(deltaTime);
            if (TryBeginSpinPromptIfReady())
            {
                return;
            }

            if (!ShouldHoldSpawningForSpin())
            {
                if (ShouldUseSongSync())
                {
                    UpdateSongSpawning();
                }
                else
                {
                    UpdateSpawning(deltaTime);
                }
            }

            CheckForCompletion();
        }

        public void SetSong(RhythmSongDefinition song)
        {
            songDefinition = song;
            ApplySongDefinition();
        }

        public void BeginSong(RhythmSongDefinition song)
        {
            SetSong(song);
            BeginGame();
        }

        public void BeginGame()
        {
            ApplySongDefinition();

            if (activatePanelOnBegin)
            {
                PanelRoot.SetActive(true);
            }

            CacheCanvas();
            ValidateConfig();
            ClearActiveNotes();

            Score = 0;
            Combo = 0;
            MaxCombo = 0;
            PerfectHits = 0;
            GoodHits = 0;
            BadHits = 0;
            Misses = 0;
            spawnedNotes = 0;
            laneSequenceIndex = 0;
            nextSongNoteIndex = 0;
            songPlaybackStarted = false;
            songTimer = 0f;
            spinPhaseActive = false;
            completedSpinCount = 0;
            currentSpinPromptPresses = 0;
            nextSpinPromptAtNote = Mathf.Max(1, firstSpinAfterNotes);
            spinPresses = 0;
            spinTimer = 0f;
            startTimer = startDelaySeconds;
            spawnTimer = 0f;
            isPlaying = true;
            isComplete = false;

            CacheTargetBaseScale();
            SetSpinUiActive(false);
            ResetTargetPulse();
            ClearFeedback();
            RefreshUi();

            if (startTimer <= 0f)
            {
                StartSongPlaybackIfNeeded();
            }
        }

        public void StopGame()
        {
            isPlaying = false;
            isComplete = true;
            StopSongPlayback();
            ClearActiveNotes();
            RefreshUi();
            SetSpinUiActive(false);
            ResetTargetPulse();

            if (hidePanelOnStop)
            {
                PanelRoot.SetActive(false);
            }
        }

        public void SpawnNoteInLane(int laneIndex)
        {
            TrySpawnNote(laneIndex);
        }

        public void HandleNoteClicked(int noteId)
        {
            ActiveNote note = FindActiveNote(noteId);
            if (note == null || note.Judged)
            {
                return;
            }

            float distance = GetDistanceFromTarget(note);
            RhythmHitRating rating = GetRating(distance);
            if (rating == RhythmHitRating.Miss && !destroyNoteOnBadClick)
            {
                RegisterMiss(missLabel, distance, false);
                return;
            }

            ApplyJudgement(note, rating, distance);
        }

        public void HandleLanePressed(int laneIndex)
        {
            ActiveNote note = FindClosestPendingNoteInLane(laneIndex);
            if (note == null)
            {
                RegisterMiss(missLabel, badWindowPixels + 1f, true);
                return;
            }

            float distance = GetDistanceFromTarget(note);
            RhythmHitRating rating = GetRating(distance);
            if (rating == RhythmHitRating.Miss && !destroyNoteOnBadClick)
            {
                RegisterMiss(missLabel, distance, true);
                return;
            }

            ApplyJudgement(note, rating, distance);
        }

        public void HandleSpinTick()
        {
            if (!spinPhaseActive)
            {
                return;
            }

            spinPresses++;
            completedSpinCount++;
            currentSpinPromptPresses++;
            RefreshSpinUi();

            if (spinPresses >= requiredSpinPresses)
            {
                CompleteSpinPhase();
            }
        }

        private void HandleKeyboardInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (!enableKeyboardInput)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (WasKeyPressed(keyboard, greenLaneKey))
            {
                HandleLanePressed(0);
            }

            if (WasKeyPressed(keyboard, blueLaneKey))
            {
                HandleLanePressed(1);
            }

            if (WasKeyPressed(keyboard, blackLaneKey))
            {
                HandleLanePressed(2);
            }

            if (WasKeyPressed(keyboard, whiteLaneKey))
            {
                HandleLanePressed(3);
            }
#endif
        }

        private void HandleSpinInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (!enableKeyboardInput)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (!spinPhaseActive)
            {
                return;
            }

            if (keyboard != null && WasKeyPressed(keyboard, spinKey))
            {
                HandleSpinTick();
            }
#endif
        }

        private void UpdateSpawning(float deltaTime)
        {
            if (totalNotesToSpawn > 0 && spawnedNotes >= totalNotesToSpawn)
            {
                return;
            }

            spawnTimer -= deltaTime;
            while (spawnTimer <= 0f)
            {
                int laneIndex = GetNextLaneIndex();
                if (laneIndex >= 0 && TrySpawnNote(laneIndex))
                {
                    spawnedNotes++;
                }

                spawnTimer += spawnIntervalSeconds;

                if (totalNotesToSpawn > 0 && spawnedNotes >= totalNotesToSpawn)
                {
                    break;
                }
            }
        }

        private void UpdateSongSpawning()
        {
            if (!ShouldUseSongSync() || totalNotesToSpawn <= 0 || spawnedNotes >= totalNotesToSpawn)
            {
                return;
            }

            float songTime = GetSongTime();
            while (nextSongNoteIndex < totalNotesToSpawn)
            {
                int laneIndex = GetSongLaneIndex(nextSongNoteIndex);
                if (laneIndex < 0)
                {
                    return;
                }

                float targetTime = GetSongTargetTime(nextSongNoteIndex);
                float spawnTime = targetTime - GetNoteTravelTime(laneIndex);
                if (songTime > spawnTime + maxSongSpawnLateSeconds)
                {
                    nextSongNoteIndex++;
                    continue;
                }

                if (songTime < spawnTime)
                {
                    return;
                }

                if (TrySpawnNote(laneIndex))
                {
                    spawnedNotes++;
                }

                nextSongNoteIndex++;
                return;
            }
        }

        private void UpdateActiveNotes(float deltaTime)
        {
            for (int index = activeNotes.Count - 1; index >= 0; index--)
            {
                ActiveNote note = activeNotes[index];
                if (note.RectTransform == null)
                {
                    activeNotes.RemoveAt(index);
                    continue;
                }

                Vector2 position = note.RectTransform.anchoredPosition;
                position += note.MoveDirection * noteSpeedPixelsPerSecond * deltaTime;
                note.RectTransform.anchoredPosition = position;

                float pastTargetDistance = Vector2.Dot(position - note.TargetPosition, note.MoveDirection);
                if (pastTargetDistance > autoMissWindowPixels)
                {
                    ApplyJudgement(note, RhythmHitRating.Miss, GetDistanceFromTarget(note));
                    continue;
                }

                if (pastTargetDistance > despawnPastTargetPixels)
                {
                    DestroyNote(note);
                }
            }
        }

        private void CheckForCompletion()
        {
            if (loopWhenFinished && totalNotesToSpawn > 0 && AreAllNotesProcessed() && activeNotes.Count == 0)
            {
                spawnedNotes = 0;
                nextSongNoteIndex = 0;
                spawnTimer = spawnIntervalSeconds;
                return;
            }

            if (totalNotesToSpawn > 0 && AreAllNotesProcessed() && activeNotes.Count == 0)
            {
                if (!spinPhaseActive && !ShouldHoldSpawningForSpin() && !ShouldWaitForSongAudio())
                {
                    CompleteGame();
                }
            }
        }

        private bool TryBeginSpinPromptIfReady()
        {
            if (!ShouldHoldSpawningForSpin() || activeNotes.Count > 0)
            {
                return false;
            }

            nextSpinPromptAtNote += Mathf.Max(1, spinEveryNotes);
            BeginSpinPrompt();
            return true;
        }

        private bool ShouldHoldSpawningForSpin()
        {
            return enableSpinPhase &&
                   !spinPhaseActive &&
                   nextSpinPromptAtNote > 0 &&
                   spawnedNotes >= nextSpinPromptAtNote;
        }

        private bool AreAllNotesProcessed()
        {
            if (ShouldUseSongSync())
            {
                return nextSongNoteIndex >= totalNotesToSpawn;
            }

            return spawnedNotes >= totalNotesToSpawn;
        }

        private void BeginSpinPrompt()
        {
            spinPhaseActive = true;
            currentSpinPromptPresses = 0;
            spinPresses = 0;
            spinTimer = spinTimeLimitSeconds;
            CacheTargetBaseScale();
            SetSpinUiActive(true);
            RefreshSpinUi();
        }

        private void UpdateSpinPrompt(float deltaTime)
        {
            if (!spinPhaseActive)
            {
                return;
            }

            if (useSpinTimeLimit)
            {
                spinTimer -= deltaTime;
            }

            PulseTarget(deltaTime);

            if (useSpinTimeLimit && spinTimer <= 0f)
            {
                EndSpinPrompt(false);
            }
        }

        private void CompleteSpinPhase()
        {
            EndSpinPrompt(true);
        }

        private void EndSpinPrompt(bool completed)
        {
            spinPhaseActive = false;
            ResetTargetPulse();

            if (completed)
            {
                SetSpinUiActive(false);
                return;
            }

            if (completedSpinCount <= 0)
            {
                SetSpinUiActive(false);
            }
        }

        private void CompleteGame()
        {
            isPlaying = false;
            isComplete = true;
            StopSongPlayback();
            ShowFinishedScreenIfNeeded();

            if (hidePanelOnComplete)
            {
                PanelRoot.SetActive(false);
            }
        }

        private void ShowFinishedScreenIfNeeded()
        {
            if (!showFinishedScreenOnComplete)
            {
                return;
            }

            if (resultsScreen != null)
            {
                resultsScreen.SetResults(Accuracy, MaxCombo);
            }

            if (screenSwitcher != null && finishedScreen != null)
            {
                screenSwitcher.Show(finishedScreen);
            }
            else if (finishedScreen != null)
            {
                finishedScreen.SetActive(true);
            }
        }

        private bool TrySpawnNote(int laneIndex)
        {
            if (!CanSpawnLane(laneIndex) || NoteParent == null)
            {
                return false;
            }

            CanvasRhythmLane lane = lanes[laneIndex];
            GameObject noteObject = Instantiate(lane.NotePrefab, NoteParent);
            noteObject.name = lane.LaneName + " Note";

            RectTransform noteRect = noteObject.GetComponent<RectTransform>();
            if (noteRect == null)
            {
                noteRect = noteObject.AddComponent<RectTransform>();
            }

            if (overrideNoteSize)
            {
                noteRect.sizeDelta = noteSize;
            }

            EnsureNoteCanReceiveClicks(noteObject);

            Vector2 spawnPosition = GetSpawnPosition(laneIndex);
            Vector2 targetPosition = GetTargetPosition();
            Vector2 moveDirection = targetPosition - spawnPosition;
            if (moveDirection.sqrMagnitude <= 0.0001f)
            {
                moveDirection = Vector2.up;
            }
            else
            {
                moveDirection.Normalize();
            }

            noteRect.anchoredPosition = spawnPosition;

            int noteId = nextNoteId++;
            CanvasRhythmNote clickTarget = noteObject.GetComponent<CanvasRhythmNote>();
            if (clickTarget == null)
            {
                clickTarget = noteObject.AddComponent<CanvasRhythmNote>();
            }

            clickTarget.Initialize(this, noteId);

            activeNotes.Add(new ActiveNote
            {
                Id = noteId,
                LaneIndex = laneIndex,
                RectTransform = noteRect,
                TargetPosition = targetPosition,
                MoveDirection = moveDirection,
                Judged = false
            });

            return true;
        }

        private int GetNextLaneIndex()
        {
            if (lanes == null || lanes.Length == 0)
            {
                return -1;
            }

            if (randomizeLanes)
            {
                List<int> spawnableLanes = new List<int>();
                for (int index = 0; index < lanes.Length; index++)
                {
                    if (CanSpawnLane(index))
                    {
                        spawnableLanes.Add(index);
                    }
                }

                if (spawnableLanes.Count == 0)
                {
                    return -1;
                }

                return spawnableLanes[UnityEngine.Random.Range(0, spawnableLanes.Count)];
            }

            for (int attempts = 0; attempts < lanes.Length; attempts++)
            {
                int laneIndex = laneSequenceIndex % lanes.Length;
                laneSequenceIndex++;
                if (CanSpawnLane(laneIndex))
                {
                    return laneIndex;
                }
            }

            return -1;
        }

        private int GetSongLaneIndex(int noteIndex)
        {
            if (generatedSongChart != null &&
                noteIndex >= 0 &&
                noteIndex < generatedSongChart.Notes.Count)
            {
                return generatedSongChart.Notes[noteIndex].LaneIndex;
            }

            if (songDefinition == null || lanes == null)
            {
                return -1;
            }

            return songDefinition.GetLaneForNote(noteIndex, lanes.Length);
        }

        private float GetSongTargetTime(int noteIndex)
        {
            if (generatedSongChart != null &&
                noteIndex >= 0 &&
                noteIndex < generatedSongChart.Notes.Count)
            {
                return generatedSongChart.Notes[noteIndex].TargetTimeSeconds;
            }

            if (songDefinition == null)
            {
                return 0f;
            }

            float beat = songDefinition.StartBeat + (noteIndex * songDefinition.NoteBeatInterval);
            return songDefinition.FirstBeatOffsetSeconds + (beat * songDefinition.BeatDurationSeconds);
        }

        private float GetNoteTravelTime(int laneIndex)
        {
            if (!CanSpawnLane(laneIndex) || NoteParent == null)
            {
                return 0f;
            }

            float distance = Vector2.Distance(GetSpawnPosition(laneIndex), GetTargetPosition());
            return distance / Mathf.Max(1f, noteSpeedPixelsPerSecond);
        }

        private bool ShouldUseSongSync()
        {
            return generatedSongChart != null || (songDefinition != null && songDefinition.UseBeatSync);
        }

        private float GetSongTime()
        {
            if (audioSource != null && audioSource.clip != null)
            {
                return audioSource.time;
            }

            return songTimer;
        }

        private bool ShouldWaitForSongAudio()
        {
            return songDefinition != null &&
                   audioSource != null &&
                   audioSource.clip != null &&
                   audioSource.isPlaying;
        }

        private void ApplySongDefinition()
        {
            generatedSongChart = null;

            if (songDefinition == null)
            {
                return;
            }

            EnsureAudioSource();
            if (audioSource != null)
            {
                audioSource.clip = songDefinition.AudioClip;
                audioSource.playOnAwake = false;
            }

            if (songDefinition.AutoGenerateFromAudio &&
                RhythmSongAnalyzer.TryGenerateChart(songDefinition, GetLaneCount(), totalNotesToSpawn, out RhythmGeneratedChart chart))
            {
                generatedSongChart = chart;
                spawnIntervalSeconds = Mathf.Max(0.05f, chart.AverageNoteIntervalSeconds);
                totalNotesToSpawn = chart.Notes.Count;
                randomizeLanes = false;
                enableSpinPhase = songDefinition.EnableSpinPhase;
                firstSpinAfterNotes = chart.FirstSpinAfterNotes;
                spinEveryNotes = chart.SpinEveryNotes;
                requiredSpinPresses = chart.RequiredSpinPresses;
                useSpinTimeLimit = true;
                spinTimeLimitSeconds = chart.SpinTimeLimitSeconds;
                return;
            }

            ApplyManualSongFallback();
        }

        private void ApplyManualSongFallback()
        {
            spawnIntervalSeconds = Mathf.Max(0.05f, songDefinition.NoteIntervalSeconds);
            totalNotesToSpawn = songDefinition.GetResolvedNoteCount(totalNotesToSpawn);
            randomizeLanes = songDefinition.RandomizeLanesWhenPatternEmpty;
            enableSpinPhase = songDefinition.EnableSpinPhase;
            firstSpinAfterNotes = songDefinition.FirstSpinAfterNotes;
            spinEveryNotes = songDefinition.SpinEveryNotes;
            requiredSpinPresses = songDefinition.RequiredSpinPresses;
            useSpinTimeLimit = songDefinition.UseSpinTimeLimit;
            spinTimeLimitSeconds = songDefinition.SpinTimeLimitSeconds;
        }

        private int GetLaneCount()
        {
            return lanes != null ? lanes.Length : 0;
        }

        private void EnsureAudioSource()
        {
            if (audioSource != null)
            {
                return;
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        private void StartSongPlaybackIfNeeded()
        {
            if (songPlaybackStarted)
            {
                return;
            }

            songPlaybackStarted = true;
            songTimer = 0f;

            if (songDefinition == null)
            {
                return;
            }

            EnsureAudioSource();
            if (audioSource == null || songDefinition.AudioClip == null)
            {
                return;
            }

            audioSource.clip = songDefinition.AudioClip;
            audioSource.time = 0f;
            audioSource.Play();
        }

        private void StopSongPlayback()
        {
            if (!stopAudioOnComplete || audioSource == null || !audioSource.isPlaying)
            {
                return;
            }

            audioSource.Stop();
        }

        private bool CanSpawnLane(int laneIndex)
        {
            return lanes != null &&
                   laneIndex >= 0 &&
                   laneIndex < lanes.Length &&
                   lanes[laneIndex] != null &&
                   lanes[laneIndex].NotePrefab != null;
        }

        private Vector2 GetSpawnPosition(int laneIndex)
        {
            RectTransform parent = NoteParent;
            float x = GetLaneX(laneIndex);
            float y = parent.rect.yMin + bottomSpawnOffsetPixels;
            return new Vector2(x, y);
        }

        private Vector2 GetTargetPosition()
        {
            if (targetNote != null)
            {
                return WorldToNoteParentPosition(targetNote.position);
            }

            RectTransform parent = NoteParent;
            return new Vector2(parent.rect.center.x, parent.rect.yMax - defaultTargetOffsetFromTopPixels);
        }

        private float GetLaneX(int laneIndex)
        {
            RectTransform parent = NoteParent;
            int laneCount = Mathf.Max(1, lanes.Length);
            if (laneCount == 1)
            {
                return GetLaneGroupCenterX();
            }

            float availableWidth = parent.rect.width * autoLaneWidthPercent;
            float spacing = laneSpacingPixels > 0f ? laneSpacingPixels : availableWidth / (laneCount - 1);
            spacing = Mathf.Min(spacing, parent.rect.width / laneCount);
            float centerX = GetLaneGroupCenterX();
            float leftX = centerX - (spacing * (laneCount - 1) * 0.5f);
            float x = leftX + (spacing * laneIndex);

            return Mathf.Clamp(x, parent.rect.xMin, parent.rect.xMax);
        }

        private float GetLaneGroupCenterX()
        {
            RectTransform parent = NoteParent;
            if (centerLanesOnTarget && targetNote != null)
            {
                return WorldToNoteParentPosition(targetNote.position).x;
            }

            return parent.rect.center.x;
        }

        private Vector2 WorldToNoteParentPosition(Vector3 worldPosition)
        {
            RectTransform parent = NoteParent;
            if (parent == null)
            {
                return Vector2.zero;
            }

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, uiCamera, out Vector2 localPoint);
            return localPoint;
        }

        private RhythmHitRating GetRating(float distancePixels)
        {
            if (distancePixels <= perfectWindowPixels)
            {
                return RhythmHitRating.Perfect;
            }

            if (distancePixels <= goodWindowPixels)
            {
                return RhythmHitRating.Good;
            }

            if (distancePixels <= badWindowPixels)
            {
                return RhythmHitRating.Bad;
            }

            return RhythmHitRating.Miss;
        }

        private void ApplyJudgement(ActiveNote note, RhythmHitRating rating, float distancePixels)
        {
            note.Judged = true;

            switch (rating)
            {
                case RhythmHitRating.Perfect:
                    Score += perfectScore;
                    Combo++;
                    PerfectHits++;
                    break;
                case RhythmHitRating.Good:
                    Score += goodScore;
                    Combo++;
                    GoodHits++;
                    break;
                case RhythmHitRating.Bad:
                    Score += badScore;
                    Combo++;
                    BadHits++;
                    break;
                default:
                    RegisterMiss(missLabel, distancePixels, true);
                    DestroyNote(note);
                    return;
            }

            MaxCombo = Mathf.Max(MaxCombo, Combo);
            SetFeedback(GetHitLabel(rating), distancePixels);
            DestroyNote(note);
            RefreshUi();
        }

        private void RegisterMiss(string label, float distancePixels, bool refreshUi)
        {
            Combo = 0;
            Misses++;
            SetFeedback(label, distancePixels);

            if (refreshUi)
            {
                RefreshUi();
            }
        }

        private void SetFeedback(string label, float distancePixels)
        {
            if (feedbackText != null)
            {
                feedbackText.text = label + " (" + distancePixels.ToString("0") + "px)";
            }

            if (noteHitText != null)
            {
                noteHitText.text = label;
            }

            if (logHitsToConsole)
            {
                Debug.Log(label + " at " + distancePixels.ToString("0.0") + "px", this);
            }
        }

        private void ClearFeedback()
        {
            if (feedbackText != null)
            {
                feedbackText.text = string.Empty;
            }

            if (noteHitText != null)
            {
                noteHitText.text = string.Empty;
            }
        }

        private void RefreshUi()
        {
            if (scoreText != null)
            {
                scoreText.text = Score.ToString();
            }

            if (comboText != null)
            {
                comboText.text = Combo.ToString();
            }

            if (accuracyText != null)
            {
                accuracyText.text = string.Format(accuracyFormat, (Accuracy * 100f).ToString("0"));
            }
        }

        private void RefreshSpinUi()
        {
            if (spinText != null)
            {
                spinText.text = currentSpinPromptPresses > 0
                    ? string.Format(completedSpinFormat, currentSpinPromptPresses)
                    : spinLabel;
            }
        }

        private ActiveNote FindActiveNote(int noteId)
        {
            for (int index = 0; index < activeNotes.Count; index++)
            {
                if (activeNotes[index].Id == noteId)
                {
                    return activeNotes[index];
                }
            }

            return null;
        }

        private ActiveNote FindClosestPendingNoteInLane(int laneIndex)
        {
            ActiveNote closestNote = null;
            float closestDistance = float.MaxValue;

            for (int index = 0; index < activeNotes.Count; index++)
            {
                ActiveNote note = activeNotes[index];
                if (note == null ||
                    note.Judged ||
                    note.LaneIndex != laneIndex ||
                    note.RectTransform == null)
                {
                    continue;
                }

                float distance = GetDistanceFromTarget(note);
                if (distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                closestNote = note;
            }

            return closestNote;
        }

        private void DestroyNote(ActiveNote note)
        {
            activeNotes.Remove(note);

            if (note.RectTransform != null)
            {
                Destroy(note.RectTransform.gameObject);
            }
        }

        private void ClearActiveNotes()
        {
            for (int index = activeNotes.Count - 1; index >= 0; index--)
            {
                ActiveNote note = activeNotes[index];
                if (note.RectTransform != null)
                {
                    Destroy(note.RectTransform.gameObject);
                }
            }

            activeNotes.Clear();
        }

        private void EnsureNoteCanReceiveClicks(GameObject noteObject)
        {
            Graphic graphic = noteObject.GetComponent<Graphic>();
            if (graphic == null)
            {
                graphic = noteObject.GetComponentInChildren<Graphic>();
            }

            if (graphic != null)
            {
                graphic.raycastTarget = true;
            }
        }

        private void CacheCanvas()
        {
            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = rootCanvas.worldCamera;
            }
            else
            {
                uiCamera = null;
            }
        }

        private void EnsureEventSystemExists()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            Debug.LogWarning("CanvasRhythmGame needs an EventSystem in the scene for note clicks.", this);
        }

        private void ValidateConfig()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (playArea == null)
            {
                playArea = transform as RectTransform;
            }

            if (noteParent == null)
            {
                noteParent = playArea;
            }

            spawnIntervalSeconds = Mathf.Max(0.05f, spawnIntervalSeconds);
            noteSpeedPixelsPerSecond = Mathf.Max(1f, noteSpeedPixelsPerSecond);
            totalNotesToSpawn = Mathf.Max(0, totalNotesToSpawn);
            maxSongSpawnLateSeconds = Mathf.Max(0f, maxSongSpawnLateSeconds);
            laneSpacingPixels = Mathf.Max(0f, laneSpacingPixels);
            autoLaneWidthPercent = Mathf.Clamp(autoLaneWidthPercent, 0.1f, 1f);
            noteSize.x = Mathf.Max(1f, noteSize.x);
            noteSize.y = Mathf.Max(1f, noteSize.y);

            perfectWindowPixels = Mathf.Max(0f, perfectWindowPixels);
            goodWindowPixels = Mathf.Max(perfectWindowPixels, goodWindowPixels);
            badWindowPixels = Mathf.Max(goodWindowPixels, badWindowPixels);
            autoMissWindowPixels = Mathf.Max(badWindowPixels, autoMissWindowPixels);
            despawnPastTargetPixels = Mathf.Max(autoMissWindowPixels, despawnPastTargetPixels);

            if (string.IsNullOrEmpty(accuracyFormat))
            {
                accuracyFormat = "Accuracy: {0}%";
            }

            if (string.IsNullOrEmpty(spinLabel))
            {
                spinLabel = "SPIN!";
            }

            if (string.IsNullOrEmpty(completedSpinFormat))
            {
                completedSpinFormat = "SPIN x{0}";
            }

            requiredSpinPresses = Mathf.Max(1, requiredSpinPresses);
            firstSpinAfterNotes = Mathf.Max(1, firstSpinAfterNotes);
            spinEveryNotes = Mathf.Max(1, spinEveryNotes);
            spinTimeLimitSeconds = Mathf.Max(0.1f, spinTimeLimitSeconds);
            targetPulseScale = Mathf.Max(1f, targetPulseScale);
            targetPulseSpeed = Mathf.Max(0.1f, targetPulseSpeed);
        }

        private void SetSpinUiActive(bool active)
        {
            if (spinText != null)
            {
                spinText.gameObject.SetActive(active);
                if (active)
                {
                    RefreshSpinUi();
                }
            }
        }

        private void CacheTargetBaseScale()
        {
            if (targetNote == null)
            {
                return;
            }

            targetBaseScale = targetNote.localScale;
            hasTargetBaseScale = true;
        }

        private void PulseTarget(float deltaTime)
        {
            if (targetNote == null)
            {
                return;
            }

            if (!hasTargetBaseScale)
            {
                CacheTargetBaseScale();
            }

            float pulse = 1f + ((targetPulseScale - 1f) * ((Mathf.Sin(Time.time * targetPulseSpeed) + 1f) * 0.5f));
            targetNote.localScale = targetBaseScale * pulse;
        }

        private void ResetTargetPulse()
        {
            if (targetNote != null && hasTargetBaseScale)
            {
                targetNote.localScale = targetBaseScale;
            }
        }

        private static float GetDistanceFromTarget(ActiveNote note)
        {
            if (note == null || note.RectTransform == null)
            {
                return float.MaxValue;
            }

            return Vector2.Distance(note.RectTransform.anchoredPosition, note.TargetPosition);
        }

        private string GetHitLabel(RhythmHitRating rating)
        {
            switch (rating)
            {
                case RhythmHitRating.Perfect:
                    return perfectLabel;
                case RhythmHitRating.Good:
                    return goodLabel;
                case RhythmHitRating.Bad:
                    return badLabel;
                default:
                    return missLabel;
            }
        }

        private void ApplyDefaultLaneNames()
        {
            if (lanes == null)
            {
                return;
            }

            string[] defaults = { "Green", "Blue", "Black", "White" };
            for (int index = 0; index < lanes.Length; index++)
            {
                if (lanes[index] != null)
                {
                    lanes[index].SetDefaultName(index < defaults.Length ? defaults[index] : "Lane " + (index + 1));
                }
            }
        }

        private GameObject PanelRoot => panelRoot != null ? panelRoot : gameObject;

#if ENABLE_INPUT_SYSTEM
        private static bool WasKeyPressed(Keyboard keyboard, Key key)
        {
            if (key == Key.None)
            {
                return false;
            }

            var keyControl = keyboard[key];
            return keyControl != null && keyControl.wasPressedThisFrame;
        }
#endif
    }

    public sealed class CanvasRhythmNote : MonoBehaviour, IPointerClickHandler
    {
        private CanvasRhythmGame rhythmGame;
        private int noteId;

        public void Initialize(CanvasRhythmGame game, int id)
        {
            rhythmGame = game;
            noteId = id;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (rhythmGame != null)
            {
                rhythmGame.HandleNoteClicked(noteId);
            }
        }
    }
}
