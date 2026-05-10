using CSaVe_Electrochemical_Data.Services.Polarization.Reactions;

using System;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// Holds the seven-parameter Butler-Volmer model for polarization curve fitting.
    /// All three half-reactions — metal oxidation, ORR, and HER — are represented by the
    /// full Butler-Volmer equation using a Nernst-fixed equilibrium potential and a
    /// symmetry factor β.  ORR additionally includes a Koutecky-Levich mass-transport
    /// correction to capture the limiting-current plateau.
    /// Equilibrium potentials for each reaction are fixed by the Nernst equation via
    /// <see cref="ElectrochemicalReaction"/> objects; they are not fit parameters.
    /// </summary>
    public sealed class BvModelParameters
    {
        private readonly IBvReaction _metalReaction;
        private readonly IBvReaction _orrReaction;
        private readonly IBvReaction _herReaction;

        // ── Constructor ───────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Initialises a new <see cref="BvModelParameters"/> bound to the given reaction objects.
        /// All other parameters are set via object-initialiser syntax after construction.
        /// </summary>
        /// <param name="metalReaction">Reaction object for the metal oxidation half-reaction (e.g. Fe/Fe²⁺).</param>
        /// <param name="orrReaction">Reaction object for the oxygen reduction half-reaction (ORR).</param>
        /// <param name="herReaction">Reaction object for the hydrogen evolution half-reaction (HER).</param>
        public BvModelParameters(
            IBvReaction metalReaction,
            IBvReaction orrReaction,
            IBvReaction herReaction)
        {
            _metalReaction = metalReaction ?? throw new ArgumentNullException(nameof(metalReaction));
            _orrReaction   = orrReaction   ?? throw new ArgumentNullException(nameof(orrReaction));
            _herReaction   = herReaction   ?? throw new ArgumentNullException(nameof(herReaction));
        }

        // ── Metal oxidation branch (Fe → Fe²⁺ + 2e⁻) ────────────────────────────────────────────
        /// <summary>Metal-oxidation exchange current density I₀,metal (A/cm²) at the Nernst equilibrium potential.</summary>
        public double I0Metal { get; init; }

        /// <summary>
        /// Metal-oxidation symmetry factor βₘₑₜₐₗ (dimensionless, 0 &lt; β &lt; 1);
        /// governs the forward/reverse asymmetry of the Butler-Volmer equation.
        /// </summary>
        public double BetaMetal { get; init; }

        /// <summary>Metal equilibrium potential Eₘₑₜₐₗ (V) fixed by the Nernst equation; not a fit parameter.</summary>
        public double EMetalEquilibriumV { get; init; }

        // ── ORR branch (O₂ + 2H₂O + 4e⁻ → 4OH⁻) ────────────────────────────────────────────────
        /// <summary>ORR exchange current density I₀,ₒᵣᵣ (A/cm²) at the Nernst equilibrium potential.</summary>
        public double I0Orr { get; init; }

        /// <summary>
        /// ORR symmetry factor βₒᵣᵣ (dimensionless, 0 &lt; β &lt; 1);
        /// governs the forward/reverse asymmetry of the Butler-Volmer equation.
        /// </summary>
        public double BetaOrr { get; init; }

        /// <summary>ORR mass-transport limiting current density iₗᵢₘ,ₒᵣᵣ (A/cm²); always a positive magnitude.</summary>
        public double IlimOrr { get; init; }

        /// <summary>ORR equilibrium potential Eₒᵣᵣ (V) fixed by the Nernst equation; not a fit parameter.</summary>
        public double EorrEquilibriumV { get; init; }

        // ── HER branch (2H⁺ + 2e⁻ → H₂) ─────────────────────────────────────────────────────────
        /// <summary>HER exchange current density I₀,ₕₑᵣ (A/cm²) at the Nernst equilibrium potential.</summary>
        public double I0Her { get; init; }

        /// <summary>
        /// HER symmetry factor βₕₑᵣ (dimensionless, 0 &lt; β &lt; 1);
        /// governs the cathodic/anodic asymmetry of the Butler-Volmer equation.
        /// </summary>
        public double BetaHer { get; init; }

        /// <summary>HER equilibrium potential Eₕₑᵣ (V) fixed by the Nernst equation; not a fit parameter.</summary>
        public double EherEquilibriumV { get; init; }

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

        // ── Public current-density methods ────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluates the net Butler-Volmer model current density (A/cm²) at a single electrode potential.
        /// Net current = metal oxidation + ORR + HER.
        /// </summary>
        /// <param name="potentialV">Electrode potential (V vs. reference).</param>
        /// <returns>Net signed current density (A/cm²); positive = net anodic.</returns>
        public double ComputeCurrentDensity(double potentialV) =>
            ComputeMetalOxidationComponent(potentialV)
          + ComputeOrrComponent(potentialV)
          + ComputeHerComponent(potentialV);

        /// <summary>
        /// Evaluates the metal-oxidation Butler-Volmer component at a single electrode potential.
        /// Uses the full BV equation referenced to the Nernst equilibrium potential.
        /// Net metal current is anodic (positive) above E_eq,metal.
        /// </summary>
        /// <param name="potentialV">Electrode potential (V vs. reference).</param>
        /// <returns>Metal oxidation net current density (A/cm²); positive = anodic (dissolution).</returns>
        public double ComputeMetalOxidationComponent(double potentialV)
        {
            double eta      = potentialV - EMetalEquilibriumV;
            double zFoverRT = _metalReaction.Z * ElectrochemicalConstants.F
                              / (ElectrochemicalConstants.R * _metalReaction.TemperatureKelvin);

            double forward  = Math.Exp(Math.Clamp( BetaMetal         * zFoverRT * eta, ExpClipMin, ExpClipMax));
            double reverse  = Math.Exp(Math.Clamp(-(1.0 - BetaMetal) * zFoverRT * eta, ExpClipMin, ExpClipMax));

            return I0Metal * (forward - reverse);
        }

        /// <summary>
        /// Evaluates the ORR Butler-Volmer component with Koutecky-Levich mass-transport correction
        /// at a single electrode potential.
        /// Kinetic current is computed from the full BV equation referenced to E_eq,ORR; the
        /// cathodic branch is then limited by the mass-transport plateau iₗᵢₘ,ₒᵣᵣ.
        /// Net ORR current is cathodic (negative) below E_eq,ORR.
        /// </summary>
        /// <param name="potentialV">Electrode potential (V vs. reference).</param>
        /// <returns>ORR net current density (A/cm²); non-positive in the cathodic region.</returns>
        public double ComputeOrrComponent(double potentialV)
        {
            double eta      = potentialV - EorrEquilibriumV;
            double zFoverRT = _orrReaction.Z * ElectrochemicalConstants.F
                              / (ElectrochemicalConstants.R * _orrReaction.TemperatureKelvin);

            double cathodic = Math.Exp(Math.Clamp(-(1.0 - BetaOrr) * zFoverRT * eta, ExpClipMin, ExpClipMax));
            double anodic   = Math.Exp(Math.Clamp( BetaOrr          * zFoverRT * eta, ExpClipMin, ExpClipMax));

            // Kinetic current (negative = cathodic ORR reduction).
            double iKinetic = -I0Orr * (cathodic - anodic);

            // Apply Koutecky-Levich mass-transport correction only to the cathodic branch.
            // Anodic reverse ORR is not mass-transport limited in this model.
            if (iKinetic >= 0.0)
                return iKinetic;

            // denom > 0 because iKinetic < 0 and IlimOrr > 0.
            double denom = IlimOrr - iKinetic;
            return iKinetic * IlimOrr / denom;
        }

        /// <summary>
        /// Evaluates the HER Butler-Volmer component at a single electrode potential using the
        /// full Butler-Volmer equation referenced to the Nernst equilibrium potential.
        /// Net HER current is cathodic (negative) below E_eq,HER.
        /// </summary>
        /// <param name="potentialV">Electrode potential (V vs. reference).</param>
        /// <returns>HER current density (A/cm²); non-positive in the cathodic region.</returns>
        public double ComputeHerComponent(double potentialV)
        {
            double eta      = potentialV - EherEquilibriumV;
            double zFoverRT = _herReaction.Z * ElectrochemicalConstants.F
                              / (ElectrochemicalConstants.R * _herReaction.TemperatureKelvin);

            double anodic   = Math.Exp(Math.Clamp( BetaHer         * zFoverRT * eta, ExpClipMin, ExpClipMax));
            double cathodic = Math.Exp(Math.Clamp(-(1.0 - BetaHer) * zFoverRT * eta, ExpClipMin, ExpClipMax));

            return -I0Her * (cathodic - anodic);
        }
    }
}
