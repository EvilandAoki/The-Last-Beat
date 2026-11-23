# Audio de The Last Beat

Todo el audio del juego se genera con estos scripts: composición y diseño de sonido originales, sin samples ni
grabaciones de terceros (ver `CREDITS.md`).

| Script | Tarea | Genera |
|---|---|---|
| `musica.py` | #29, #30 | `Assets/_Project/Audio/Music/`: canción de la bardo, música de combate por capas y ambiente del pueblo |
| `sfx.py` | #31 | `Assets/_Project/Audio/SFX/`: golpes, impactos, habilidades, daño, muerte del soldado y canción interrumpida |
| `chiptune.py` | — | Sintetizador de 8 bits compartido (pulso, triángulo, ruido LFSR, envolventes y filtros) |

## Regenerar

```bash
pip install numpy scipy soundfile
python Tools/Audio/musica.py
python Tools/Audio/sfx.py
```

Los resultados son siempre los mismos (semillas fijas). Si cambias algo, regenera y commitea los archivos de
audio junto con el script.

## Música (`Audio/Music/`, OGG estéreo, 48 kHz)

Toda la música comparte **tempo (120 BPM), tonalidad (La menor), acordes (Am F C G, uno por compás) y duración
(16 compases = 32,000 s)**. Por eso cualquier combinación de capas suena junta, a tempo y en armonía. Cada archivo
es un bucle exacto: el final empalma con el principio.

| Archivo | Qué es |
|---|---|
| `Bardo_Cancion_Completa.ogg` | La canción de la bardo (dembow en 8 bits), todas las pistas |
| `Bardo_Cancion_Bateria.ogg`, `_Bajo`, `_Laud`, `_Melodia` | Sus pistas por separado; sumadas dan la completa |
| `Combate_Base.ogg`, `_Percusion`, `_Melodia` | Música de combate por capas |
| `Combate_Completa.ogg` | Las tres capas de combate juntas (referencia para escuchar) |
| `Ambiente_Pueblo.ogg` | Pueblo en silencio: viento y pasos lejanos (48 s, bucle) |

**Niveles:** las 7 capas (4 de la bardo y 3 de combate) tienen una sola ganancia común. Sonando todas a volumen 1 al
mismo tiempo, el pico queda en −1 dBFS: ninguna combinación satura.

## Mapa para la integración (#32)

Sugerencia; quien integra decide:

1. Al entrar en combate, arrancar **todas** las capas a la vez con `AudioSource.PlayScheduled` (el mismo `dspTime`) y en
   bucle. Desde ahí, las capas solo suben y bajan de volumen: nunca se paran, así no se desfasan.
2. Combate sin la bardo: `Combate_Base` + `Combate_Percusion`; `Combate_Melodia` cuando haya horda.
3. `SongBuff.OnSongStateChanged`:
   - `Singing`: suben `Bardo_Cancion_Melodia` y `Bardo_Cancion_Laud` (y, si se quiere, su batería y bajo en lugar de
     los de combate).
   - `Interrupted`: esas capas bajan a 0 al instante. El efecto `Cancion_Interrumpida_*` ya lo puede tocar solo el
     campo `Interrupt Clip` de `SongInterruptFeedback` en `Bardo.prefab`.
   - `Recovering`: vuelven con un fundido de la duración de la recuperación (`recoveryDuration`, 3 s por defecto).
4. Fuera de combate: `Ambiente_Pueblo` en su propio grupo del AudioMixer.

En la importación de Unity conviene **Load Type: Streaming** para la música y el ambiente.

## Efectos (`Audio/SFX/`, WAV mono, 48 kHz)

Cada categoría tiene 2 o 3 variantes distintas (para escoger una al azar) y un volumen de referencia común, con pico
máximo en −1 dBFS.

| Archivo | Cuándo | Gancho en el código |
|---|---|---|
| `Combo_Ataque_Debil_01..03` | Empieza un golpe débil | `PlayerCombat.StepStarted(paso)`, pasos 0 y 1 (`Light1`, `Light2`) |
| `Combo_Ataque_Fuerte_01..03` | Empieza un golpe fuerte | `PlayerCombat.StepStarted(paso)`, pasos 2 y 3 (`Heavy1`, `Heavy2`) |
| `Impacto_Debil_01..03`, `Impacto_Fuerte_01..03` | El golpe conecta con un enemigo | `PlayerCombat.EnemyHit(paso, enemigo, daño)` |
| `Giro_01..02` | Habilidad Giro (1,4 s, 3 vueltas) | `AbilityCaster.Cast(ranura, habilidad)` |
| `Explosion_01..02` | Explosión al final del Giro | No hay evento propio: 1,52 s después del `Cast` del Giro (`spinDuration` + `explosionDelay`) |
| `Embestida_01..02` | Habilidad Embestida (0,7 s) | `AbilityCaster.Cast(ranura, habilidad)` |
| `Dano_Recibido_01..03` | El caballero recibe daño | `Health.Damaged` del Knight |
| `Soldado_Muerte_01..02` | Muere un demonio soldado | `Health.Died` del soldado |
| `Cancion_Interrumpida_01..02` | Golpean a la bardo | Campo `Interrupt Clip` de `SongInterruptFeedback` (sin código) |

La variación de tono que pide #32 (±5 % en `AudioSource.pitch`) se suma bien a estas variantes.
