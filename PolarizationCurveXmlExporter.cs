using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace CSaVe_Electrochemical_Data
{
    public static class PolarizationCurveXmlExporter
    {
        /// <summary>
        /// Parses a CSV file and returns a list of (current_A, voltage_V) data points.
        /// Supports two formats:
        ///   Format A — GAMRY-converted multi-column CSV with a header row (e.g. "Pt,T,Vf,Im,...").
        ///   Format B — Simple 2-column headerless CSV (current, voltage).
        /// </summary>
        private static List<(double I, double V)> ParseCsv(string csvPath)
        {
            var points = new List<(double I, double V)>();
            string[] allLines = File.ReadAllLines(csvPath);

            int currentColIndex = 0;
            int voltageColIndex = 1;
            bool headerParsed = false;
            bool isFormatA = false;

            foreach (string line in allLines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                string[] parts = trimmed.Split(',');

                if (!headerParsed)
                {
                    headerParsed = true;
                    string firstToken = parts[0].Trim();

                    if (!double.TryParse(firstToken, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        // Format A: header row — find column indices for Im (current) and Vf (voltage)
                        isFormatA = true;
                        currentColIndex = -1;
                        voltageColIndex = -1;
                        for (int c = 0; c < parts.Length; c++)
                        {
                            string col = parts[c].Trim();
                            if (string.Equals(col, "Im", StringComparison.OrdinalIgnoreCase))
                                currentColIndex = c;
                            else if (string.Equals(col, "Vf", StringComparison.OrdinalIgnoreCase))
                                voltageColIndex = c;
                        }
                        if (currentColIndex < 0)
                            throw new FormatException("CSV header does not contain an 'Im' column. Check that the selected file is a valid CSV produced by CSaVe Electrochemical Data.");
                        if (voltageColIndex < 0)
                            throw new FormatException("CSV header does not contain a 'Vf' column. Check that the selected file is a valid CSV produced by CSaVe Electrochemical Data.");

                        // Header row processed; move to next line for data
                        continue;
                    }
                    // else Format B: first line is data — fall through to parse it below
                }

                // Skip secondary header/unit rows: the first token must parse as an integer point number
                // (e.g. skip lines like "#  s  V vs. Ref." that appear in some GAMRY exports)
                if (isFormatA && !int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    continue;

                if (parts.Length <= Math.Max(currentColIndex, voltageColIndex))
                    continue;

                string currentStr = parts[currentColIndex].Trim();
                string voltageStr = parts[voltageColIndex].Trim();

                if (!double.TryParse(currentStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double current))
                    throw new FormatException($"Cannot parse '{currentStr}' as a number. Check that the selected file is a valid CSV produced by CSaVe Electrochemical Data.");

                if (!double.TryParse(voltageStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double voltage))
                    throw new FormatException($"Cannot parse '{voltageStr}' as a number. Check that the selected file is a valid CSV produced by CSaVe Electrochemical Data.");

                points.Add((current, voltage));
            }
            return points;
        }

        /// <summary>
        /// Exports a PolarizationCurve XML file from either a single CSV (single-file mode)
        /// or separate anodic and cathodic CSV files (two-file mode).
        /// </summary>
        /// <param name="primaryCsvPath">
        /// Required. Path to the anodic-only CSV, or to the single combined CSV when
        /// <paramref name="cathodicCsvPath"/> is null or empty.
        /// </param>
        /// <param name="cathodicCsvPath">
        /// Optional. Path to the cathodic CSV. Pass null or an empty string to use single-file mode,
        /// where both branches are extracted automatically from <paramref name="primaryCsvPath"/>.
        /// In single-file mode the CSV may contain either a true cyclic potentiodynamic sweep or
        /// two separate scans (anodic + cathodic) concatenated into one file.  All rows are sorted
        /// by voltage so that the potential axis is monotonically increasing, OCP is located as the
        /// point of minimum |I| in the sorted data, and the data are then split at OCP into the
        /// anodic (V &gt;= V_ocp) and cathodic (V &lt; V_ocp) branches.
        /// </param>
        /// <param name="outputXmlPath">Destination XML file path.</param>
        /// <param name="metadata">Experiment metadata to embed in the XML.</param>
        public static void Export(
            string primaryCsvPath,
            string cathodicCsvPath,
            string outputXmlPath,
            PolarizationCurveMetadata metadata)
        {
            List<(double I, double V)> anodicTrimmed;
            List<(double I, double V)> cathodicTrimmed;

            if (string.IsNullOrWhiteSpace(cathodicCsvPath))
            {
                // ── Single-file mode ─────────────────────────────────────────────────────
                // The CSV may contain a true cyclic sweep OR two separate scans
                // (anodic + cathodic) concatenated into one file.  In either case the
                // correct approach is the same: sort all rows by voltage so that the
                // potential axis is monotonically increasing, then locate OCP as the
                // point of minimum |I|, and finally split the sorted data at OCP into
                // the anodic (V >= V_ocp) and cathodic (V < V_ocp) branches.

                // 1. Parse the single file.
                List<(double I, double V)> allPoints = ParseCsv(primaryCsvPath);
                if (allPoints.Count == 0)
                    throw new InvalidOperationException("CSV contains no data points.");

                // 2. Sort ascending by voltage to ensure a monotonic potential axis.
                allPoints = allPoints.OrderBy(p => p.V).ToList();

                // 3. Find OCP: the point of minimum |I| in the sorted list.
                int ocpIndex = 0;
                double minAbsI = Math.Abs(allPoints[0].I);
                for (int i = 1; i < allPoints.Count; i++)
                {
                    double absI = Math.Abs(allPoints[i].I);
                    if (absI < minAbsI)
                    {
                        minAbsI = absI;
                        ocpIndex = i;
                    }
                }
                double vOcp = allPoints[ocpIndex].V;

                // 4. Split at OCP.
                //    Anodic branch: oxidation region, keep V >= V_ocp.
                //    Cathodic branch: reduction region, keep V < V_ocp.
                anodicTrimmed   = allPoints.Where(p => p.V >= vOcp).ToList();
                cathodicTrimmed = allPoints.Where(p => p.V <  vOcp).ToList();
            }
            else
            {
                // ── Two-file mode ────────────────────────────────────────────────────────
                //
                // The anodic and cathodic experiments are run in separate potentiostatic
                // sessions and the open-circuit potential (OCP) can drift slightly between
                // them.  To produce a well-aligned merged curve the following steps are
                // applied after trimming each forward sweep to its apex:
                //
                //   a. OCP is found independently in each branch as the voltage at min |I|.
                //   b. The half-difference between the two OCPs is computed.
                //   c. The anodic branch potentials are shifted down by halfDiff and the
                //      cathodic branch potentials are shifted up by halfDiff so that both
                //      branches share a common aligned OCP (vOcpMid).
                //   d. Both branches are then trimmed at vOcpMid to remove the small
                //      "wrong-side" segment that each experiment starts on (typically the
                //      instrument begins ~20 mV on the opposite side of OCP).
                //
                // This alignment eliminates the gap or step artefact visible near OCP in
                // the merged XML when the two experiments are not perfectly matched.

                // 1. Parse both CSV files.
                List<(double I, double V)> anodicPoints   = ParseCsv(primaryCsvPath);
                List<(double I, double V)> cathodicPoints = ParseCsv(cathodicCsvPath);

                if (anodicPoints.Count == 0)
                    throw new InvalidOperationException("Anodic CSV contains no data points.");
                if (cathodicPoints.Count == 0)
                    throw new InvalidOperationException("Cathodic CSV contains no data points.");

                // 2. Trim anodic return sweep: keep only the forward sweep up to the apex (max voltage).
                //    Cyclic polarization goes up to the apex then returns; discard the return portion.
                int anodicApexIndex = 0;
                for (int i = 1; i < anodicPoints.Count; i++)
                {
                    if (anodicPoints[i].V > anodicPoints[anodicApexIndex].V)
                        anodicApexIndex = i;
                }
                anodicPoints = anodicPoints.GetRange(0, anodicApexIndex + 1);

                // 3. Trim cathodic return sweep using a noise-tolerant forward scan.
                //    Scan forward tracking the running minimum.  Stop as soon as V rises
                //    more than apexNoiseTolerance above the running minimum — this signals
                //    the turn-around onto the return sweep, even if there is a fractional
                //    overshoot at the true apex due to instrument noise.
                const double apexNoiseTolerance = 0.005; // 5 mV
                int cathodicApexIndex = 0;
                double runningMinV = cathodicPoints[0].V;
                for (int i = 1; i < cathodicPoints.Count; i++)
                {
                    if (cathodicPoints[i].V <= runningMinV)
                    {
                        runningMinV = cathodicPoints[i].V;
                        cathodicApexIndex = i;
                    }
                    else if (cathodicPoints[i].V > runningMinV + apexNoiseTolerance)
                    {
                        // V has risen clearly above the running minimum — we are on the return sweep.
                        break;
                    }
                    // else: V is within the noise band above the running minimum — keep scanning.
                }
                cathodicPoints = cathodicPoints.GetRange(0, cathodicApexIndex + 1);

                // 4. Find OCP from the anodic branch: V at min |I|.
                int anOcpIdx = 0;
                double anMinAbsI = Math.Abs(anodicPoints[0].I);
                for (int i = 1; i < anodicPoints.Count; i++)
                {
                    double absI = Math.Abs(anodicPoints[i].I);
                    if (absI < anMinAbsI) { anMinAbsI = absI; anOcpIdx = i; }
                }
                double vOcpAnodic = anodicPoints[anOcpIdx].V;

                // 5. Find OCP from the cathodic branch: V at min |I|.
                int catOcpIdx = 0;
                double catMinAbsI = Math.Abs(cathodicPoints[0].I);
                for (int i = 1; i < cathodicPoints.Count; i++)
                {
                    double absI = Math.Abs(cathodicPoints[i].I);
                    if (absI < catMinAbsI) { catMinAbsI = absI; catOcpIdx = i; }
                }
                double vOcpCathodic = cathodicPoints[catOcpIdx].V;

                // 6. Compute the aligned midpoint OCP and the per-branch shift.
                //    Shift each branch toward the midpoint so they overlap near E_corr.
                double halfDiff = (vOcpAnodic - vOcpCathodic) / 2.0;
                double vOcpMid  = vOcpAnodic - halfDiff; // == (vOcpAnodic + vOcpCathodic) / 2

                // 7. Apply the potential shift to every point in each branch.
                //    Anodic branch shifts down by halfDiff; cathodic branch shifts up by halfDiff.
                anodicPoints   = anodicPoints
                    .Select(p => (p.I, p.V - halfDiff))
                    .ToList();
                cathodicPoints = cathodicPoints
                    .Select(p => (p.I, p.V + halfDiff))
                    .ToList();

                // 8. Trim both branches at the aligned OCP boundary.
                //    Anodic branch: oxidation data, keep V >= vOcpMid.
                //    Cathodic branch: reduction data, keep V < vOcpMid.
                anodicTrimmed   = anodicPoints.Where(p => p.V >= vOcpMid).ToList();
                cathodicTrimmed = cathodicPoints.Where(p => p.V < vOcpMid).ToList();
            }

            // 7. Combine and sort ascending by voltage
            var merged = anodicTrimmed.Concat(cathodicTrimmed)
                                      .OrderBy(p => p.V)
                                      .ToList();

            // 8. Write XML
            var xmlSettings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                Encoding = System.Text.Encoding.UTF8
            };

            using var writer = XmlWriter.Create(outputXmlPath, xmlSettings);

            writer.WriteStartDocument();
            writer.WriteStartElement("PolarizationCurve");

            // InstituteLocation
            writer.WriteStartElement("InstituteLocation");
            writer.WriteElementString("Name", metadata.InstituteName);
            writer.WriteElementString("City", metadata.City);
            writer.WriteElementString("State", metadata.State);
            writer.WriteElementString("Country", metadata.Country);
            writer.WriteEndElement();

            // MaterialData
            writer.WriteStartElement("MaterialData");
            writer.WriteElementString("UNSCode", metadata.UNSCode);
            writer.WriteElementString("CommonName", metadata.CommonName);
            writer.WriteElementString("SurfacePrep", metadata.SurfacePrep);
            writer.WriteStartElement("ExpArea");
            writer.WriteAttributeString("units", "m2");
            writer.WriteString(metadata.ExpArea.ToString("E6", CultureInfo.InvariantCulture));
            writer.WriteEndElement();
            writer.WriteElementString("NdataPoints", merged.Count.ToString());
            writer.WriteEndElement();

            // ElectrolyteData
            writer.WriteStartElement("ElectrolyteData");
            WriteValueWithUnits(writer, "ClConc", "M", metadata.ClConc.ToString(CultureInfo.InvariantCulture));
            WriteValueWithUnits(writer, "pH", "unitless", metadata.pH.ToString(CultureInfo.InvariantCulture));
            WriteValueWithUnits(writer, "O2conc", "M", metadata.O2conc.ToString("E6", CultureInfo.InvariantCulture));
            WriteValueWithUnits(writer, "S2conc", "M", metadata.S2conc.ToString(CultureInfo.InvariantCulture));
            WriteValueWithUnits(writer, "Temperature", "C", metadata.Temperature.ToString(CultureInfo.InvariantCulture));
            WriteValueWithUnits(writer, "Conductivity", "S/m", metadata.Conductivity.ToString(CultureInfo.InvariantCulture));
            WriteValueWithUnits(writer, "Flow", "m/s", metadata.Flow.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();

            // Data
            writer.WriteStartElement("Data");
            for (int n = 0; n < merged.Count; n++)
            {
                double currentDensity = merged[n].I / metadata.ExpArea;
                double voltage = merged[n].V;

                WriteValueWithUnits(writer, "point", "unitless", (n + 1).ToString());
                // Component currents (anodici, orri, heri) are set to 0 for raw experimental data.
                // The CSaVe schema reserves these fields for model-fitted components.
                WriteValueWithUnits(writer, "anodici", "A/m2", "0");
                WriteValueWithUnits(writer, "orri", "A/m2", "0");
                WriteValueWithUnits(writer, "heri", "A/m2", "0");
                WriteValueWithUnits(writer, "totali", "A/m2", currentDensity.ToString("E6", CultureInfo.InvariantCulture));
                WriteValueWithUnits(writer, "Vapp", "Vsce", voltage.ToString("F6", CultureInfo.InvariantCulture));
            }
            writer.WriteEndElement(); // Data

            writer.WriteEndElement(); // PolarizationCurve
            writer.WriteEndDocument();
        }

        private static void WriteValueWithUnits(XmlWriter writer, string elementName, string units, string value)
        {
            writer.WriteStartElement(elementName);
            writer.WriteAttributeString("units", units);
            writer.WriteString(value);
            writer.WriteEndElement();
        }
    }
}
