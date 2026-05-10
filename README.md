<img src = "https://github.com/sap8b/CSaVe-Electrochemical-Data/blob/master/CSaVe Electrochemical Data Logo.png" align = "left" width = "1024" height = "200">

# 
A utility for extracting, converting, and analyzing electrochemical corrosion data.

## Introduction
* CSaVe-Electrochemical-Data is an x86 app utility that provides batch processing to extract impedance data and column names from DTA files and convert them to comma-separated-values (CSV) files.  
* The CSV files have the same name as the DTA files but can be stored in a new, specified folder for easier visualization and analysis.

## Background
This project started as a Windows Presentation Foundation (WPF) project for use as locally to speed up visualization and analysis of EIS experimental datafiles.  However, by adding a paakaging project to the original repository, the project was able to be converted to a Win32 app that could be distributed through the Microsoft Store.

## Code
This project was written in C# and XAML using Microsoft Visual Studio 2019. 

## Current tabs
- **Tab 1**: DTA → CSV conversion
- **Tab 2**: CSVs → XML export (enabled only for CYCPOL/POTENTIODYNAMIC-derived CSV inputs)
- **Tab 3**: Polarization analysis (Butler-Volmer-oriented fitting workflow scaffold with replicate summaries)
- **Tab 4**: EIS analysis (matrix-based equivalent-circuit fitting via NumPy/SciPy)

## Polarization Analysis (Tab 3)

### Butler-Volmer model

All three electrochemical half-reactions are modelled with the **full Butler-Volmer equation**
referenced to a Nernst-equation equilibrium potential:

| Reaction | E₀ (vs. SHE) | z | Notes |
|---|---|---|---|
| Metal oxidation (Fe/Fe²⁺) | −0.44 V | 2 | Net anodic current; cathodic reverse term included |
| ORR (O₂/H₂O) | +1.229 V | 4 | BV kinetics + Koutecky-Levich mass-transport correction |
| HER (H⁺/H₂) | 0.00 V | 2 | BV kinetics only |

The net model current is:

```
i_net = i_metal_BV  +  i_ORR_BV_limited  +  i_HER_BV
```

Each component uses symmetry factor β and exchange current density I₀ as fit parameters.
The corrosion potential Ecorr is derived post-fit as the zero-crossing of i_net.

### Polarization plot legend

The plot in Tab 3 shows the following curves (|i| vs. E, logarithmic current axis):

| Series | Style | Description |
|---|---|---|
| Anodic file | Light gray dotted | Raw measured data from the anodic scan file (two-file mode only) |
| Cathodic file | Light gray dotted | Raw measured data from the cathodic scan file (two-file mode only) |
| Combined data | Light gray dashed | All measured data merged and sorted by potential |
| Metal oxidation BV | Light blue solid | Forward + reverse terms of the metal-oxidation Butler-Volmer equation |
| ORR BV | Green solid | Forward + reverse terms of the ORR Butler-Volmer equation (with mass-transport limit) |
| HER BV | Orange solid | Forward + reverse terms of the HER Butler-Volmer equation |

### Fitted parameters (7 total, plus Ecorr derived)

| Parameter | Symbol | Units |
|---|---|---|
| Metal-oxidation exchange current density | I₀,metal | A/cm² |
| Metal-oxidation symmetry factor | βₘₑₜₐₗ | — |
| ORR exchange current density | I₀,ORR | A/cm² |
| ORR symmetry factor | βₒᵣᵣ | — |
| ORR limiting current density | iₗᵢₘ,ORR | A/cm² |
| HER exchange current density | I₀,HER | A/cm² |
| HER symmetry factor | βₕₑᵣ | — |

## Python requirements (system Python)
The analysis tabs assume a system Python install available as `python` (or override with env var `CSAVE_PYTHON_EXECUTABLE`).

Install dependencies:

```bash
pip install -r Python/requirements.txt
```

Required packages:
- `numpy`
- `scipy`
- `matplotlib` (used by existing diagnostic utility)

## Installation
The app is available here: https://www.microsoft.com/store/productId/9MWJ4GK6S7ZK

## Next Steps
* Add the capability to extract data for the following filetypes:
    - <strike>Corrosion potential</strike>
    - <strike>Cyclic polarization</strike>
    - <strike>Potentiodynamic polarization</strike>
    - <strike>Cyclic voltammetry</strike>
    - <strike>Potentiostatic EIS</strike>
    - <strike>Galvanostatic EIS</strike>
    - <strike>BiPotentiostat RDE</strike>
    - <strike>Galvanostatic</strike>
    - <strike>Potentiostatic</strike>
* Add support for other manufacturers datafiles - if they aren't already easily plottable
* Add plotting scripts for some common plotting packages
