# The Last Beat (El Último Compás)

Juego de acción con cámara cenital en Unity: un caballero, una bardo cuya canción potencia a sus aliados,
y hordas de demonios en un pueblo corrupto. Proyecto de equipo de 3 personas con dos entregas.

## Requisitos

- **Unity 6000.3.28f1** (Unity 6.3), render pipeline **URP**.
- **Git** y **Git LFS** (los modelos, texturas y audio van por LFS).
- Una llave SSH asociada a tu cuenta de GitHub.

## Cómo empezar

```bash
git clone git@github.com:EvilandAoki/The-Last-Beat.git
cd The-Last-Beat
git lfs install
git lfs pull
```

Abre la carpeta con Unity Hub (**Add → Add project from disk**). Debe detectar la versión `6000.3.28f1` sola.
Si ves errores como `could not be imported because the format is unsupported`, falta `git lfs pull`.

Escena principal: `Assets/_Project/Scenes/Pueblo.unity`.

## Estructura

```
Assets/_Project/
  Scripts/
    Core/        capas (GameLayers), IRespawnable
    Combat/      Health, DamageInfo, KnockbackReceiver
    Player/      PlayerController, PlayerCombat, PlayerInputReader
    Abilities/   AbilityData y las habilidades del caballero
    Companions/  BardCompanion (IA) y SongBuff (canción)
    Buffs/       BuffReceiver y BuffEffect (la potenciación de la canción)
    UI/          GameHud (vida, recargas y canción)
    VFX/         Vfx (pool de partículas) y VfxLibrary
    Debug/       herramientas de prueba (TheLastBeat.DebugTools)
    Editor/      menús de configuración (The Last Beat > ...)
  Scenes/        Pueblo.unity (la principal) y Test_Combo.unity (pruebas); cada escena con su NavMesh
  Prefabs/       Heroes/ (Knight, Bardo)  Enemies/ (para los demonios)  UI/ (Hud)
  ScriptableObjects/  Abilities/  Buffs/  (y Waves/ para las hordas)
  Art/{Models,ConceptArt}   Audio/{Music,SFX}   Input/
  VFX/           Prefabs/  Materials/  Textures/  Resources/ (VfxLibrary)
Tools/Blender/     genera el héroe provisional (build_hero.py)
Tools/Planeacion/  scripts que crean issues, hitos y tablero en GitHub
docs/              guías del equipo
```

Convención: una carpeta por área dentro de `Scripts/` y el namespace `TheLastBeat.<Área>`.
Guías según tu línea de trabajo:
- Enemigos, hordas, flujo del nivel y escena principal: [docs/INTEGRACION.md](docs/INTEGRACION.md).
- Modelos, animaciones, audio e iconos: [docs/ARTE_Y_AUDIO.md](docs/ARTE_Y_AUDIO.md).

## Capas físicas

Se resuelven por nombre desde `Assets/_Project/Scripts/Core/GameLayers.cs`. Deben coincidir con
**Project Settings → Tags and Layers**.

| Capa | Uso |
|---|---|
| `Ground` | Suelo y obstáculos que bloquean cargas, proyectiles y línea de visión |
| `Player` | El caballero (héroe controlado por el jugador) |
| `Ally` | Compañeros controlados por IA (bardo, etc.) |
| `Enemy` | Demonios |
| `Projectile` | Proyectiles. Se usa en lugar de la capa `Hitbox` que mencionaba la tarea 3 |

## Controles

| Acción | Teclado y ratón | Mando |
|---|---|---|
| Mover | WASD o flechas | Stick izquierdo |
| Ataque | Clic izquierdo o `J` | Botón oeste |
| Habilidad 1 | `Q` o clic derecho | Botón norte |
| Habilidad 2 | `E` | Botón este |
| Pausa | `Esc` | Start |

Definidos en `Assets/_Project/Input/GameControls.inputactions`.

## Equipo y tablero

Las 71 tareas están como issues en GitHub, agrupadas en dos hitos (**Entrega 1** y **Entrega final**) y en el
tablero **El Último Compás** (pestaña *Projects* del repositorio). Cada persona tiene una línea de trabajo
con pocas dependencias entre sí.

| Línea | Persona | Alcance |
|---|---|---|
| A | `EvilandAoki` | Héroes y combate: caballero, habilidades, bardo, canción, VFX de habilidades, HUD y diálogos |
| B | `kev405` | Enemigos, nivel e integración: IA de demonios, hordas, flujo del nivel, escena principal, builds |
| C | `alejandro9481` | Arte, animación, audio y narrativa: Tripo3D, Mixamo, música y efectos, guion |

Filtra los issues por etiqueta `linea-A`, `linea-B` o `linea-C`. El flujo de trabajo está en
[CONTRIBUTING.md](CONTRIBUTING.md).

## Regenerar la planeación

Solo en un repositorio nuevo y sin issues. Detalles en `Tools/Planeacion/crear-planeacion.sh`
(Linux/macOS) y `Tools/Planeacion/crear-planeacion.ps1` (Windows).
