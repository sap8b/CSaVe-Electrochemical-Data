using CSaVe_Electrochemical_Data.Services.Polarization.Reactions;

using System;
using System.Collections.Generic;
using System.Linq;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// Holds the Butler-Volmer model for polarization curve fitting.
    /// Stores a list of <see cref="ReactionParameters"/> sorted ascending by equilibrium potential,
    /// one per registered electrochemical reaction. Each reaction may have a Koutecky-Levich
    /// limiting current (Ilim &gt; 0) applied to its cathodic branch.
    /// Backward-compatible named properties are provided for the UI layer.
    /// </summary>
    public sealed class BvModelParameters
    {
        // ── Inner record ─────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Fitted parameters for one electrochemical half-reaction.
        /// </summary>
        public sealed class ReactionParameters
        {
            public ReactionParameters(IBvReaction reaction, double i0, double beta, double ilim, bool isIncluded)
            {
                Reaction   = reaction   ?? throw new ArgumentNullException(nameof(reaction));
                I0         = i0;
                Beta       = beta;
                Ilim       = ilim;
                IsIncluded = isIncluded;
            }

            /// <summary>The electrochemical half-reaction (thermodynamic and kinetic data).</summary>
            public IBvReaction Reaction { get; }

            /// <summary>Fitted exchange current density (A/cm2).</summary>
            public double I0 { get; }

            /// <summary>Fitted symmetry factor β (dimensionless).</summary>
            public double Beta { get; }

            /// <summary>
            /// Fitted Koutecky-Levich limiting current density (A/cm2).
            /// Zero means no mass-transport correction is applied.
            /// </summary>
            public double Ilim { get; }

            /// <summary>Whether this reaction contributes to the model current.</summary>
            public bool IsIncluded { get; }
        }

        // ── Fields ────────────────────────────────────────────────────────────────────────────────
        private readonly IReadOnlyList<ReactionParameters> _reactions;

        // ── Constructor ───────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Initialises a new <see cref="BvModelParameters"/> from a list of per-reaction parameters
        /// sorted ascending by <see cref="IBvReaction.EquilibriumPotentialVshe"/>.
        /// </summary>
        /// <param name="reactions">Per-reaction fitted parameters. Must not be null.</param>
        public BvModelParameters(IReadOnlyList<ReactionParameters> reactions)
        {
            _reactions = reactions ?? throw new ArgumentNullException(nameof(reactions));
        }

        // ── Corrosion potential (derived, not a fit parameter) ────────────────────────────────────
        /// <summary>
        /// Corrosion potential Ecorr (V vs. reference), defined as the zero-crossing of the net model
        /// current. Computed post-fit via binary search; not included in the parameter vector.
        /// </summary>
        public double Ecorr { get; init; }

        // ── Exponential argument clip limits ─────────────────────────────────────────────────────
        // Clipping to [-50, 50] prevents overflow in exp() while retaining all physically meaningful values.
        private const double ExpClipMin = -50.0;
        private const double ExpClipMax =  50.0;

        // ── Per-reaction list access ──────────────────────────────────────────────────────────────
        /// <summary>Returns the fitted parameters for the reaction matching <paramref name="name"/>, or null.</summary>
        public ReactionParameters GetReactionParam(ReactionType name) =>
            _reactions.FirstOrDefault(r => r.Reaction.Name == name);

        // ── Backward-compatible named property accessors ──────────────────────────────────────────
        // These allow the UI layer (PolarizationAnalysisService, WinForms) to continue reading
        // named fields without modification while the internal model has been generalised to a list.

        /// <summary>Fitted metal-oxidation exchange current density I₀,metal (A/cm2).</summary>
        public double I0Metal => GetReactionParam(ReactionType.MetalOxidation)?.I0 ?? 0.0;

        /// <summary>Fitted metal-oxidation symmetry factor βₘₑₜₐₗ (dimensionless).</summary>
        public double BetaMetal => GetReactionParam(ReactionType.MetalOxidation)?.Beta ?? 0.5;

        /// <summary>Fitted cathodic limiting current density for metal reduction (A/cm2). Zero = no KL correction.</summary>
        public double IlimMetal => GetReactionParam(ReactionType.MetalOxidation)?.Ilim ?? 0.0;

        /// <summary>Metal equilibrium potential Eₘₑₜₐₗ (V) fixed by the Nernst equation; not a fit parameter.</summary>
        public double EMetalEquilibriumV => GetReactionParam(ReactionType.MetalOxidation)?.Reaction.EquilibriumPotentialVshe ?? 0.0;

        /// <summary>Whether the metal oxidation reaction is included in the model.</summary>
        public bool IncludeMetal => GetReactionParam(ReactionType.MetalOxidation)?.IsIncluded ?? false;

        /// <summary>Fitted ORR exchange current density I₀,ₒᵣᵣ (A/cm2).</summary>
        public double I0Orr => GetReactionParam(ReactionType.OxygenReduction)?.I0 ?? 0.0;

        /// <summary>Fitted ORR symmetry factor βₒᵣᵣ (dimensionless).</summary>
        public double BetaOrr => GetReactionParam(ReactionType.OxygenReduction)?.Beta ?? 0.5;

        /// <summary>Fitted ORR mass-transport limiting current density iₗᵢₘ,ₒᵣᵣ (A/cm2).</summary>
        public double IlimOrr => GetReactionParam(ReactionType.OxygenReduction)?.Ilim ?? 0.0;

        /// <summary>ORR equilibrium potential Eₒᵣᵣ (V) fixed by the Nernst equation; not a fit parameter.</summary>
        public double EorrEquilibriumV => GetReactionParam(ReactionType.OxygenReduction)?.Reaction.EquilibriumPotentialVshe ?? 0.0;

        /// <summary>Whether the ORR reaction is included in the model.</summary>
        public bool IncludeOrr => GetReactionParam(ReactionType.OxygenReduction)?.IsIncluded ?? false;

        /// <summary>Fitted HER exchange current density I₀,ₕₑᵣ (A/cm2).</summary>
        public double I0Her => GetReactionParam(ReactionType.HydrogenEvolution)?.I0 ?? 0.0;

        /// <summary>Fitted HER symmetry factor βₕₑᵣ (dimensionless).</summary>
        public double BetaHer => GetReactionParam(ReactionType.HydrogenEvolution)?.Beta ?? 0.5;

        /// <summary>Fitted HER limiting current density (A/cm2). Zero = no KL correction.</summary>
        public double IlimHer => GetReactionParam(ReactionType.HydrogenEvolution)?.Ilim ?? 0.0;

        /// <summary>HER equilibrium potential Eₕₑᵣ (V) fixed by the Nernst equation; not a fit parameter.</summary>
        public double EherEquilibriumV => GetReactionParam(ReactionType.HydrogenEvolution)?.Reaction.EquilibriumPotentialVshe ?? 0.0;

        /// <summary>Whether the HER reaction is included in the model.</summary>
        public bool IncludeHer => GetReactionParam(ReactionType.HydrogenEvolution)?.IsIncluded ?? false;

        // ── Public current-density methods ────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluates the net Butler-Volmer model current density (A/cm2) at a single electrode potential.
        /// Net current = sum of all included reactions, each optionally limited by Koutecky-Levich.
        /// </summary>
        /// <param name="potentialV">Electrode potential (V vs. reference).</param>
        /// <returns>Net signed current density (A/cm2); positive = net anodic.</returns>
        public double ComputeCurrentDensity(double potentialV)
        {
            double total = 0.0;
            foreach (ReactionParameters rp in _reactions)
            {
                if (rp.IsIncluded)
                    total += ComputeReactionComponent(rp, potentialV);
            }
            return total;
        }

        /// <summary>
        /// Evaluates the metal-oxidation Butler-Volmer component at a single electrode potential.
        /// Net metal current is anodic (positive) above E_eq,metal.
        /// </summary>
        public double ComputeMetalOxidationComponent(double potentialV)
        {
            ReactionParameters rp = GetReactionParam(ReactionType.MetalOxidation);
            return rp != null && rp.IsIncluded ? ComputeReactionComponent(rp, potentialV) : 0.0;
        }

        /// <summary>
        /// Evaluates the ORR Butler-Volmer component with Koutecky-Levich mass-transport correction.
        /// Net ORR current is cathodic (negative) below E_eq,ORR.
        /// </summary>
        public double ComputeOrrComponent(double potentialV)
        {
            ReactionParameters rp = GetReactionParam(ReactionType.OxygenReduction);
            return rp != null && rp.IsIncluded ? ComputeReactionComponent(rp, potentialV) : 0.0;
        }

        /// <summary>
        /// Evaluates the HER Butler-Volmer component at a single electrode potential.
        /// Net HER current is cathodic (negative) below E_eq,HER.
        /// </summary>
        public double ComputeHerComponent(double potentialV)
        {
            ReactionParameters rp = GetReactionParam(ReactionType.HydrogenEvolution);
            return rp != null && rp.IsIncluded ? ComputeReactionComponent(rp, potentialV) : 0.0;
        }

        // ── Private helper ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Computes the Butler-Volmer current for one reaction, applying a Koutecky-Levich
        /// correction to the cathodic branch if <paramref name="rp"/>.Ilim &gt; 0.
        /// </summary>
        private static double ComputeReactionComponent(ReactionParameters rp, double potentialV)
        {
            double eta      = potentialV - rp.Reaction.EquilibriumPotentialVshe;
            double zFoverRT = rp.Reaction.Z * ElectrochemicalConstants.F
                              / (ElectrochemicalConstants.R * rp.Reaction.TemperatureKelvin);

            double forward  = Math.Exp(Math.Clamp( rp.Beta         * zFoverRT * eta, ExpClipMin, ExpClipMax));
            double reverse  = Math.Exp(Math.Clamp(-(1.0 - rp.Beta) * zFoverRT * eta, ExpClipMin, ExpClipMax));

            // Standard Butler-Volmer: positive = anodic (above E_eq), negative = cathodic (below E_eq).
            double iKinetic = rp.I0 * (forward - reverse);

            // Apply Koutecky-Levich mass-transport correction to the cathodic branch only.
            if (rp.Ilim > 0.0 && iKinetic < 0.0)
            {
                // denom = Ilim - iKinetic > 0 because iKinetic < 0 and Ilim > 0.
                double denom = rp.Ilim - iKinetic;
                iKinetic = iKinetic * rp.Ilim / denom;
            }

            return iKinetic;
        }
    }
}
