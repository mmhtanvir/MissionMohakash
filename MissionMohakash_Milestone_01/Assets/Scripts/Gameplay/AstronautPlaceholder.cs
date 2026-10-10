using UnityEngine;

namespace Mohakash
{
    /// <summary>Handles to the pivots that the procedural walk cycle swings.</summary>
    public class AstronautRig
    {
        public Transform visual, legL, legR, armL, armR;
    }

    /// <summary>
    /// TEMPORARY PLACEHOLDER astronaut made from primitives (white suit, gold visor, backpack).
    /// Replaced by a rigged/animated model in Milestone 4. Character faces local +Z.
    /// </summary>
    public static class AstronautPlaceholder
    {
        public static AstronautRig Build(Transform root)
        {
            var rig = new AstronautRig();
            var suit = Placeholder.Mat(new Color(0.92f, 0.93f, 0.95f));
            var dark = Placeholder.Mat(new Color(0.25f, 0.27f, 0.32f));
            var visor = Placeholder.Mat(new Color(0.85f, 0.65f, 0.2f));
            var pack = Placeholder.Mat(new Color(0.7f, 0.72f, 0.76f));

            var v = new GameObject("Visual_PLACEHOLDER").transform;
            v.SetParent(root, false);
            rig.visual = v;

            Placeholder.Prim(PrimitiveType.Capsule, "Torso", v, new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 0.36f, 0.34f), suit, false);
            Placeholder.Prim(PrimitiveType.Sphere, "Helmet", v, new Vector3(0f, 1.68f, 0f), new Vector3(0.42f, 0.42f, 0.42f), suit, false);
            Placeholder.Prim(PrimitiveType.Sphere, "Visor", v, new Vector3(0f, 1.68f, 0.12f), new Vector3(0.34f, 0.26f, 0.22f), visor, false);
            Placeholder.Prim(PrimitiveType.Cube, "Backpack", v, new Vector3(0f, 1.2f, -0.27f), new Vector3(0.4f, 0.55f, 0.22f), pack, false);

            rig.legL = MakeLimb(v, "LegL", new Vector3(-0.15f, 0.85f, 0f), new Vector3(0.16f, 0.42f, 0.16f), -0.42f, dark);
            rig.legR = MakeLimb(v, "LegR", new Vector3(0.15f, 0.85f, 0f), new Vector3(0.16f, 0.42f, 0.16f), -0.42f, dark);
            rig.armL = MakeLimb(v, "ArmL", new Vector3(-0.34f, 1.45f, 0f), new Vector3(0.11f, 0.3f, 0.11f), -0.3f, suit);
            rig.armR = MakeLimb(v, "ArmR", new Vector3(0.34f, 1.45f, 0f), new Vector3(0.11f, 0.3f, 0.11f), -0.3f, suit);
            return rig;
        }

        static Transform MakeLimb(Transform parent, string name, Vector3 pivotPos, Vector3 scale, float centerY, Material mat)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = pivotPos;
            Placeholder.Prim(PrimitiveType.Cylinder, name + "_Mesh", pivot, new Vector3(0f, centerY, 0f), scale, mat, false);
            return pivot;
        }
    }
}
