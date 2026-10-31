using Microsoft.Extensions.Logging.Abstractions;
using Shiny.Infrastructure;
using Shouldly;

namespace Shiny.Maui.Shell.Tests;

/// <summary>
/// The read side of <see cref="ShellTabBadgeManager"/>. Setting a badge needs a live Shell to find
/// the tab, so only the empty state and route handling are testable here.
/// </summary>
public class ShellTabBadgeManagerTests
{
    class InlineMainThread : IMainThread
    {
        public Task InvokeOnMainThreadAsync(Action action) { action(); return Task.CompletedTask; }
        public Task InvokeOnMainThreadAsync(Func<Task> func) => func();
        public Task<T> InvokeOnMainThreadAsync<T>(Func<Task<T>> func) => func();
        public void BeginInvokeOnMainThread(Action action) => action();
    }

    static ShellTabBadgeManager Build() => new(NullLogger<ShellTabBadgeManager>.Instance, new InlineMainThread());


    [Fact]
    public void NothingSet_ReadsEmpty()
    {
        var manager = Build();

        manager.Values.ShouldBeEmpty();
        manager.Get("inbox").ShouldBeNull();
        // Full routes resolve to their last segment, like Set and Clear.
        manager.Get("//main/inbox").ShouldBeNull();
    }


    [Fact]
    public void Get_RejectsWhatSetRejects()
    {
        var manager = Build();

        Should.Throw<ArgumentException>(() => manager.Get(""));
        Should.Throw<ArgumentException>(() => manager.Get("//"));
    }


    [Fact]
    public async Task FailedSet_RecordsNothing()
    {
        // No Shell, so the tab cannot be resolved - the value must not be remembered as set.
        var manager = Build();

        await Should.ThrowAsync<InvalidOperationException>(() => manager.Set("inbox", 3));

        manager.Get("inbox").ShouldBeNull();
        manager.Values.ShouldBeEmpty();
    }
}
