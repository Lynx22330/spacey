using Content.Shared.Chemistry.Reagent;
using Content.Shared.Tonic.Components;
using Content.Shared.Tonic.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Server.Chemistry.ReagentEffects
{
    /// <summary>
    /// Default metabolism for drink reagents. Attempts to find a TonicComponent on the target,
    /// and to update it's tonic values.
    /// </summary>
    public sealed partial class SatiateTonic : ReagentEffect
    {
        private const float DefaultTonicFactor = 3.0f;

        /// How much tonic is satiated each metabolism tick. Not currently tied to
        /// rate or anything.
        [DataField("factor")]
        public float TonicizeFactor { get; set; } = DefaultTonicFactor;

        /// Satiate tonic if a TonicComponent can be found
        public override void Effect(ReagentEffectArgs args)
        {
            var uid = args.SolutionEntity;
            if (args.EntityManager.TryGetComponent(uid, out TonicComponent? tonic))
                args.EntityManager.System<TonicSystem>().ModifyTonic(uid, tonic, TonicizeFactor);
        }

        protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
            => Loc.GetString("reagent-effect-guidebook-satiate-tonic", ("chance", Probability), ("relative", TonicizeFactor / DefaultTonicFactor));
    }
}
