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
        /// Parses a CSV file with two columns (current_A, voltage_V_vs_SCE) and returns a list of data points.
        /// </summary>
        private static List<(double I, double V)> ParseCsv(string csvPath)
        {
            var points = new List<(double I, double V)>();
            foreach (string line in File.ReadAllLines(csvPath))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                string[] parts = trimmed.Split(',');
                if (parts.Length < 2)
                    throw new FormatException($"Invalid CSV line (expected 2 columns): \"{line}\"");

                if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double current))
                    throw new FormatException($"Cannot parse current value: \"{parts[0].Trim()}\"");

                if (!double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double voltage))
                    throw new FormatException($"Cannot parse voltage value: \"{parts[1].Trim()}\"");

                points.Add((current, voltage));
            }
            return points;
        }

        /// <summary>
        /// Exports a PolarizationCurve XML file by merging an anodic CSV and a cathodic CSV.
        /// </summary>
        public static void Export(
            string anodicCsvPath,
            string cathodicCsvPath,
            string outputXmlPath,
            PolarizationCurveMetadata metadata)
        {
            // 1. Parse both CSV files
            List<(double I, double V)> anodicPoints = ParseCsv(anodicCsvPath);
            List<(double I, double V)> cathodicPoints = ParseCsv(cathodicCsvPath);

            if (anodicPoints.Count == 0)
                throw new InvalidOperationException("Anodic CSV contains no data points.");
            if (cathodicPoints.Count == 0)
                throw new InvalidOperationException("Cathodic CSV contains no data points.");

            // 2. Find E_corr: index with minimum |I| in anodic CSV
            int ecorrIndex = 0;
            double minAbsI = Math.Abs(anodicPoints[0].I);
            for (int i = 1; i < anodicPoints.Count; i++)
            {
                double absI = Math.Abs(anodicPoints[i].I);
                if (absI < minAbsI)
                {
                    minAbsI = absI;
                    ecorrIndex = i;
                }
            }
            double vEcorr = anodicPoints[ecorrIndex].V;

            // 3. Trim anodic branch: keep points where V >= V_ecorr.
            //    The anodic (oxidation) sweep runs from E_corr upward in voltage.
            var anodicTrimmed = anodicPoints.Where(p => p.V >= vEcorr).ToList();

            // 4. Trim cathodic branch: keep points where V < V_ecorr (remove overlap near E_corr).
            //    The cathodic (reduction) sweep runs from near E_corr downward in voltage.
            var cathodicTrimmed = cathodicPoints.Where(p => p.V < vEcorr).ToList();

            // 5. Combine and sort ascending by voltage
            var merged = anodicTrimmed.Concat(cathodicTrimmed)
                                      .OrderBy(p => p.V)
                                      .ToList();

            // 6. Write XML
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
                WriteValueWithUnits(writer, "totali", "A/m2", currentDensity.ToString("G6", CultureInfo.InvariantCulture));
                WriteValueWithUnits(writer, "Vapp", "Vsce", voltage.ToString("F2", CultureInfo.InvariantCulture));
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
