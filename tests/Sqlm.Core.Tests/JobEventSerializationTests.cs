using System.Text.Json;
using FluentAssertions;
using Sqlm.Contracts;

namespace Sqlm.Core.Tests;

/// <summary>
/// The C# JobEvent union and the generated TypeScript contracts must not drift (PLAN.md §4.2,
/// §12). Locking the "kind" discriminator shape here is the C#-side half of that guarantee.
/// </summary>
public class JobEventSerializationTests
{
    [Fact]
    public void PhaseEvent_round_trips_through_the_kind_discriminator()
    {
        JobEvent original = new PhaseEvent(Phase.Copy);

        var json = JsonSerializer.Serialize(original, SqlmJsonOptions.Default);
        var roundTripped = JsonSerializer.Deserialize<JobEvent>(json, SqlmJsonOptions.Default);

        json.Should().Contain("\"kind\":\"phase\"");
        json.Should().Contain("\"phase\":\"copy\"", "enums serialize as camelCase strings, matching the generated TS union");
        roundTripped.Should().Be(original);
    }

    [Fact]
    public void FailedEvent_round_trips_with_a_resume_token()
    {
        JobEvent original = new FailedEvent(new SerializedError("boom", "detail", null), "resume-123");

        var json = JsonSerializer.Serialize(original, SqlmJsonOptions.Default);
        var roundTripped = JsonSerializer.Deserialize<JobEvent>(json, SqlmJsonOptions.Default);

        json.Should().Contain("\"kind\":\"failed\"");
        json.Should().Contain("\"resumeToken\":\"resume-123\"", "properties serialize as camelCase, matching the generated TS interfaces");
        roundTripped.Should().Be(original);
    }
}
