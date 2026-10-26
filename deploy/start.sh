#!/usr/bin/env bash
set -uo pipefail
cd "$(dirname "$0")"
unset ASPNETCORE_HTTP_PORTS

(cd api && ASPNETCORE_URLS=http://127.0.0.1:5080 exec dotnet Ardan.Api.dll) &
(cd web && ASPNETCORE_URLS="http://0.0.0.0:${PORT:-8080}" Api__BaseUrl=http://127.0.0.1:5080/ exec dotnet Ardan.Web.dll) &

trap 'kill -TERM $(jobs -p) 2>/dev/null; wait' TERM INT
wait -n
status=$?
kill -TERM $(jobs -p) 2>/dev/null
wait
exit "$status"
