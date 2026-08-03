#!/usr/bin/env bash
#
# instance.sh — boot / stop / validate the committed reference Umbraco site.
#
#   instance.sh boot            Start the instance (idempotent) and wait until it answers.
#   instance.sh status          Print whether the instance is up.
#   instance.sh api-user        Create the API user the Umbraco MCP authenticates as (idempotent).
#   instance.sh stop            Stop the instance if this script started it.
#   instance.sh try <skill-dir> Materialize a skill's assets/*.cs into a sidecar library
#                               and reference it from the instance (build happens on boot).
#   instance.sh reset           Remove the sidecar reference and delete the scratch project.
#
# Env: UMBRACO_URL (default https://localhost:44372), UMBRACO_USER_LOGIN, UMBRACO_USER_PASSWORD.
set -euo pipefail

# --- locate the repo root (two levels up from .claude/skills/<name>/scripts) ---
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"

PROJECT_DIR="$REPO_ROOT/Umbraco-CMS.Skills"
PROJECT="$PROJECT_DIR/Umbraco-CMS.Skills.csproj"
SANDBOX_DIR="$REPO_ROOT/Umbraco.Skills.Sandbox"
SANDBOX="$SANDBOX_DIR/Umbraco.Skills.Sandbox.csproj"
SANDBOX_NS="Umbraco.Skills.Sandbox"
PIDFILE="$PROJECT_DIR/.instance.pid"

UMBRACO_URL="${UMBRACO_URL:-https://localhost:44372}"

log() { printf '\033[36m[instance]\033[0m %s\n' "$*"; }
err() { printf '\033[31m[instance]\033[0m %s\n' "$*" >&2; }

is_up() {
  # 200/302 (or the boot screen) means the app is serving.
  local code
  code="$(curl -sk -o /dev/null -w '%{http_code}' --max-time 5 "$UMBRACO_URL/" 2>/dev/null || echo 000)"
  [[ "$code" =~ ^(200|301|302)$ ]]
}

cmd_status() {
  if is_up; then log "up at $UMBRACO_URL"; else log "not responding at $UMBRACO_URL"; fi
}

cmd_boot() {
  if is_up; then
    log "already up at $UMBRACO_URL — reusing it (not starting a second instance)."
    return 0
  fi
  local log_file="$PROJECT_DIR/.instance.log"
  log "building…"
  if ! dotnet build "$PROJECT" -c Debug -v quiet >"$log_file" 2>&1; then
    err "build failed. Last log lines:"; tail -n 25 "$log_file" >&2 || true; return 1
  fi
  local dll="$PROJECT_DIR/bin/Debug/net10.0/Umbraco-CMS.Skills.dll"
  [[ -f "$dll" ]] || { err "built dll not found at $dll"; return 1; }

  # Run the built DLL directly (not `dotnet run`, which forks an app-host child that
  # outlives a kill of the wrapper). Ports come from ASPNETCORE_URLS since launchSettings
  # only applies to `dotnet run`. ContentRoot = PROJECT_DIR (cd), so App_Data/appsettings resolve.
  #
  # Detach into its OWN session so the app survives this script — and its caller — being
  # killed; otherwise a killed wrapper takes the whole process group (incl. the instance)
  # down with it. macOS has no `setsid`, so fall back to perl's POSIX::setsid + exec (which
  # keeps the PID stable, so $! is the real dotnet pid), then plain nohup as a last resort.
  local -a detach
  if command -v setsid >/dev/null 2>&1; then
    detach=(setsid)
  elif command -v perl >/dev/null 2>&1; then
    detach=(perl -MPOSIX=setsid -e 'setsid(); exec @ARGV' --)
  else
    detach=(nohup)
  fi
  log "starting instance (first boot runs the unattended install + Clean import — be patient)…"
  ( cd "$PROJECT_DIR" \
      && ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="$UMBRACO_URL" \
         "${detach[@]}" dotnet "$dll" >>"$log_file" 2>&1 & echo $! >"$PIDFILE" )
  disown 2>/dev/null || true
  log "pid $(cat "$PIDFILE"), logging to $log_file"

  local waited=0 timeout=300
  until is_up; do
    if ! kill -0 "$(cat "$PIDFILE")" 2>/dev/null; then
      err "instance process exited during startup. Last log lines:"; tail -n 25 "$log_file" >&2 || true; return 1
    fi
    sleep 3; waited=$((waited + 3))
    if (( waited >= timeout )); then
      err "instance did not come up within ${timeout}s. Last log lines:"
      tail -n 25 "$log_file" >&2 || true
      return 1
    fi
  done
  log "ready at $UMBRACO_URL (took ~${waited}s). Backoffice: $UMBRACO_URL/umbraco"
}

# Creates the API user that .mcp.json's client credentials authenticate as, so the Umbraco MCP can
# read content, Document Types and templates over the Management API. Wraps create-api-user.mjs so
# callers get one entry point and don't have to hand-manage the TLS exemption below.
cmd_api_user() {
  local script="$SCRIPT_DIR/create-api-user.mjs"
  [[ -f "$script" ]] || { err "missing $script"; return 1; }
  command -v node >/dev/null 2>&1 || { err "node not found — needed to create the API user."; return 1; }

  if ! is_up; then
    err "instance is not responding at $UMBRACO_URL — run 'instance.sh boot' first."
    return 1
  fi

  local login="${UMBRACO_USER_LOGIN:-admin@example.com}"
  local password="${UMBRACO_USER_PASSWORD:-1234567890}"
  log "creating/verifying the MCP API user on ${UMBRACO_URL}…"

  # The instance serves the ASP.NET dev certificate, which Node won't trust. Relax verification
  # for localhost only, and only for this one process — never export it into the caller's shell,
  # and never apply it to a remote host where a cert error would be a real signal.
  if [[ "$UMBRACO_URL" == https://localhost* || "$UMBRACO_URL" == https://127.0.0.1* ]]; then
    NODE_TLS_REJECT_UNAUTHORIZED=0 node "$script" "$UMBRACO_URL" "$login" "$password"
  else
    node "$script" "$UMBRACO_URL" "$login" "$password"
  fi
}

cmd_stop() {
  local killed=0
  if [[ -f "$PIDFILE" ]] && kill -0 "$(cat "$PIDFILE")" 2>/dev/null; then
    log "stopping pid $(cat "$PIDFILE")…"
    kill "$(cat "$PIDFILE")" 2>/dev/null || true
    killed=1
  fi
  # belt & suspenders: kill any app host bound to THIS project's build output
  if pkill -f "$PROJECT_DIR/bin/.*Umbraco-CMS.Skills" 2>/dev/null; then killed=1; fi
  rm -f "$PIDFILE"
  if (( killed )); then log "stopped."; else log "nothing to stop (no instance started by this script)."; fi
}

cmd_try() {
  local skill_dir="${1:-}"
  [[ -n "$skill_dir" ]] || { err "usage: instance.sh try <skill-dir>"; return 2; }
  # allow either an absolute path or one relative to the repo root
  [[ -d "$skill_dir" ]] || skill_dir="$REPO_ROOT/$skill_dir"
  local assets="$skill_dir/assets"
  [[ -d "$assets" ]] || { err "no assets/ folder in $skill_dir"; return 1; }
  ls "$assets"/*.cs >/dev/null 2>&1 || { err "no *.cs assets in $assets"; return 1; }

  log "materializing $(basename "$skill_dir") assets into sidecar library…"
  rm -rf "$SANDBOX_DIR"; mkdir -p "$SANDBOX_DIR"

  cat >"$SANDBOX" <<EOF
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
  </PropertyGroup>
  <ItemGroup>
    <!-- Web.Website pulls in Web.Common + Core + Infrastructure transitively, which covers
         the Umbraco.Cms.* namespaces skill assets use (controllers, composers, services). -->
    <PackageReference Include="Umbraco.Cms.Web.Website" Version="17.*" />
  </ItemGroup>
</Project>
EOF

  # copy each .cs asset, substituting the <Namespace> placeholder for the sandbox namespace
  local f base
  for f in "$assets"/*.cs; do
    base="$(basename "$f")"
    sed "s/<Namespace>/$SANDBOX_NS/g" "$f" >"$SANDBOX_DIR/$base"
  done

  # fail loudly if any placeholder survived (asset used a different token)
  if grep -Rn "<Namespace>" "$SANDBOX_DIR" >/dev/null 2>&1; then
    err "a literal <Namespace> placeholder remains after substitution:"
    grep -Rn "<Namespace>" "$SANDBOX_DIR" >&2 || true
    return 1
  fi

  if ! grep -q "Umbraco.Skills.Sandbox" "$PROJECT"; then
    dotnet add "$PROJECT" reference "$SANDBOX" >/dev/null
    log "added ProjectReference to $SANDBOX_NS on the instance."
  else
    log "instance already references the sandbox (reusing)."
  fi
  log "done. Now: instance.sh boot, then curl your endpoint. instance.sh reset when finished."
}

cmd_reset() {
  if grep -q "Umbraco.Skills.Sandbox" "$PROJECT" 2>/dev/null; then
    dotnet remove "$PROJECT" reference "$SANDBOX" >/dev/null 2>&1 || true
    log "removed sandbox ProjectReference from the instance."
  fi
  rm -rf "$SANDBOX_DIR"
  log "deleted $SANDBOX_DIR — committed instance restored."
}

case "${1:-}" in
  boot)     cmd_boot ;;
  status)   cmd_status ;;
  api-user) cmd_api_user ;;
  stop)     cmd_stop ;;
  try)      shift; cmd_try "$@" ;;
  reset)    cmd_reset ;;
  *) err "usage: instance.sh {boot|status|api-user|stop|try <skill-dir>|reset}"; exit 2 ;;
esac
