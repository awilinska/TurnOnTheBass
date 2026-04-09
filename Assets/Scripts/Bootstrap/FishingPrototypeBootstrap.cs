using UnityEngine;

namespace TurnOnTheBass
{
    public sealed class FishingPrototypeBootstrap : MonoBehaviour
    {
        private static Sprite solidSprite;
        private static Texture2D solidTexture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (FindObjectOfType<FishingGameController>() != null)
            {
                return;
            }

            GameObject root = new GameObject("FishingPrototypeRuntime");
            root.AddComponent<FishingPrototypeBootstrap>();
        }

        private void Awake()
        {
            EnsureCamera();
            TopDownPlayerController player = EnsurePlayer();
            EnsureDemoWaterZones();

            FishingGameController controller = GetComponent<FishingGameController>();
            if (controller == null)
            {
                controller = gameObject.AddComponent<FishingGameController>();
            }

            PlayerWaterDetector detector = player.GetComponent<PlayerWaterDetector>();
            if (detector == null)
            {
                detector = player.gameObject.AddComponent<PlayerWaterDetector>();
            }

            controller.Initialize(player, detector, FishCatalog.CreateDefault());
        }

        private static void EnsureCamera()
        {
            Camera sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                sceneCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            }

            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = Mathf.Max(7f, sceneCamera.orthographicSize);
            sceneCamera.backgroundColor = new Color(0.2f, 0.34f, 0.42f, 1f);
        }

        private static TopDownPlayerController EnsurePlayer()
        {
            TopDownPlayerController existingPlayer = FindObjectOfType<TopDownPlayerController>();
            if (existingPlayer != null)
            {
                return existingPlayer;
            }

            GameObject playerObject = new GameObject("Player");
            playerObject.transform.position = new Vector3(0f, 0.3f, 0f);

            SpriteRenderer renderer = playerObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSolidSprite();
            renderer.color = new Color(0.95f, 0.9f, 0.56f, 1f);
            renderer.sortingOrder = 5;
            playerObject.transform.localScale = new Vector3(0.7f, 1f, 1f);

            CapsuleCollider2D collider = playerObject.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.85f, 1f);

            Rigidbody2D body = playerObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;

            playerObject.AddComponent<PlayerWaterDetector>();
            return playerObject.AddComponent<TopDownPlayerController>();
        }

        private static void EnsureDemoWaterZones()
        {
            if (FindObjectsOfType<WaterZone>().Length > 0)
            {
                return;
            }

            CreateBackdrop("Ground", new Vector2(0f, 0f), new Vector2(30f, 18f), new Color(0.23f, 0.48f, 0.24f, 1f), -10);

            CreateZone("Ocean Zone", WaterBodyType.Ocean, "Open Ocean", new Vector2(0f, -4f), new Vector2(30f, 5.5f), new Color(0.1f, 0.35f, 0.72f, 0.9f));
            CreateZone("Lake Zone", WaterBodyType.Lake, "Clear Lake", new Vector2(-7.5f, 2.6f), new Vector2(5.6f, 4f), new Color(0.2f, 0.53f, 0.84f, 0.9f));
            CreateZone("River Zone", WaterBodyType.River, "Rapid River", new Vector2(7.4f, 1.4f), new Vector2(3.2f, 8.4f), new Color(0.18f, 0.45f, 0.8f, 0.9f));
        }

        private static void CreateBackdrop(string objectName, Vector2 position, Vector2 size, Color color, int sortingOrder)
        {
            GameObject backdrop = new GameObject(objectName);
            backdrop.transform.position = new Vector3(position.x, position.y, 0f);
            backdrop.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = backdrop.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSolidSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private static void CreateZone(string objectName, WaterBodyType type, string readableName, Vector2 position, Vector2 size, Color color)
        {
            GameObject zoneObject = new GameObject(objectName);
            zoneObject.transform.position = new Vector3(position.x, position.y, 0f);
            zoneObject.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = zoneObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSolidSprite();
            renderer.color = color;
            renderer.sortingOrder = -2;

            BoxCollider2D collider = zoneObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;

            WaterZone zone = zoneObject.AddComponent<WaterZone>();
            zone.Configure(type, readableName);
        }

        private static Sprite GetSolidSprite()
        {
            if (solidSprite != null)
            {
                return solidSprite;
            }

            solidTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            solidTexture.SetPixel(0, 0, Color.white);
            solidTexture.wrapMode = TextureWrapMode.Clamp;
            solidTexture.filterMode = FilterMode.Point;
            solidTexture.Apply();

            solidSprite = Sprite.Create(solidTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return solidSprite;
        }
    }
}
