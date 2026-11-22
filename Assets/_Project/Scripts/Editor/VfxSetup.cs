using System;
using System.IO;
using TheLastBeat.VFX;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Genera los efectos de partículas del juego (tarea 33): texturas suaves, materiales de URP, un prefab por efecto con
    /// su PooledEffect y maxParticles limitado, y la biblioteca VfxLibrary en Resources. Colores por héroe: azul el
    /// caballero y morado la bardo. No toca ninguna escena.
    /// Menús: The Last Beat/VFX/Crear efectos (conserva los prefabs que ya existan, por si los retocas)
    ///        y Regenerar efectos (los vuelve a crear desde cero).
    /// </summary>
    public static class VfxSetup
    {
        private const string Root = "Assets/_Project/VFX";
        private const string Textures = Root + "/Textures";
        private const string Materials = Root + "/Materials";
        private const string Prefabs = Root + "/Prefabs";
        private const string ResourcesFolder = Root + "/Resources";

        private static readonly Color KnightBlue = new Color(0.35f, 0.65f, 1f);
        private static readonly Color KnightWhite = new Color(0.8f, 0.92f, 1f);
        private static readonly Color BardPurple = new Color(0.78f, 0.4f, 0.92f);
        private static readonly Color BardLight = new Color(0.92f, 0.75f, 1f);

        [MenuItem("The Last Beat/VFX/Crear efectos (tarea 33)")]
        public static void Create() => Run(false);

        [MenuItem("The Last Beat/VFX/Regenerar efectos (pisa los prefabs)")]
        public static void Regenerate() => Run(true);

        private static void Run(bool overwrite)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[VfxSetup] Sal de Play antes de ejecutar esto.");
                return;
            }

            if (Shader.Find("Universal Render Pipeline/Particles/Unlit") == null)
            {
                Debug.LogError("[VfxSetup] No encuentro el shader 'Universal Render Pipeline/Particles/Unlit'. ¿Está URP instalado?");
                return;
            }

            foreach (string folder in new[] { Textures, Materials, Prefabs, ResourcesFolder }) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            Texture2D soft = MakeTexture("Soft.png", d => Mathf.Pow(Mathf.Clamp01(1f - d), 2f));
            Texture2D ring = MakeTexture("Ring.png", d => Mathf.Clamp01(1f - Mathf.Abs(d - 0.88f) / 0.1f));
            Material additive = MakeMaterial(Materials + "/VFX_Aditivo.mat", soft);
            Material wave = MakeMaterial(Materials + "/VFX_Onda.mat", ring);

            var library = LoadOrCreateLibrary();
            library.impact.prefab = Prefab("VFX_Impacto", overwrite, go => BuildImpact(go, additive, false, wave));
            library.impactHeavy.prefab = Prefab("VFX_ImpactoPesado", overwrite, go => BuildImpact(go, additive, true, wave));
            library.spin.prefab = Prefab("VFX_Giro", overwrite, go => BuildSpin(go, additive));
            library.explosion.prefab = Prefab("VFX_Explosion", overwrite, go => BuildExplosion(go, additive, wave));
            library.charge.prefab = Prefab("VFX_Embestida", overwrite, go => BuildCharge(go, additive));
            library.songAura.prefab = Prefab("VFX_AuraCancion", overwrite, go => BuildAura(go, additive));
            library.songBreak.prefab = Prefab("VFX_CancionRota", overwrite, go => BuildSongBreak(go, additive));

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("[VfxSetup] Listo. Efectos en " + Prefabs + " y biblioteca en " + ResourcesFolder + "/VfxLibrary.asset.");
        }

        // ---------------------------------------------------------------- Efectos

        private static void BuildImpact(GameObject go, Material material, bool heavy, Material waveMaterial)
        {
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            Configure(ps, duration: 0.1f, loop: false, life: heavy ? (0.3f, 0.55f) : (0.25f, 0.45f),
                speed: heavy ? (3.5f, 8f) : (2.5f, 5.5f), size: heavy ? (0.16f, 0.34f) : (0.1f, 0.22f),
                max: heavy ? 80 : 40, space: ParticleSystemSimulationSpace.World);
            ParticleSystem.MainModule main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(KnightBlue, KnightWhite);
            main.gravityModifier = 0.6f;
            Burst(ps, heavy ? 28 : 14);
            Shape(ps, ParticleSystemShapeType.Sphere, 0.05f);
            ColorFade(ps, 1f);
            SizeShrink(ps);
            Render(ps, material);

            if (heavy) AddWave(go, waveMaterial, KnightBlue, startSize: 1.4f, life: 0.25f);
        }

        private static void BuildSpin(GameObject go, Material material)
        {
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            Configure(ps, duration: 1f, loop: true, life: (0.4f, 0.5f), speed: (0f, 0f), size: (0.3f, 0.4f), max: 120,
                space: ParticleSystemSimulationSpace.Local);
            ParticleSystem.MainModule main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(KnightBlue, KnightWhite);
            main.scalingMode = ParticleSystemScalingMode.Shape;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 80f;

            // Anillo horizontal que gira alrededor del héroe (el radio se ajusta con la escala del efecto).
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1f;
            shape.radiusThickness = 0.05f;
            shape.arc = 360f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = 0f;
            velocity.y = 0f;
            velocity.z = 0f;
            velocity.orbitalY = 7f;

            ColorFade(ps, 0.9f);
            SizeShrink(ps);
            Render(ps, material);
        }

        private static void BuildExplosion(GameObject go, Material material, Material waveMaterial)
        {
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            Configure(ps, duration: 0.2f, loop: false, life: (0.4f, 0.7f), speed: (6f, 13f), size: (0.3f, 0.6f), max: 120,
                space: ParticleSystemSimulationSpace.World);
            ParticleSystem.MainModule main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(KnightBlue, KnightWhite);
            main.scalingMode = ParticleSystemScalingMode.Shape;
            Burst(ps, 50);
            Shape(ps, ParticleSystemShapeType.Sphere, 0.3f);
            ColorFade(ps, 1f);
            SizeShrink(ps);
            Render(ps, material);

            // Onda en el suelo que se expande hasta el radio de la explosión (la escala del efecto es ese radio).
            AddWave(go, waveMaterial, KnightBlue, startSize: 2f, life: 0.45f);
        }

        private static void BuildCharge(GameObject go, Material material)
        {
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            Configure(ps, duration: 1f, loop: true, life: (0.4f, 0.6f), speed: (0.2f, 0.5f), size: (0.5f, 0.8f), max: 80,
                space: ParticleSystemSimulationSpace.World);
            ParticleSystem.MainModule main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(KnightBlue, KnightWhite);
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 10f; // sigue al héroe: más partículas cuanto más rápido avanza
            Shape(ps, ParticleSystemShapeType.Sphere, 0.25f);
            ColorFade(ps, 0.7f);
            SizeShrink(ps);
            Render(ps, material);
        }

        private static void BuildAura(GameObject go, Material material)
        {
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            Configure(ps, duration: 1f, loop: true, life: (1.4f, 2f), speed: (0f, 0f), size: (0.15f, 0.28f), max: 160,
                space: ParticleSystemSimulationSpace.Local);
            ParticleSystem.MainModule main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(BardPurple, BardLight);
            main.scalingMode = ParticleSystemScalingMode.Shape;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 30f;

            // Partículas moradas que suben por el borde del radio de la canción (la escala del efecto es ese radio).
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1f;
            shape.radiusThickness = 0.02f;
            shape.arc = 360f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = 0f;
            velocity.y = 0.6f;
            velocity.z = 0f;

            ColorFade(ps, 0.8f);
            Render(ps, material);
        }

        private static void BuildSongBreak(GameObject go, Material material)
        {
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            Configure(ps, duration: 0.1f, loop: false, life: (0.5f, 0.8f), speed: (3f, 6f), size: (0.15f, 0.3f), max: 60,
                space: ParticleSystemSimulationSpace.World);
            ParticleSystem.MainModule main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(BardPurple, BardLight);
            main.gravityModifier = 0.3f;
            Burst(ps, 26);
            Shape(ps, ParticleSystemShapeType.Sphere, 0.3f);
            ColorFade(ps, 1f);
            SizeShrink(ps);
            Render(ps, material);
        }

        /// <summary>Onda plana sobre el suelo que crece y se desvanece. Hija del efecto: dura menos que la raíz.</summary>
        private static void AddWave(GameObject parent, Material material, Color color, float startSize, float life)
        {
            var child = new GameObject("Onda", typeof(ParticleSystem));
            child.transform.SetParent(parent.transform, false);
            ParticleSystem ps = child.GetComponent<ParticleSystem>();
            Configure(ps, duration: 0.1f, loop: false, life: (life, life), speed: (0f, 0f), size: (startSize, startSize), max: 2,
                space: ParticleSystemSimulationSpace.World);
            ParticleSystem.MainModule main = ps.main;
            main.startColor = color;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            Burst(ps, 1);
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.15f, 1f, 1f));
            ColorFade(ps, 0.9f);

            Render(ps, material);
            ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        }

        // ---------------------------------------------------------------- Módulos de partículas

        private static void Configure(ParticleSystem ps, float duration, bool loop, (float, float) life, (float, float) speed,
            (float, float) size, int max, ParticleSystemSimulationSpace space)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.duration = duration;
            main.loop = loop;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.Item1, life.Item2);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.maxParticles = max;
            main.simulationSpace = space;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
        }

        private static void Burst(ParticleSystem ps, int count)
        {
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        private static void Shape(ParticleSystem ps, ParticleSystemShapeType type, float radius)
        {
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = type;
            shape.radius = radius;
        }

        private static void ColorFade(ParticleSystem ps, float peakAlpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.12f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = gradient;
        }

        private static void SizeShrink(ParticleSystem ps)
        {
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
        }

        private static void Render(ParticleSystem ps, Material material)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ---------------------------------------------------------------- Assets

        private static Texture2D MakeTexture(string file, Func<float, float> alphaByDistance)
        {
            string path = Textures + "/" + file;
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / (size * 0.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alphaByDistance(distance)));
                }
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Material de partículas de URP con mezcla aditiva (brilla sobre el suelo claro y el oscuro).</summary>
        private static Material MakeMaterial(string path, Texture2D texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);                                 // transparente
            material.SetFloat("_Blend", 2f);                                   // aditivo
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static PooledEffect Prefab(string name, bool overwrite, Action<GameObject> build)
        {
            string path = Prefabs + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !overwrite) return existing.GetComponent<PooledEffect>();

            var go = new GameObject(name, typeof(ParticleSystem), typeof(PooledEffect));
            try
            {
                build(go);
                return PrefabUtility.SaveAsPrefabAsset(go, path).GetComponent<PooledEffect>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static VfxLibrary LoadOrCreateLibrary()
        {
            string path = ResourcesFolder + "/VfxLibrary.asset";
            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(path);
            if (library != null) return library;

            library = ScriptableObject.CreateInstance<VfxLibrary>();
            AssetDatabase.CreateAsset(library, path);
            return library;
        }
    }
}
