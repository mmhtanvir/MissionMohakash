using UnityEditor;
using UnityEngine;

namespace Mohakash.EditorTools
{
    /// <summary>
    /// glTFast's editor importer defaults to "Animation Method = Mecanim" (serialized value 2). CrewRig plays the clips through the legacy
    /// Animation component, so the rigged character GLBs must import as Legacy (value 1). With Mecanim there is no Animation component and the
    /// character silently stays in its bind pose (no walking, running, carrying or sleeping). This forces Legacy whenever one of them is (re)imported.
    /// </summary>
    public class CharacterImportFix : AssetPostprocessor
    {
        static readonly string[] Names = { "AstronautEVA.glb", "CrewCasual.glb", "Astronaut.glb" };

        void OnPreprocessAsset()
        {
            bool match = false;
            foreach (var n in Names) if (assetPath.EndsWith("/Resources/Models/" + n)) match = true;
            if (!match) return;
            try
            {
                var so = new SerializedObject(assetImporter);
                var prop = so.FindProperty("importSettings.animationMethod");
                if (prop != null && prop.intValue != 1)
                {
                    prop.intValue = 1;   // GLTFast.AnimationMethod.Legacy
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log("CharacterImportFix: set Animation Method = Legacy for " + assetPath);
                }
            }
            catch (System.Exception e) { Debug.LogWarning("CharacterImportFix could not patch " + assetPath + ": " + e.Message); }
        }
    }
}
