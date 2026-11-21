#!/usr/bin/env bash
# Crea en GitHub las etiquetas, los hitos (milestones), los issues y el tablero
# de GitHub Projects de "El Último Compás" a partir de tareas.json.
# Equivalente en Bash de crear-planeacion.ps1 (Linux/macOS), con asignación
# de responsables (equipo.json) y cierre de tareas ya hechas (--cerrar-hechas).
#
# REQUISITOS
#   - GitHub CLI (gh) y python3.
#   - Sesión iniciada con permiso de proyectos (una sola vez):
#       gh auth login
#       gh auth refresh -s project
#   - Un repositorio NUEVO y sin issues, para que el issue #N sea la tarea N.
#   - Las personas de equipo.json deben ser colaboradoras del repositorio.
#
# EJEMPLOS
#   Ver qué haría, sin crear nada:
#     bash crear-planeacion.sh --repo EvilandAoki/The-Last-Beat --fecha-final 2026-12-10 --dry-run
#   Crear todo:
#     bash crear-planeacion.sh --repo EvilandAoki/The-Last-Beat --fecha-final 2026-12-10
#   Cerrar las tareas que ya están hechas en main (commits con "closes #N"):
#     bash crear-planeacion.sh --repo EvilandAoki/The-Last-Beat --cerrar-hechas
#   Reanudar la creación de issues desde la tarea 40 (si falló a medias):
#     bash crear-planeacion.sh --repo EvilandAoki/The-Last-Beat --desde 40

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO=""
FECHA_FINAL=""
TITULO="El Último Compás"
TAREAS="$SCRIPT_DIR/tareas.json"
EQUIPO="$SCRIPT_DIR/equipo.json"
DRY=0
CERRAR=0
DESDE=1
SIN_ASIGNAR=""

die() { echo "Error: $*" >&2; exit 1; }

uso() {
  sed -n '2,/^$/p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
  cat <<'EOF'
OPCIONES
  --repo dueño/repo     (obligatorio)
  --fecha-final AAAA-MM-DD
  --titulo "Nombre del proyecto"
  --tareas ruta.json    (por defecto tareas.json junto al script)
  --equipo ruta.json    (por defecto equipo.json junto al script)
  --sin-asignar A,B,C   líneas cuyos issues se crean sin assignee (p. ej. aún no es colaborador)
  --desde N             reanuda la creación de issues desde la tarea N
  --cerrar-hechas       cierra los issues con "closes #N" en main y termina
  --dry-run             muestra los comandos sin ejecutarlos
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --repo)          REPO="${2:-}"; shift 2 ;;
    --fecha-final)   FECHA_FINAL="${2:-}"; shift 2 ;;
    --titulo)        TITULO="${2:-}"; shift 2 ;;
    --tareas)        TAREAS="${2:-}"; shift 2 ;;
    --equipo)        EQUIPO="${2:-}"; shift 2 ;;
    --desde)         DESDE="${2:-}"; shift 2 ;;
    --sin-asignar)   SIN_ASIGNAR="${2:-}"; shift 2 ;;
    --cerrar-hechas) CERRAR=1; shift ;;
    --dry-run)       DRY=1; shift ;;
    -h|--help)       uso; exit 0 ;;
    *)               die "opción desconocida: $1 (usa --help)" ;;
  esac
done

[[ -n "$REPO" ]] || die "falta --repo dueño/repositorio"
[[ "$REPO" == */* ]] || die "--repo debe tener la forma dueño/repositorio"
[[ "$DESDE" =~ ^[0-9]+$ ]] || die "--desde debe ser un número"
[[ -z "$FECHA_FINAL" || "$FECHA_FINAL" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}$ ]] || die "--fecha-final debe ser AAAA-MM-DD"
[[ -f "$TAREAS" ]] || die "no existe $TAREAS"
[[ -f "$EQUIPO" ]] || die "no existe $EQUIPO"
command -v python3 >/dev/null || die "se necesita python3"
(( DRY )) || command -v gh >/dev/null || die "se necesita GitHub CLI (gh)"

OWNER="${REPO%%/*}"

# Ejecuta gh, o solo lo muestra en --dry-run.
gh_run() {
  if (( DRY )); then printf '  gh %s\n' "$*" >&2; return 0; fi
  gh "$@"
}

# --- Cerrar tareas ya hechas -------------------------------------------------
if (( CERRAR )); then
  (( DRY )) || gh auth status >/dev/null 2>&1 || die "inicia sesión con: gh auth login"
  REPO_DIR="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel)"
  echo "== Cerrando tareas hechas (closes #N en main) =="
  git -C "$REPO_DIR" log main --format='%h%x1f%B%x1e' |
    python3 -I -c '
import re, sys
vistos = {}
for bloque in sys.stdin.read().split("\x1e"):
    if "\x1f" not in bloque:
        continue
    h, cuerpo = bloque.strip().split("\x1f", 1)
    for n in re.findall(r"closes\s+#(\d+)", cuerpo, re.I):
        vistos.setdefault(int(n), h)
for n in sorted(vistos):
    print(n, vistos[n])
' |
  while read -r n hash; do
    gh_run issue close "$n" --repo "$REPO" --reason completed --comment "Completada en $hash (main)." >/dev/null
    echo "  #$n cerrada ($hash)"
  done
  echo "Listo."
  exit 0
fi

# --- Comprobaciones ------------------------------------------------------------
if (( ! DRY )); then
  gh auth status >/dev/null 2>&1 || die "inicia sesión con: gh auth login"
  if (( DESDE == 1 )); then
    existentes="$(gh issue list --repo "$REPO" --state all --limit 1 --json number --jq length)"
    [[ "$existentes" == "0" ]] || die "el repositorio ya tiene issues. Usa uno nuevo para que el issue #N coincida con la tarea N (o --desde N para reanudar)."
  fi
  for u in $(python3 -I -c 'import json,sys; omit=sys.argv[2].split(","); print(" ".join(u for k,u in json.load(open(sys.argv[1], encoding="utf-8")).items() if k not in omit))' "$EQUIPO" "$SIN_ASIGNAR"); do
    gh api "users/$u" --silent 2>/dev/null || die "el usuario de GitHub '$u' no existe (revisa equipo.json)"
    gh api "repos/$REPO/collaborators/$u" --silent 2>/dev/null || die "'$u' no es colaborador de $REPO. Invítalo en Settings > Collaborators y vuelve a ejecutar."
  done
fi

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

HITO1="Entrega 1 (25 nov)"
HITO2="Entrega final"
[[ -z "$FECHA_FINAL" ]] || HITO2="Entrega final ($FECHA_FINAL)"

# Prepara cuerpos de issues e índice (n, título, etiquetas, hito, responsable).
python3 -I - "$TAREAS" "$EQUIPO" "$WORK" "$HITO1" "$HITO2" "$SIN_ASIGNAR" <<'PY'
import json, sys
tareas = json.load(open(sys.argv[1], encoding="utf-8"))
equipo = json.load(open(sys.argv[2], encoding="utf-8"))
out, hito1, hito2 = sys.argv[3], sys.argv[4], sys.argv[5]
sin_asignar = set(sys.argv[6].split(","))
hitos = {1: hito1, 2: hito2}
total = len(tareas)
filas = []
for t in tareas:
    n = t["n"]
    titulo = ("🏁 " if t.get("hito") else "") + t["titulo"]
    resp = t.get("responsable")
    usuario = equipo.get(resp, "") if resp else ""
    hito = hitos[int(t["entrega"])]
    cuerpo = [t["descripcion"], "", "### Hecho cuando"]
    cuerpo += ["- [ ] " + c for c in t["hecho"]]
    if t["depende"]:
        cuerpo += ["", "**Depende de:** " + ", ".join("#%d" % d for d in t["depende"])]
    if usuario:
        cuerpo += ["", "**Responsable:** @" + usuario]
    cuerpo += ["", "_Tarea %d de %d · %s_" % (n, total, hito), ""]
    with open("%s/body_%d.md" % (out, n), "w", encoding="utf-8") as f:
        f.write("\n".join(cuerpo))
    labels = list(t["areas"])
    if t.get("sinCodigo"): labels.append("sin-código")
    if t.get("hito"): labels.append("hito")
    if resp: labels.append("linea-" + resp)
    asignado = "" if resp in sin_asignar else usuario
    filas.append("\t".join([str(n), titulo, ",".join(labels), hito, asignado]))
with open(out + "/indice.tsv", "w", encoding="utf-8") as f:
    f.write("\n".join(filas) + "\n")
PY

# --- Etiquetas -------------------------------------------------------------------
echo
echo "== Etiquetas =="
ETIQUETAS=(
  "setup|6e7781|Configuración del proyecto y del repositorio"
  "diseño|d4c5f9|Historia, guion y diseño de juego"
  "arte-3d|f9d0c4|Concept art, Tripo3D y modelos"
  "animación|fbca04|Rig, Mixamo y Animator"
  "programación|1d76db|Código de gameplay y sistemas"
  "ia|5319e7|IA de compañeros y enemigos"
  "nivel|0e8a16|Escenario, NavMesh y flujo del nivel"
  "audio|b60205|Música, efectos de sonido e integración"
  "vfx|ff7619|Efectos visuales y partículas"
  "ui|c2e0c6|Interfaz y menús"
  "qa|bfd4f2|Pruebas, balance y corrección de bugs"
  "entrega|000000|Builds, README y entregas"
  "sin-código|ededed|Tarea que no produce código"
  "hito|e99695|Última tarea de una entrega"
  "linea-A|0366d6|Línea A: héroes y combate"
  "linea-B|d73a4a|Línea B: enemigos, nivel e integración"
  "linea-C|28a745|Línea C: arte, animación, audio y narrativa"
)
for e in "${ETIQUETAS[@]}"; do
  IFS='|' read -r nombre color desc <<<"$e"
  gh_run label create "$nombre" --repo "$REPO" --color "$color" --description "$desc" --force >/dev/null
  echo "  $nombre"
done

# --- Hitos (milestones) ----------------------------------------------------------
echo
echo "== Hitos =="
if (( DESDE == 1 )); then
  gh_run api "repos/$REPO/milestones" -f "title=$HITO1" \
    -f "description=Avance del 30 al 50 %: caballero jugable, bardo con IA mínima y hordas de soldados." \
    -f "due_on=2026-11-26T04:59:00Z" >/dev/null
  echo "  $HITO1"
  hito2_args=(-f "title=$HITO2" -f "description=Nivel completo con los 4 héroes, todos los enemigos y el miniboss. Demo y sustentación.")
  if [[ -n "$FECHA_FINAL" ]]; then
    # 11:59 p. m. hora de Colombia = 04:59 UTC del día siguiente
    hito2_args+=(-f "due_on=$(date -u -d "$FECHA_FINAL + 1 day" +%Y-%m-%d)T04:59:00Z")
  fi
  gh_run api "repos/$REPO/milestones" "${hito2_args[@]}" >/dev/null
  echo "  $HITO2"
else
  echo "  (omitidos: --desde $DESDE)"
fi

# --- Issues --------------------------------------------------------------------
echo
echo "== Issues =="
while IFS=$'\t' read -r n titulo labels hito asignado; do
  (( n >= DESDE )) || continue
  args=(issue create --repo "$REPO" --title "$titulo" --body-file "$WORK/body_$n.md"
        --label "$labels" --milestone "$hito")
  [[ -z "$asignado" ]] || args+=(--assignee "$asignado")
  url="$(gh_run "${args[@]}")"
  if (( ! DRY )); then
    numero="${url##*/}"
    [[ "$numero" == "$n" ]] || die "se creó el issue #$numero pero se esperaba #$n. Revisa el repo; reanuda con --desde $((n + 1)) si procede."
  fi
  printf '  #%-3s %s%s\n' "$n" "$titulo" "${asignado:+  → @$asignado}"
done <"$WORK/indice.tsv"

# --- Tablero de GitHub Projects -----------------------------------------------
echo
echo "== Proyecto =="
if (( DRY )); then
  gh_run project create --owner "$OWNER" --title "$TITULO" --format json >/dev/null
  gh_run project link "<número>" --owner "$OWNER" --repo "$REPO" >/dev/null
  echo "  (en ejecución real se añaden todos los issues del repo)"
else
  numero="$(gh project list --owner "$OWNER" --format json --jq ".projects[] | select(.title==\"$TITULO\") | .number" | head -n1)"
  if [[ -z "$numero" ]]; then
    numero="$(gh project create --owner "$OWNER" --title "$TITULO" --format json --jq .number)"
  fi
  gh project link "$numero" --owner "$OWNER" --repo "$REPO" >/dev/null
  total=0
  while read -r url; do
    gh project item-add "$numero" --owner "$OWNER" --url "$url" >/dev/null
    total=$((total + 1))
  done < <(gh issue list --repo "$REPO" --state all --limit 500 --json url --jq '.[].url')
  echo "  Proyecto #$numero con $total tareas, enlazado a $REPO"
fi

echo
echo "Listo."
