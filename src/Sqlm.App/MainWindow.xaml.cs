using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Sqlm.App.Rpc;
using Sqlm.Contracts;

namespace Sqlm.App;

/// <summary>
/// Hosts the React renderer in a single WebView2 control. PLAN.md §4.1: the UI is served from
/// <c>https://app.sqlm/index.html</c> via <c>SetVirtualHostNameToFolderMapping</c> — no local HTTP
/// listener, no port, no firewall prompt. Navigation is locked to that origin; real links are
/// handed off to the system browser instead of opening inside the app.
/// </summary>
public partial class MainWindow : Window
{
    private const string VirtualHostName = "app.sqlm";

    private readonly RpcRouter _rpcRouter = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoadedAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        await Browser.EnsureCoreWebView2Async().ConfigureAwait(true);

        var core = Browser.CoreWebView2;

#if !DEBUG
        core.Settings.AreDevToolsEnabled = false;
#endif

        core.SetVirtualHostNameToFolderMapping(VirtualHostName, ResolveAssetDirectory(), CoreWebView2HostResourceAccessKind.DenyCors);

        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
        core.WebMessageReceived += OnWebMessageReceivedAsync;

        core.Navigate($"https://{VirtualHostName}/index.html");
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var uri = new Uri(e.Uri);
        if (!string.Equals(uri.Host, VirtualHostName, StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
        }
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        // Real links (e.g. an "open documentation" action) go to the system browser, never a
        // second WebView2 window inside the app.
        e.Handled = true;
        Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
    }

    private async void OnWebMessageReceivedAsync(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        RpcRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<RpcRequest>(e.WebMessageAsJson, SqlmJsonOptions.Default);
        }
        catch (JsonException)
        {
            return;
        }

        if (request is null)
        {
            return;
        }

        var response = await _rpcRouter.DispatchAsync(request, CancellationToken.None).ConfigureAwait(true);
        Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(response, SqlmJsonOptions.Default));
    }

    /// <summary>
    /// Vite build output for <c>src/renderer</c>, copied next to the executable at publish time
    /// (PLAN.md §2.2). Resolved relative to the running assembly so both `dotnet run` from the
    /// repo and a published, self-contained exe find it the same way.
    /// </summary>
    private static string ResolveAssetDirectory() => Path.Combine(AppContext.BaseDirectory, "renderer");
}
