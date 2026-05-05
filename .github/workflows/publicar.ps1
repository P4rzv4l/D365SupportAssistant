# =============================================================================
#  publicar.ps1 — Script para publicar nova versão do D365 Support Assistant
#
#  USO:
#    .\publicar.ps1 -Versao "1.2.0" -Mensagem "Corrige bug no timer"
#    .\publicar.ps1 -Versao "1.2.0"               (mensagem automática)
#    .\publicar.ps1 -Versao "1.2.0" -Major         (bump de versão maior)
#
#  O script:
#    1. Valida que o código compila
#    2. Faz commit das mudanças pendentes
#    3. Cria a tag de versão (ex: v1.2.0)
#    4. Faz push — o GitHub Actions publica automaticamente
# =============================================================================

param(
    [Parameter(Mandatory = $true)]
    [string]$Versao,

    [string]$Mensagem = "",

    [switch]$DryRun   # Simula sem fazer push/tag
)

$ErrorActionPreference = "Stop"
$Host.UI.RawUI.WindowTitle = "D365 Assistant — Publicar v$Versao"

# ── Cores ─────────────────────────────────────────────────────────────────────
function Write-Step  ($msg) { Write-Host "`n▶ $msg" -ForegroundColor Cyan }
function Write-Ok    ($msg) { Write-Host "  ✓ $msg" -ForegroundColor Green }
function Write-Warn  ($msg) { Write-Host "  ⚠ $msg" -ForegroundColor Yellow }
function Write-Fail  ($msg) { Write-Host "`n✗ $msg" -ForegroundColor Red; exit 1 }

# ── Banner ────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "╔══════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "║   D365 Support Assistant — Publicar v$Versao" -ForegroundColor Magenta
Write-Host "╚══════════════════════════════════════════════╝" -ForegroundColor Magenta
Write-Host ""

if ($DryRun) {
    Write-Warn "MODO DRY RUN — nenhuma alteração será feita no repositório"
}

# ── Validar formato da versão ─────────────────────────────────────────────────
Write-Step "Validando versão '$Versao'"
if ($Versao -notmatch '^\d+\.\d+\.\d+$') {
    Write-Fail "Versão inválida. Use o formato X.Y.Z  (ex: 1.2.0)"
}
Write-Ok "Formato válido"

# ── Verificar que estamos num repositório Git ─────────────────────────────────
Write-Step "Verificando repositório Git"
$gitStatus = git status --porcelain 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Fail "Não é um repositório Git. Execute git init ou clone o repositório."
}
Write-Ok "Repositório Git encontrado"

# ── Verificar se a tag já existe ─────────────────────────────────────────────
Write-Step "Verificando tag v$Versao"
$tagExists = git tag -l "v$Versao"
if ($tagExists) {
    Write-Fail "Tag v$Versao já existe! Escolha uma versão maior."
}
Write-Ok "Tag disponível"

# ── Compilar em Release ───────────────────────────────────────────────────────
Write-Step "Compilando em modo Release"
Write-Host "  (isso pode levar alguns segundos...)" -ForegroundColor DarkGray

dotnet build D365SupportAssistant.csproj -c Release --nologo -v quiet 2>&1 | 
    Where-Object { $_ -notmatch "^$" } | 
    ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }

if ($LASTEXITCODE -ne 0) {
    Write-Fail "Build falhou! Corrija os erros antes de publicar."
}
Write-Ok "Build Release concluído com sucesso"

# ── Verificar arquivos não commitados ─────────────────────────────────────────
Write-Step "Verificando arquivos pendentes"
$pending = git status --porcelain

if ($pending) {
    Write-Host ""
    Write-Host "  Arquivos com mudanças:" -ForegroundColor DarkGray
    $pending | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
    Write-Host ""

    # Monta mensagem de commit
    if ([string]::IsNullOrWhiteSpace($Mensagem)) {
        $Mensagem = "release: versao $Versao"
    }

    Write-Host "  Mensagem do commit: " -NoNewline
    Write-Host $Mensagem -ForegroundColor Yellow

    if (-not $DryRun) {
        $confirm = Read-Host "  Commitar e publicar? [S/n]"
        if ($confirm -eq 'n' -or $confirm -eq 'N') {
            Write-Warn "Publicação cancelada."
            exit 0
        }

        git add -A
        git commit -m $Mensagem
        if ($LASTEXITCODE -ne 0) { Write-Fail "Falha ao commitar." }
        Write-Ok "Commit realizado"
    } else {
        Write-Warn "DRY RUN: commit seria feito com mensagem '$Mensagem'"
    }
} else {
    Write-Ok "Nenhum arquivo pendente"
}

# ── Criar tag ─────────────────────────────────────────────────────────────────
Write-Step "Criando tag v$Versao"

if (-not $DryRun) {
    $tagMsg = if ([string]::IsNullOrWhiteSpace($Mensagem)) { "Versao $Versao" } else { $Mensagem }
    git tag -a "v$Versao" -m $tagMsg
    if ($LASTEXITCODE -ne 0) { Write-Fail "Falha ao criar tag." }
    Write-Ok "Tag v$Versao criada"
} else {
    Write-Warn "DRY RUN: tag v$Versao seria criada"
}

# ── Push ──────────────────────────────────────────────────────────────────────
Write-Step "Enviando para o GitHub"

if (-not $DryRun) {
    Write-Host "  Fazendo push do código..." -ForegroundColor DarkGray
    git push origin
    if ($LASTEXITCODE -ne 0) { Write-Fail "Falha no git push." }

    Write-Host "  Fazendo push da tag..." -ForegroundColor DarkGray
    git push origin "v$Versao"
    if ($LASTEXITCODE -ne 0) { Write-Fail "Falha no push da tag." }

    Write-Ok "Push concluído"
} else {
    Write-Warn "DRY RUN: git push seria executado"
}

# ── Resultado ─────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║  ✅  Publicação iniciada com sucesso!                        ║" -ForegroundColor Green
Write-Host "╠══════════════════════════════════════════════════════════════╣" -ForegroundColor Green

if (-not $DryRun) {
    Write-Host "║                                                              ║" -ForegroundColor Green
    Write-Host "║  O GitHub Actions está publicando a versão v$Versao...        ║" -ForegroundColor Green
    Write-Host "║                                                              ║" -ForegroundColor Green
    Write-Host "║  Acompanhe em:                                               ║" -ForegroundColor Green
    Write-Host "║  https://github.com/p4rzv4l/D365SupportAssistant/actions     ║" -ForegroundColor Green
    Write-Host "║                                                              ║" -ForegroundColor Green
    Write-Host "║  Em ~3 minutos estará disponível em:                         ║" -ForegroundColor Green
    Write-Host "║  https://p4rzv4l.github.io/D365SupportAssistant/             ║" -ForegroundColor Green
    Write-Host "║                                                              ║" -ForegroundColor Green
    Write-Host "║  Os usuários receberão a atualização automaticamente         ║" -ForegroundColor Green
    Write-Host "║  na próxima vez que abrirem o app. 🚀                        ║" -ForegroundColor Green
} else {
    Write-Host "║  DRY RUN concluído — nenhuma alteração foi feita             ║" -ForegroundColor Green
}

Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""