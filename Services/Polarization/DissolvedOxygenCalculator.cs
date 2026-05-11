using System;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// Calculates dissolved-oxygen properties (concentration and diffusion coefficient) in NaCl
    /// solutions as a function of temperature and chloride concentration.
    ///
    /// Ported from MATLAB class O2.m authored by Steven A. Policastro, Ph.D., NRL.
    ///
    /// References used in the original MATLAB model:
    ///   - Analytica Chimica Acta, Vol. 279, Issue 2, 15 July 1993, pp. 213–221
    ///   - Wilke-Chang / modified Stokes-Einstein diffusion model
    /// </summary>
    public static class DissolvedOxygenCalculator
    {
        // ── Physical constants ────────────────────────────────────────────────────────────────────
        private const double M_O2   = 32.0;      // g/mol – molar mass of O₂
        private const double M_H2O  = 18.01528;  // g/mol – molar mass of H₂O
        private const double M_Cl   = 35.45;     // g/mol – molar mass of Cl⁻
        private const double P_O2_atm = 0.2095;  // atm   – partial pressure of O₂ in dry air
        private const double VO2    = 22.414;    // L/mol – molar volume used in Stokes model
        private const double Phi    = 2.6;       // association factor for water (Wilke-Chang)

        // ── Temperature-parameterised b-vector for the diffusivity Stokes model ─────────────────
        // Each row i contains [a, b, c] for LinearLinear(a, b, c, T_K):
        //   b_i(T_K) = (a + b*T_K) / (1 + c*T_K)
        // Row index matches MATLAB params(i,:) (1-based → 0-based).
        private static readonly double[,] DiffParams = new double[6, 3]
        {
            {  0.193015581,  -0.000936823,  -3738.145703 },   // b[0] – prefactor K
            {  0.586220598,  -0.001982362,  -0.003767555 },   // b[1] – A'  (viscosity concentration term)
            { -2058331786,    7380780.538,  -725742.0949 },   // b[2] – B'₁ (viscosity T term 1)
            {  -12341118,     7397.380585, -1024619.196  },   // b[3] – B'₂ (viscosity T term 2)
            { -0.082481761,   8.05605e-06,  -0.005230993 },   // b[4] – η₀ pre-exponential
            { -13685.50552,   11.9799009,   -0.05822883  },   // b[5] – η₀ exponential coefficient
        };

        // ────────────────────────────────────────────────────────────────────────────────────────
        // Public API
        // ────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Calculates the dissolved O₂ concentration in an NaCl solution using a
        /// Henry's-law correlation that accounts for temperature and salinity.
        /// </summary>
        /// <param name="tempC">Solution temperature (°C).</param>
        /// <param name="chlorideM">Cl⁻ concentration (mol/L).</param>
        /// <returns>Dissolved O₂ concentration in mol/cm³.</returns>
        public static double CalcConcentrationMolPerCm3(double tempC, double chlorideM)
        {
            double gPerCm3 = CalcConcentrationGPerCm3(tempC, chlorideM);
            return gPerCm3 / M_O2;
        }

        /// <summary>
        /// Calculates the diffusion coefficient of dissolved O₂ in an NaCl solution using
        /// a modified Stokes-Einstein / Wilke-Chang model parameterised by temperature and
        /// chloride concentration.
        /// </summary>
        /// <param name="tempC">Solution temperature (°C).</param>
        /// <param name="chlorideM">Cl⁻ concentration (mol/L).</param>
        /// <returns>O₂ diffusion coefficient (cm²/s).</returns>
        public static double CalcDiffusivityCm2PerS(double tempC, double chlorideM)
        {
            double tempK = tempC + 273.15;
            double[] b = new double[6];
            for (int i = 0; i < 6; i++)
                b[i] = LinearLinear(DiffParams[i, 0], DiffParams[i, 1], DiffParams[i, 2], tempK);
            return StokesModel2(b, tempK, chlorideM);
        }

        /// <summary>
        /// Estimates the ORR limiting current density using
        ///   i_lim = n·F·D_O₂·c_O₂ / δ
        /// where n = 4, F = 96 485 C/mol, D_O₂ and c_O₂ are computed from T and Cl⁻,
        /// and δ is the diffusion-layer thickness.
        /// </summary>
        /// <param name="tempC">Solution temperature (°C).</param>
        /// <param name="chlorideM">Cl⁻ concentration (mol/L).</param>
        /// <param name="diffLayerThicknessCm">Diffusion-layer thickness δ (cm).</param>
        /// <returns>ORR limiting current density i_lim (A/cm²).</returns>
        public static double CalcOrrIlimAcm2(double tempC, double chlorideM, double diffLayerThicknessCm)
        {
            if (diffLayerThicknessCm <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(diffLayerThicknessCm), "Diffusion-layer thickness must be > 0.");

            const double nElectrons = 4.0;
            double cO2 = CalcConcentrationMolPerCm3(tempC, chlorideM);
            double dO2 = CalcDiffusivityCm2PerS(tempC, chlorideM);
            return nElectrons * ElectrochemicalConstants.F * dO2 * cO2 / diffLayerThicknessCm;
        }

        // ────────────────────────────────────────────────────────────────────────────────────────
        // Private helpers (direct ports of private MATLAB methods in O2.m)
        // ────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Calculates dissolved O₂ concentration via Henry's law (g/cm³).
        /// Ported from MATLAB O2.calcConcO2.
        /// </summary>
        private static double CalcConcentrationGPerCm3(double tempC, double chlorideM)
        {
            double tempK = tempC + 273.15;

            // Chloride mass concentration (mg Cl per L of solution)
            double clMgL = M_Cl * chlorideM * 1000.0;

            // Henry's-constant parameters for O₂ using acentric-factor correlation
            const double acentric = 0.022;
            const double a1 = 31820.0, b1 = -229.9, c1 = -19.12, d1 = 0.3081;
            const double a2 = -1409.0, b2 = 10.4,   c2 = 0.8628,  d2 = -0.0005235, d3 = 0.07464;

            double num1   = a1 * acentric + a2;
            double num2   = b1 * acentric + b2;
            double denom1 = c1 * acentric + c2;
            double denom2 = 1.0 + denom1 * tempK;

            double lnHs0 = (num1 + num2 * tempK) / denom2;

            // Salinity correction
            double salinity = 0.001 * clMgL;
            double num3   = d1 + d2 * tempK;
            double denom3 = 1.0 + d3 * tempK;
            double expTerm = (num3 / denom3) * salinity;

            double kH = Math.Exp(lnHs0 + expTerm);   // Henry's constant (atm·L/mol)

            // Dissolved O₂ concentration: c [mol/L] = p_O2 [atm] / K_H [atm·L/mol]
            double cMolL  = P_O2_atm / kH;
            double cGPerL  = cMolL * M_O2;
            return cGPerL / 1000.0;  // g/cm³
        }

        /// <summary>
        /// Modified Stokes-Einstein / Wilke-Chang diffusion model for O₂ in NaCl.
        /// Ported from MATLAB O2.StokesModel2 (6-parameter form, O2Diffusivity_RevB).
        /// </summary>
        private static double StokesModel2(double[] b, double tempK, double cCl)
        {
            const double exponent = 0.6;

            double eta0 = b[4] * Math.Exp(b[5] / tempK);
            double bigB = b[2] + b[3] * (tempK - 273.15);
            double bigA = b[1];
            double eta  = eta0 * (1.0 + bigA * Math.Sqrt(cCl) + bigB * cCl);

            return b[0] * (Math.Sqrt(Phi * M_H2O) * tempK) / Math.Pow(VO2 * eta, exponent);
        }

        /// <summary>
        /// Rational temperature model: y = (a + b·x) / (1 + c·x).
        /// Ported from MATLAB LinearLinear().
        /// </summary>
        private static double LinearLinear(double a, double b, double c, double x)
        {
            return (a + b * x) / (1.0 + c * x);
        }
    }
}
