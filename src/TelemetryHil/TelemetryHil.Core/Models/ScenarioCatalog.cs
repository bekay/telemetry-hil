namespace TelemetryHil.Core.Models
{
    /// <summary>
    /// The built-in downhole scenario set, shared by the WPF executive and
    /// the headless runner. Mirrors config/scenarios/default.yaml — loading
    /// from the YAML file itself is still a TODO.
    /// </summary>
    public static class ScenarioCatalog
    {
        public static readonly IReadOnlyList<ScenarioDefinition> All =
        [
            new("normal_operation",     SensorMode: 0, FaultInjection: "none",          DurationSeconds: 30),
            new("shallow_run",          SensorMode: 0, FaultInjection: "none",          DurationSeconds: 30,
                SimPressurePsi: 1200,   SimTemperatureC: 45,   SimRotationRpm: 800,
                SimDepthM: 200,         SimTensionKn: 10,      SimLineSpeedMs: 2.5),
            new("deep_high_tension",    SensorMode: 0, FaultInjection: "none",          DurationSeconds: 30,
                SimPressurePsi: 13500,  SimTemperatureC: 160,  SimRotationRpm: 2800,
                SimDepthM: 4500,        SimTensionKn: 45,      SimLineSpeedMs: 0.5),
            new("uart_framing_error",   SensorMode: 0, FaultInjection: "framing_error", DurationSeconds: 10),
            new("sensor_dropout",       SensorMode: 3, FaultInjection: "stuck_value",   DurationSeconds: 15),
            new("tripping_out",         SensorMode: 0, FaultInjection: "none",          DurationSeconds: 30,
                SimPressurePsi: 9000,   SimTemperatureC: 120,  SimRotationRpm: 2200,
                SimDepthM: 3000,        SimTensionKn: 35,      SimLineSpeedMs: 4.5),
            new("high_rotation_stress", SensorMode: 0, FaultInjection: "none",          DurationSeconds: 30,
                SimPressurePsi: 7500,   SimTemperatureC: 110,  SimRotationRpm: 2900,
                SimDepthM: 2500,        SimTensionKn: 30,      SimLineSpeedMs: 1.5),
        ];

        public static ScenarioDefinition? Find(string name) =>
            All.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
