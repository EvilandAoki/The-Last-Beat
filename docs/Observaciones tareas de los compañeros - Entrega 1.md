# Observaciones sobre las tareas de los compañeros (Entrega 1)

Revisión del repositorio del grupo (`EvilandAoki/The-Last-Beat`, rama `main` en el commit `e242655`, 23 de noviembre
a las 11 p. m.) contra los criterios de "hecho" de cada issue (`Tools/Planeacion/tareas.json`). Solo se anotan
diferencias: no se cambió nada del trabajo de Sebastian ni de Alejandro.

## Resumen

| Tarea | Responsable | Estado en el tablero | Lo que hay en `main` |
|---|---|---|---|
| #8 Combo básico de 3 golpes | Sebastian | Done | Completa |
| #9 Sistema de habilidades | Sebastian | Done | Completa |
| #10 Giro con explosión final | Sebastian | Done | Completa |
| #11 Embestida | Sebastian | Done | Completa |
| **#12 Sensación de impacto** | Sebastian | Todo | **No está** |
| #16 Bardo: IA mínima | Sebastian | Done | Completa |
| #17 Canción de la bardo | Sebastian | Done | Completa |
| #18 Interrupción de la canción | Sebastian | Done | Completa; el sonido lo pone la tarea 32 |
| #19 Potenciación en el caballero | Sebastian | Done | Completa |
| #33 Efectos visuales básicos | Sebastian | Done | Completa |
| #34 HUD | Sebastian | Done | Completa, con dos observaciones |
| #21 Concept art | Alejandro | Todo (ya fusionada, PR #86) | Completa |
| **#22 a #27 Modelos y rigs** | Alejandro | Todo | **No están** |
| **#28 Importar personajes** | Alejandro | Todo | **No está** |
| #29 Canción de la bardo (dembow) | Alejandro | Todo (ya fusionada, PR #87) | Completa |
| #30 Música por capas y ambiente | Alejandro | Todo (ya fusionada, PR #87) | Completa |
| #31 Efectos de sonido | Alejandro | Todo (ya fusionada, PR #87) | Completa |

## Lo que falta

### #12 Sensación de impacto (Sebastian)

No hay nada de esta tarea en `main`: ni pausa de impacto (HitStop), ni sacudida de cámara con
`CinemachineImpulseSource`, ni números de daño con pool (`DamageNumberPool`). Los tres criterios quedan pendientes:

- El hit stop no se acumula si llegan varios golpes juntos.
- La sacudida es más fuerte en el tercer golpe y en la explosión.
- Los números de daño se reutilizan desde un pool.

Efecto en el juego: los golpes contra los soldados solo se notan por las chispas de `Vfx` (tarea 33), el empuje de
`KnockbackReceiver` y el sonido (tarea 32). Los soldados no parpadean al recibir daño, porque ese destello tampoco
existe todavía (en `main` solo lo tiene el maniquí de pruebas `TrainingDummy`).

### #22 a #28 Modelos, rigs e importación (Alejandro)

En `main` no hay ningún modelo de Tripo3D ni animación de Mixamo:

- **#22, #23 y #24:** no están los modelos del caballero con el espadón, de la bardo con el laúd ni del soldado con la
  cimitarra. En `Art/Models` solo están el héroe provisional de Blender (`Hero/`) y el material de la cápsula de la
  bardo (`Bard/`).
- **#25, #26 y #27:** no hay rigs ni animaciones. El caballero usa los clips del héroe provisional (Idle, Run, Light1,
  Light2, Heavy1, Heavy2, Spin y Charge) y **no tiene animaciones de recibir golpe ni de morir**: al caer, se queda de pie.
- **#28:** no hay rigs Humanoid, ni Animator con los parámetros `Speed`, `Attack`, `Hit` y `Die`, ni armas en la mano.
  La bardo y el soldado siguen siendo cápsulas. El soldado de la tarea 13 ya escribe `Speed` y dispara `Attack`, `Hit`
  y `Die` si se le asigna un Animator, así que la #28 puede engancharse sin tocar su código.

Como la tarea 36 (integración y prueba completa) depende de la 28, la primera entrega va con el héroe provisional y
las dos cápsulas.

## Observaciones sobre tareas completas

### #34 HUD (Sebastian)

- **No tiene un estado "sin cantar todavía".** El nivel empieza con la canción apagada (el pueblo en silencio, tarea 20)
  y el icono de la canción muestra el estado ámbar de "recuperándose" con el temporizador vacío hasta que empieza el
  combate. Es correcto según los estados que existen (`Singing`, `Interrupted`, `Recovering`), pero puede confundir.
- **La barra del caballero es roja** (`knightColor`), mientras el resto del juego usa azul para el caballero (los efectos
  de la tarea 33 y el diseño). No es un criterio de la tarea.
- No muestra la horda actual. No es un criterio de la #34; lo cubre el contador de la tarea 20.

### #18 Interrupción de la canción (Sebastian)

El criterio "se nota claramente en pantalla y en el audio" queda cubierto: la bardo parpadea en rojo, sale el efecto
`SongBreak` y el HUD lo marca. El campo `Interrupt Clip` de `SongInterruptFeedback` está vacío en `Bardo.prefab`; el
sonido `Cancion_Interrumpida_*` lo reproduce la integración de audio (tarea 32) desde `SongBuff.OnInterrupted`. Si
alguien llena también ese campo, el sonido se oirá dos veces.

### #21, #29, #30 y #31 (Alejandro)

Cumplen sus criterios: una imagen por personaje y por arma; canción original de 32 s en bucle, completa y por pistas;
capas de combate con el mismo tempo y duración y ambiente en bucle; dos o tres variantes por efecto, `CREDITS.md` y
volúmenes normalizados. Lo único pendiente es administrativo: en el tablero siguen en **Todo** aunque sus PR ya se
fusionaron. Si los commits de `closes #N` no cerraron los issues, hay que cerrarlos y moverlos a Done a mano.

## Otras cosas del repositorio

- **Commits de `kev405` sin correo.** Los commits de las tareas 1 a 7 tienen autor `kev405 <>`. GitHub no los asocia a
  ninguna cuenta, así que no cuentan como contribuciones de Kevin. Puede afectar el criterio "commits de los tres
  integrantes" de la tarea 38. Los commits nuevos de la línea B ya salen con el correo de GitHub de Kevin (ver la guía).
- **Pausa.** El README lista la pausa (`Esc` o Start) y la acción existe en `GameControls`, pero nadie escucha
  `PlayerInputReader.PausePressed`: no hace nada. No es criterio de ninguna tarea de la primera entrega. El README de
  la tarea 37 lo aclara.
- **El caballero no se detiene al morir.** Nada en el código del caballero reacciona a su muerte. El flujo del nivel
  (tarea 20) desactiva el control del jugador cuando cae, así que en el juego no se nota.
- **Rama `Witch_Character_Test_1`.** Sigue abierta sin fusionar (prueba de la bruja). No afecta a la entrega.
- **Hacer público el repositorio (tarea 38).** El dueño es `EvilandAoki`: solo Sebastian puede cambiar la visibilidad
  en Settings.
