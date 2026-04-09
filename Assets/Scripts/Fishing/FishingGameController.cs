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
        [SerializeField, Range(0f, 0.2f)] private float lanePaddingPercent = 0.08f;
        [SerializeField, Range(0f, 0.3f)] private float trackTopPercent = 0.1f;
        [SerializeField, Range(0.1f, 0.5f)] private float trackBottomPercent = 0.2f;
        [SerializeField, Range(0f, 0.25f)] private float hitLineFromBottomPercent = 0.1f;
        [SerializeField] private Color rhythmBackgroundColor = new Color(0.02f, 0.03f, 0.07f, 0.92f);

        [Header("Rhythm Perspective")]
        [SerializeField, Range(0.2f, 0.9f)] private float farWidthPercent = 0.38f;
        [SerializeField, Range(-0.3f, 0.3f)] private float horizonHorizontalOffsetPercent;
        [SerializeField, Range(0.6f, 2.5f)] private float depthCurve = 1.5f;
        [SerializeField, Range(40, 240)] private int laneSliceCount = 120;
        [SerializeField, Range(0f, 1f)] private float previewLeadPercent = 0.45f;
        [SerializeField, Range(0.02f, 0.22f)] private float noteHorizontalPaddingPercent = 0.12f;
        [SerializeField, Range(0.004f, 0.04f)] private float farNoteHeightPercent = 0.008f;
        [SerializeField, Range(0.01f, 0.08f)] private float nearNoteHeightPercent = 0.026f;
        [SerializeField, Range(1f, 5f)] private float noteWidthMultiplier = 5f;
        [SerializeField] private NoteShape noteShape = NoteShape.Rectangle;
        [SerializeField, Range(0.8f, 2f)] private float noteCircleDiameterScale = 1.2f;
        [SerializeField, Range(1f, 4f)] private float laneSeparatorThickness = 2f;

        private FishCatalog fishCatalog;
        private AudioSource songSource;
        private readonly RhythmMinigame rhythmMinigame = new RhythmMinigame();
        private readonly float[] nearBoundariesCache = new float[RhythmMinigame.LaneCount + 1];
        private readonly float[] farBoundariesCache = new float[RhythmMinigame.LaneCount + 1];

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
            rhythmMinigame.Configure(rhythmSettings);
            EnsureAudioSource();
        }

        public void Initialize(TopDownPlayerController playerController, PlayerWaterDetector detector, FishCatalog catalog)
        {
            player = playerController;
            waterDetector = detector;
            fishCatalog = catalog;
            ValidateConfig();
            rhythmMinigame.Configure(rhythmSettings);
            EnsureAudioSource();
        }

        private void OnValidate()
        {
            ValidateConfig();
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
            InputRouter.ClearExternalLanePresses();
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

            float lanePadding = boardRect.width * lanePaddingPercent;
            float nearLeft = boardRect.x + lanePadding;
            float nearRight = boardRect.xMax - lanePadding;
            float nearWidth = nearRight - nearLeft;
            float nearY = boardRect.yMax - (boardRect.height * trackBottomPercent);
            float farY = boardRect.y + (boardRect.height * trackTopPercent);
            float hitLineY = nearY - (boardRect.height * hitLineFromBottomPercent);

            float horizonCenterX = boardRect.center.x + (boardRect.width * horizonHorizontalOffsetPercent);
            float farWidth = nearWidth * farWidthPercent;
            float farLeft = horizonCenterX - (farWidth * 0.5f);
            float farRight = horizonCenterX + (farWidth * 0.5f);

            for (int boundary = 0; boundary <= RhythmMinigame.LaneCount; boundary++)
            {
                float boundaryRatio = boundary / (float)RhythmMinigame.LaneCount;
                nearBoundariesCache[boundary] = Mathf.Lerp(nearLeft, nearRight, boundaryRatio);
                farBoundariesCache[boundary] = Mathf.Lerp(farLeft, farRight, boundaryRatio);
            }

            for (int lane = 0; lane < RhythmMinigame.LaneCount; lane++)
            {
                Color laneShade = LaneColors[lane];
                laneShade.a = 0.22f;
                DrawLanePerspectiveFill(laneShade, lane, farY, hitLineY, farBoundariesCache, nearBoundariesCache);
            }

            DrawLaneBoundaries(farY, hitLineY, farBoundariesCache, nearBoundariesCache);

            for (int lane = 0; lane < RhythmMinigame.LaneCount; lane++)
            {
                float leftAtHit = Mathf.Lerp(farBoundariesCache[lane], nearBoundariesCache[lane], 1f);
                float rightAtHit = Mathf.Lerp(farBoundariesCache[lane + 1], nearBoundariesCache[lane + 1], 1f);
                Rect labelRect = new Rect(
                    leftAtHit,
                    nearY + (boardRect.height * 0.015f),
                    rightAtHit - leftAtHit,
                    34f);
                GUI.Label(labelRect, LaneLabels[lane], centeredStyle);
            }

            DrawRect(new Rect(nearLeft, hitLineY, nearWidth, 4f), Color.white);

            if (state == FishingState.Rhythm)
            {
                for (int index = 0; index < rhythmMinigame.Notes.Count; index++)
                {
                    RhythmNote note = rhythmMinigame.Notes[index];
                    if (note.Judgement != RhythmJudgement.Pending)
                    {
                        continue;
                    }

                    float spawnTime = note.HitTime - (rhythmMinigame.NoteTravelTime * (1f + previewLeadPercent));
                    float progress = Mathf.InverseLerp(spawnTime, note.HitTime, rhythmMinigame.ElapsedTime);
                    if (progress < 0f || progress > 1.1f)
                    {
                        continue;
                    }

                    float depth = DepthMap(progress);
                    float y = Mathf.Lerp(farY, hitLineY, depth);
                    float laneLeft = Mathf.Lerp(farBoundariesCache[note.Lane], nearBoundariesCache[note.Lane], depth);
                    float laneRight = Mathf.Lerp(farBoundariesCache[note.Lane + 1], nearBoundariesCache[note.Lane + 1], depth);
                    float laneWidth = laneRight - laneLeft;
                    float notePadding = laneWidth * noteHorizontalPaddingPercent;
                    float noteHeight = Mathf.Lerp(
                        boardRect.height * farNoteHeightPercent,
                        boardRect.height * nearNoteHeightPercent,
                        depth);
                    float unclampedWidth = (laneWidth - (notePadding * 2f)) * noteWidthMultiplier;
                    float noteWidth = Mathf.Clamp(unclampedWidth, 3f, laneWidth * 5f);
                    float centerX = (laneLeft + laneRight) * 0.5f;
                    Rect noteRect = new Rect(centerX - (noteWidth * 0.5f), y - (noteHeight * 0.5f), noteWidth, noteHeight);

                    if (noteShape == NoteShape.Circle)
                    {
                        float diameter = Mathf.Max(4f, noteHeight * noteCircleDiameterScale);
                        diameter = Mathf.Min(diameter, noteWidth);
                        Rect circleRect = new Rect(
                            centerX - (diameter * 0.5f),
                            y - (diameter * 0.5f),
                            diameter,
                            diameter);
                        DrawCircle(circleRect, LaneColors[note.Lane]);
                    }
                    else
                    {
                        DrawRect(noteRect, LaneColors[note.Lane]);
                    }
                }
            }

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
            lanePaddingPercent = Mathf.Clamp(lanePaddingPercent, 0f, 0.2f);
            trackTopPercent = Mathf.Clamp(trackTopPercent, 0f, 0.3f);
            trackBottomPercent = Mathf.Clamp(trackBottomPercent, 0.1f, 0.5f);
            hitLineFromBottomPercent = Mathf.Clamp(hitLineFromBottomPercent, 0f, 0.25f);
            farWidthPercent = Mathf.Clamp(farWidthPercent, 0.2f, 0.9f);
            horizonHorizontalOffsetPercent = Mathf.Clamp(horizonHorizontalOffsetPercent, -0.3f, 0.3f);
            depthCurve = Mathf.Clamp(depthCurve, 0.6f, 2.5f);
            laneSliceCount = Mathf.Clamp(laneSliceCount, 40, 240);
            previewLeadPercent = Mathf.Clamp01(previewLeadPercent);
            noteHorizontalPaddingPercent = Mathf.Clamp(noteHorizontalPaddingPercent, 0.02f, 0.22f);
            farNoteHeightPercent = Mathf.Clamp(farNoteHeightPercent, 0.004f, 0.04f);
            nearNoteHeightPercent = Mathf.Clamp(nearNoteHeightPercent, farNoteHeightPercent, 0.08f);
            noteWidthMultiplier = Mathf.Clamp(noteWidthMultiplier, 1f, 5f);
            noteCircleDiameterScale = Mathf.Clamp(noteCircleDiameterScale, 0.8f, 2f);
            laneSeparatorThickness = Mathf.Clamp(laneSeparatorThickness, 1f, 4f);

            if (rhythmSettings != null)
            {
                rhythmSettings.Sanitize();
            }
        }

        private void DrawLanePerspectiveFill(
            Color laneColor,
            int lane,
            float farY,
            float hitLineY,
            float[] farBoundaries,
            float[] nearBoundaries)
        {
            for (int slice = 0; slice < laneSliceCount; slice++)
            {
                float t0 = slice / (float)laneSliceCount;
                float t1 = (slice + 1) / (float)laneSliceCount;
                float d0 = DepthMap(t0);
                float d1 = DepthMap(t1);
                float dMid = (d0 + d1) * 0.5f;
                float y0 = Mathf.Lerp(farY, hitLineY, d0);
                float y1 = Mathf.Lerp(farY, hitLineY, d1);
                float yMid = Mathf.Lerp(farY, hitLineY, dMid);
                float stripHeight = Mathf.Max(1f, Mathf.Abs(y1 - y0) + 1f);

                float left = Mathf.Lerp(farBoundaries[lane], nearBoundaries[lane], dMid);
                float right = Mathf.Lerp(farBoundaries[lane + 1], nearBoundaries[lane + 1], dMid);
                DrawRect(new Rect(left, yMid - (stripHeight * 0.5f), right - left, stripHeight), laneColor);
            }
        }

        private void DrawLaneBoundaries(float farY, float hitLineY, float[] farBoundaries, float[] nearBoundaries)
        {
            Color separatorColor = new Color(1f, 1f, 1f, 0.22f);
            for (int boundary = 0; boundary <= RhythmMinigame.LaneCount; boundary++)
            {
                for (int slice = 0; slice < laneSliceCount; slice++)
                {
                    float t0 = slice / (float)laneSliceCount;
                    float t1 = (slice + 1) / (float)laneSliceCount;
                    float d0 = DepthMap(t0);
                    float d1 = DepthMap(t1);
                    float dMid = (d0 + d1) * 0.5f;
                    float y0 = Mathf.Lerp(farY, hitLineY, d0);
                    float y1 = Mathf.Lerp(farY, hitLineY, d1);
                    float stripHeight = Mathf.Max(1f, Mathf.Abs(y1 - y0) + 1f);
                    float x = Mathf.Lerp(farBoundaries[boundary], nearBoundaries[boundary], dMid);
                    DrawRect(
                        new Rect(x - (laneSeparatorThickness * 0.5f), Mathf.Min(y0, y1), laneSeparatorThickness, stripHeight),
                        separatorColor);
                }
            }
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
