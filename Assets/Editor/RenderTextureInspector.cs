using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class RenderTextureInspector : EditorWindow
{
    [MenuItem("Tools/Fix RenderTexture Scene Materials")]
    public static void FixMaterials()
    {
        // 1. URP Uyumlu Yumuşak Partikül Materyali Oluştur
        string matPath = "Assets/Unity UI Samples/Materials/URP-Soft-Particle.mat";
        Material urpParticleMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        if (urpParticleMat == null)
        {
            Shader urpParticlesUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpParticlesUnlit == null)
            {
                Debug.LogError("URP Particles/Unlit shader not found!");
                return;
            }

            urpParticleMat = new Material(urpParticlesUnlit);
            
            // Yumuşak yuvarlak partikül dokusunu Unity dahili kaynaklarından yüklüyoruz
            Texture2D defaultParticleTex = 
                AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd") ??
                AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.png") ??
                AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.tga");

            if (defaultParticleTex != null)
            {
                urpParticleMat.SetTexture("_BaseMap", defaultParticleTex);
            }
            else
            {
                Debug.LogWarning("Built-in Default-Particle texture could not be loaded!");
            }

            // Şeffaflık (Transparent/Alpha Blend) Ayarları
            urpParticleMat.SetFloat("_Surface", 1); // 0: Opaque, 1: Transparent
            urpParticleMat.SetFloat("_Blend", 0);   // 0: Alpha, 1: Premultiply, 2: Additive
            urpParticleMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            urpParticleMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            urpParticleMat.SetInt("_ZWrite", 0);
            urpParticleMat.DisableKeyword("_ALPHATEST_ON");
            urpParticleMat.EnableKeyword("_ALPHABLEND_ON");
            urpParticleMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            urpParticleMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // Dosya olarak kaydet
            AssetDatabase.CreateAsset(urpParticleMat, matPath);
            Debug.Log("Created new URP-Soft-Particle material at Assets/Unity UI Samples/Materials/URP-Soft-Particle.mat");
        }

        // 2. Ortak Arka Plan Prefabindeki (SF Scene Elements) Partikül Materyalini Ayarla
        string prefabPath = "Assets/Unity UI Samples/Prefabs/SF Scene Elements.prefab";
        GameObject prefabGo = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabGo != null)
        {
            Transform particleTrans = prefabGo.transform.Find("Particle System");
            if (particleTrans != null)
            {
                ParticleSystemRenderer psr = particleTrans.GetComponent<ParticleSystemRenderer>();
                if (psr != null)
                {
                    psr.sharedMaterial = urpParticleMat;
                    EditorUtility.SetDirty(prefabGo);
                    Debug.Log("Set Particle System material to URP-Soft-Particle inside the prefab!");
                }
            }
        }

        // Sahnelerin listesi
        string[] scenesToFix = new string[] {
            "Assets/Unity UI Samples/Scenes/RenderTexture.unity",
            "Assets/Unity UI Samples/Scenes/Menu 3D.unity",
            "Assets/Scenes/MainMenu.unity"
        };

        foreach (string scenePath in scenesToFix)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) continue;

            Debug.Log($"Fixing scene: {scenePath}");

            // Sahnedeki Particle System nesnesini bul ve yeni URP materyalini zorla ata
            GameObject particleGo = GameObject.Find("SF Scene Elements/Particle System");
            if (particleGo == null) particleGo = GameObject.Find("Particle System");

            if (particleGo != null)
            {
                ParticleSystemRenderer psr = particleGo.GetComponent<ParticleSystemRenderer>();
                if (psr != null)
                {
                    psr.sharedMaterial = urpParticleMat;
                    EditorUtility.SetDirty(particleGo);
                    Debug.Log($"Forced URP-Soft-Particle material on [{particleGo.name}] in scene: {scenePath}");
                }
            }

            // Sadece RenderTexture sahnesine özel diğer 3D/UI düzeltmeleri
            if (scenePath.Contains("RenderTexture.unity"))
            {
                // vehicle_rcFlyer_dome_mat materyalini URP/Lit yap
                Material domeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Unity UI Samples/Models/Materials/vehicle_rcFlyer_dome_mat.mat");
                if (domeMat != null)
                {
                    Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpLit != null)
                    {
                        domeMat.shader = urpLit;
                        EditorUtility.SetDirty(domeMat);
                        Debug.Log("Upgraded vehicle_rcFlyer_dome_mat shader to Universal Render Pipeline/Lit");
                    }
                }

                // UI Opaque materyalini UI/Default yap
                Material uiOpaqueMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Unity UI Samples/Materials/UI Opaque.mat");
                if (uiOpaqueMat != null)
                {
                    Shader uiDefault = Shader.Find("UI/Default");
                    if (uiDefault != null)
                    {
                        uiOpaqueMat.shader = uiDefault;
                        EditorUtility.SetDirty(uiOpaqueMat);
                        Debug.Log("Upgraded UI Opaque material shader to UI/Default");
                    }
                }

                // Sahnedeki kameraların URP Renderer ayarlarını sıfırla
                var cameras = Object.FindObjectsOfType<Camera>(true);
                foreach (var cam in cameras)
                {
                    var cameraData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    if (cameraData == null)
                    {
                        cameraData = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    }

                    cameraData.SetRenderer(0);
                    EditorUtility.SetDirty(cameraData);
                    Debug.Log($"Camera: {cam.name}, Forced Renderer to Index 0");
                }
            }

            // Değişiklikleri kaydet
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // 3. Lit Slider materyallerini (Lighting sahnesi için) düzelt (UI/Default yap)
        string[] sliderMats = new string[] {
            "Assets/Unity UI Samples/Materials/Slider Background.mat",
            "Assets/Unity UI Samples/Materials/Slider Fill.mat",
            "Assets/Unity UI Samples/Materials/Slider Knob.mat"
        };

        Shader uiDefaultShader = Shader.Find("UI/Default");
        if (uiDefaultShader != null)
        {
            foreach (var path in sliderMats)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                {
                    mat.shader = uiDefaultShader;
                    EditorUtility.SetDirty(mat);
                    Debug.Log($"Upgraded {mat.name} material shader to UI/Default");
                }
            }
        }

        // Değişiklikleri kaydet
        AssetDatabase.SaveAssets();
        Debug.Log("All Unity UI Samples materials, shaders, and particle systems fixed globally across all scenes!");
    }
}
