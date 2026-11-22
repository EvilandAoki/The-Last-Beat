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

## 3. Reemplazar a la bardo

`Prefabs/Heroes/Bardo.prefab` es una cápsula con todo el comportamiento en la raíz (`NavMeshAgent`, `BardCompanion`, `SongBuff`,
`Health`). Pon tu modelo como hijo y desactiva el `MeshRenderer` de la cápsula; **deja intactos el collider y los componentes**.
Los parpadeos y el icono de la canción usan los renderers de los hijos, así que funcionan con tu material.

## 4. Audio: ganchos listos

| Qué suena | Dónde engancharlo |
|---|---|
| Canción interrumpida | Arrastra el clip al campo `Interrupt Clip` de `SongInterruptFeedback` en `Bardo.prefab`. Suena solo, sin código |
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
