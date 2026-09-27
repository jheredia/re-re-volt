namespace Vehicle
{
    /// <summary>Neutral public identifiers used by the Stage 5.1 blind handling comparison.</summary>
    public enum HandlingModelId
    {
        A = 0,
        B = 1,
        C = 2,
        D = 3
    }

    public static class HandlingModelCatalog
    {
        public static bool IsDefined(HandlingModelId model)
        {
            int value = (int)model;
            return value >= (int)HandlingModelId.A && value <= (int)HandlingModelId.D;
        }

        // Candidate agents change only their own entry after their implementation is complete.
        public static bool IsImplemented(HandlingModelId model) => model == HandlingModelId.A;

        public static HandlingModelId ResolveImplementedModel(HandlingModelId selected) =>
            IsDefined(selected) && IsImplemented(selected) ? selected : HandlingModelId.A;

        public static string GetPublicLabel(HandlingModelId model) => IsDefined(model) ? $"MODEL {model}" : "MODEL ?";
    }

    /// <summary>One runtime dispatch point; candidate implementations remain outside input, combat and presentation.</summary>
    internal interface IVehicleHandlingModelRuntime
    {
        HandlingModelId Id { get; }
        void FixedStep(VehicleController vehicle);
        void OnCombatModifiersChanged(VehicleController vehicle);
        void Reset(VehicleController vehicle);
    }

    internal static class VehicleHandlingModelFactory
    {
        public static IVehicleHandlingModelRuntime Create(HandlingModelId selected)
        {
            // B-D deliberately fall back to the accepted baseline until each bounded candidate is delivered.
            switch (HandlingModelCatalog.ResolveImplementedModel(selected))
            {
                default:
                    return BaselineHandlingModelRuntime.Instance;
            }
        }
    }

    internal sealed class BaselineHandlingModelRuntime : IVehicleHandlingModelRuntime
    {
        public static readonly BaselineHandlingModelRuntime Instance = new BaselineHandlingModelRuntime();

        private BaselineHandlingModelRuntime() { }

        public HandlingModelId Id => HandlingModelId.A;
        public void FixedStep(VehicleController vehicle) => vehicle.FixedStepBaselineHandling();
        public void OnCombatModifiersChanged(VehicleController vehicle) => vehicle.ApplyBaselineCombatGrip();
        public void Reset(VehicleController vehicle) { }
    }
}
