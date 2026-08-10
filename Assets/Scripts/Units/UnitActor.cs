using UnityEngine;
using VeilWar.Core;
using VeilWar.Fog;

namespace VeilWar.Units
{
    public sealed class UnitActor : MonoBehaviour
    {
        [SerializeField] Renderer bodyRenderer;

        public GridCoord Coord { get; private set; }
        public PlayerId Owner { get; private set; }
        public UnitKind Kind { get; private set; }
        public bool Alive { get; private set; } = true;
        public FogOfWarAgent FogAgent { get; private set; }

        public static UnitActor Spawn(
            GridCoord coord,
            PlayerId owner,
            UnitKind kind,
            GameConfig config,
            Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"{owner}_{kind}_{coord}";
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = config.CellToWorld(coord) + Vector3.up * 0.55f;
            go.transform.localScale = kind == UnitKind.Hq
                ? new Vector3(0.55f, 0.45f, 0.55f)
                : new Vector3(0.4f, 0.35f, 0.4f);

            var actor = go.AddComponent<UnitActor>();
            actor.Coord = coord;
            actor.Owner = owner;
            actor.Kind = kind;
            actor.bodyRenderer = go.GetComponent<Renderer>();
            actor.ApplyTeamColor(config);

            actor.FogAgent = go.AddComponent<FogOfWarAgent>();
            actor.FogAgent.Configure(
                friendly: owner == PlayerId.Local,
                agentMode: FogAgentMode.HideOrDesaturate);

            Object.Destroy(go.GetComponent<Collider>());
            return actor;
        }

        public void MoveTo(GridCoord coord, GameConfig config)
        {
            if (!Alive) return;
            Coord = coord;
            transform.position = config.CellToWorld(coord) + Vector3.up * 0.55f;
            FogAgent?.TeleportToCoord(coord);
        }

        public void Kill()
        {
            Alive = false;
            gameObject.SetActive(false);
        }

        void ApplyTeamColor(GameConfig config)
        {
            if (bodyRenderer == null) return;
            var block = new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(block);
            var color = Owner == PlayerId.Local
                ? config.AccentSignal
                : new Color(0.95f, 0.4f, 0.35f, 1f);
            if (Kind == UnitKind.Hq) color = Color.Lerp(color, Color.white, 0.25f);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            bodyRenderer.SetPropertyBlock(block);
        }
    }
}
