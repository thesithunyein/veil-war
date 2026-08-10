using System;
using System.Collections.Generic;
using UnityEngine;
using VeilWar.Core;
using VeilWar.Fog;

namespace VeilWar.Network
{
    /// <summary>
    /// Wire format for a decrypted Inco / commit-reveal coordinate push.
    /// Sandbox + real bridge share this payload.
    /// </summary>
    [Serializable]
    public struct DecryptedCoordPacket
    {
        public string EntityId;
        public int X;
        public int Y;
        public float VisionRadiusCells;
        public bool ClearEphemeralFirst;
        public long TimestampMs;

        public GridCoord Coord => new(X, Y);

        public static DecryptedCoordPacket Create(
            string entityId,
            GridCoord coord,
            float visionRadiusCells = 1.35f,
            bool clearEphemeralFirst = false)
        {
            return new DecryptedCoordPacket
            {
                EntityId = entityId,
                X = coord.X,
                Y = coord.Y,
                VisionRadiusCells = visionRadiusCells,
                ClearEphemeralFirst = clearEphemeralFirst,
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }
    }

    /// <summary>
    /// Event-driven network façade. Real Inco Lightning / RPC adapters publish into
    /// <see cref="PublishDecrypted"/>; FoW listens and mutates <see cref="FogOfWarManager"/>.
    /// </summary>
    public sealed class IncoNetworkBridge : MonoBehaviour
    {
        public static IncoNetworkBridge Instance { get; private set; }

        [SerializeField] FogOfWarManager fogManager;
        [SerializeField] bool autoFindFog = true;
        [SerializeField] bool clearEphemeralBetweenBatch = false;

        readonly Dictionary<string, DecryptedCoordPacket> _lastByEntity = new();

        /// <summary>Raw packet ingress (before FoW apply).</summary>
        public event Action<DecryptedCoordPacket> DecryptedPacketReceived;

        /// <summary>After FogOfWarManager has consumed the packet.</summary>
        public event Action<DecryptedCoordPacket> FogRevealApplied;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ResolveFog();
            DecryptedPacketReceived += OnDecryptedForFog;
        }

        void OnDestroy()
        {
            DecryptedPacketReceived -= OnDecryptedForFog;
            if (Instance == this) Instance = null;
        }

        void ResolveFog()
        {
            if (fogManager == null && autoFindFog)
                    fogManager = FogOfWarManager.Instance != null
                    ? FogOfWarManager.Instance
                    : FindObjectOfType<FogOfWarManager>();
        }

        /// <summary>Primary entry for live Inco decrypt callbacks or sandbox mocks.</summary>
        public void PublishDecrypted(DecryptedCoordPacket packet)
        {
            if (string.IsNullOrWhiteSpace(packet.EntityId))
                packet.EntityId = $"anon-{packet.X}-{packet.Y}";

            _lastByEntity[packet.EntityId] = packet;
            DecryptedPacketReceived?.Invoke(packet);
        }

        public void PublishDecrypted(string entityId, GridCoord coord, float visionRadiusCells = 1.35f)
        {
            PublishDecrypted(DecryptedCoordPacket.Create(entityId, coord, visionRadiusCells));
        }

        public void PublishBatch(IReadOnlyList<DecryptedCoordPacket> packets, bool clearEphemeral = true)
        {
            ResolveFog();
            if (fogManager != null && (clearEphemeral || clearEphemeralBetweenBatch))
                fogManager.ClearEphemeralVision();

            for (var i = 0; i < packets.Count; i++)
                PublishDecrypted(packets[i]);
        }

        public bool TryGetLast(string entityId, out DecryptedCoordPacket packet) =>
            _lastByEntity.TryGetValue(entityId, out packet);

        void OnDecryptedForFog(DecryptedCoordPacket packet)
        {
            ResolveFog();
            if (fogManager == null)
            {
                Debug.LogWarning("[IncoBridge] No FogOfWarManager — dropped packet " + packet.EntityId);
                return;
            }

            if (packet.ClearEphemeralFirst)
                fogManager.ClearEphemeralVision();

            fogManager.ApplyDecryptedVision(
                packet.Coord,
                packet.VisionRadiusCells > 0f ? packet.VisionRadiusCells : 1.35f,
                packet.EntityId);

            FogRevealApplied?.Invoke(packet);
        }
    }
}
