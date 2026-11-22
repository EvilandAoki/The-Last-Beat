namespace TheLastBeat.Core
{
    /// <summary>
    /// Lo que debe poder volver a su estado inicial cuando el nivel se reinicia sin recargar la escena
    /// (tarea 35: Reintentar). El héroe llama a ResetForRestart de todos sus componentes que lo implementen.
    /// </summary>
    public interface IRespawnable
    {
        /// <summary>Cancela lo que esté haciendo (golpes, habilidades, recargas) y vuelve a empezar limpio.</summary>
        void ResetForRestart();
    }
}
