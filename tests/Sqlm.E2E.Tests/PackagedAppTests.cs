namespace Sqlm.E2E.Tests;

/// <summary>
/// PLAN.md §11 E2E suite: Playwright connecting over CDP to the WebView2 instance of the
/// packaged app — connect → plan → run → browse → edit → save. Requires the built
/// <c>Sqlm.App</c> executable, which only exists on Windows (§13); there's nothing to drive yet
/// since Sqlm.App has no UI beyond the empty shell from Phase 0.
/// </summary>
public class PackagedAppTests
{
    [Fact(Skip = "Requires the packaged Sqlm.App on Windows (PLAN.md §13) and a real UI (Phase 4+).")]
    public void Connect_plan_run_browse_edit_save()
    {
    }
}
