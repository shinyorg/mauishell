#!/usr/bin/env bash
# Builds the Sample (Debug, with the MAUI DevFlow agent), installs it fresh on a dedicated iOS simulator and runs the
# UI tests against it. See readme.md in this folder.
#
#   tests/Sample.UITests/run-ios.sh                         build + install + run everything
#   tests/Sample.UITests/run-ios.sh --no-build              reuse the last build (still a fresh install)
#   tests/Sample.UITests/run-ios.sh --filter DialogTests    any `dotnet test --filter` expression (class or method name)
#
# Environment (all optional): SAMPLE_UITEST_SIM (simulator name, default "MauiShell-UITests"), SAMPLE_UITEST_UDID,
# SAMPLE_UITEST_DEVICE_TYPE (default "iPhone 18 Pro"). Needs: Xcode with an iOS simulator runtime and the maui CLI
# (dotnet tool install -g Microsoft.Maui.Cli --prerelease).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
BUILD=1
FILTER=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        --no-build) BUILD=0; shift ;;
        --filter) FILTER="$2"; shift 2 ;;
        *) echo "unknown option: $1" >&2; exit 2 ;;
    esac
done

SIM_NAME="${SAMPLE_UITEST_SIM:-MauiShell-UITests}"
DEVICE_TYPE="${SAMPLE_UITEST_DEVICE_TYPE:-iPhone 18 Pro}"
APP="$HERE/.app-build/bin/Sample.app"
BUNDLE_ID="org.shiny.maui"

step() { printf '\n\033[1;33m== %s\033[0m\n' "$*"; }

step "Tools"
command -v maui >/dev/null || { echo "maui CLI missing: dotnet tool install -g Microsoft.Maui.Cli --prerelease" >&2; exit 1; }
AGENT_VERSION="$(sed -n 's/.*Include="Microsoft.Maui.DevFlow.Agent" Version="\([^"]*\)".*/\1/p' "$ROOT/Directory.Packages.props")"
CLI_VERSION="$(maui --version | cut -d+ -f1)"
echo "maui CLI $CLI_VERSION · DevFlow agent $AGENT_VERSION"
[[ "$CLI_VERSION" == "$AGENT_VERSION" ]] || echo "warning: keep them equal — dotnet tool update -g Microsoft.Maui.Cli --version $AGENT_VERSION"

step "Simulator '$SIM_NAME'"
if [[ -n "${SAMPLE_UITEST_UDID:-}" ]]; then
    UDID="$SAMPLE_UITEST_UDID"
else
    UDID="$(xcrun simctl list devices available -j | python3 -c "import json,sys; n=sys.argv[1]; print(next((d['udid'] for r,ds in json.load(sys.stdin)['devices'].items() if 'iOS' in r for d in ds if d['name']==n), ''))" "$SIM_NAME")"
    if [[ -z "$UDID" ]]; then
        echo "creating it ($DEVICE_TYPE)"
        UDID="$(xcrun simctl create "$SIM_NAME" "$DEVICE_TYPE")"
    fi
fi
xcrun simctl boot "$UDID" 2>/dev/null || true
xcrun simctl bootstatus "$UDID" -b >/dev/null
echo "$UDID"

if [[ $BUILD == 1 ]]; then
    step "Build the Sample (Debug, iOS simulator)"
    # Its own output/intermediate folders, so this never fights over Sample/bin|obj with an IDE build. Relative to
    # the Sample project.
    dotnet build "$ROOT/Sample/Sample.csproj" -f net10.0-ios -c Debug \
        -p:RuntimeIdentifier=iossimulator-arm64 \
        -p:OutputPath=../tests/Sample.UITests/.app-build/bin/ \
        -p:IntermediateOutputPath=../tests/Sample.UITests/.app-build/obj/ \
        -nologo -v:q -clp:ErrorsOnly
fi
[[ -d "$APP" ]] || { echo "No app bundle at $APP — run without --no-build." >&2; exit 1; }

step "Install (fresh data container)"
xcrun simctl terminate "$UDID" "$BUNDLE_ID" 2>/dev/null || true
xcrun simctl uninstall "$UDID" "$BUNDLE_ID" 2>/dev/null || true
xcrun simctl install "$UDID" "$APP"

step "Run the UI tests"
export SAMPLE_UITEST_UDID="$UDID"
ARGS=(test "$HERE/Sample.UITests.csproj" --logger "console;verbosity=normal")
[[ -n "$FILTER" ]] && ARGS+=(--filter "$FILTER")
# The fixture launches the app itself, finds its DevFlow agent by pid and drives it.
dotnet "${ARGS[@]}"
