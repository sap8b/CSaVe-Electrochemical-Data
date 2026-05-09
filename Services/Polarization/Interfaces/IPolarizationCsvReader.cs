using System.Collections.Generic;

namespace CSaVe_Electrochemical_Data;

/// <summary>
/// Reads a polarization curve CSV file and returns the raw data points in file order.
/// The reader must handle:
///   – GAMRY-style multi-column CSVs with a header row containing "Vf" and "Im" columns.
///   – Simple two-column headerless CSVs (current, voltage).
///   – Any CSV with headers containing recognisable aliases:
///       potential: "vf", "potential", "ewe", "v"
///       current:   "im", "current",   "i",   "ia"
/// Rows that cannot be parsed are silently skipped.
/// At least 20 finite rows must remain after filtering or an exception is thrown.
/// </summary>
public interface IPolarizationCsvReader
{
    /// <summary>
    /// Read <paramref name="csvPath"/> and return the data points in file order.
    /// </summary>
    /// <param name="csvPath">Full path to the CSV file.</param>
    /// <returns>Data points in the order they appear in the file.</returns>
    /// <exception cref="System.IO.FileNotFoundException">
    /// Thrown when <paramref name="csvPath"/> does not exist.
    /// </exception>
    /// <exception cref="System.FormatException">
    /// Thrown when required columns cannot be identified in the file.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when fewer than 20 finite data rows remain after filtering.
    /// </exception>
    IReadOnlyList<PolarizationPoint> Read(string csvPath);
}
