#!/usr/bin/env bash
set -euo pipefail

should_wait_for_sql() {
  if [[ "${SKIP_SQL_WAIT:-}" == "true" ]]; then
    return 1
  fi

  if [[ "${SQL_HOST:-}" == *".database.windows.net"* ]]; then
    return 1
  fi

  if [[ "${SQL_WAIT:-}" != "true" ]]; then
    return 1
  fi

  return 0
}

if should_wait_for_sql; then
  SQL_HOST="${SQL_HOST:-sql}"
  SQL_PORT="${SQL_PORT:-1433}"
  SQL_WAIT_SECONDS="${SQL_WAIT_SECONDS:-60}"

  echo "Waiting for SQL Server at ${SQL_HOST}:${SQL_PORT} (timeout: ${SQL_WAIT_SECONDS}s)..."

  start="$(date +%s)"
  while true; do
    if (echo >"/dev/tcp/${SQL_HOST}/${SQL_PORT}") >/dev/null 2>&1; then
      echo "SQL Server is reachable."
      break
    fi

    now="$(date +%s)"
    if [ $((now - start)) -ge "${SQL_WAIT_SECONDS}" ]; then
      echo "Timed out waiting for SQL Server." >&2
      exit 1
    fi

    sleep 1
  done
else
  echo "Skipping SQL wait (Azure SQL / Container Apps or SQL_WAIT not enabled)."
fi

exec dotnet RecipeLibrary.Web.dll
