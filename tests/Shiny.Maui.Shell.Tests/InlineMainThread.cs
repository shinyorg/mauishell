using System.Reflection;
using System.Runtime.CompilerServices;

namespace Shiny.Maui.Shell.Tests;

/// <summary>
/// The tests run on plain net11.0, where MAUI's MainThread has no platform behind it and throws. MAUI 11 lets a
/// platform plug one in, but only internally - install one that says every thread is the main thread, so
/// InvokeOnMainThreadAsync runs inline.
/// </summary>
static class InlineMainThread
{
    [ModuleInitializer]
    internal static void Install()
    {
        var set = typeof(MainThread).GetMethod(
            "SetCustomImplementation",
            BindingFlags.NonPublic | BindingFlags.Static,
            [typeof(Func<bool>), typeof(Action<Action>)]
        ) ?? throw new InvalidOperationException("MainThread.SetCustomImplementation is gone - MAUI changed how a main thread is plugged in");

        set.Invoke(null, [(Func<bool>)(() => true), (Action<Action>)(action => action())]);
    }
}
