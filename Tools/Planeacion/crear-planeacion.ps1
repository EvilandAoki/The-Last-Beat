<#
.SYNOPSIS
  Crea en GitHub las etiquetas, los hitos (milestones), los issues y el tablero
  de GitHub Projects de "El Último Compás" a partir de tareas.json.

.REQUISITOS
  1. GitHub CLI instalado: winget install --id GitHub.cli
  2. Sesión iniciada con permiso de proyectos (una sola vez):
       gh auth login
       gh auth refresh -s project
  3. Un repositorio NUEVO y sin issues, para que el issue #N sea la tarea N.

.EJEMPLOS
  # Ver qué haría, sin crear nada:
  .\crear-planeacion.ps1 -Repo kevin/el-ultimo-compas -DryRun

  # Crear todo:
  .\crear-planeacion.ps1 -Repo kevin/el-ultimo-compas -FechaFinal 2026-12-10
#>
param(
  [Parameter(Mandatory = $true)][string]$Repo,          # dueño/repositorio
  [string]$FechaFinal = "",                             # AAAA-MM-DD de la entrega final (opcional)
  [string]$TituloProyecto = "El Último Compás",
  [string]$ArchivoTareas = (Join-Path $PSScriptRoot "tareas.json"),
  [switch]$DryRun
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$OutputEncoding = [Text.Encoding]::UTF8
$utf8SinBom = New-Object Text.UTF8Encoding $false

$Owner = $Repo.Split("/")[0]
$tareas = Get-Content -Raw -Encoding UTF8 $ArchivoTareas | ConvertFrom-Json

function Invoke-Gh {
  param([string[]]$GhArgs)
  if ($DryRun) { Write-Host "  gh $($GhArgs -join ' ')" -ForegroundColor DarkGray; return "" }
  $salida = & gh @GhArgs
  if ($LASTEXITCODE -ne 0) { throw "Falló: gh $($GhArgs -join ' ')" }
  return $salida
}

# --- Comprobaciones -----------------------------------------------------------
if (-not $DryRun) {
  & gh auth status | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "Inicia sesión con: gh auth login" }
  $existentes = & gh issue list --repo $Repo --state all --limit 1 --json number | ConvertFrom-Json
  if ($existentes.Count -gt 0) {
    throw "El repositorio ya tiene issues. Usa uno nuevo para que el issue #N coincida con la tarea N."
  }
}

# --- Etiquetas -------------------------------------------------------------------
Write-Host "`n== Etiquetas ==" -ForegroundColor Cyan
$etiquetas = @(
  @{ n = "setup";        c = "6e7781"; d = "Configuración del proyecto y del repositorio" },
  @{ n = "diseño";       c = "d4c5f9"; d = "Historia, guion y diseño de juego" },
  @{ n = "arte-3d";      c = "f9d0c4"; d = "Concept art, Tripo3D y modelos" },
  @{ n = "animación";    c = "fbca04"; d = "Rig, Mixamo y Animator" },
  @{ n = "programación"; c = "1d76db"; d = "Código de gameplay y sistemas" },
  @{ n = "ia";           c = "5319e7"; d = "IA de compañeros y enemigos" },
  @{ n = "nivel";        c = "0e8a16"; d = "Escenario, NavMesh y flujo del nivel" },
  @{ n = "audio";        c = "b60205"; d = "Música, efectos de sonido e integración" },
  @{ n = "vfx";          c = "ff7619"; d = "Efectos visuales y partículas" },
  @{ n = "ui";           c = "c2e0c6"; d = "Interfaz y menús" },
  @{ n = "qa";           c = "bfd4f2"; d = "Pruebas, balance y corrección de bugs" },
  @{ n = "entrega";      c = "000000"; d = "Builds, README y entregas" },
  @{ n = "sin-código";   c = "ededed"; d = "Tarea que no produce código" },
  @{ n = "hito";         c = "e99695"; d = "Última tarea de una entrega" }
)
foreach ($e in $etiquetas) {
  Invoke-Gh @("label", "create", $e.n, "--repo", $Repo, "--color", $e.c, "--description", $e.d, "--force") | Out-Null
  Write-Host "  $($e.n)"
}

# --- Hitos (milestones) ----------------------------------------------------------
Write-Host "`n== Hitos ==" -ForegroundColor Cyan
$hitos = @{
  1 = @{ titulo = "Entrega 1 (25 nov)"; fecha = "2026-11-26T04:59:00Z"; desc = "Avance del 30 al 50 %: caballero jugable, bardo con IA mínima y hordas de soldados." }
  2 = @{ titulo = "Entrega final";      fecha = "";                     desc = "Nivel completo con los 4 héroes, todos los enemigos y el miniboss. Demo y sustentación." }
}
if ($FechaFinal -ne "") {
  # 11:59 p. m. hora de Colombia = 04:59 UTC del día siguiente
  $hitos[2].fecha = ([datetime]::ParseExact($FechaFinal, "yyyy-MM-dd", $null)).AddDays(1).ToString("yyyy-MM-dd") + "T04:59:00Z"
  $hitos[2].titulo = "Entrega final ($FechaFinal)"
}
foreach ($k in 1, 2) {
  $h = $hitos[$k]
  $hitoArgs = @("api", "repos/$Repo/milestones", "-f", "title=$($h.titulo)", "-f", "description=$($h.desc)")
  if ($h.fecha -ne "") { $hitoArgs += @("-f", "due_on=$($h.fecha)") }
  Invoke-Gh $hitoArgs | Out-Null
  Write-Host "  $($h.titulo)"
}

# --- Issues --------------------------------------------------------------------
Write-Host "`n== Issues ==" -ForegroundColor Cyan
$urls = @()
$tmp = New-TemporaryFile
foreach ($t in $tareas) {
  $hito = $hitos[[int]$t.entrega].titulo
  $titulo = if ($t.hito) { "🏁 $($t.titulo)" } else { $t.titulo }

  $cuerpo = New-Object Text.StringBuilder
  [void]$cuerpo.AppendLine($t.descripcion)
  [void]$cuerpo.AppendLine()
  [void]$cuerpo.AppendLine("### Hecho cuando")
  foreach ($c in $t.hecho) { [void]$cuerpo.AppendLine("- [ ] $c") }
  if ($t.depende.Count -gt 0) {
    [void]$cuerpo.AppendLine()
    [void]$cuerpo.AppendLine("**Depende de:** " + (($t.depende | ForEach-Object { "#$_" }) -join ", "))
  }
  [void]$cuerpo.AppendLine()
  [void]$cuerpo.AppendLine("_Tarea $($t.n) de $($tareas.Count) · $($hito)_")
  [IO.File]::WriteAllText($tmp.FullName, $cuerpo.ToString(), $utf8SinBom)

  $labels = @($t.areas)
  if ($t.sinCodigo) { $labels += "sin-código" }
  if ($t.hito) { $labels += "hito" }

  $url = Invoke-Gh @("issue", "create", "--repo", $Repo, "--title", $titulo,
                     "--body-file", $tmp.FullName, "--label", ($labels -join ","), "--milestone", $hito)
  $urls += $url
  Write-Host ("  #{0,-3} {1}" -f $t.n, $titulo)
}
Remove-Item $tmp.FullName

# --- Tablero de GitHub Projects -----------------------------------------------
Write-Host "`n== Proyecto ==" -ForegroundColor Cyan
$proyecto = Invoke-Gh @("project", "create", "--owner", $Owner, "--title", $TituloProyecto, "--format", "json")
if (-not $DryRun) {
  $numero = ($proyecto | ConvertFrom-Json).number
  Invoke-Gh @("project", "link", $numero, "--owner", $Owner, "--repo", $Repo) | Out-Null
  foreach ($u in $urls) {
    Invoke-Gh @("project", "item-add", $numero, "--owner", $Owner, "--url", $u) | Out-Null
  }
  Write-Host "  Proyecto #$numero con $($urls.Count) tareas, enlazado a $Repo"
}

Write-Host "`nListo." -ForegroundColor Green
