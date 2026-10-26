#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
if [[ -z "${DOTNET_ROOT:-}" && -d /opt/homebrew/opt/dotnet/libexec ]]; then
    export DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec
fi

dotnet build Ardan.slnx -nologo -v quiet

dotnet run --no-build --project src/Ardan.Api &
api=$!
dotnet run --no-build --project src/Ardan.Web &
web=$!
trap 'kill "$api" "$web" 2>/dev/null' EXIT INT TERM

echo "Ardán is starting. Console: http://localhost:5090  API: http://localhost:5080"
wait
