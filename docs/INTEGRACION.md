# Guía de integración

Para quien conecta sistemas entre sí: enemigos, hordas, flujo del nivel y la escena principal. Todo lo de aquí ya
existe en `main`; esta guía dice cómo engancharse sin tocar el código de los demás.

## 1. Dónde va cada cosa

| Qué | Dónde |
|---|---|
| Código de enemigos | `Scripts/Enemies/` (namespace `TheLastBeat.Enemies`) |
| Hordas y flujo del nivel | `Scripts/Level/` (namespace `TheLastBeat.Level`) |
| Prefabs de demonios | `Prefabs/Enemies/` |
| Datos de hordas (`WaveData`) | `ScriptableObjects/Waves/` |
| Héroes (Knight, Bardo) | `Prefabs/Heroes/` |
| Escenas | `Scenes/<Nombre>.unity` y, si tiene NavMesh, `Scenes/<Nombre>/NavMesh-*.asset` |

## 2. Qué necesita un enemigo para funcionar con el héroe

Un enemigo funciona con el combo, las tres habilidades y la huida de la bardo si cumple esto, sin tocar nada más:

1. Está en la capa **`Enemy`** y tiene un **collider que no es trigger** (los ataques buscan con `Physics.Overlap*`
   ignorando triggers).
2. Tiene un **`Health`** con equipo **`Demons`**, en el mismo objeto o en un padre del collider.
3. Para que reaccione al empuje, añade **`KnockbackReceiver`** (usa `NavMeshAgent`, `CharacterController` o `Transform`,
   y expone `IsBeingPushed` por si la IA debe esperar).
4. Al morir, `Health.Died` avisa. Un enemigo muerto deja de recibir golpes (`IsDead`).

Para que el enemigo dañe al héroe:

```csharp
var info = DamageInfo.Create(amount, gameObject, Team.Demons, hitPoint, direction, knockback);
target.TakeDamage(info);   // target: IDamageable del Knight o de la Bardo
```

`Interrupts` es `true` por defecto: un golpe a la bardo corta su canción al instante y, si ya estaba cortada, reinicia el
contador de recuperación (seguir golpeándola la mantiene sin cantar). Ponlo en `false` para daño que no debe interrumpir
(veneno, quemadura).

**Objetivos válidos**: el Knight está en la capa `Player` y la Bardo en `Ally` (`GameLayers.HeroesMask` agrupa ambas).
Ambos tienen `Health`. La bardo huye de cualquier enemigo vivo en la capa `Enemy` que entre en su radio de peligro.

**Pooling**: al reutilizar un enemigo, `Health.ResetHealth()` devuelve la vida y lo revive; `KnockbackReceiver` cancela
su empuje solo al desactivarse. Ni el combo ni las habilidades reservan memoria por golpe.

## 3. Poner al héroe y a la bardo en `Pueblo`

`Pueblo.unity` es de la línea B, así que esto se hace en su rama:

1. **Knight**: abre `Pueblo`, menú **The Last Beat > Hero > 2. Reemplazar el Knight de la escena por el prefab**.
   Conserva su posición y reenlaza la cámara de Cinemachine.
2. **Bardo**: arrastra `Prefabs/Heroes/Bardo.prefab` a la escena, cerca del Knight y **sobre el NavMesh** de Pueblo.
   Encuentra sola al `PlayerController`; no hay que asignar nada.
3. Dale Play: la bardo debe seguir al caballero y la canción potenciarlo (los multiplicadores están en `BuffReceiver`).

Menús de configuración: **The Last Beat > Habilidades** y **> Bardo** son seguros de repetir. **Hero > Crear escenario de
prueba de combate** solo sirve la primera vez; con el Knight ya como prefab solo reconfigura el FBX y el controller.

## 4. Eventos para el flujo del nivel

| Para | Qué escuchar |
|---|---|
| **Derrota** (bardo cae) | `BardCompanion.Died` |
| **Derrota** (caballero cae) | `Health.Died` del Knight |
| Estado de la canción (audio, VFX, UI) | `SongBuff.OnSongStateChanged`, `SongBuff.State`, `StateTimeRemaining` |
| Canción interrumpida (sonido, aviso), también golpes repetidos | `SongBuff.OnInterrupted(bool reiniciado)`; `SongInterruptFeedback` ya hace parpadear a la bardo. El sonido lo pone `GameSfx` (tarea 32): deja vacío su campo `Interrupt Clip` para no duplicarlo |
| HUD (vida, recargas y canción) | Arrastra `Prefabs/UI/Hud.prefab` a la escena: encuentra solo al caballero y a la bardo y se actualiza por eventos |
| Recargas de habilidades (HUD) | `AbilityCaster.CooldownRemaining(slot)`, `CooldownNormalized(slot)`, eventos `Cast`, `CooldownStarted`, `CooldownFinished` |
| Golpes del héroe a enemigos | `PlayerCombat.EnemyHit` |
| Sensación de impacto (pausa, sacudida, números de daño) | `ImpactFeedback.Hit(punto, daño, ImpactKind.X)` una vez por enemigo golpeado. Los valores se editan en `VFX/Resources/ImpactSettings.asset` (sin él usa valores por defecto). La cámara no necesita configuración: se añade sola el listener de Cinemachine |
| Efectos visuales (partículas con pooling) | `Vfx.Play(VfxKind.X, posición)` (una vez) o `Vfx.Follow(...)` + `Vfx.Stop(ref fx)` (acompañan a un objeto). Los prefabs viven en `VFX/Prefabs` y se registran en `VFX/Resources/VfxLibrary.asset`; para añadir uno nuevo, un valor en `VfxKind` y una entrada en la biblioteca |
| Caballero potenciado | `BuffReceiver.IsBuffed`, `BuffChanged`; los valores (daño, área de habilidades, esquiva) viven en el asset `ScriptableObjects/Buffs/PotenciacionDeCancion.asset` |
| Esquiva automática | `KnightAutoDodge.Dodged`, `DodgeCount` (es un `IDamageFilter` del Knight; el aviso lo da `DodgeFeedback`) |

## 5. Reiniciar el nivel sin recargar la escena

Todo el estado del héroe se puede limpiar con dos llamadas (la derrota o "Reintentar" de la tarea 35):

```csharp
knight.Respawn(spawnPoint.position, spawnPoint.rotation);  // vida, posición, golpes, habilidades y recargas
bard.Revive(spawnPoint.position + offset);                  // vida, posición y vuelve a cantar
```

`Respawn` llama a `ResetForRestart()` de todo componente que implemente `IRespawnable` (hoy `PlayerCombat` y
`AbilityCaster`). Si añades algo con estado al héroe, impleméntalo ahí. Recargar la escena también funciona.

## 6. Capas y colisiones

Las capas están en `Scripts/Core/GameLayers.cs` y deben coincidir con **Project Settings > Tags and Layers**.
El Knight ignora la capa `Ally` al moverse (`CharacterController.excludeLayers`) para que la bardo no lo bloquee.

> Aviso: la matriz de colisiones física (`Project Settings > Physics`) está con todas las capas activadas entre sí;
> el commit de la tarea 3 no la cambió. Si con muchos enemigos se empujan entre ellos, desactiva allí `Enemy` con `Enemy`.
