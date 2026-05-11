using System;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// A self-contained bounded Levenberg-Marquardt nonlinear least-squares solver.
    /// Operates entirely on <see cref="double"/> arrays; has no external dependencies.
    /// </summary>
    /// <remarks>
    /// Algorithm:
    ///   Iterate up to <see cref="MaxIterations"/> times:
    ///     1. Evaluate residual vector r = f(p).
    ///     2. Approximate Jacobian J by forward finite differences with step h_j = max(|p_j|, 1) * 1e-6.
    ///     3. Form the normal equations:  (J'J + λ·diag(J'J)) Δp = −J'r  (Marquardt scaling).
    ///     4. Solve for Δp using Gaussian elimination with partial pivoting.
    ///     5. Project p + Δp onto box constraints by clamping each component.
    ///     6. Accept step if new ‖r‖2 &lt; old ‖r‖2; reduce λ by factor 10.
    ///        Reject step (increase λ by 10) otherwise.
    ///     7. Terminate early when ‖Δp‖/‖p‖ &lt; <see cref="ParameterTolerance"/>.
    /// </remarks>
    internal static class LevenbergMarquardtSolver
    {
        /// <summary>Maximum number of LM iterations before the solver returns the best solution found.</summary>
        public const int MaxIterations = 600;

        /// <summary>Initial damping factor λ; small value starts the solver close to Gauss-Newton.</summary>
        public const double LambdaInitial = 1e-3;

        /// <summary>Factor by which λ is multiplied when a step is rejected.</summary>
        public const double LambdaIncreaseFactor = 10.0;

        /// <summary>Factor by which λ is divided when a step is accepted.</summary>
        public const double LambdaDecreaseFactor = 10.0;

        /// <summary>Upper limit for λ; prevents numerical overflow in the normal equations.</summary>
        public const double LambdaMax = 1e16;

        /// <summary>Convergence tolerance on the relative parameter change ‖Δp‖/‖p‖.</summary>
        public const double ParameterTolerance = 1e-8;

        // Finite-difference step scale factor; h_j = max(|p_j|, 1) * FdRelStep.
        // Small enough to give good derivative approximations yet large enough to avoid cancellation.
        private const double FdRelStep = 1e-6;

        /// <summary>
        /// Minimise the sum of squares of <paramref name="residualFunc"/>(p) subject to
        /// lb[i] &lt;= p[i] &lt;= ub[i].
        /// </summary>
        /// <param name="residualFunc">
        /// Function that, given a parameter vector, returns the residual vector whose
        /// squared norm is to be minimised.
        /// </param>
        /// <param name="p0">Initial parameter vector.  Mutated to satisfy box constraints on entry.</param>
        /// <param name="lb">Lower bounds for each parameter.</param>
        /// <param name="ub">Upper bounds for each parameter.</param>
        /// <returns>Optimised parameter vector satisfying the box constraints.</returns>
        public static double[] Solve(
            Func<double[], double[]> residualFunc,
            double[] p0,
            double[] lb,
            double[] ub)
        {
            int n = p0.Length;

            // Ensure initial point is feasible.
            double[] p = new double[n];
            for (int j = 0; j < n; j++)
                p[j] = Math.Clamp(p0[j], lb[j], ub[j]);

            double[] r  = residualFunc(p);
            double   ss = SumOfSquares(r);

            double lambda = LambdaInitial;

            for (int iter = 0; iter < MaxIterations; iter++)
            {
                // ── Step 2: forward finite-difference Jacobian ─────────────────────────────────
                double[][] J = ComputeJacobian(residualFunc, p, r, lb, ub);

                // ── Step 3: form J'J and J'r ──────────────────────────────────────────────────
                double[,] JtJ = new double[n, n];
                double[]  Jtr = new double[n];

                int m = r.Length;
                for (int k = 0; k < m; k++)
                {
                    for (int a = 0; a < n; a++)
                    {
                        Jtr[a] += J[k][a] * r[k];
                        for (int b = 0; b < n; b++)
                            JtJ[a, b] += J[k][a] * J[k][b];
                    }
                }

                // ── Step 3 (cont.): apply Marquardt scaling and form augmented matrix ─────────
                double[,] A = new double[n, n];
                for (int a = 0; a < n; a++)
                {
                    for (int b = 0; b < n; b++)
                        A[a, b] = JtJ[a, b];
                    // Marquardt scaling: augment diagonal with lambda * diag(J'J).
                    double diag = JtJ[a, a];
                    A[a, a] = diag + lambda * Math.Max(diag, 1e-30);
                }

                // ── Step 4: solve (A) Δp = -J'r via Gaussian elimination ─────────────────────
                double[] rhs = new double[n];
                for (int a = 0; a < n; a++)
                    rhs[a] = -Jtr[a];

                if (!TryGaussianElimination(A, rhs, out double[] dp))
                {
                    // Matrix was singular — increase damping and retry.
                    lambda = Math.Min(lambda * LambdaIncreaseFactor, LambdaMax);
                    continue;
                }

                // ── Step 5: project trial point onto box constraints ──────────────────────────
                double[] pTrial = new double[n];
                for (int j = 0; j < n; j++)
                    pTrial[j] = Math.Clamp(p[j] + dp[j], lb[j], ub[j]);

                double[] rTrial = residualFunc(pTrial);
                double   ssTrial = SumOfSquares(rTrial);

                // ── Step 6: accept or reject ──────────────────────────────────────────────────
                if (ssTrial < ss)
                {
                    // Accept: update p, r, ss; reduce damping.
                    p  = pTrial;
                    r  = rTrial;
                    ss = ssTrial;
                    lambda = Math.Max(lambda / LambdaDecreaseFactor, 1e-20);
                }
                else
                {
                    // Reject: increase damping.
                    lambda = Math.Min(lambda * LambdaIncreaseFactor, LambdaMax);
                }

                // ── Step 7: convergence check ─────────────────────────────────────────────────
                double pNorm  = Norm(p);
                double dpNorm = Norm(dp);
                if (pNorm > 0 && dpNorm / pNorm < ParameterTolerance)
                    break;
            }

            return p;
        }

        /// <summary>
        /// Compute the forward finite-difference Jacobian J[i][j] = ∂r_i/∂p_j.
        /// Each column is computed by perturbing p_j by h_j = max(|p_j|, 1) * FdRelStep.
        /// For parameters at the upper bound the perturbation is negated to stay feasible.
        /// </summary>
        private static double[][] ComputeJacobian(
            Func<double[], double[]> residualFunc,
            double[] p,
            double[] r0,
            double[] lb,
            double[] ub)
        {
            int m = r0.Length;
            int n = p.Length;

            double[][] J = new double[m][];
            for (int i = 0; i < m; i++)
                J[i] = new double[n];

            double[] pPerturb = (double[])p.Clone();

            for (int j = 0; j < n; j++)
            {
                double h = Math.Max(Math.Abs(p[j]), 1.0) * FdRelStep;
                // Use backward step if the parameter is at its upper bound.
                double sign = (p[j] + h > ub[j]) ? -1.0 : 1.0;
                pPerturb[j] = Math.Clamp(p[j] + sign * h, lb[j], ub[j]);

                double[] rPerturb = residualFunc(pPerturb);
                double   hActual  = pPerturb[j] - p[j];

                if (hActual != 0.0)
                {
                    for (int i = 0; i < m; i++)
                        J[i][j] = (rPerturb[i] - r0[i]) / hActual;
                }
                // else column stays zero — parameter is pinned at a bound.

                pPerturb[j] = p[j]; // restore
            }

            return J;
        }

        /// <summary>
        /// Solve the n×n linear system A·x = b using Gaussian elimination with partial pivoting.
        /// Returns <c>false</c> and sets <paramref name="x"/> to an empty array if the matrix is
        /// numerically singular.
        /// </summary>
        /// <param name="A">n×n coefficient matrix (will be modified in place).</param>
        /// <param name="b">Right-hand-side vector (will be modified in place).</param>
        /// <param name="x">On success, receives the solution vector; otherwise an empty array.</param>
        /// <returns><c>true</c> if the system was solved; <c>false</c> if the matrix is singular.</returns>
        private static bool TryGaussianElimination(double[,] A, double[] b, out double[] x)
        {
            int n = b.Length;

            for (int col = 0; col < n; col++)
            {
                // Partial pivoting: find the row with the largest absolute value in this column.
                int    pivotRow = col;
                double pivotVal = Math.Abs(A[col, col]);
                for (int row = col + 1; row < n; row++)
                {
                    double absVal = Math.Abs(A[row, col]);
                    if (absVal > pivotVal) { pivotVal = absVal; pivotRow = row; }
                }

                if (pivotVal < 1e-20)
                {
                    x = new double[0];
                    return false; // singular or near-singular
                }

                // Swap rows.
                if (pivotRow != col)
                {
                    for (int k = 0; k < n; k++)
                    {
                        (A[col, k], A[pivotRow, k]) = (A[pivotRow, k], A[col, k]);
                    }
                    (b[col], b[pivotRow]) = (b[pivotRow], b[col]);
                }

                // Eliminate below.
                for (int row = col + 1; row < n; row++)
                {
                    double factor = A[row, col] / A[col, col];
                    for (int k = col; k < n; k++)
                        A[row, k] -= factor * A[col, k];
                    b[row] -= factor * b[col];
                }
            }

            // Back-substitution.
            x = new double[n];
            for (int row = n - 1; row >= 0; row--)
            {
                double sum = b[row];
                for (int k = row + 1; k < n; k++)
                    sum -= A[row, k] * x[k];
                if (Math.Abs(A[row, row]) < 1e-20)
                {
                    x = new double[0];
                    return false;
                }
                x[row] = sum / A[row, row];
            }

            return true;
        }

        /// <summary>Returns the sum of squares of all elements in <paramref name="v"/>.</summary>
        private static double SumOfSquares(double[] v)
        {
            double ss = 0.0;
            foreach (double x in v)
                ss += x * x;
            return ss;
        }

        /// <summary>Returns the Euclidean norm (‖v‖₂) of <paramref name="v"/>.</summary>
        private static double Norm(double[] v)
        {
            double ss = 0.0;
            foreach (double x in v)
                ss += x * x;
            return Math.Sqrt(ss);
        }
    }
}
