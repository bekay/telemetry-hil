using ForgeHil.Core.Models;

namespace ForgeHil.Core.Interfaces
{
    /// <summary>
    /// Executes a named scenario end-to-end:
    /// configure device → run capture → validate → publish result.
    /// </summary>
    public interface IScenarioRunner
    {
        Task<ScenarioResult> RunAsync(ScenarioDefinition scenario, CancellationToken ct = default);
    }
}
