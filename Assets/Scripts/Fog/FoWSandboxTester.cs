using UnityEngine;
using VeilWar.Core;
using VeilWar.Fog;
using VeilWar.Network;

namespace VeilWar.Fog
{
    /// <summary>
    /// Editor/local sandbox: mocks Inco decrypt packets so you can exercise 3D FoW transitions
    /// without a chain. Hotkeys are Editor/Standalone oriented.
    /// </summary>
    public sealed class FoWSandboxTester : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] IncoNetworkBridge bridge;
        [SerializeField] FogOfWarManager fog;
        [SerializeField] FogOfWarAgent[] trackedEnemies;
        [SerializeField] bool autoSpawnEnemyProxies = true;
        [SerializeField] int enemyProxyCount = 2;

        [Header("Mock vision")]
        [SerializeField] float friendlyVisionRadius = 1.6f;
        [SerializeField] float enemyProbeRadius = 0f;
        [SerializeField] GridCoord friendlySensor = new(2, 2);

        [Header("Hotkeys")]
        [SerializeField] KeyCode revealAtSensorKey = KeyCode.Alpha1;
        [SerializeField] KeyCode moveSensorNorthKey = KeyCode.W;
        [SerializeField] KeyCode moveSensorSouthKey = KeyCode.S;
        [SerializeField] KeyCode moveSensorWestKey = KeyCode.A;
        [SerializeField] KeyCode moveSensorEastKey = KeyCode.D;
        [SerializeField] KeyCode clearVisionKey = KeyCode.C;
        [SerializeField] KeyCode teleportEnemyIntoVisionKey = KeyCode.E;
        [SerializeField] KeyCode teleportEnemyOutKey = KeyCode.Q;
        [SerializeField] KeyCode cycleEnemyKey = KeyCode.Tab;
        [SerializeField] KeyCode pulseRandomRevealKey = KeyCode.R;

        int _enemyIndex;
        bool _loggedHelp;

        void Awake()
        {
            if (bridge == null) bridge = IncoNetworkBridge.Instance ?? FindObjectOfType<IncoNetworkBridge>();
            if (fog == null) fog = FogOfWarManager.Instance ?? FindObjectOfType<FogOfWarManager>();
        }

        void Start()
        {
            if (autoSpawnEnemyProxies && (trackedEnemies == null || trackedEnemies.Length == 0))
                SpawnEnemyProxies();

            // Start shrouded, then push one friendly sensor so local vision exists.
            fog?.ClearVision(keepExplored: false);
            PushFriendlySensor(clearEphemeral: true);
            LogHelpOnce();
        }

        void Update()
        {
            if (bridge == null || fog == null) return;

            if (Pressed(moveSensorNorthKey)) MoveSensor(0, 1);
            if (Pressed(moveSensorSouthKey)) MoveSensor(0, -1);
            if (Pressed(moveSensorWestKey)) MoveSensor(-1, 0);
            if (Pressed(moveSensorEastKey)) MoveSensor(1, 0);

            if (Pressed(revealAtSensorKey)) PushFriendlySensor(clearEphemeral: true);
            if (Pressed(clearVisionKey))
            {
                fog.ClearVision(keepExplored: false);
                Debug.Log("[FoW Sandbox] Cleared all vision.");
            }

            if (Pressed(cycleEnemyKey))
            {
                if (trackedEnemies != null && trackedEnemies.Length > 0)
                {
                    _enemyIndex = (_enemyIndex + 1) % trackedEnemies.Length;
                    Debug.Log($"[FoW Sandbox] Selected enemy #{_enemyIndex}");
                }
            }

            if (Pressed(teleportEnemyIntoVisionKey)) TeleportSelectedEnemy(friendlySensor);
            if (Pressed(teleportEnemyOutKey)) TeleportSelectedEnemy(FindShroudedCell());
            if (Pressed(pulseRandomRevealKey)) PulseRandomEnemyMotion();
        }

        void MoveSensor(int dx, int dy)
        {
            var next = new GridCoord(
                Mathf.Clamp(friendlySensor.X + dx, 0, fog.Size - 1),
                Mathf.Clamp(friendlySensor.Y + dy, 0, fog.Size - 1));
            friendlySensor = next;
            PushFriendlySensor(clearEphemeral: true);
            Debug.Log($"[FoW Sandbox] Sensor → {friendlySensor}");
        }

        void PushFriendlySensor(bool clearEphemeral)
        {
            var packet = DecryptedCoordPacket.Create(
                "friendly-sensor",
                friendlySensor,
                friendlyVisionRadius,
                clearEphemeralFirst: clearEphemeral);
            bridge.PublishDecrypted(packet);
        }

        void TeleportSelectedEnemy(GridCoord dest)
        {
            var agent = SelectedEnemy();
            if (agent == null)
            {
                Debug.LogWarning("[FoW Sandbox] No enemy agents.");
                return;
            }

            agent.TeleportToCoord(dest);

            // Optional: mock an enemy self-reveal packet (usually enemies do NOT grant vision).
            if (enemyProbeRadius > 0f)
            {
                bridge.PublishDecrypted(
                    DecryptedCoordPacket.Create(agent.name, dest, enemyProbeRadius));
            }

            Debug.Log($"[FoW Sandbox] {agent.name} → {dest} visible={fog.IsVisible(dest)}");
        }

        void PulseRandomEnemyMotion()
        {
            if (trackedEnemies == null || trackedEnemies.Length == 0) return;
            var agent = trackedEnemies[Random.Range(0, trackedEnemies.Length)];
            if (agent == null) return;

            var dest = new GridCoord(Random.Range(0, fog.Size), Random.Range(0, fog.Size));
            agent.TeleportToCoord(dest);

            // Re-assert friendly vision so transitions read clearly when they step into the circle.
            PushFriendlySensor(clearEphemeral: true);
            Debug.Log($"[FoW Sandbox] Random move {agent.name} → {dest}");
        }

        GridCoord FindShroudedCell()
        {
            for (var y = 0; y < fog.Size; y++)
            for (var x = 0; x < fog.Size; x++)
            {
                var c = new GridCoord(x, y);
                if (!fog.IsVisible(c)) return c;
            }
            return new GridCoord(0, 0);
        }

        FogOfWarAgent SelectedEnemy()
        {
            if (trackedEnemies == null || trackedEnemies.Length == 0) return null;
            _enemyIndex = Mathf.Clamp(_enemyIndex, 0, trackedEnemies.Length - 1);
            return trackedEnemies[_enemyIndex];
        }

        void SpawnEnemyProxies()
        {
            trackedEnemies = new FogOfWarAgent[Mathf.Max(1, enemyProxyCount)];
            for (var i = 0; i < trackedEnemies.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = $"EnemyProxy_{i}";
                go.transform.SetParent(transform, false);
                var start = new GridCoord(fog != null ? fog.Size - 1 : 4, i);
                if (fog != null)
                    go.transform.position = fog.CoordToWorld(start) + Vector3.up * 0.55f;
                else
                    go.transform.position = new Vector3(start.X, 0.55f, start.Y);

                var agent = go.AddComponent<FogOfWarAgent>();
                agent.Configure(friendly: false, agentMode: FogAgentMode.HideOrDesaturate);
                trackedEnemies[i] = agent;
                Object.Destroy(go.GetComponent<Collider>());
            }
        }

        void LogHelpOnce()
        {
            if (_loggedHelp) return;
            _loggedHelp = true;
            Debug.Log(
                "[FoW Sandbox] Keys: WASD move sensor | 1 re-push vision | C clear | E enemy into vision | Q enemy out | Tab cycle | R random");
        }

        static bool Pressed(KeyCode key) => UnityEngine.Input.GetKeyDown(key);

        void OnGUI()
        {
            const int w = 420;
            GUI.Box(new Rect(12, 12, w, 118), "FoW Sandbox");
            GUI.Label(new Rect(24, 36, w - 24, 90),
                $"Sensor {friendlySensor}  r={friendlyVisionRadius:0.0}\n" +
                $"WASD move · 1 push · C clear\n" +
                $"E into vision · Q into shroud · Tab cycle · R random");
        }
    }
}
