using Unity.Cinemachine;
using UnityEngine;

namespace TheLastBeat.Feedback
{
    /// <summary>
    /// Sacudida de cámara con CinemachineImpulseSource (tarea 12). Crea una sola fuente de impulso y, la primera vez,
    /// añade un CinemachineImpulseListener a la CinemachineCamera de la escena si no lo tiene, así funciona en cualquier
    /// escena (Test_Combo, Pueblo) sin configurar nada. El listener se configura a mano porque sus valores por defecto
    /// solo se aplican al añadirlo desde el editor.
    /// </summary>
    public static class CameraShake
    {
        private static CinemachineImpulseSource source;
        private static CinemachineCamera targetCamera;
        private static float nextSearch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            source = null;
            targetCamera = null;
            nextSearch = 0f;
        }

        public static void Prepare()
        {
            EnsureSource();
            EnsureListener();
        }

        public static void Shake(Vector3 position, float strength, bool big, ImpactSettings settings)
        {
            if (strength <= 0f) return;
            EnsureListener();
            if (targetCamera == null) return;
            EnsureSource();

            // Forma y duración: golpes fuertes con "explosión", el resto con un golpe corto.
            source.ImpulseDefinition.ImpulseShape = big
                ? CinemachineImpulseDefinition.ImpulseShapes.Explosion
                : CinemachineImpulseDefinition.ImpulseShapes.Bump;
            source.ImpulseDefinition.ImpulseDuration = big ? settings.shakeDurationBig : settings.shakeDuration;

            // Dirección horizontal al azar (la cámara es cenital) con un poco de vertical.
            Vector2 direction = Random.insideUnitCircle;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
            direction.Normalize();
            source.GenerateImpulseAtPositionWithVelocity(position, new Vector3(direction.x, 0.3f, direction.y) * strength);
        }

        private static void EnsureSource()
        {
            if (source != null) return;
            var go = new GameObject("ImpactImpulse");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<CinemachineImpulseSource>();
            source.ImpulseDefinition.ImpulseChannel = 1;
            source.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform; // igual en toda la escena
        }

        private static void EnsureListener()
        {
            if (targetCamera != null && targetCamera.isActiveAndEnabled) return;
            if (Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + 1f;

            targetCamera = Object.FindFirstObjectByType<CinemachineCamera>();
            if (targetCamera == null || targetCamera.GetComponent<CinemachineImpulseListener>() != null) return;

            // Reset() solo corre al añadirlo en el editor: en tiempo de juego hay que dar los valores a mano.
            var listener = targetCamera.gameObject.AddComponent<CinemachineImpulseListener>();
            listener.ApplyAfter = CinemachineCore.Stage.Noise;
            listener.ChannelMask = 1;
            listener.Gain = 1f;
            listener.Use2DDistance = false;
            listener.UseCameraSpace = true;
            listener.SignalCombinationMode = CinemachineImpulseListener.SignalCombinationModes.Additive;
            listener.ReactionSettings = new CinemachineImpulseListener.ImpulseReaction
            {
                AmplitudeGain = 1f,
                FrequencyGain = 1f,
                Duration = 1f,
            };
        }
    }
}
