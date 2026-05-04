namespace CSaVe_Electrochemical_Data
{
    public class PolarizationCurveMetadata
    {
        public string InstituteName { get; set; } = "NRL";
        public string City { get; set; } = "Washington";
        public string State { get; set; } = "DC";
        public string Country { get; set; } = "USA";
        public string UNSCode { get; set; } = "";
        public string CommonName { get; set; } = "sample";
        public string SurfacePrep { get; set; } = "600 grit SiC";
        public double ExpArea { get; set; } = 1.0e-4; // 1 cm² in m²
        public double ClConc { get; set; } = 1.0;
        public double pH { get; set; } = 8.0;
        public double O2conc { get; set; } = 2.5e-4;
        public double S2conc { get; set; } = 0.08;
        public double Temperature { get; set; } = 50.0;
        public double Conductivity { get; set; } = 5.0;
        public double Flow { get; set; } = 0.0;
    }
}
