#!/usr/bin/env bash
# Build check: restore + build. Prints ::pass:: on success, ::fail:: <step> otherwise.
set -uo pipefail
cd "$(dirname "$0")/.."
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

step() {
  local name=$1; shift
  echo "::step:: $name"
  if ! "$@"; then echo "::fail:: $name"; exit 1; fi
}

step build dotnet build RunMemtestStartup.sln --nologo -clp:NoSummary
echo "::pass::"
