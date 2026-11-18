# Cómo trabajamos

Somos tres personas con tres líneas de trabajo (A, B, C; ver el [README](README.md)). El objetivo es
no pisarnos: cada quien avanza en lo suyo y las tareas de integración las hace una sola persona.

## Flujo de una tarea

1. Toma un issue de tu línea (etiqueta `linea-X`) y revisa su sección **Depende de**. Si una dependencia
   no está cerrada, avisa en el issue en lugar de adelantarte.
2. Actualiza `main` y crea tu rama: una por tarea, nombrada `tarea-N-descripcion-corta`.
   ```bash
   git switch main && git pull
   git switch -c tarea-8-combo-basico
   ```
3. Trabaja en commits pequeños. Cierra el trabajo con un commit que diga `closes #N`.
4. Sube la rama y abre un **pull request hacia `main`**. Usa la plantilla y pide revisión a otra persona.
5. Tras fusionar, borra la rama.

No se trabaja directo en `main`.

## Evitar conflictos en Unity

- **Escenas y prefabs no se fusionan bien.** Nadie edita `Pueblo.unity` salvo su dueño (línea B).
  Para probar tus cosas crea una escena propia en `Assets/_Project/Scenes/Test_<tarea>.unity` o trabaja
  con prefabs, y la línea B los integra en la escena principal.
- **Siempre commitea los `.meta`.** Un archivo sin su `.meta` genera un GUID distinto en cada máquina y
  rompe las referencias.
- **No commitees cambios automáticos de Unity** en `Packages/`, `ProjectSettings/` ni `Assets/Settings/`
  salvo que sean intencionales y estén acordados. Revisa `git status` antes de cada commit. Para descartar:
  `git checkout -- <archivo>`.
- Haz `git pull` **antes** de abrir Unity, no con el editor abierto.
- Si dos personas necesitan el mismo prefab, coordinen por el issue.

## Archivos pesados (Git LFS)

Modelos (`.fbx`, `.obj`, `.blend`…), imágenes, audio y video van por LFS según `.gitattributes`. Tras clonar:
`git lfs install && git lfs pull`. Usa extensiones en **minúscula** (`.png`, `.jpg`, `.wav`): `.JPEG` o `.PNG`
no coinciden con las reglas y se subirían como archivos normales.

## Código

- Namespace `TheLastBeat.*` y una carpeta por área en `Assets/_Project/Scripts/`.
- Los scripts de editor van en `Scripts/Editor/`.
- La consola debe quedar sin errores al entrar en Play.

## Commits

Mensajes en español, en imperativo y con el número de tarea cuando aplique:
`Combo básico de 3 golpes (closes #8)`.
