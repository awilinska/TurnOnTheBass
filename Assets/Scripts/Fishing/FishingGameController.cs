using UnityEngine;

namespace TurnOnTheBass
{
    [DisallowMultipleComponent]
    public sealed class FishingGameController : MonoBehaviour
    {
        private enum NoteShape
        {
            Rectangle = 0,
            Circle = 1
        }

        private static readonly string[] LaneLabels = { "A", "S", "D", "F" };
        private static readonly Color[] LaneColors =
        {
            new Color(0.96f, 0.41f, 0.33f, 0.95f),
            new Color(0.99f, 0.75f, 0.27f, 0.95f),
            new Color(0.32f, 0.82f, 0.61f, 0.95f),
            new Color(0.37f, 0.57f, 0.98f, 0.95f)
        };

        private enum FishingState
        {
            Exploring = 0,
            WaitingForBite = 1,
            Countdown = 2,
            Rhythm = 3,
            Result = 4
        }

        private struct CatchOutcome
        {
            public bool Caught;
            public string FishName;
            public string ZoneName;
            public float SizeKg;
            public string Quality;
            public float Accuracy;
            public int Score;
            public int MaxCombo;
        }

        [Header("Scene References")]
        [SerializeField] private TopDownPlayerController player;
        [SerializeField] private PlayerWaterDetector waterDetector;

        [Header("Startup")]
        [SerializeField] private bool autoFindPlayerIfMissing = true;
        [SerializeField] private bool useDefaultFishCatalog = true;

        [Header("Fishing Timing")]
        [SerializeField, Min(0.1f)] private float minBiteDelay = 0.45f;
        [SerializeField, Min(0.1f)] private float maxBiteDelay = 1.25f;
        [SerializeField, Min(1f)] private float rhythmStartCountdownSeconds = 3f;

        [Header("Rhythm Gameplay")]
        [SerializeField] private RhythmGameSettings rhythmSettings = new RhythmGameSettings();

        [Header("Rhythm UI")]
        [SerializeField, Range(0.5f, 1f)] private float boardWidthPercent = 1f;
        [SerializeField, Range(0.5f, 1f)] private float boardHeightPercent = 1f;
        [SerializeField, Range(0f, 0.2f)] private float boardPaddingPercent = 0.04f;
        [SerializeField] private Color rhythmBackgroundColor = new Color(0.02f, 0.03f, 0.07f, 0.92f);

        [Header("Diva Layout")]
        [SerializeField, Range(0.12f, 0.42f)] private float targetSpreadXPercent = 0.24f;
        [SerializeField, Range(0.1f, 0.38f)] private float targetSpreadYPercent = 0.2f;
        [SerializeField, Range(0.03f, 0.45f)] private float hitRadiusScale = 0.16f;
        [SerializeField, Range(0.04f, 0.4f)] private float centerCoreScale = 0.12f;
        [SerializeField, Range(0.4f, 1.2f)] private float hitTargetScale = 0.9f;
        [SerializeField] private Color centerCoreColor = new Color(0.95f, 0.95f, 1f, 0.9f);

        [Header("Diva Motion")]
        [SerializeField, Range(0f, 0.8f)] private float previewLeadPercent = 0.25f;
        [SerializeField, Range(4, 24)] private int maxVisibleNotes = 10;
        [SerializeField, Range(0.45f, 1.5f)] private float spawnDistanceScale = 1.05f;
        [SerializeField, Range(0.6f, 2.5f)] private float depthCurve = 1.25f;
        [SerializeField, Range(0.01f, 0.15f)] private float farNoteSizePercent = 0.022f;
        [SerializeField, Range(0.01f, 0.15f)] private float nearNoteSizePercent = 0.046f;

        [Header("Diva Notes")]
        [SerializeField] private NoteShape noteShape = NoteShape.Circle;
        [SerializeField, Range(1f, 6f)] private float noteWidthMultiplier = 2.2f;
        [SerializeField, Range(0.8f, 2f)] private float noteCircleDiameterScale = 1.1f;

        private FishCatalog fishCatalog;
        private AudioSource songSource;
        private readonly RhythmMinigame rhythmMinigame = new RhythmMinigame();

        private FishingState state = FishingState.Exploring;
        private WaterZone activeZone;
        private FishDefinition activeFish;
        private float biteTimer;
        private CatchOutcome lastOutcome;
        private string feedbackLabel = string.Empty;
        private float feedbackTimer;
        private float countdownTimer;

        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle centeredStyle;
        private GUIStyle largeCenteredStyle;
        private static Texture2D circleTexture;

        private void Awake()
        {
            if (autoFindPlayerIfMissing && player == null)
            {
                player = FindObjectOfType<TopDownPlayerController>();
            }

            if (waterDetector == null && player != null)
            {
                waterDetector = player.GetComponent<PlayerWaterDetector>();
            }

            if (useDefaultFishCatalog && fishCatalog == null)
            {
                fishCatalog = FishCatalog.CreateDefault();
            }

            ValidateConfig();
            EnsureMinimumChartLeadForVisualPreview();
            rhythmMinigame.Configure(rhythmSettings);
            EnsureAudioSource();
        }

        public void Initialize(TopDownPlayerController playerController, PlayerWaterDetector detector, FishCatalog catalog)
        {
            player = playerController;
            waterDetector = detector;
            fishCatalog = catalog;
            ValidateConfig();
            EnsureMinimumChartLeadForVisualPreview();
            rhythmMinigame.Configure(rhythmSettings);
            EnsureAudioSource();
        }

        private void OnValidate()
        {
            ValidateConfig();
            EnsureMinimumChartLeadForVisualPreview();
            rhythmMinigame.Configure(rhythmSettings);
        }

        private void EnsureAudioSource()
        {
            if (songSource == null)
            {
                songSource = GetComponent<AudioSource>();
                if (songSource == null)
                {
                    songSource = gameObject.AddComponent<AudioSource>();
                }

                songSource.playOnAwake = false;
                songSource.loop = true;
            }
        }

        private void Update()
        {
            if (player == null || waterDetector == null || fishCatalog == null)
            {
                return;
            }

            if (feedbackTimer > 0f)
            {
                feedbackTimer -= Time.deltaTime;
                if (feedbackTimer <= 0f)
                {
                    feedbackLabel = string.Empty;
                }
            }

            switch (state)
            {
                case FishingState.Exploring:
                    HandleExploring();
                    break;
                case FishingState.WaitingForBite:
                    HandleWaitingForBite();
                    break;
                case FishingState.Countdown:
                    HandleCountdown();
                    break;
                case FishingState.Rhythm:
                    HandleRhythmGameplay();
                    break;
                case FishingState.Result:
                    HandleResultState();
                    break;
            }
        }

        private void OnGUI()
        {
            if (player == null || waterDetector == null)
            {
                return;
            }

            EnsureStyles();
            if (state == FishingState.Rhythm || state == FishingState.Countdown)
            {
                DrawRhythmBoard();
            }

            DrawStatusPanel();
        }

        private void HandleExploring()
        {
            if (!InputRouter.WasInteractPressed())
            {
                return;
            }

            if (!waterDetector.IsNearWater)
            {
                return;
            }

            BeginFishing(waterDetector.CurrentZone);
        }

        private void HandleWaitingForBite()
        {
            biteTimer -= Time.deltaTime;
            if (biteTimer > 0f)
            {
                return;
            }

            HookFishAndBeginRhythm();
        }

        private void HandleRhythmGameplay()
        {
            for (int lane = 0; lane < RhythmMinigame.LaneCount; lane++)
            {
                if (!InputRouter.WasLanePressed(lane))
                {
                    continue;
                }

                HitFeedback feedback = rhythmMinigame.TryHitLane(lane);
                if (feedback.ConsumedInput)
                {
                    feedbackLabel = feedback.Label;
                    feedbackTimer = 0.4f;
                }
            }

            rhythmMinigame.Tick(Time.deltaTime);
            if (rhythmMinigame.IsComplete)
            {
                ResolveRhythmResult();
            }
        }

        private void HandleCountdown()
        {
            countdownTimer -= Time.deltaTime;
            if (countdownTimer > 0f)
            {
                return;
            }

            StartRhythmGameplay();
        }

        private void HandleResultState()
        {
            if (!InputRouter.WasConfirmPressed())
            {
                return;
            }

            activeFish = null;
            activeZone = null;
            state = FishingState.Exploring;
            player.MovementEnabled = true;
        }

        private void BeginFishing(WaterZone zone)
        {
            if (zone == null)
            {
                return;
            }

            activeZone = zone;
            biteTimer = Random.Range(minBiteDelay, maxBiteDelay);
            state = FishingState.WaitingForBite;
        }

        private void HookFishAndBeginRhythm()
        {
            activeFish = fishCatalog.GetRandomFish(activeZone.WaterType);
            if (activeFish == null)
            {
                state = FishingState.Exploring;
                return;
            }

            player.MovementEnabled = false;
            feedbackLabel = string.Empty;
            feedbackTimer = 0f;
            countdownTimer = rhythmStartCountdownSeconds;
            state = FishingState.Countdown;
        }

        private void StartRhythmGameplay()
        {
            rhythmMinigame.Begin(activeFish);
            TryPlayFishSong(activeFish);
            state = FishingState.Rhythm;
        }

        private void ResolveRhythmResult()
        {
            StopSong();

            RhythmResult rhythmResult = rhythmMinigame.GetResult();
            if (rhythmResult.Success)
            {
                float adjustedAccuracy = Mathf.Clamp01(rhythmResult.Accuracy + activeFish.QualityBias);
                float sizeMultiplier = Mathf.Lerp(0.75f, 1.65f, adjustedAccuracy);
                float finalSize = activeFish.BaseSizeKg * sizeMultiplier * Random.Range(0.9f, 1.1f);

                lastOutcome = new CatchOutcome
                {
                    Caught = true,
                    FishName = activeFish.DisplayName,
                    ZoneName = activeZone.ZoneName,
                    SizeKg = finalSize,
                    Quality = EvaluateQuality(adjustedAccuracy),
                    Accuracy = rhythmResult.Accuracy,
                    Score = rhythmResult.Score,
                    MaxCombo = rhythmResult.MaxCombo
                };
            }
            else
            {
                lastOutcome = new CatchOutcome
                {
                    Caught = false,
                    FishName = activeFish.DisplayName,
                    ZoneName = activeZone.ZoneName,
                    SizeKg = 0f,
                    Quality = "Escaped",
                    Accuracy = rhythmResult.Accuracy,
                    Score = rhythmResult.Score,
                    MaxCombo = rhythmResult.MaxCombo
                };
            }

            state = FishingState.Result;
        }

        private void TryPlayFishSong(FishDefinition fish)
        {
            AudioClip clip = fish.LoadSongClip();
            if (clip == null || songSource == null)
            {
                return;
            }

            songSource.clip = clip;
            songSource.Play();
        }

        private void StopSong()
        {
            if (songSource == null)
            {
                return;
            }

            songSource.Stop();
            songSource.clip = null;
        }

        private static string EvaluateQuality(float accuracy)
        {
            if (accuracy >= 0.95f)
            {
                return "Legendary";
            }

            if (accuracy >= 0.85f)
            {
                return "Premium";
            }

            if (accuracy >= 0.72f)
            {
                return "Fine";
            }

            if (accuracy >= 0.6f)
            {
                return "Standard";
            }

            return "Rough";
        }

        private void DrawStatusPanel()
        {
            Rect panel = new Rect(12f, 12f, 460f, 230f);
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label("Turn On The Bass - Rhythm Fishing Prototype", titleStyle);
            GUILayout.Label("Move: WASD    Start Fishing: E/F    Minigame Lanes: A/S/D/F", bodyStyle);

            switch (state)
            {
                case FishingState.Exploring:
                    DrawExploringStatus();
                    break;
                case FishingState.WaitingForBite:
                    DrawWaitingStatus();
                    break;
                case FishingState.Rhythm:
                    DrawRhythmStatus();
                    break;
                case FishingState.Countdown:
                    DrawCountdownStatus();
                    break;
                case FishingState.Result:
                    DrawResultStatus();
                    break;
            }

            GUILayout.EndArea();
        }

        private void DrawExploringStatus()
        {
            WaterZone zone = waterDetector.CurrentZone;
            if (zone == null)
            {
                GUILayout.Label("Walk near water to fish. Ocean, lake, and river each have unique fish.", bodyStyle);
                return;
            }

            GUILayout.Label("Near: " + zone.ZoneName + " (" + zone.WaterType + ")", bodyStyle);
            GUILayout.Label("Press E or F to cast your line.", bodyStyle);
        }

        private void DrawWaitingStatus()
        {
            GUILayout.Label("Casting in " + activeZone.ZoneName + "...", bodyStyle);
            GUILayout.Label("Waiting for a bite: " + biteTimer.ToString("0.0") + "s", bodyStyle);
        }

        private void DrawRhythmStatus()
        {
            GUILayout.Label("Hooked: " + activeFish.DisplayName, bodyStyle);
            GUILayout.Label(
                "Score " + rhythmMinigame.Score +
                " | Combo " + rhythmMinigame.Combo +
                " | Accuracy " + (rhythmMinigame.Accuracy * 100f).ToString("0.0") + "%", bodyStyle);
            GUILayout.Label(
                "Required Accuracy: " + (rhythmMinigame.RequiredAccuracy * 100f).ToString("0") + "%", bodyStyle);
        }

        private void DrawCountdownStatus()
        {
            int countdownValue = Mathf.Clamp(Mathf.CeilToInt(countdownTimer), 1, 99);
            GUILayout.Label("Hooked: " + activeFish.DisplayName, bodyStyle);
            GUILayout.Label("Get ready... " + countdownValue, bodyStyle);
            GUILayout.Label("Minigame starts in " + countdownValue, bodyStyle);
        }

        private void DrawResultStatus()
        {
            if (lastOutcome.Caught)
            {
                GUILayout.Label(
                    "Caught " + lastOutcome.FishName + " from " + lastOutcome.ZoneName + "!",
                    bodyStyle);
                GUILayout.Label(
                    "Quality: " + lastOutcome.Quality + " | Size: " + lastOutcome.SizeKg.ToString("0.00") + " kg",
                    bodyStyle);
            }
            else
            {
                GUILayout.Label(
                    lastOutcome.FishName + " escaped in " + lastOutcome.ZoneName + ".",
                    bodyStyle);
            }

            GUILayout.Label(
                "Accuracy " + (lastOutcome.Accuracy * 100f).ToString("0.0") +
                "% | Score " + lastOutcome.Score +
                " | Max Combo " + lastOutcome.MaxCombo,
                bodyStyle);
            GUILayout.Label("Press Space or Enter to continue.", bodyStyle);
        }

        private void DrawRhythmBoard()
        {
            float boardWidth = Screen.width * boardWidthPercent;
            float boardHeight = Screen.height * boardHeightPercent;
            float boardX = (Screen.width - boardWidth) * 0.5f;
            float boardY = (Screen.height - boardHeight) * 0.5f;
            Rect boardRect = new Rect(boardX, boardY, boardWidth, boardHeight);

            DrawRect(boardRect, rhythmBackgroundColor);
            GUI.Box(boardRect, string.Empty);

            Vector2 center = new Vector2(boardRect.center.x, boardRect.center.y);
            float boardRadius = Mathf.Min(boardRect.width, boardRect.height) * 0.5f * (1f - boardPaddingPercent);
            float centerRadius = boardRadius * centerCoreScale;

            if (state == FishingState.Rhythm)
            {
                int visibleNotesDrawn = 0;
                for (int index = 0; index < rhythmMinigame.Notes.Count; index++)
                {
                    RhythmNote note = rhythmMinigame.Notes[index];
                    if (note.Judgement != RhythmJudgement.Pending)
                    {
                        continue;
                    }

                    float spawnLeadTime = rhythmMinigame.NoteTravelTime * (1f + previewLeadPercent);
                    float spawnTime = note.HitTime - spawnLeadTime;
                    float visibleStartTime = Mathf.Max(0f, spawnTime);
                    if (rhythmMinigame.ElapsedTime < visibleStartTime)
                    {
                        continue;
                    }

                    float progress;
                    if (note.HitTime <= visibleStartTime + 0.0001f)
                    {
                        progress = 1f;
                    }
                    else
                    {
                        progress = Mathf.InverseLerp(visibleStartTime, note.HitTime, rhythmMinigame.ElapsedTime);
                    }

                    if (progress > 1.1f)
                    {
                        continue;
                    }

                    if (visibleNotesDrawn >= maxVisibleNotes)
                    {
                        continue;
                    }

                    visibleNotesDrawn++;

                    float depth = DepthMap(progress);
                    Vector2 noteTargetPoint = GetNoteTargetPoint(note, index, boardRect, center);
                    Vector2 noteSpawnPoint = GetNoteSpawnPoint(note, index, boardRect, noteTargetPoint, boardRadius);
                    Vector2 notePosition = Vector2.Lerp(noteSpawnPoint, noteTargetPoint, depth);
                    float baseSize = Mathf.Lerp(
                        boardRect.width * farNoteSizePercent,
                        boardRect.width * nearNoteSizePercent,
                        depth);
                    float hitTargetDiameter = Mathf.Max(8f, boardRect.width * nearNoteSizePercent * hitTargetScale);
                    Color hitTargetColor = LaneColors[note.Lane];
                    hitTargetColor.a = 0.82f;
                    float pulseScale = 1f + (0.08f * Mathf.Sin((note.HitTime - rhythmMinigame.ElapsedTime) * 16f));
                    DrawCircleAt(
                        noteTargetPoint,
                        hitTargetDiameter * 1.28f * pulseScale,
                        new Color(hitTargetColor.r, hitTargetColor.g, hitTargetColor.b, 0.2f));
                    DrawCircleAt(noteTargetPoint, hitTargetDiameter, hitTargetColor);
                    DrawCircleAt(noteTargetPoint, hitTargetDiameter * 0.66f, rhythmBackgroundColor);
                    DrawNoteSymbol(noteTargetPoint, hitTargetDiameter * 0.66f, LaneLabels[note.Lane]);

                    Rect noteRect = new Rect(
                        notePosition.x - ((baseSize * noteWidthMultiplier) * 0.5f),
                        notePosition.y - (baseSize * 0.5f),
                        baseSize * noteWidthMultiplier,
                        baseSize);

                    if (noteShape == NoteShape.Circle)
                    {
                        float diameter = Mathf.Max(4f, baseSize * noteCircleDiameterScale);
                        Rect circleRect = new Rect(
                            notePosition.x - (diameter * 0.5f),
                            notePosition.y - (diameter * 0.5f),
                            diameter,
                            diameter);
                        DrawCircle(circleRect, LaneColors[note.Lane]);
                        DrawNoteSymbol(notePosition, diameter, LaneLabels[note.Lane]);
                    }
                    else
                    {
                        DrawRect(noteRect, LaneColors[note.Lane]);
                        DrawNoteSymbol(notePosition, baseSize, LaneLabels[note.Lane]);
                    }
                }
            }

            DrawCircleAt(center, centerRadius * 1.8f, centerCoreColor);
            DrawCircleAt(center, centerRadius * 1.1f, rhythmBackgroundColor);

            if (!string.IsNullOrEmpty(feedbackLabel))
            {
                Rect feedbackRect = new Rect(boardRect.x, boardRect.y + 16f, boardRect.width, 40f);
                GUI.Label(feedbackRect, feedbackLabel, largeCenteredStyle);
            }

            if (state == FishingState.Countdown)
            {
                int countdownValue = Mathf.Clamp(Mathf.CeilToInt(countdownTimer), 1, 99);
                Rect countdownRect = new Rect(boardRect.x, boardRect.y + (boardRect.height * 0.42f), boardRect.width, 80f);
                GUI.Label(countdownRect, countdownValue.ToString(), largeCenteredStyle);
            }
        }

        private void ValidateConfig()
        {
            minBiteDelay = Mathf.Max(0.1f, minBiteDelay);
            maxBiteDelay = Mathf.Max(minBiteDelay, maxBiteDelay);
            rhythmStartCountdownSeconds = Mathf.Max(1f, rhythmStartCountdownSeconds);

            boardWidthPercent = Mathf.Clamp(boardWidthPercent, 0.5f, 1f);
            boardHeightPercent = Mathf.Clamp(boardHeightPercent, 0.5f, 1f);
            boardPaddingPercent = Mathf.Clamp(boardPaddingPercent, 0f, 0.2f);

            targetSpreadXPercent = Mathf.Clamp(targetSpreadXPercent, 0.12f, 0.42f);
            targetSpreadYPercent = Mathf.Clamp(targetSpreadYPercent, 0.1f, 0.38f);
            hitRadiusScale = Mathf.Clamp(hitRadiusScale, 0.05f, 0.8f);
            centerCoreScale = Mathf.Clamp(centerCoreScale, 0.04f, 0.4f);
            hitTargetScale = Mathf.Clamp(hitTargetScale, 0.4f, 1f);

            previewLeadPercent = Mathf.Clamp(previewLeadPercent, 0f, 0.8f);
            maxVisibleNotes = Mathf.Clamp(maxVisibleNotes, 4, 24);
            spawnDistanceScale = Mathf.Clamp(spawnDistanceScale, 0.45f, 1.5f);
            depthCurve = Mathf.Clamp(depthCurve, 0.6f, 2.5f);
            farNoteSizePercent = Mathf.Clamp(farNoteSizePercent, 0.01f, 0.15f);
            nearNoteSizePercent = Mathf.Clamp(nearNoteSizePercent, farNoteSizePercent, 0.15f);
            noteWidthMultiplier = Mathf.Clamp(noteWidthMultiplier, 1f, 6f);
            noteCircleDiameterScale = Mathf.Clamp(noteCircleDiameterScale, 0.8f, 2f);

            if (rhythmSettings != null)
            {
                rhythmSettings.Sanitize();
            }
        }

        private void EnsureMinimumChartLeadForVisualPreview()
        {
            if (rhythmSettings == null)
            {
                return;
            }

            // Prevent "first notes too fast": if notes should spawn before t=0, enforce more chart intro lead.
            float requiredLead = (rhythmSettings.NoteTravelTime * (1f + previewLeadPercent)) + 0.02f;
            rhythmSettings.EnsureMinimumIntroLead(requiredLead);
        }

        private Vector2 GetNoteTargetPoint(RhythmNote note, int noteIndex, Rect boardRect, Vector2 center)
        {
            Vector2 laneBaseOffset;
            switch (note.Lane)
            {
                case 0:
                    laneBaseOffset = new Vector2(-1f, -1f);
                    break;
                case 1:
                    laneBaseOffset = new Vector2(1f, -1f);
                    break;
                case 2:
                    laneBaseOffset = new Vector2(-1f, 1f);
                    break;
                default:
                    laneBaseOffset = new Vector2(1f, 1f);
                    break;
            }

            float spreadX = boardRect.width * targetSpreadXPercent;
            float spreadY = boardRect.height * targetSpreadYPercent;
            float jitterX = (Hash01((noteIndex * 17f) + (note.Lane * 9.1f) + note.HitTime) - 0.5f) * spreadX * 1.5f;
            float jitterY = (Hash01((noteIndex * 23f) + (note.Lane * 11.6f) + note.HitTime) - 0.5f) * spreadY * 1.5f;

            Vector2 target = center + new Vector2((laneBaseOffset.x * spreadX) + jitterX, (laneBaseOffset.y * spreadY) + jitterY);
            const float margin = 36f;
            target.x = Mathf.Clamp(target.x, boardRect.x + margin, boardRect.xMax - margin);
            target.y = Mathf.Clamp(target.y, boardRect.y + margin, boardRect.yMax - margin);
            return target;
        }

        private Vector2 GetNoteSpawnPoint(RhythmNote note, int noteIndex, Rect boardRect, Vector2 laneTarget, float boardRadius)
        {
            float angle = Hash01((noteIndex * 29f) + (note.Lane * 7.3f) + note.HitTime) * Mathf.PI * 2f;
            Vector2 randomDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float spawnDistance = boardRadius * spawnDistanceScale;
            Vector2 spawnPoint = laneTarget + (randomDirection * spawnDistance);

            const float margin = 24f;
            spawnPoint.x = Mathf.Clamp(spawnPoint.x, boardRect.x + margin, boardRect.xMax - margin);
            spawnPoint.y = Mathf.Clamp(spawnPoint.y, boardRect.y + margin, boardRect.yMax - margin);
            return spawnPoint;
        }

        private void DrawNoteSymbol(Vector2 position, float diameter, string label)
        {
            Rect labelRect = new Rect(
                position.x - (diameter * 0.5f),
                position.y - (diameter * 0.5f),
                diameter,
                diameter);
            GUI.Label(labelRect, label, centeredStyle);
        }

        private static float Hash01(float seed)
        {
            float hashed = Mathf.Sin(seed * 12.9898f) * 43758.547f;
            return hashed - Mathf.Floor(hashed);
        }

        private float DepthMap(float normalizedProgress)
        {
            return Mathf.Pow(Mathf.Clamp01(normalizedProgress), depthCurve);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };

            centeredStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            largeCenteredStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        private static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void DrawCircleAt(Vector2 center, float diameter, Color color)
        {
            Rect rect = new Rect(
                center.x - (diameter * 0.5f),
                center.y - (diameter * 0.5f),
                diameter,
                diameter);
            DrawCircle(rect, color);
        }

        private static void DrawCircle(Rect rect, Color color)
        {
            Texture2D texture = GetCircleTexture();
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture);
            GUI.color = previous;
        }

        private static Texture2D GetCircleTexture()
        {
            if (circleTexture != null)
            {
                return circleTexture;
            }

            const int size = 64;
            circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float radius = (size - 1) * 0.5f;
            Vector2 center = new Vector2(radius, radius);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = distance <= radius ? 1f : 0f;
                    circleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            circleTexture.Apply();
            return circleTexture;
        }
    }
}
