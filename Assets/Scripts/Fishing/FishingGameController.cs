using UnityEngine;

namespace TurnOnTheBass
{
    [DisallowMultipleComponent]
    public sealed class FishingGameController : MonoBehaviour
    {
        private enum FishingState
        {
            Exploring = 0,
            WaitingForBite = 1,
            Minigame = 2,
            Result = 3
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
        [SerializeField] private CanvasRhythmGame rhythmMinigame;

        [Header("Startup")]
        [SerializeField] private bool autoFindPlayerIfMissing = true;
        [SerializeField] private bool autoFindMinigameIfMissing = true;
        [SerializeField] private bool useDefaultFishCatalog = true;

        [Header("Fishing Timing")]
        [SerializeField, Min(0.1f)] private float minBiteDelay = 0.45f;
        [SerializeField, Min(0.1f)] private float maxBiteDelay = 1.25f;

        [Header("Minigame Result")]
        [SerializeField, Range(0f, 1f)] private float requiredAccuracyToCatch = 0.45f;
        [SerializeField] private bool catchAutomaticallyWithoutMinigame = true;

        private FishCatalog fishCatalog;
        private FishingState state = FishingState.Exploring;
        private WaterZone activeZone;
        private FishDefinition activeFish;
        private float biteTimer;
        private CatchOutcome lastOutcome;

        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;

        private void Awake()
        {
            if (autoFindPlayerIfMissing && player == null)
            {
                player = FindAnyObjectByType<TopDownPlayerController>();
            }

            if (waterDetector == null && player != null)
            {
                waterDetector = player.GetComponent<PlayerWaterDetector>();
            }

            if (autoFindMinigameIfMissing && rhythmMinigame == null)
            {
                rhythmMinigame = FindRhythmMinigameIncludingInactive();
            }

            if (useDefaultFishCatalog && fishCatalog == null)
            {
                fishCatalog = FishCatalog.CreateDefault();
            }

            ValidateConfig();
        }

        public void Initialize(TopDownPlayerController playerController, PlayerWaterDetector detector, FishCatalog catalog)
        {
            player = playerController;
            waterDetector = detector;
            fishCatalog = catalog;
            ValidateConfig();
        }

        private void OnValidate()
        {
            ValidateConfig();
        }

        private void Update()
        {
            if (player == null || waterDetector == null || fishCatalog == null)
            {
                return;
            }

            switch (state)
            {
                case FishingState.Exploring:
                    HandleExploring();
                    break;
                case FishingState.WaitingForBite:
                    HandleWaitingForBite();
                    break;
                case FishingState.Minigame:
                    HandleMinigame();
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
            DrawStatusPanel();
        }

        private void HandleExploring()
        {
            if (!InputRouter.WasInteractPressed() || !waterDetector.IsNearWater)
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

            HookFishAndStartMinigame();
        }

        private void HandleMinigame()
        {
            if (rhythmMinigame == null)
            {
                ResolveCatch(catchAutomaticallyWithoutMinigame ? 1f : 0f, 0, 0);
                return;
            }

            if (!rhythmMinigame.IsComplete)
            {
                return;
            }

            ResolveCatch(rhythmMinigame.Accuracy, rhythmMinigame.Score, rhythmMinigame.MaxCombo);
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
            player.MovementEnabled = false;
            state = FishingState.WaitingForBite;
        }

        private void HookFishAndStartMinigame()
        {
            activeFish = fishCatalog.GetRandomFish(activeZone.WaterType);
            if (activeFish == null)
            {
                state = FishingState.Exploring;
                player.MovementEnabled = true;
                return;
            }

            if (rhythmMinigame == null)
            {
                ResolveCatch(catchAutomaticallyWithoutMinigame ? 1f : 0f, 0, 0);
                return;
            }

            rhythmMinigame.BeginGame();
            state = FishingState.Minigame;
        }

        private void ResolveCatch(float accuracy, int score, int maxCombo)
        {
            bool caught = accuracy >= requiredAccuracyToCatch;
            float qualityRoll = Mathf.Clamp01(accuracy);
            float sizeMultiplier = Mathf.Lerp(0.75f, 1.65f, qualityRoll);
            lastOutcome = new CatchOutcome
            {
                Caught = caught,
                FishName = activeFish.DisplayName,
                ZoneName = activeZone.ZoneName,
                SizeKg = caught ? activeFish.BaseSizeKg * sizeMultiplier * Random.Range(0.9f, 1.1f) : 0f,
                Quality = caught ? EvaluateQuality(qualityRoll) : "Escaped",
                Accuracy = accuracy,
                Score = score,
                MaxCombo = maxCombo
            };

            state = FishingState.Result;
        }

        private static string EvaluateQuality(float roll)
        {
            if (roll >= 0.95f)
            {
                return "Legendary";
            }

            if (roll >= 0.85f)
            {
                return "Premium";
            }

            if (roll >= 0.72f)
            {
                return "Fine";
            }

            if (roll >= 0.6f)
            {
                return "Standard";
            }

            return "Rough";
        }

        private void DrawStatusPanel()
        {
            Rect panel = new Rect(12f, 12f, 460f, 180f);
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label("Turn On The Bass - Fishing Prototype", titleStyle);
            GUILayout.Label("Move: WASD    Start Fishing: E/F", bodyStyle);

            switch (state)
            {
                case FishingState.Exploring:
                    DrawExploringStatus();
                    break;
                case FishingState.WaitingForBite:
                    DrawWaitingStatus();
                    break;
                case FishingState.Minigame:
                    DrawMinigameStatus();
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

        private void DrawMinigameStatus()
        {
            GUILayout.Label("Hooked: " + activeFish.DisplayName, bodyStyle);

            if (rhythmMinigame == null)
            {
                GUILayout.Label("No rhythm minigame assigned.", bodyStyle);
                return;
            }

            GUILayout.Label(
                "Score " + rhythmMinigame.Score +
                " | Combo " + rhythmMinigame.Combo +
                " | Accuracy " + (rhythmMinigame.Accuracy * 100f).ToString("0") + "%",
                bodyStyle);
            GUILayout.Label("Click notes or press A/S/D/F when they reach the target.", bodyStyle);
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
                GUILayout.Label(lastOutcome.FishName + " escaped in " + lastOutcome.ZoneName + ".", bodyStyle);
            }

            GUILayout.Label(
                "Accuracy " + (lastOutcome.Accuracy * 100f).ToString("0") +
                "% | Score " + lastOutcome.Score +
                " | Max Combo " + lastOutcome.MaxCombo,
                bodyStyle);
            GUILayout.Label("Press Space or Enter to continue.", bodyStyle);
        }

        private void ValidateConfig()
        {
            minBiteDelay = Mathf.Max(0.1f, minBiteDelay);
            maxBiteDelay = Mathf.Max(minBiteDelay, maxBiteDelay);
            requiredAccuracyToCatch = Mathf.Clamp01(requiredAccuracyToCatch);
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
        }

        private static CanvasRhythmGame FindRhythmMinigameIncludingInactive()
        {
            CanvasRhythmGame[] minigames = Resources.FindObjectsOfTypeAll<CanvasRhythmGame>();
            for (int index = 0; index < minigames.Length; index++)
            {
                CanvasRhythmGame minigame = minigames[index];
                if (minigame != null && minigame.gameObject.scene.IsValid())
                {
                    return minigame;
                }
            }

            return null;
        }
    }
}
