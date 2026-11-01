#!/usr/bin/env bash
# Builds the Sample (Debug, with the MAUI DevFlow agent), installs it fresh on a dedicated Android emulator and runs the
# UI tests against it. See readme.md in this folder.
#
#   tests/Sample.UITests/run-android.sh                         build + install + run everything
#   tests/Sample.UITests/run-android.sh --no-build              reuse the last build (still a fresh install)
#   tests/Sample.UITests/run-android.sh --filter DialogTests    any `dotnet test --filter` expression (class or method name)
#
# Environment (all optional): SAMPLE_UITEST_AVD (emulator name, default "MauiShell-UITests"), SAMPLE_UITEST_ANDROID_SERIAL
# (use this device instead), SAMPLE_UITEST_SYSTEM_IMAGE (for creating the AVD, default
# "system-images;android-35;google_apis;arm64-v8a"), ANDROID_HOME (default ~/Library/Android/sdk). Needs: the Android SDK
# with the emulator and that system image, and the maui CLI (dotnet tool install -g Microsoft.Maui.Cli --prerelease).
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

SDK="${ANDROID_HOME:-$HOME/Library/Android/sdk}"
ADB="$SDK/platform-tools/adb"
EMULATOR="$SDK/emulator/emulator"
AVDMANAGER="$SDK/cmdline-tools/latest/bin/avdmanager"
AVD="${SAMPLE_UITEST_AVD:-MauiShell-UITests}"
SYSTEM_IMAGE="${SAMPLE_UITEST_SYSTEM_IMAGE:-system-images;android-35;google_apis;arm64-v8a}"
APK="$HERE/.app-build-android/bin/org.shiny.maui-Signed.apk"
PACKAGE="org.shiny.maui"

step() { printf '\n\033[1;33m== %s\033[0m\n' "$*"; }

step "Tools"
command -v maui >/dev/null || { echo "maui CLI missing: dotnet tool install -g Microsoft.Maui.Cli --prerelease" >&2; exit 1; }
[[ -x "$ADB" && -x "$EMULATOR" ]] || { echo "No Android SDK at $SDK (set ANDROID_HOME)" >&2; exit 1; }
AGENT_VERSION="$(sed -n 's/.*Include="Microsoft.Maui.DevFlow.Agent" Version="\([^"]*\)".*/\1/p' "$ROOT/Directory.Packages.props")"
CLI_VERSION="$(maui --version | cut -d+ -f1)"
echo "maui CLI $CLI_VERSION · DevFlow agent $AGENT_VERSION"
[[ "$CLI_VERSION" == "$AGENT_VERSION" ]] || echo "warning: keep them equal — dotnet tool update -g Microsoft.Maui.Cli --version $AGENT_VERSION"

find_running() {
    for s in $("$ADB" devices | awk 'NR>1 && $1 ~ /^emulator-/ {print $1}'); do
        [[ "$("$ADB" -s "$s" emu avd name 2>/dev/null | head -1 | tr -d '\r')" == "$AVD" ]] && { echo "$s"; return; }
    done
}

if [[ -n "${SAMPLE_UITEST_ANDROID_SERIAL:-}" ]]; then
    step "Device ${SAMPLE_UITEST_ANDROID_SERIAL}"
    SERIAL="$SAMPLE_UITEST_ANDROID_SERIAL"
else
    step "Emulator '$AVD'"
    if ! "$EMULATOR" -list-avds | grep -qx "$AVD"; then
        echo "creating it ($SYSTEM_IMAGE)"
        echo no | "$AVDMANAGER" create avd -n "$AVD" -k "$SYSTEM_IMAGE" -d pixel_8 >/dev/null
    fi
    SERIAL="$(find_running)"
    if [[ -z "$SERIAL" ]]; then
        nohup "$EMULATOR" -avd "$AVD" -no-snapshot-save -no-boot-anim -no-audio >/dev/null 2>&1 &
        for _ in $(seq 90); do
            SERIAL="$(find_running)"
            [[ -n "$SERIAL" ]] && break
            sleep 2
        done
        [[ -n "$SERIAL" ]] || { echo "emulator '$AVD' did not start" >&2; exit 1; }
    fi
fi
"$ADB" -s "$SERIAL" wait-for-device
until [[ "$("$ADB" -s "$SERIAL" shell getprop sys.boot_completed | tr -d '\r')" == "1" ]]; do sleep 2; done
echo "$SERIAL"

if [[ $BUILD == 1 ]]; then
    step "Build the Sample (Debug, Android)"
    # Its own output/intermediate folders, so this never fights over Sample/bin|obj with an IDE build. Relative to
    # the Sample project. A Debug build normally leaves the assemblies out of the apk (fast deployment); an apk
    # installed with adb needs them embedded.
    dotnet build "$ROOT/Sample/Sample.csproj" -f net10.0-android -c Debug \
        -p:RuntimeIdentifier=android-arm64 \
        -p:EmbedAssembliesIntoApk=true \
        -p:AndroidPackageFormat=apk \
        -p:OutputPath=../tests/Sample.UITests/.app-build-android/bin/ \
        -p:IntermediateOutputPath=../tests/Sample.UITests/.app-build-android/obj/ \
        -nologo -v:q -clp:ErrorsOnly
fi
[[ -f "$APK" ]] || { echo "No apk at $APK — run without --no-build." >&2; exit 1; }

step "Install (fresh data)"
"$ADB" -s "$SERIAL" shell am force-stop "$PACKAGE" || true
"$ADB" -s "$SERIAL" uninstall "$PACKAGE" >/dev/null 2>&1 || true
"$ADB" -s "$SERIAL" install "$APK"

step "Run the UI tests"
export SAMPLE_UITEST_PLATFORM=android
export SAMPLE_UITEST_ANDROID_SERIAL="$SERIAL"
ARGS=(test "$HERE/Sample.UITests.csproj" --logger "console;verbosity=normal")
[[ -n "$FILTER" ]] && ARGS+=(--filter "$FILTER")
# The fixture launches the app itself, forwards a port to its DevFlow agent and drives it.
dotnet "${ARGS[@]}"
