using System.CommandLine;

// Headless entry point over Sqlm.Core — makes the engine testable and scriptable without a UI
// (PLAN.md §3). Each subcommand is a thin wrapper; the actual planning/execution logic lives in
// Sqlm.Core so Sqlm.App can call the same code paths through the RPC bridge.

var projectOption = new Option<FileInfo>("--project")
{
    Description = "Path to a .sqlmproj file.",
    Required = true,
};

var planCommand = new Command("plan", "Introspect the source and print the migration plan without writing anything.")
{
    projectOption,
};
planCommand.SetAction(_ =>
{
    Console.WriteLine("plan: not yet implemented — see PLAN.md §5.2 step 2.");
    return 1;
});

var runCommand = new Command("run", "Execute a migration end to end.")
{
    projectOption,
};
runCommand.SetAction(_ =>
{
    Console.WriteLine("run: not yet implemented — see PLAN.md §5.2.");
    return 1;
});

var dryRunCommand = new Command("dry-run", "Execute the full pipeline against a capped, temporary SQLite file.")
{
    projectOption,
};
dryRunCommand.SetAction(_ =>
{
    Console.WriteLine("dry-run: not yet implemented — see PLAN.md §7.2.");
    return 1;
});

var root = new RootCommand("sqlm — headless MSSQL-to-SQLite migration engine (PLAN.md §3).")
{
    planCommand,
    runCommand,
    dryRunCommand,
};

return await root.Parse(args).InvokeAsync();
