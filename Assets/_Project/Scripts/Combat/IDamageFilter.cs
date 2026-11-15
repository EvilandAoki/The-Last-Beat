namespace TheLastBeat.Combat
{
    /// <summary>
    /// Componente que puede modificar o cancelar un golpe antes de que Health lo aplique.
    /// Se pone en el mismo GameObject que Health. Ejemplos:
    ///  - KnightAutoDodge: con la potenciación de la bardo, cancela un % de golpes (tarea 19).
    ///  - EarthBarrier: reduce el daño mientras la barrera está activa (tarea 53).
    /// Health los ejecuta en el orden en que aparecen en el Inspector.
    /// </summary>
    public interface IDamageFilter
    {
        /// <summary>
        /// Puede cambiar info (por ejemplo, bajar Amount). Devolver false cancela el golpe por completo.
        /// </summary>
        bool Filter(ref DamageInfo info);
    }
}
