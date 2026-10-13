using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dano dos componentes e estoque (idempotente; Tools > E-stagiar.io > Dano e estoque dos componentes):
///  - Pickup_PlacaMae e Pickup_MemoriaRAM: PartFallDamage (quebram na 1ª queda real);
///  - Pickup_HD, Pickup_Fonte, Pickup_Cooler: PartFallDamage (1ª queda registrada, quebram na 2ª);
///  - cada um com o filho "Visual_Quebrado" (desativado) com o modelo Item_*_Quebrad* (componentes.fbx,
///    Tools/blender/componentes_build.py);
///  - estoque das gavetas (GaveteirosSetup.UpdateStock).
/// Antes: copiar Art/Source/Blender/export/componentes.fbx para Assets/Art/Models (feito aqui se existir).
/// </summary>
public static class ComponentesDanoSetup
{
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string Fbx = "Assets/Art/Models/componentes.fbx";

    [MenuItem("Tools/E-stagiar.io/Dano e estoque dos componentes")]
    public static void Run()
    {
        if (File.Exists("Art/Source/Blender/export/componentes.fbx")) File.Copy("Art/Source/Blender/export/componentes.fbx", Fbx, true);
        AssetDatabase.Refresh();
        // frágeis: quebram na 1ª queda real; resistentes: a 1ª queda só é registrada, quebram na 2ª
        Configure("Pickup_PlacaMae", 1, "Item_PlacaMae_Quebrada");
        Configure("Pickup_MemoriaRAM", 1, "Item_MemoriaRAM_Quebrada");
        Configure("Pickup_HD", 2, "Item_HD_Quebrado");
        Configure("Pickup_Fonte", 2, "Item_Fonte_Quebrada");
        Configure("Pickup_Cooler", 2, "Item_Cooler_Quebrado");
        GaveteirosSetup.UpdateStock();
        AssetDatabase.SaveAssets();
        Debug.Log("ComponentesDanoSetup: pronto");
    }

    private static void Configure(string prefab, int dropsToBreak, string brokenMesh)
    {
        string path = Gameplay + prefab + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Transform visual = root.transform.Find("Visual");
        Transform broken = root.transform.Find("Visual_Quebrado");
        if (brokenMesh != null)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            MeshFilter src = model.GetComponentsInChildren<MeshFilter>(true).First(f => f.sharedMesh.name == brokenMesh);
            if (broken == null)
            {
                broken = new GameObject("Visual_Quebrado").transform;
                broken.SetParent(root.transform, false);
            }
            // mesma pose do Visual inteiro (as duas malhas têm a base em y = 0 e o centro na origem)
            broken.localPosition = visual.localPosition; broken.localRotation = visual.localRotation; broken.localScale = visual.localScale;
            MeshFilter mf = broken.GetComponent<MeshFilter>();
            if (mf == null) mf = broken.gameObject.AddComponent<MeshFilter>();
            MeshRenderer mr = broken.GetComponent<MeshRenderer>();
            if (mr == null) mr = broken.gameObject.AddComponent<MeshRenderer>();
            mf.sharedMesh = src.sharedMesh;
            mr.sharedMaterials = src.GetComponent<MeshRenderer>().sharedMaterials;
            broken.gameObject.SetActive(false);
        }
        PartFallDamage dmg = root.GetComponent<PartFallDamage>();
        if (dmg == null) dmg = root.AddComponent<PartFallDamage>();
        var so = new SerializedObject(dmg);
        so.FindProperty("dropsToBreak").intValue = dropsToBreak;
        so.FindProperty("intactVisual").objectReferenceValue = visual != null ? visual.gameObject : null;
        so.FindProperty("brokenVisual").objectReferenceValue = broken != null ? broken.gameObject : null;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }
}
