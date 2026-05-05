"""
plot_polarization_diagnostic.py
================================
Diagnostic script for CSaVe Electrochemical Data polarization curves.

Plots raw Gamry DTA source data overlaid against the exported XML so that
OCP/branch-linkup discrepancies near E_corr are immediately visible.

Usage
-----
Two-file mode (separate anodic and cathodic DTA files):
    python plot_polarization_diagnostic.py \\
        --anodic path/to/anodic.dta \\
        --cathodic path/to/cathodic.dta \\
        --xml path/to/output.xml

Single-file mode (one cyclic polarization DTA file):
    python plot_polarization_diagnostic.py \\
        --dta path/to/cycpol.dta \\
        --xml path/to/output.xml

All arguments are optional; omit any source to skip that trace.

Required packages: numpy, matplotlib (stdlib: argparse, xml.etree.ElementTree)
"""

import argparse
import os
import sys
import xml.etree.ElementTree as ET

import numpy as np
import matplotlib
import matplotlib.pyplot as plt


# ---------------------------------------------------------------------------
# DTA file parsing
# ---------------------------------------------------------------------------

def parse_dta(path):
    """Parse a Gamry .dta file and return arrays (V, I).

    Gamry .dta files are tab-delimited.  The polarization curve data lives in
    the ``CURVE`` section (keyword token at the start of a line).  The section
    header is followed by a column-name line (containing ``Vf`` and ``Im``),
    a units line, and then numeric data rows that each start with an integer
    point index.  The section ends when a new keyword token appears.

    The parser scans forward from the ``CURVE`` keyword to find the
    column-name line (identified by the presence of both ``Vf`` and ``Im``
    tokens, case-insensitive), then skips one units row and collects data.

    Returns
    -------
    V : np.ndarray
        Working-electrode potential in V vs. reference.
    I : np.ndarray
        Measured current in A.
    """
    V_list = []
    I_list = []

    with open(path, encoding="utf-8", errors="replace") as fh:
        lines = fh.readlines()

    def tokenize(line):
        """Split a DTA line on tabs, strip whitespace, drop empty trailing tokens."""
        toks = [t.strip() for t in line.rstrip("\n").split("\t")]
        while toks and toks[-1] == "":
            toks.pop()
        return toks

    def first_nonempty(toks):
        """Return the first non-empty token (mirrors C# RemoveEmptyEntries[0])."""
        for t in toks:
            if t:
                return t
        return ""

    # ── Step 1: locate the CURVE keyword line ────────────────────────────────
    curve_line_idx = None
    for i, line in enumerate(lines):
        toks = tokenize(line)
        if toks and toks[0] == "CURVE":
            curve_line_idx = i
            break

    if curve_line_idx is None:
        raise ValueError(f"No 'CURVE' section found in '{path}'.")

    # ── Step 2: scan forward to find the column-name header ──────────────────
    # The column-name line is identified by containing both 'Vf' and 'Im'
    # tokens (case-insensitive) somewhere in the CURVE section preamble.
    vf_col = None
    im_col = None
    col_header_idx = None

    for i in range(curve_line_idx + 1, len(lines)):
        toks = tokenize(lines[i])
        if not toks:
            continue  # skip blank lines
        col_lower = [t.lower() for t in toks]
        if "vf" in col_lower and "im" in col_lower:
            vf_col = col_lower.index("vf")
            im_col = col_lower.index("im")
            col_header_idx = i
            break
        # If we encounter a new section keyword, the CURVE section ended
        # without a proper column header — give up.
        first = first_nonempty(toks)
        if first and first == first.upper() and not first[0].isdigit() and first != "#":
            break

    if vf_col is None or im_col is None:
        raise ValueError(
            f"Could not find 'Vf' and 'Im' column headers in the CURVE section of '{path}'."
        )

    # ── Step 3: skip one units row, then read data rows ──────────────────────
    # The units row immediately follows the column-name header.
    data_start_idx = col_header_idx + 2  # +1 units row, then data begins

    for i in range(data_start_idx, len(lines)):
        toks = tokenize(lines[i])
        if not toks:
            continue  # skip blank lines within data section

        first = first_nonempty(toks)

        # A non-integer first token signals a new section keyword — stop.
        try:
            int(first)
        except ValueError:
            break

        # Parse the data row.
        try:
            vf = float(toks[vf_col])
            im = float(toks[im_col])
        except (IndexError, ValueError):
            continue

        V_list.append(vf)
        I_list.append(im)

    if not V_list:
        raise ValueError(f"No data found in CURVE section of '{path}'.")

    return np.array(V_list), np.array(I_list)


# ---------------------------------------------------------------------------
# XML file parsing
# ---------------------------------------------------------------------------

def parse_xml(path):
    """Parse a CSaVe PolarizationCurve XML file.

    The XML stores data as flat repeated elements inside ``<Data>``:
        <point units="unitless">N</point>
        <totali units="A/m2">value</totali>
        <Vapp units="Vsce">value</Vapp>
        ...

    Returns
    -------
    V_xml : np.ndarray
        Applied potential in V vs. SCE.
    I_xml : np.ndarray
        Total current density in A/m².
    exp_area : float
        Exposed area in m² from ``<ExpArea>`` in ``<MaterialData>``.
    """
    tree = ET.parse(path)
    root = tree.getroot()

    # Read exposed area from MaterialData
    exp_area = None
    mat = root.find("MaterialData")
    if mat is not None:
        ea_el = mat.find("ExpArea")
        if ea_el is not None and ea_el.text:
            exp_area = float(ea_el.text.strip())
    if exp_area is None:
        exp_area = 1.0  # fallback — current density already stored in XML

    # Parse flat Data elements
    data_el = root.find("Data")
    if data_el is None:
        raise ValueError(f"No <Data> element found in '{path}'.")

    V_list = []
    I_list = []
    pending_i = None  # totali value waiting for a matching Vapp

    for child in data_el:
        tag = child.tag.lower()
        if tag == "totali":
            pending_i = float(child.text.strip())
        elif tag == "vapp":
            if pending_i is not None:
                V_list.append(float(child.text.strip()))
                I_list.append(pending_i)
                pending_i = None

    if not V_list:
        raise ValueError(f"No data points found in <Data> of '{path}'.")

    return np.array(V_list), np.array(I_list), exp_area


# ---------------------------------------------------------------------------
# Single-file branch splitting (mirrors PolarizationCurveXmlExporter.cs)
# ---------------------------------------------------------------------------

def split_single_file_branches(V, I):
    """Split a full cyclic polarization sweep into anodic and cathodic branches.

    This mirrors the branch-splitting logic in ``PolarizationCurveXmlExporter.cs``
    (steps 2–5 of single-file mode):

    1. Find the global voltage maximum (apexMax) and minimum (apexMin).
    2. Determine scan direction: whichever apex comes first in the time series
       defines the first sweep direction.
       - Anodic-first  (apexMax < apexMin): data goes OCP → Vmax → Vmin
       - Cathodic-first (apexMin < apexMax): data goes OCP → Vmin → Vmax
       Data after the second apex (return sweep) is discarded.
    3. Find OCP (E_corr) as the point of minimum |I| across all data.
    4. Trim: anodic segment keeps V >= V_ocp; cathodic segment keeps V < V_ocp.

    Parameters
    ----------
    V, I : array_like
        Voltage (V) and current (A) from the DTA file, in acquisition order.

    Returns
    -------
    V_an, I_an : np.ndarray  — anodic branch
    V_cat, I_cat : np.ndarray — cathodic branch
    v_ocp : float — OCP voltage
    """
    V = np.asarray(V, dtype=float)
    I = np.asarray(I, dtype=float)

    apex_max_idx = int(np.argmax(V))
    apex_min_idx = int(np.argmin(V))

    # OCP: point of minimum |I| in the full sweep
    ocp_idx = int(np.argmin(np.abs(I)))
    v_ocp = V[ocp_idx]

    if apex_max_idx < apex_min_idx:
        # Anodic-first sweep: OCP → Vmax → Vmin
        anodic_seg_V = V[: apex_max_idx + 1]
        anodic_seg_I = I[: apex_max_idx + 1]
        cathodic_seg_V = V[apex_max_idx + 1 : apex_min_idx + 1]
        cathodic_seg_I = I[apex_max_idx + 1 : apex_min_idx + 1]
    else:
        # Cathodic-first sweep: OCP → Vmin → Vmax
        cathodic_seg_V = V[: apex_min_idx + 1]
        cathodic_seg_I = I[: apex_min_idx + 1]
        anodic_seg_V = V[apex_min_idx + 1 : apex_max_idx + 1]
        anodic_seg_I = I[apex_min_idx + 1 : apex_max_idx + 1]

    # Trim at OCP boundary
    an_mask = anodic_seg_V >= v_ocp
    cat_mask = cathodic_seg_V < v_ocp

    return (
        anodic_seg_V[an_mask],
        anodic_seg_I[an_mask],
        cathodic_seg_V[cat_mask],
        cathodic_seg_I[cat_mask],
        v_ocp,
    )


# ---------------------------------------------------------------------------
# Two-file branch trimming (mirrors PolarizationCurveXmlExporter.cs)
# ---------------------------------------------------------------------------

def trim_two_file_branches(V_an, I_an, V_cat, I_cat):
    """Trim anodic and cathodic DTA branches for two-file mode.

    Mirrors steps 2–6 of two-file mode in ``PolarizationCurveXmlExporter.cs``:

    1. Trim anodic forward sweep: keep only data up to the apex (max V).
    2. Trim cathodic forward sweep: keep only data up to the apex (min V).
    3. Find E_corr from the anodic branch: point of minimum |I|.
    4. Anodic branch: keep V >= E_corr.
    5. Cathodic branch: keep V < E_corr.

    Returns
    -------
    V_an_t, I_an_t : np.ndarray — trimmed anodic branch
    V_cat_t, I_cat_t : np.ndarray — trimmed cathodic branch
    v_ecorr : float — E_corr voltage
    """
    V_an = np.asarray(V_an, dtype=float)
    I_an = np.asarray(I_an, dtype=float)
    V_cat = np.asarray(V_cat, dtype=float)
    I_cat = np.asarray(I_cat, dtype=float)

    # Trim anodic to forward sweep (up to apex)
    an_apex = int(np.argmax(V_an))
    V_an = V_an[: an_apex + 1]
    I_an = I_an[: an_apex + 1]

    # Trim cathodic to forward sweep (down to apex)
    cat_apex = int(np.argmin(V_cat))
    V_cat = V_cat[: cat_apex + 1]
    I_cat = I_cat[: cat_apex + 1]

    # E_corr from anodic branch
    ecorr_idx = int(np.argmin(np.abs(I_an)))
    v_ecorr = V_an[ecorr_idx]

    # Trim at E_corr boundary
    an_mask = V_an >= v_ecorr
    cat_mask = V_cat < v_ecorr

    return V_an[an_mask], I_an[an_mask], V_cat[cat_mask], I_cat[cat_mask], v_ecorr


# ---------------------------------------------------------------------------
# Diagnostics
# ---------------------------------------------------------------------------

def print_diagnostics(V_an, I_an, V_cat, I_cat, v_ecorr, exp_area):
    """Print OCP / branch-linkup diagnostics to stdout.

    Parameters
    ----------
    V_an, I_an   : anodic branch (already trimmed, V >= E_corr)
    V_cat, I_cat : cathodic branch (already trimmed, V < E_corr)
    v_ecorr      : OCP / E_corr in V
    exp_area     : exposed area in m² (used only to report units)
    """
    print("\n" + "=" * 60)
    print("OCP / Branch-Linkup Diagnostics")
    print("=" * 60)
    print(f"  1. E_corr (OCP from anodic branch, min |I|): {v_ecorr:.4f} V")

    # Lowest-voltage point in the anodic branch
    if len(V_an) > 0:
        an_low_v = V_an[np.argmin(V_an)]
        print(f"  2. Lowest V in anodic branch (V >= E_corr): {an_low_v:.4f} V")
    else:
        an_low_v = np.nan
        print("  2. Anodic branch is empty.")

    # Highest-voltage point in the cathodic branch
    if len(V_cat) > 0:
        cat_high_v = V_cat[np.argmax(V_cat)]
        print(f"  3. Highest V in cathodic branch (V < E_corr): {cat_high_v:.4f} V")
    else:
        cat_high_v = np.nan
        print("  3. Cathodic branch is empty.")

    # Gap between items 2 and 3
    if not (np.isnan(an_low_v) or np.isnan(cat_high_v)):
        gap = an_low_v - cat_high_v
        print(f"  4. Gap (anodic_low_V - cathodic_high_V): {gap*1000:.2f} mV")
    else:
        gap = np.nan
        print("  4. Gap: N/A (one or both branches empty)")

    # Average potential offset between branches in ±50 mV window around E_corr
    window = 0.050  # 50 mV
    an_window_mask = (V_an >= v_ecorr) & (V_an <= v_ecorr + window)
    cat_window_mask = (V_cat >= v_ecorr - window) & (V_cat < v_ecorr)

    V_an_win = V_an[an_window_mask]
    V_cat_win = V_cat[cat_window_mask]

    if len(V_an_win) > 0 and len(V_cat_win) > 0:
        mean_an = np.mean(V_an_win)
        mean_cat = np.mean(V_cat_win)
        offset = mean_an - mean_cat
        print(
            f"  5. Avg potential offset (anodic − cathodic) in ±50 mV window: "
            f"{offset*1000:.2f} mV"
        )
    else:
        offset = np.nan
        print("  5. Avg potential offset: insufficient data in ±50 mV window.")

    # Suggestion
    if not np.isnan(gap) and abs(gap) > 0.005:
        print(
            f"\n  ⚠  Gap |{gap*1000:.1f} mV| > 5 mV — consider applying a potential offset "
            "to one branch to close the gap near OCP before exporting."
        )
    else:
        print("\n  ✓  Gap is within ±5 mV — branches link up well near OCP.")

    print("=" * 60 + "\n")


# ---------------------------------------------------------------------------
# Main plotting routine
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description=(
            "Diagnostic overlay plot: raw Gamry DTA data vs. exported XML "
            "for polarization-curve branch-linkup inspection."
        )
    )
    # Two-file mode
    parser.add_argument("--anodic", metavar="FILE",
                        help="Anodic DTA file (two-file mode).")
    parser.add_argument("--cathodic", metavar="FILE",
                        help="Cathodic DTA file (two-file mode).")
    # Single-file mode
    parser.add_argument("--dta", metavar="FILE",
                        help="Single cyclic-polarization DTA file.")
    # XML
    parser.add_argument("--xml", metavar="FILE",
                        help="Exported PolarizationCurve XML file.")
    args = parser.parse_args()

    # Validate: --dta and --anodic/--cathodic are mutually exclusive
    if args.dta and (args.anodic or args.cathodic):
        parser.error("Use either --dta (single-file mode) OR --anodic/--cathodic "
                     "(two-file mode), not both.")
    if not any([args.anodic, args.cathodic, args.dta, args.xml]):
        parser.error("Provide at least one of --dta, --anodic, --cathodic, or --xml.")

    # ------------------------------------------------------------------
    # Parse inputs
    # ------------------------------------------------------------------
    V_an_dta = I_an_dta = None
    V_cat_dta = I_cat_dta = None
    v_ecorr = None
    exp_area = 1.0  # default; overridden from XML if available

    # --- Single-file mode ---
    if args.dta:
        print(f"Parsing single DTA file: {args.dta}")
        V_all, I_all = parse_dta(args.dta)
        V_an_dta, I_an_dta, V_cat_dta, I_cat_dta, v_ecorr = \
            split_single_file_branches(V_all, I_all)

    # --- Two-file mode ---
    if args.anodic:
        print(f"Parsing anodic DTA file: {args.anodic}")
        V_an_raw, I_an_raw = parse_dta(args.anodic)

        if args.cathodic:
            print(f"Parsing cathodic DTA file: {args.cathodic}")
            V_cat_raw, I_cat_raw = parse_dta(args.cathodic)
        else:
            V_cat_raw = I_cat_raw = np.array([])

        V_an_dta, I_an_dta, V_cat_dta, I_cat_dta, v_ecorr = \
            trim_two_file_branches(V_an_raw, I_an_raw, V_cat_raw, I_cat_raw)

    elif args.cathodic and not args.anodic:
        # Only cathodic provided — plot raw without trimming
        print(f"Parsing cathodic DTA file (no anodic provided): {args.cathodic}")
        V_cat_dta, I_cat_dta = parse_dta(args.cathodic)

    # --- XML ---
    V_xml = I_xml = None
    if args.xml:
        print(f"Parsing XML file: {args.xml}")
        V_xml, I_xml, exp_area = parse_xml(args.xml)

    # ------------------------------------------------------------------
    # Convert DTA currents to current density (A/m²) using exp_area
    # ------------------------------------------------------------------
    def to_density(I_A):
        """Convert raw current (A) to current density (A/m²)."""
        return I_A / exp_area if exp_area != 0 else I_A

    # ------------------------------------------------------------------
    # Diagnostics (requires both branches)
    # ------------------------------------------------------------------
    if V_an_dta is not None and V_cat_dta is not None and v_ecorr is not None:
        print_diagnostics(V_an_dta, I_an_dta, V_cat_dta, I_cat_dta, v_ecorr, exp_area)

    # ------------------------------------------------------------------
    # Build title
    # ------------------------------------------------------------------
    stems = []
    for attr in ("dta", "anodic", "cathodic", "xml"):
        val = getattr(args, attr, None)
        if val:
            stems.append(os.path.splitext(os.path.basename(val))[0])
    title = "Polarization Curve Diagnostic"
    if stems:
        title += "\n" + " | ".join(stems)

    # ------------------------------------------------------------------
    # Plot
    # ------------------------------------------------------------------
    fig, ax = plt.subplots(figsize=(8, 6))

    # Anodic DTA branch — thin blue dotted
    if V_an_dta is not None and len(V_an_dta) > 0:
        ax.plot(
            to_density(I_an_dta), V_an_dta,
            linestyle=":", linewidth=1, color="blue", label="Anodic DTA",
        )

    # Cathodic DTA branch — thin red dotted
    if V_cat_dta is not None and len(V_cat_dta) > 0:
        ax.plot(
            to_density(I_cat_dta), V_cat_dta,
            linestyle=":", linewidth=1, color="red", label="Cathodic DTA",
        )

    # XML (merged) — thick black solid
    if V_xml is not None and len(V_xml) > 0:
        ax.plot(
            I_xml, V_xml,
            linestyle="-", linewidth=2.5, color="black", label="XML (merged)",
        )

    # OCP marker
    if v_ecorr is not None:
        ax.axhline(
            y=v_ecorr, color="grey", linestyle="--", linewidth=1.0,
            label=f"E_corr = {v_ecorr:.4f} V",
        )
        ax.annotate(
            f" E_corr = {v_ecorr:.4f} V",
            xy=(0.01, v_ecorr),
            xycoords=("axes fraction", "data"),
            fontsize=8,
            color="grey",
            va="bottom",
        )

    # Axes — Evans diagram: potential on Y, current density on X (symlog)
    ax.set_xscale("symlog", linthresh=0.1)
    ax.set_xlabel("Current Density (A/m²)")
    ax.set_ylabel("Potential (V vs. SCE)")
    ax.set_title(title)
    ax.legend(loc="best", fontsize=9)
    ax.grid(True, which="both", linestyle="--", linewidth=0.4, alpha=0.6)

    fig.tight_layout()

    # ------------------------------------------------------------------
    # Save figure
    # ------------------------------------------------------------------
    # Determine output directory: same as XML, else current directory.
    if args.xml:
        out_dir = os.path.dirname(os.path.abspath(args.xml))
    else:
        out_dir = os.getcwd()

    # Build a sensible filename from the input stems (all inputs, joined by '__').
    if stems:
        base_name = "__".join(stems) + "_diagnostic"
    else:
        base_name = "polarization_diagnostic"
    out_path = os.path.join(out_dir, base_name + ".png")

    fig.savefig(out_path, dpi=150, bbox_inches="tight")
    print(f"Figure saved to: {out_path}")

    plt.show()


if __name__ == "__main__":
    main()
