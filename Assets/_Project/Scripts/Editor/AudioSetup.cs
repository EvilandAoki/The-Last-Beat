using System;
using System.Collections.Generic;
using System.Reflection;
using TheLastBeat.Audio;
using TheLastBeat.Level;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Integración de audio (tarea 32). Menú The Last Beat/Audio/Montar el audio en Pueblo:
    ///  1. Audio Mixer en Audio/MainMixer.mixer con los grupos Música, Efectos y Ambiente (lo crea si no existe y le
    ///     añade los grupos que falten).
    ///  2. En Pueblo, el objeto "Audio" con SfxPlayer (pool de efectos), GameSfx (los efectos de la tarea 31 enganchados
    ///     a los eventos del juego), MusicLayerController (música por capas de las tareas 29 y 30) y AmbiencePlayer (el
    ///     ambiente del pueblo), cada uno en su grupo del mixer.
    /// Usa los archivos de Alejandro tal como están en Audio/Music y Audio/SFX. Es repetible: vuelve a asignar los clips
    /// sin tocar los volúmenes que se hayan ajustado a mano.
    /// </summary>
    public static class AudioSetup
    {
        private const string Tag = "AudioSetup";
        private const string MixerPath = "Assets/_Project/Audio/MainMixer.mixer";
        private const string MusicFolder = "Assets/_Project/Audio/Music/";
        private const string SfxFolder = "Assets/_Project/Audio/SFX/";
        private const string RootName = "Audio";

        private const string MusicGroup = "Música";
        private const string SfxGroup = "Efectos";
        private const string AmbienceGroup = "Ambiente";

        // Campo de GameSfx → nombre del efecto en Audio/SFX (las variantes _01, _02... se cargan solas).
        private static readonly string[,] Cues =
        {
            { "comboLight", "Combo_Ataque_Debil" }, { "comboHeavy", "Combo_Ataque_Fuerte" },
            { "spin", "Giro" }, { "spinExplosion", "Explosion" }, { "charge", "Embestida" },
            { "knightHurt", "Dano_Recibido" }, { "impactLight", "Impacto_Debil" }, { "impactHeavy", "Impacto_Fuerte" },
            { "soldierDeath", "Soldado_Muerte" }, { "songInterrupted", "Cancion_Interrumpida" },
        };

        // Campo de MusicLayerController → archivo en Audio/Music.
        private static readonly string[,] Layers =
        {
            { "combatBase", "Combate_Base.ogg" }, { "combatPercussion", "Combate_Percusion.ogg" },
            { "combatMelody", "Combate_Melodia.ogg" }, { "bardMelody", "Bardo_Cancion_Melodia.ogg" },
            { "bardLute", "Bardo_Cancion_Laud.ogg" },
        };

        private const string AmbienceClip = "Ambiente_Pueblo.ogg";

        [MenuItem("The Last Beat/Audio/Montar el audio en Pueblo (tarea 32)")]
        public static void Run()
        {
            if (!SetupUtils.NotPlaying(Tag) || !SetupUtils.OpenPueblo()) return;

            LevelFlow flow = SetupUtils.FindInScene<LevelFlow>();
            if (flow == null)
            {
                Debug.LogError($"[{Tag}] Pueblo no tiene LevelFlow. Ejecuta antes The Last Beat > Nivel > 2. Montar el flujo del nivel.");
                return;
            }

            var missing = new List<string>();
            string notes = string.Empty;

            // 1. El mixer.
            AudioMixer mixer = LoadOrCreateMixer();
            AudioMixerGroup music = FindGroup(mixer, MusicGroup);
            AudioMixerGroup sfx = FindGroup(mixer, SfxGroup);
            AudioMixerGroup ambience = FindGroup(mixer, AmbienceGroup);
            if (mixer == null)
                notes += "\n- No pude crear el Audio Mixer por código. Todo suena igual sin él; para tenerlo, crea en Assets/_Project/Audio " +
                         "un Audio Mixer llamado MainMixer con los grupos Música, Efectos y Ambiente y vuelve a ejecutar este menú.";
            else if (music == null || sfx == null || ambience == null)
                notes += "\n- Al Audio Mixer le falta algún grupo (Música, Efectos o Ambiente).";

            // 2. El objeto Audio.
            GameObject root = SetupUtils.GetOrCreateRoot(RootName);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            SfxPlayer player = SetupUtils.GetOrAdd<SfxPlayer>(root);
            SetupUtils.Set(player, "output", sfx);

            GameSfx gameSfx = SetupUtils.GetOrAdd<GameSfx>(root);
            var sfxObject = new SerializedObject(gameSfx);
            for (int i = 0; i < Cues.GetLength(0); i++) SetCue(sfxObject, Cues[i, 0], Cues[i, 1], missing);
            sfxObject.ApplyModifiedProperties();

            MusicLayerController layers = SetupUtils.GetOrAdd<MusicLayerController>(root);
            SetupUtils.Set(layers, "flow", flow);
            SetupUtils.Set(layers, "output", music);
            for (int i = 0; i < Layers.GetLength(0); i++)
                SetupUtils.Set(layers, Layers[i, 0], LoadClip(MusicFolder + Layers[i, 1], missing));

            GameObject ambienceObject = SetupUtils.GetOrCreateChild(root, "Ambiente");
            SetupUtils.GetOrAdd<AudioSource>(ambienceObject);
            AmbiencePlayer ambiencePlayer = SetupUtils.GetOrAdd<AmbiencePlayer>(ambienceObject);
            SetupUtils.Set(ambiencePlayer, "flow", flow);
            SetupUtils.Set(ambiencePlayer, "output", ambience);
            SetupUtils.Set(ambiencePlayer, "clip", LoadClip(MusicFolder + AmbienceClip, missing));

            if (missing.Count > 0) notes += "\n- No encuentro estos sonidos: " + string.Join(", ", missing.ToArray()) + ".";
            if (SetupUtils.FindInScene<AudioListener>() == null) notes += "\n- No hay ningún AudioListener en la escena: no se va a oír nada.";

            SetupUtils.SaveScene();
            if (notes.Length == 0) Debug.Log($"[{Tag}] Audio montado en Pueblo: mixer con Música, Efectos y Ambiente, y el objeto '{RootName}'.");
            else Debug.LogWarning($"[{Tag}] Audio montado en Pueblo, con avisos:" + notes);
        }

        // ---------------------------------------------------------------- Clips

        private static AudioClip LoadClip(string path, List<string> missing)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) missing.Add(path);
            return clip;
        }

        /// <summary>Carga Nombre.wav o sus variantes Nombre_01.wav, Nombre_02.wav...</summary>
        private static AudioClip[] LoadVariants(string clipName)
        {
            var clips = new List<AudioClip>();
            var single = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFolder + clipName + ".wav");
            if (single != null) clips.Add(single);
            for (int i = 1; i <= 9; i++)
            {
                var variant = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFolder + clipName + "_0" + i + ".wav");
                if (variant == null) break;
                clips.Add(variant);
            }

            return clips.ToArray();
        }

        /// <summary>Asigna los clips de un SfxCue sin tocar su volumen ni sus demás ajustes.</summary>
        private static void SetCue(SerializedObject target, string field, string clipName, List<string> missing)
        {
            SerializedProperty clips = target.FindProperty(field + ".clips");
            if (clips == null)
            {
                Debug.LogWarning($"[{Tag}] GameSfx no tiene el campo '{field}'.");
                return;
            }

            AudioClip[] found = LoadVariants(clipName);
            if (found.Length == 0)
            {
                missing.Add(clipName);
                return;
            }

            clips.arraySize = found.Length;
            for (int i = 0; i < found.Length; i++) clips.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
        }

        // ---------------------------------------------------------------- Mixer

        /// <summary>
        /// Unity no tiene una API pública para crear un Audio Mixer ni sus grupos; esto usa la clase interna del editor
        /// (AudioMixerController) por reflexión. Si algo falla, avisa y el juego suena igual sin mixer.
        /// </summary>
        private static AudioMixer LoadOrCreateMixer()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            try
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.Audio.AudioMixerController");
                if (type == null) return mixer;

                if (mixer == null)
                {
                    MethodInfo create = type.GetMethod("CreateMixerControllerAtPath", flags);
                    if (create == null) return null;
                    mixer = create.Invoke(null, new object[] { MixerPath }) as AudioMixer;
                    if (mixer == null) mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
                }

                if (mixer == null || !type.IsInstanceOfType(mixer)) return mixer;

                MethodInfo createGroup = type.GetMethod("CreateNewGroup", flags);
                MethodInfo addChild = type.GetMethod("AddChildToParent", flags);
                MethodInfo addToView = type.GetMethod("AddGroupToCurrentView", flags);
                PropertyInfo master = type.GetProperty("masterGroup", flags);
                if (createGroup == null || addChild == null || master == null) return mixer;

                object masterGroup = master.GetValue(mixer, null);
                string[] names = { MusicGroup, SfxGroup, AmbienceGroup };
                bool changed = false;
                for (int i = 0; i < names.Length; i++)
                {
                    if (FindGroup(mixer, names[i]) != null) continue;

                    object group = createGroup.Invoke(mixer, new object[] { names[i], false });
                    addChild.Invoke(mixer, new object[] { group, masterGroup });
                    changed = true;

                    // Que se vea en la ventana del mixer. Es opcional y puede fallar en un mixer recién creado (aún no
                    // tiene vista): el grupo funciona igual.
                    try
                    {
                        if (addToView != null) addToView.Invoke(mixer, new object[] { group });
                    }
                    catch (Exception)
                    {
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(mixer);
                    AssetDatabase.SaveAssets();
                }
            }
            catch (Exception e)
            {
                Exception cause = e.InnerException ?? e;
                Debug.LogWarning($"[{Tag}] No pude completar el Audio Mixer por código: {cause.GetType().Name}: {cause.Message}");
            }

            return mixer != null ? mixer : AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }

        private static AudioMixerGroup FindGroup(AudioMixer mixer, string groupName)
        {
            if (mixer == null) return null;
            AudioMixerGroup[] groups = mixer.FindMatchingGroups(groupName);
            for (int i = 0; i < groups.Length; i++)
                if (groups[i].name == groupName) return groups[i];
            return null;
        }
    }
}
