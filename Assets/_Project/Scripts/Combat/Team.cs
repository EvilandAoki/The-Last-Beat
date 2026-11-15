namespace TheLastBeat.Combat
{
    /// <summary>
    /// Bando de cada entidad. El daño entre miembros del mismo bando se ignora.
    /// Neutral daña a todos (trampas, caídas) y lo dañan todos.
    /// </summary>
    public enum Team
    {
        Neutral = 0,
        Heroes = 1,
        Demons = 2
    }
}
