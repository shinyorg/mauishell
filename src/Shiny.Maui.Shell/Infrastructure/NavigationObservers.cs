using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Shiny.Infrastructure;


/// <summary>
/// Reports navigation to the registered <see cref="INavigationObserver"/>s. Owns the bookkeeping
/// the navigator would otherwise have to repeat on every path: ids, durations, exactly-once
/// finishing, observer fault isolation, and the one Shell-driven navigation Shiny is waiting on
/// Shell to finish.
/// </summary>
/// <remarks>
/// Every method is a no-op when no observer is registered - <see cref="Start"/> returns null and
/// everything that accepts that null ignores it - so apps that observe nothing pay nothing.
/// </remarks>
public class NavigationObservers(
    ILogger<NavigationObservers> logger,
    IServiceProvider services
)
{
    long lastId;
    IReadOnlyList<INavigationObserver>? observers;
    readonly ConcurrentDictionary<long, NavigationStarted> inFlight = new();
    readonly object shellLock = new();

    /// <summary>
    /// The Shell-driven navigation Shiny has let through and is waiting on Shell to complete.
    /// Shell runs one navigation at a time, so there is at most one.
    /// </summary>
    NavigationStarted? pendingShell;


    IReadOnlyList<INavigationObserver> Observers
        => this.observers ??= services.GetServices<INavigationObserver>().ToList();


    /// <summary>Whether anything is observing. Checked before any work is done for the observers.</summary>
    public bool HasObservers => this.Observers.Count > 0;


    /// <summary>Navigations that have started and not yet finished.</summary>
    public IReadOnlyCollection<NavigationStarted> InFlight => this.inFlight.Values.ToList();


    /// <summary>
    /// Reports a navigation starting. Returns null, and reports nothing, when nobody is observing.
    /// </summary>
    public NavigationStarted? Start(
        NavigationSource source,
        NavigationType navigationType,
        string uri,
        IReadOnlyDictionary<string, object>? parameters,
        string? fromUri,
        object? fromViewModel,
        ShellNavigationSource? shellSource = null
    )
    {
        if (!this.HasObservers)
            return null;

        var navigation = new NavigationStarted(
            Interlocked.Increment(ref this.lastId),
            source,
            navigationType,
            uri,
            fromUri,
            fromViewModel,
            // Copied: the navigator hands the same dictionary to OnNavigatingFrom, which may add to it.
            parameters == null ? new Dictionary<string, object>() : new Dictionary<string, object>(parameters),
            shellSource,
            DateTimeOffset.Now
        )
        {
            Timestamp = Stopwatch.GetTimestamp()
        };

        this.inFlight[navigation.Id] = navigation;
        foreach (var observer in this.Observers)
        {
            try
            {
                observer.OnNavigationStarted(navigation);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Observer] '{observer}' threw from OnNavigationStarted", observer.GetType().Name);
            }
        }
        return navigation;
    }


    /// <summary>
    /// Reports a navigation finishing. Only the first call for a navigation reports anything, so
    /// every exit path can call this without coordinating with the others.
    /// </summary>
    public void Finish(
        NavigationStarted? navigation,
        NavigationOutcome outcome,
        string uri,
        IReadOnlyList<NavigationRedirect>? redirects = null,
        object? cancelledBy = null,
        Exception? exception = null
    )
    {
        if (navigation == null || !this.inFlight.TryRemove(navigation.Id, out _))
            return;

        lock (this.shellLock)
        {
            if (ReferenceEquals(this.pendingShell, navigation))
                this.pendingShell = null;
        }

        var finished = new NavigationFinished(
            navigation,
            outcome,
            uri,
            redirects ?? Array.Empty<NavigationRedirect>(),
            cancelledBy,
            exception,
            Stopwatch.GetElapsedTime(navigation.Timestamp)
        );

        foreach (var observer in this.Observers)
        {
            try
            {
                observer.OnNavigationFinished(finished);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Observer] '{observer}' threw from OnNavigationFinished", observer.GetType().Name);
            }
        }
    }


    /// <summary>
    /// Reports a navigation Shell started on its own, and remembers it as the one Shell is now
    /// running. A Shell-driven navigation still waiting when the next one starts was abandoned
    /// without Shell saying so - cancelled by a deferred handler outside Shiny - and is finished as
    /// cancelled.
    /// </summary>
    public NavigationStarted? StartShell(
        NavigationType navigationType,
        string uri,
        string? fromUri,
        object? fromViewModel,
        ShellNavigationSource shellSource
    )
    {
        if (!this.HasObservers)
            return null;

        NavigationStarted? abandoned;
        lock (this.shellLock)
        {
            abandoned = this.pendingShell;
            this.pendingShell = null;
        }
        if (abandoned != null)
            this.Finish(abandoned, NavigationOutcome.Cancelled, abandoned.Uri);

        var navigation = this.Start(
            NavigationSource.Shell,
            navigationType,
            uri,
            null,
            fromUri,
            fromViewModel,
            shellSource
        );

        lock (this.shellLock)
            this.pendingShell = navigation;

        return navigation;
    }


    /// <summary>
    /// Shiny has taken over a Shell-driven navigation - a redirect reissues it as Shiny's own - so
    /// Shell completing or cancelling the original must no longer finish it.
    /// </summary>
    public void ReleaseShell(NavigationStarted? navigation)
    {
        if (navigation == null)
            return;

        lock (this.shellLock)
        {
            if (ReferenceEquals(this.pendingShell, navigation))
                this.pendingShell = null;
        }
    }


    /// <summary>
    /// Shell raised <see cref="Shell.Navigated"/>: the Shell-driven navigation it was running, if
    /// any, completed.
    /// </summary>
    /// <param name="location">Shell's location after the navigation.</param>
    public void ShellNavigated(string? location)
    {
        NavigationStarted? navigation;
        lock (this.shellLock)
        {
            navigation = this.pendingShell;
            this.pendingShell = null;
        }

        if (navigation != null)
            this.Finish(navigation, NavigationOutcome.Completed, location ?? navigation.Uri);
    }


    /// <summary>
    /// Shell, or a handler outside Shiny, cancelled a Shell-driven navigation Shiny had let through.
    /// Does nothing when the navigation has already been finished or taken over.
    /// </summary>
    public void ShellCancelled(NavigationStarted? navigation)
    {
        if (navigation == null)
            return;

        lock (this.shellLock)
        {
            if (!ReferenceEquals(this.pendingShell, navigation))
                return;

            this.pendingShell = null;
        }
        this.Finish(navigation, NavigationOutcome.Cancelled, navigation.Uri);
    }
}
