using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CSaVe_Electrochemical_Data;

/// <summary>
/// Reads a polarization curve CSV file and returns the raw data points in file order.
/// Handles GAMRY-style multi-column CSVs (header with "Vf"/"Im") and simple two-column
/// headerless CSVs, as well as any CSV with recognised potential/current column aliases.
/// </summary>
public sealed class PolarizationCsvReader : IPolarizationCsvReader
{
    // Minimum number of finite data rows required for a valid polarization dataset.
    private const int MinRequiredRows = 20;

    // Priority-ordered column-name aliases for the potential axis.
    private static readonly string[] PotentialAliases = { "vf", "potential", "ewe", "v" };

    // Priority-ordered column-name aliases for the current axis.
    private static readonly string[] CurrentAliases   = { "im", "current", "i", "ia" };

    /// <summary>
    /// Read <paramref name="csvPath"/> and return the data points in file order.
    /// Rows that cannot be parsed are silently skipped.
    /// </summary>
    /// <param name="csvPath">Full path to the CSV file.</param>
    /// <returns>Data points in the order they appear in the file.</returns>
    /// <exception cref="FileNotFoundException">
    /// Thrown when <paramref name="csvPath"/> does not exist.
    /// </exception>
    /// <exception cref="FormatException">
    /// Thrown when required potential/current columns cannot be identified.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when fewer than <see cref="MinRequiredRows"/> finite rows remain after filtering.
    /// </exception>
    public IReadOnlyList<PolarizationPoint> Read(string csvPath)
    {
        if (!File.Exists(csvPath))
            throw new FileNotFoundException($"Polarization CSV not found: {csvPath}", csvPath);

        string[] allLines = File.ReadAllLines(csvPath);
        var points = new List<PolarizationPoint>();

        int potentialColIndex = -1;
        int currentColIndex   = -1;
        bool headerParsed     = false;
        bool isFormatA        = false; // true = has a header row; false = headerless two-column CSV

        foreach (string line in allLines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            string[] parts = trimmed.Split(',');

            if (!headerParsed)
            {
                headerParsed = true;

                // Detect header row: if the first token cannot be parsed as a float it is a header.
                if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    isFormatA = true;
                    DetectColumns(parts, out potentialColIndex, out currentColIndex);

                    if (potentialColIndex < 0)
                        throw new FormatException(
                            $"Cannot find a potential column (expected one of: {string.Join(", ", PotentialAliases)}) in {csvPath}.");
                    if (currentColIndex < 0)
                        throw new FormatException(
                            $"Cannot find a current column (expected one of: {string.Join(", ", CurrentAliases)}) in {csvPath}.");

                    // Header row processed; skip to next line.
                    continue;
                }

                // Headerless two-column CSV (Format B): column 0 = current, column 1 = voltage.
                potentialColIndex = 1;
                currentColIndex   = 0;
                // Fall through to parse this line as data.
            }

            // Skip GAMRY secondary header / unit rows whose first token is not an integer point number.
            if (isFormatA && !int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                continue;

            if (parts.Length <= Math.Max(potentialColIndex, currentColIndex))
                continue;

            if (!double.TryParse(parts[potentialColIndex].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double potential))
                continue;
            if (!double.TryParse(parts[currentColIndex].Trim(),   NumberStyles.Float, CultureInfo.InvariantCulture, out double current))
                continue;

            // Silently skip non-finite values.
            if (!double.IsFinite(potential) || !double.IsFinite(current))
                continue;

            points.Add(new PolarizationPoint { PotentialV = potential, CurrentA = current });
        }

        if (points.Count < MinRequiredRows)
            throw new InvalidOperationException(
                $"Insufficient data in {csvPath}: {points.Count} finite rows found; at least {MinRequiredRows} are required for BV fitting.");

        return points;
    }

    /// <summary>
    /// Identify the potential and current column indices from a CSV header row.
    /// Uses priority-ordered aliases for both axes; also recognises the legacy GAMRY
    /// "Vf" / "Im" column names used by the existing <c>PolarizationCurveXmlExporter</c>.
    /// </summary>
    /// <param name="headerParts">Trimmed tokens from the header row.</param>
    /// <param name="potentialCol">Receives the zero-based index of the potential column, or -1 if not found.</param>
    /// <param name="currentCol">Receives the zero-based index of the current column, or -1 if not found.</param>
    private static void DetectColumns(string[] headerParts, out int potentialCol, out int currentCol)
    {
        // Build a map from lower-cased column name to its index.
        var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int c = 0; c < headerParts.Length; c++)
        {
            string name = headerParts[c].Trim();
            if (!string.IsNullOrEmpty(name))
                colMap.TryAdd(name.ToLowerInvariant(), c);
        }

        potentialCol = -1;
        foreach (string alias in PotentialAliases)
        {
            if (colMap.TryGetValue(alias, out int idx)) { potentialCol = idx; break; }
        }

        currentCol = -1;
        foreach (string alias in CurrentAliases)
        {
            if (colMap.TryGetValue(alias, out int idx)) { currentCol = idx; break; }
        }
    }
}
