using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Tracks bounded request identities and finite native progress without retaining printable transport data.</summary>
internal sealed class BrowserTrafficRequests
{
    private const int Limit = 256;
    private const int ExpectedInvalid = 3;
    private const string LedgerReport = "traffic-ledger requests={0} active={1} invalid={2}";
    private const string RequestReport = "traffic-request ordinal={0} route={1} navigation={2} status={3} terminal={4} headers-pending={5} response-stage={6}";
    private readonly object synchronization = new();
    private readonly Dictionary<object, Observation> entries = new(ReferenceEqualityComparer.Instance);
    private TaskCompletionSource idle = Completed();
    private int active;
    private int invalid;

    /// <summary>Reads a consistent completion signal for the current set of actual native requests.</summary>
    internal Task IdleAsync()
    {
        lock (synchronization)
        {
            return idle.Task;
        }
    }
    /// <summary>Counts requests without a matching native terminal event.</summary>
    internal int ActiveCount()
    {
        lock (synchronization)
        {
            return active;
        }
    }
    /// <summary>Counts duplicate, missing or excessive observations; none can be credited as absence evidence.</summary>
    internal int InvalidCount()
    {
        lock (synchronization)
        {
            return invalid;
        }
    }

    /// <summary>Captures only fixed categories from a real request before its asynchronous header read begins.</summary>
    internal bool Start(IRequest request) => Start(request, request.IsNavigationRequest, Route(request.Url));

    /// <summary>Admits each identity once and fails closed at the fixed retention bound.</summary>
    private bool Start(object key, bool navigation, RouteKind route)
    {
        lock (synchronization)
        {
            if (entries.Count >= Limit || entries.ContainsKey(key))
            {
                invalid++;
                return false;
            }
            if (active++ == 0)
            {
                idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            entries.Add(key, new(entries.Count + 1, navigation, route));
            return true;
        }
    }

    /// <summary>Records the bounded response stage separately from request-header acquisition.</summary>
    internal void Response(object key, ResponseStage stage, int status = 0)
    {
        lock (synchronization)
        {
            if (entries.TryGetValue(key, out var entry))
            {
                entry.Stage = stage;
                if (status != 0)
                {
                    entry.Status = status;
                }
            }
            else
            {
                invalid++;
            }
        }
    }

    /// <summary>Marks the actual request header read complete, including a separately rejected failed read.</summary>
    internal void HeadersRead(object key)
    {
        lock (synchronization)
        {
            if (entries.TryGetValue(key, out var entry))
            {
                entry.HeadersPending = false;
            }
            else
            {
                invalid++;
            }
        }
    }

    /// <summary>Matches every terminal event to one admitted identity; duplicate or unknown events cannot decrement another request.</summary>
    internal void End(object key)
    {
        lock (synchronization)
        {
            if (!entries.TryGetValue(key, out var entry) || entry.Terminal)
            {
                invalid++;
                return;
            }
            entry.Terminal = true;
            if (--active == 0)
            {
                idle.TrySetResult();
            }
        }
    }

    /// <summary>Emits only generated ordinals, enums, booleans and status integers for unfinished observations.</summary>
    internal void Report()
    {
        lock (synchronization)
        {
            TestContext.Current.TestOutputHelper?.WriteLine(LedgerReport, entries.Count, active, invalid);
            foreach (var entry in entries.Values.Where(entry => !entry.Terminal || entry.HeadersPending || entry.Stage is not (ResponseStage.None or ResponseStage.Complete)))
            {
                TestContext.Current.TestOutputHelper?.WriteLine(RequestReport,
                    entry.Ordinal, entry.Route, entry.Navigation, entry.Status, entry.Terminal, entry.HeadersPending, entry.Stage);
            }
        }
    }

    /// <summary>Seeds positive completion, duplicate/unmatched terminal, duplicate start and capacity defects independently of browser execution.</summary>
    internal static void RequireControls()
    {
        var ledger = new BrowserTrafficRequests();
        var key = new object();
        var other = new object();
        Assert.True(ledger.Start(key, false, RouteKind.Other));
        Assert.True(ledger.Start(other, false, RouteKind.Other));
        Assert.False(ledger.IdleAsync().IsCompleted);
        ledger.End(key);
        ledger.End(key);
        ledger.End(new object());
        Assert.False(ledger.Start(key, false, RouteKind.Other));
        Assert.Equal(ExpectedInvalid, ledger.InvalidCount());
        Assert.Equal(1, ledger.ActiveCount());
        Assert.False(ledger.IdleAsync().IsCompleted);
        ledger.End(other);
        Assert.True(ledger.IdleAsync().IsCompleted && ledger.ActiveCount() == 0 && ledger.InvalidCount() == ExpectedInvalid);
        var bounded = new BrowserTrafficRequests();
        for (var index = 0; index < Limit; index++)
        {
            Assert.True(bounded.Start(new object(), false, RouteKind.Other));
        }

        Assert.False(bounded.Start(new object(), false, RouteKind.Other));
        Assert.Equal(1, bounded.InvalidCount());
    }

    /// <summary>Classifies only reviewed fixed paths; queries, origins and unexpected paths are never retained or printed.</summary>
    private static RouteKind Route(string address) => Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri.AbsolutePath switch
    {
        StatusPath => RouteKind.Status,
        CallbackPath => RouteKind.Callback,
        ResumePath => RouteKind.Resume,
        AuthorizationPath => RouteKind.Authorization,
        BrowserHttpsJourney.Login => RouteKind.Login,
        IssuerLoginPath => RouteKind.IssuerLogin,
        FactorPath => RouteKind.Factor,
        AdministratorJourney.Security => RouteKind.Security,
        _ => RouteKind.Other
    } : RouteKind.Other;

    /// <summary>Starts an idle ledger with an already-completed signal.</summary>
    private static TaskCompletionSource Completed()
    {
        var value = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        value.SetResult();
        return value;
    }

    /// <summary>Retains bounded scalar progress for one privately keyed request.</summary>
    private sealed class Observation(int ordinal, bool navigation, RouteKind route)
    {
        internal int Ordinal { get; } = ordinal;
        internal bool Navigation { get; } = navigation;
        internal RouteKind Route { get; } = route;
        internal int Status
        {
            get; set;
        }
        internal bool Terminal
        {
            get; set;
        }
        internal bool HeadersPending { get; set; } = true;
        internal ResponseStage Stage
        {
            get; set;
        }
    }
    internal enum ResponseStage
    {
        None, Headers, Completion, Body, Complete
    }
    private enum RouteKind
    {
        Other, Status, Callback, Resume, Authorization, Login, IssuerLogin, Factor, Security
    }
    private const string StatusPath = "/bff/session";
    private const string CallbackPath = "/callback";
    private const string ResumePath = "/bff/login/resume";
    private const string AuthorizationPath = "/connect/authorize";
    private const string IssuerLoginPath = "/account/administrator/browser/login";
    private const string FactorPath = "/account/administrator/browser/mfa";
}
