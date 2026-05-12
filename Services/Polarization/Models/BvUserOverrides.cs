namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// Optional user-specified starting values and fix flags for BV curve fitting.
    /// When a reaction is fixed (<see cref="FixMetal"/>, <see cref="FixOrr"/>,
    /// <see cref="FixHer"/>), the specified i₀ and β values are held constant
    /// throughout the Levenberg-Marquardt optimisation (effectively skipping that
    /// reaction's parameter adjustment).  When a reaction is not fixed but values
    /// are provided, those values are used as the initial guess for LM.
    /// </summary>
    public sealed class BvUserOverrides
    {
        // ── Metal oxidation ───────────────────────────────────────────────────────────────────────
        /// <summary>User-specified initial exchange current density for metal oxidation (A/cm2).</summary>
        public double? I0Metal { get; init; }

        /// <summary>User-specified initial symmetry factor β for metal oxidation (dimensionless, 0–1).</summary>
        public double? BetaMetal { get; init; }

        /// <summary>User-specified initial limiting current density for metal oxidation (A/cm2).</summary>
        public double? IlimMetal { get; init; }

        /// <summary>
        /// When <c>true</c>, the metal-oxidation i₀ and β are held fixed at the user-specified
        /// values and are not adjusted by the LM optimiser.
        /// </summary>
        public bool FixMetal { get; init; }

        // ── ORR ──────────────────────────────────────────────────────────────────────────────────
        /// <summary>User-specified initial exchange current density for ORR (A/cm2).</summary>
        public double? I0Orr { get; init; }

        /// <summary>User-specified initial symmetry factor β for ORR (dimensionless, 0–1).</summary>
        public double? BetaOrr { get; init; }

        /// <summary>User-specified initial ORR limiting current density (A/cm2).</summary>
        public double? IlimOrr { get; init; }

        /// <summary>
        /// When <c>true</c>, the ORR i₀, β, and iₗᵢₘ are held fixed at the user-specified
        /// values and are not adjusted by the LM optimiser.  If <see cref="IlimOrr"/> is
        /// <c>null</c> and this flag is <c>true</c>, the automatically computed initial
        /// estimate for iₗᵢₘ,ORR will be frozen (the auto-estimate is still used as the
        /// fixed value).
        /// </summary>
        public bool FixOrr { get; init; }

        // ── HER ──────────────────────────────────────────────────────────────────────────────────
        /// <summary>User-specified initial exchange current density for HER (A/cm2).</summary>
        public double? I0Her { get; init; }

        /// <summary>User-specified initial symmetry factor β for HER (dimensionless, 0–1).</summary>
        public double? BetaHer { get; init; }

        /// <summary>User-specified initial limiting current density for HER (A/cm2).</summary>
        public double? IlimHer { get; init; }

        /// <summary>
        /// When <c>true</c>, the HER i₀ and β are held fixed at the user-specified values
        /// and are not adjusted by the LM optimiser.
        /// </summary>
        public bool FixHer { get; init; }

        // ── Reaction inclusion flags ──────────────────────────────────────────────────────────────
        /// <summary>
        /// When <c>true</c> (default), the metal oxidation reaction is included in the fit.
        /// When <c>false</c>, its contribution to the model current is zeroed and its parameters
        /// are not optimised.
        /// </summary>
        public bool IncludeMetal { get; init; } = true;

        /// <summary>
        /// When <c>true</c> (default), ORR is included in the fit.
        /// When <c>false</c>, its contribution to the model current is zeroed and its parameters
        /// are not optimised.
        /// </summary>
        public bool IncludeOrr { get; init; } = true;

        /// <summary>
        /// When <c>true</c> (default), HER is included in the fit.
        /// When <c>false</c>, its contribution to the model current is zeroed and its parameters
        /// are not optimised.
        /// </summary>
        public bool IncludeHer { get; init; } = true;
    }
}
