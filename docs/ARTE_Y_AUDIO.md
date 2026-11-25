# Guía de arte, animación y audio

Para quien reemplaza el héroe y la bardo provisionales por los modelos reales y añade música y efectos de sonido.
Todo lo de aquí ya funciona en `main`; esta guía dice dónde engancharse sin tocar código.

## 1. Reglas de archivos

- Modelos en `Assets/_Project/Art/Models/<Nombre>/` (FBX, texturas y materiales juntos). Concept art en `Art/ConceptArt/`.
- Música en `Audio/Music/` y efectos en `Audio/SFX/`.
- **Extensiones en minúscula** (`.png`, `.jpg`, `.fbx`, `.wav`, `.ogg`): Git LFS solo reconoce esas, y un `.PNG` o `.JPEG`
  se subiría como archivo normal. Tras clonar: `git lfs install && git lfs pull`.
- Siempre commitea el `.meta` de cada archivo.

## 2. Reemplazar al caballero

Medidas y orientación: **1.8 m de alto, origen en los pies, mirando a +Z**. Rig **Humanoid** (el de Mixamo sirve).

1. Importa el FBX. En su Inspector: **Rig → Animation Type: Humanoid → Create From This Model → Configure** y comprueba que
   todos los huesos salen en verde.
2. Abre `Prefabs/Heroes/Knight.prefab`. **Borra** el hijo `Modelo_Heroe` (no basta con desactivarlo) y arrastra tu modelo como hijo, en posición 0,0,0.
3. A tu modelo añádele un **Animator** con `Art/Models/Hero/Hero.controller` (**Apply Root Motion desactivado**) y el componente
   **`CombatAnimationEvents`**.
4. Asigna ese Animator al campo `Animator` de los tres componentes que lo usan: `PlayerController`, `PlayerCombat` y `AbilityCaster`.

### Animaciones: los nombres importan

`Hero.controller` tiene estos estados, que el código activa por nombre. Para cambiar una animación, abre el controller y arrastra
tu clip al campo **Motion** del estado:

| Estado | Cuándo | En bucle |
|---|---|---|
| `Idle`, `Run` | quieto / corriendo (parámetro `Speed`) | sí |
| `Light1`, `Light2` | ataque débil (J) | no |
| `Heavy1`, `Heavy2` | ataque fuerte (K) | no |
| `Spin` | durante el Giro (E); el código rota el modelo | sí |
| `Charge` | durante la Embestida (Q) | sí |

### El evento de impacto (importante)

Cada clip de ataque (`Light1`, `Light2`, `Heavy1`, `Heavy2`) lleva un **Animation Event llamado `OnHit`** en el frame en que el
golpe conecta. Sin él, el ataque se anima pero **no hace daño**. En el Inspector del FBX: pestaña **Animation → selecciona el clip
→ Events → añade `OnHit`** en el frame del golpe. La duración del clip es la del golpe y el combo se encadena por ella.

## 3. La bardo (modelo de Tripo3D ya integrado)

`Prefabs/Heroes/Bardo.prefab` ya lleva el modelo 3D de la bardo con su guitarra: `Art/Models/Bard/Bard.fbx` (esqueleto Humanoid
`mixamorig:*`, unos 16 000 triángulos entre cuerpo, guitarra y correa) como hijo `Modelo`, con `Bard.controller` y el componente
`BardAnimator` en la raíz. La cápsula quedó con el `MeshRenderer` apagado y el collider (0.4 x 1.7 m) apoyado en el suelo; los
componentes de comportamiento (`NavMeshAgent`, `BardCompanion`, `SongBuff`, `Health`) no cambian.

- **Animator**: parámetros `Speed` (float), `Singing` (bool), `Hit`, `Die` y `Attack` (triggers; `Attack` no se usa, la bardo no ataca).
  `BardAnimator` los mueve solo: velocidad del agente, estado de `SongBuff`, `Health.Damaged` y `BardCompanion.Died`. Al revivir
  (`BardCompanion.Revive`) reinicia el Animator.
- **Clips**: `Idle`, `Run`, `Sing` (bucle de 2 tiempos a 100 BPM), `Hit` y `Die`. La guitarra va rígida al torso (cuelga de una correa) y
  las manos la agarran con IK, así que puede correr o caminar tocando. En `Hit` y `Die` las manos se sueltan.
- **Si cambia el modelo**: el FBX se genera con `Tools/Blender/build_bard_tripo.py` a partir de los FBX de Tripo guardados en
  `Tools/Blender/fuentes/Bard/` (cuerpo y guitarra; el comando completo está en la cabecera del script). Después, en Unity,
  ejecuta **The Last Beat > Bardo > Instalar modelo 3D**: reconfigura el rig Humanoid, los clips, el controller, los materiales con
  textura y el prefab. Es repetible.
- **Para reemplazarla por otro modelo**: pon el tuyo como hijo y desactiva el `MeshRenderer` de la cápsula; deja intactos el collider
  y los componentes. Los parpadeos y el icono de la canción usan los renderers de los hijos, así que funcionan con tu material.

## 4. Audio: ganchos listos

| Qué suena | Dónde engancharlo |
|---|---|
| Canción interrumpida | Ya lo reproduce `GameSfx` (tarea 32) desde `SongBuff.OnInterrupted`, con los clips `Cancion_Interrumpida_*`. **Deja vacío el campo `Interrupt Clip` de `SongInterruptFeedback`**: si se llena, el sonido se oiría dos veces |
| Capas de música según la canción | `SongBuff.OnSongStateChanged(SongState)`: `Singing`, `Interrupted`, `Recovering` |
| Cada vez que la golpean | `SongBuff.OnInterrupted(bool reiniciado)` (también con golpes repetidos) |
| Inicio de cada golpe del combo | `PlayerCombat.StepStarted(int)` |
| Golpe que conecta con un enemigo | `PlayerCombat.EnemyHit(golpe, enemigo, daño)` |
| Habilidad lanzada | `AbilityCaster.Cast(ranura, habilidad)` |

## 5. Iconos y efectos

- **Iconos de habilidades**: asigna un `Sprite` al campo `Icon` del asset de la habilidad (`ScriptableObjects/Abilities/`). El HUD
  lo muestra en su ranura sin tocar nada más.
- **Efectos de partículas**: viven en `VFX/Prefabs` y se registran en `VFX/Resources/VfxLibrary.asset`. Colores: azul el
  caballero, morado la bardo. Cada sistema tiene `maxParticles`: no lo subas sin necesidad.
