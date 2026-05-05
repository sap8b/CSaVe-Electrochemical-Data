# Python Utilities – CSaVe Electrochemical Data

## `plot_polarization_diagnostic.py`

Diagnostic script that plots raw Gamry DTA source data overlaid against the
CSaVe-exported XML polarization curve.  Use it to inspect branch-linkup
discrepancies near OCP (E\_corr).

### Required packages

```
numpy
matplotlib
```

Both are available via `pip install numpy matplotlib`.
The script also uses Python standard-library modules `argparse` and
`xml.etree.ElementTree` (no extra install needed).

---

### Invocation modes

#### Two-file mode – separate anodic and cathodic DTA files

```bash
python plot_polarization_diagnostic.py \
    --anodic  path/to/anodic.dta \
    --cathodic path/to/cathodic.dta \
    --xml     path/to/output.xml
```

#### Single-file mode – one cyclic polarization DTA file

```bash
python plot_polarization_diagnostic.py \
    --dta path/to/cycpol.dta \
    --xml path/to/output.xml
```

All arguments are **optional**.  Omit `--xml` to plot only the DTA data, or
omit the DTA arguments to plot only the XML trace.

---

### What the script produces

* An **Evans/polarization diagram** (potential on Y-axis, current density on
  X-axis with symmetric-log scale) showing:
  * Thin **blue dotted** line – anodic DTA branch
  * Thin **red dotted** line – cathodic DTA branch
  * Thick **black solid** line – merged XML data
  * Dashed **grey** horizontal line – E\_corr (OCP)

* **Stdout diagnostics** including:
  * E\_corr from the anodic / single DTA file
  * Lowest potential in the anodic branch and highest in the cathodic branch
  * Gap between the two branches near OCP
  * Average potential offset in the ±50 mV window around E\_corr
  * A suggestion if the gap exceeds 5 mV

* A **PNG file** saved alongside the XML (or in the current directory) with an
  auto-generated name based on the input filenames.

---

### Examples

```bash
# Plot only the XML
python plot_polarization_diagnostic.py --xml ExampleData/HY80-SHT_Anodic_TROUGH_200mV_115h.xml

# Plot a single cyclic-polarisation DTA alongside an XML
python plot_polarization_diagnostic.py \
    --dta ExampleData/my_experiment.dta \
    --xml ExampleData/my_experiment_output.xml
```
