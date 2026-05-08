from __future__ import annotations

import csv
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
from scipy.optimize import least_squares

# Tafel / BV near-linear region boundaries relative to E_corr (V)
_BV_LOWER_OFFSET_V: float = 0.01
_BV_UPPER_OFFSET_V: float = 0.15
# Floor applied before log10 to avoid log(0) errors
_LOG_FLOOR_A_CM2: float = 1e-20


@dataclass
class PolarizationData:
    potential_v: np.ndarray
    current_a: np.ndarray


def _read_polarization_csv(path: Path) -> PolarizationData:
    with path.open("r", encoding="utf-8-sig", newline="") as f:
        reader = csv.DictReader(f)
        if not reader.fieldnames:
            raise ValueError(f"No header row in {path}")
        field_map = {name.strip().lower(): name for name in reader.fieldnames}

        pot_key = None
        for key in ("vf", "potential", "ewe", "v"):
            if key in field_map:
                pot_key = field_map[key]
                break
        cur_key = None
        for key in ("im", "current", "i", "ia"):
            if key in field_map:
                cur_key = field_map[key]
                break

        if pot_key is None or cur_key is None:
            raise ValueError(f"Unable to identify potential/current columns in {path}")

        potentials = []
        currents = []
        for row in reader:
            try:
                potentials.append(float(row[pot_key]))
                currents.append(float(row[cur_key]))
            except (ValueError, TypeError, KeyError):
                continue

    if len(potentials) < 20:
        raise ValueError(f"Insufficient data rows in {path}; at least 20 rows are required for BV fitting.")

    p = np.asarray(potentials, dtype=float)
    i = np.asarray(currents, dtype=float)
    valid = np.isfinite(p) & np.isfinite(i)
    return PolarizationData(p[valid], i[valid])


def _interp_current_density_at_potential(potential_v: np.ndarray, current_density: np.ndarray, target_v: float) -> float:
    order = np.argsort(potential_v)
    x = potential_v[order]
    y = np.abs(current_density[order])
    return float(np.interp(target_v, x, y, left=y[0], right=y[-1]))


def _model_total_current_density(e: np.ndarray, p: np.ndarray) -> np.ndarray:
    i0a, ba, i0c, bc, ecorr, ilim_orr, e_orr, w_orr, i0_her, b_her, e_her = p

    anodic = i0a * np.exp(np.clip((e - ecorr) / ba, -50, 50))
    cathodic = i0c * np.exp(np.clip(-(e - ecorr) / bc, -50, 50))
    orr = -ilim_orr / (1.0 + np.exp(np.clip((e - e_orr) / w_orr, -60, 60)))
    her = -i0_her * np.exp(np.clip(-(e - e_her) / b_her, -50, 50))
    return anodic - cathodic + orr + her


def _fit_bv_components(e: np.ndarray, i: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    idx_ecorr = int(np.argmin(np.abs(i)))
    ecorr0 = float(e[idx_ecorr])
    icorr0 = max(abs(i[idx_ecorr]), 1e-10)

    cathodic = i[e < ecorr0]
    ilim0 = float(np.percentile(np.abs(cathodic), 75)) if cathodic.size > 5 else icorr0 * 3.0
    eorr0 = float(np.median(e[e < ecorr0])) if np.any(e < ecorr0) else ecorr0 - 0.05

    p0 = np.array([
        icorr0, 0.08,
        icorr0, 0.08,
        ecorr0,
        max(ilim0, icorr0 * 1.5),
        eorr0,
        0.04,
        icorr0 * 0.5,
        0.06,
        ecorr0 - 0.15,
    ])
    # Bounds order: [i0a, ba, i0c, bc, ecorr, ilim_orr, e_orr, w_orr, i0_her, b_her, e_her]
    lb = np.array([1e-12, 0.01, 1e-12, 0.01, -2.0, 1e-10, -2.0, 0.005, 1e-12, 0.01, -2.0])
    ub = np.array([1e-1, 0.5, 1e-1, 0.5, 0.5, 1.0, 0.2, 0.2, 1e-1, 0.5, 0.0])

    scale = np.maximum(np.abs(i), np.percentile(np.abs(i), 20))

    def residual(params: np.ndarray) -> np.ndarray:
        return (_model_total_current_density(e, params) - i) / scale

    # 1200 evaluations balances convergence for 11-parameter BV/transport fits while keeping runtime interactive.
    result = least_squares(residual, p0, bounds=(lb, ub), max_nfev=1200, loss="soft_l1")
    return result.x, _model_total_current_density(e, result.x)


def _tafel_i_ox(e: np.ndarray, current_density: np.ndarray, ecorr: float) -> float:
    """Estimate anodic exchange current density via Tafel-region linear regression.

    Fits log10(|i|) vs E over the anodic Tafel window (E_corr + 0.01 V to E_corr + 0.15 V)
    and extrapolates back to E_corr to obtain i_ox.
    Returns NaN if fewer than 3 points fall in the window.
    """
    mask = (e >= ecorr + _BV_LOWER_OFFSET_V) & (e <= ecorr + _BV_UPPER_OFFSET_V)
    if np.sum(mask) < 3:
        return float("nan")

    e_win = e[mask]
    log_i_win = np.log10(np.maximum(np.abs(current_density[mask]), _LOG_FLOOR_A_CM2))

    coeffs = np.polyfit(e_win, log_i_win, 1)
    log_i_ox = np.polyval(coeffs, ecorr)
    return float(10.0 ** log_i_ox)


def _bv_region_points(
    e: np.ndarray,
    current_density: np.ndarray,
    ecorr: float,
) -> tuple[list[float], list[float]]:
    """Return the subset of (E, |i|) points in the BV near-linear region
    (0.01 V <= |E - E_corr| <= 0.15 V) used to highlight the red fit region."""
    mask = (np.abs(e - ecorr) >= _BV_LOWER_OFFSET_V) & (np.abs(e - ecorr) <= _BV_UPPER_OFFSET_V)
    e_region = e[mask]
    i_region = np.abs(current_density[mask])
    order = np.argsort(e_region)
    return e_region[order].tolist(), i_region[order].tolist()


def _safe_stats(values: list[float]) -> dict[str, float]:
    arr = np.asarray(values, dtype=float)
    if arr.size == 0:
        return {"mean": float("nan"), "std": float("nan")}
    return {"mean": float(np.mean(arr)), "std": float(np.std(arr, ddof=1) if arr.size > 1 else 0.0)}


def run_polarization_analysis(request: dict[str, Any]) -> dict[str, Any]:
    anodic_path_str = request.get("anodic_file", "")
    cathodic_path_str = request.get("cathodic_file", "")

    if not anodic_path_str:
        raise ValueError("No anodic polarization file was provided.")

    anodic_path = Path(anodic_path_str)
    anodic_data = _read_polarization_csv(anodic_path)

    if cathodic_path_str:
        cathodic_path = Path(cathodic_path_str)
        cathodic_data = _read_polarization_csv(cathodic_path)
        combined_potential = np.concatenate([anodic_data.potential_v, cathodic_data.potential_v])
        combined_current = np.concatenate([anodic_data.current_a, cathodic_data.current_a])
    else:
        combined_potential = anodic_data.potential_v
        combined_current = anodic_data.current_a

    # Sort by potential for a clean monotonic curve
    order = np.argsort(combined_potential)
    potential_v = combined_potential[order]
    current_a = combined_current[order]

    area_cm2 = float(request.get("exposed_area_cm2", 0.495))
    if area_cm2 <= 0:
        raise ValueError("Exposed area must be > 0.")

    target_mvs = request.get("protection_potentials_mv", [-850.0, -1050.0])
    target_vs = [float(v) / 1000.0 for v in target_mvs]

    current_density = current_a / area_cm2

    fit_params, model_i = _fit_bv_components(potential_v, current_density)

    ecorr = float(fit_params[4])
    icorr = float(max(fit_params[0], fit_params[2]))
    ilim = float(fit_params[5])
    her_onset = float(fit_params[10])

    i_ox = _tafel_i_ox(potential_v, current_density, ecorr)

    metric: dict[str, float] = {
        "ecorr_v": ecorr,
        "ecorr_mv": ecorr * 1000.0,
        "icorr_a_cm2": icorr,
        "icorr_ua_cm2": icorr * 1.0e6,
        "ilim_orr_a_cm2": ilim,
        "ilim_orr_ua_cm2": ilim * 1.0e6,
        "her_onset_v": her_onset,
        "her_onset_mv": her_onset * 1000.0,
        "i_ox_a_cm2": i_ox,
        "i_ox_ua_cm2": i_ox * 1.0e6 if not np.isnan(i_ox) else float("nan"),
    }

    cp_values: dict[str, list[float]] = {str(int(v)): [] for v in target_mvs}
    cp_metric: dict[str, float] = {}
    for mv, tv in zip(target_mvs, target_vs):
        val = _interp_current_density_at_potential(potential_v, current_density, tv)
        key = f"i_at_{int(mv)}mv_ua_cm2"
        cp_metric[key] = val * 1.0e6
        cp_values[str(int(mv))].append(cp_metric[key])
    metric.update(cp_metric)

    bv_e, bv_i = _bv_region_points(potential_v, current_density, ecorr)

    file_result = {
        "file": str(anodic_path),
        "metrics": metric,
        "fit_parameters": {
            "i0_anodic_a_cm2": float(fit_params[0]),
            "beta_anodic_v": float(fit_params[1]),
            "i0_cathodic_a_cm2": float(fit_params[2]),
            "beta_cathodic_v": float(fit_params[3]),
            "ecorr_v": float(fit_params[4]),
            "ilim_orr_a_cm2": float(fit_params[5]),
            "e_orr_transition_v": float(fit_params[6]),
            "w_orr_v": float(fit_params[7]),
            "i0_her_a_cm2": float(fit_params[8]),
            "beta_her_v": float(fit_params[9]),
            "e_her_onset_v": float(fit_params[10]),
        },
        "plot": {
            "potential_v": potential_v.tolist(),
            "current_density_a_cm2": np.abs(current_density).tolist(),
            "model_current_density_a_cm2": np.abs(model_i).tolist(),
            "bv_region_potential_v": bv_e,
            "bv_region_current_density_a_cm2": bv_i,
        },
    }

    summary = {
        "ecorr_mv": _safe_stats([metric["ecorr_mv"]]),
        "icorr_ua_cm2": _safe_stats([metric["icorr_ua_cm2"]]),
        "i_ox_ua_cm2": _safe_stats([metric["i_ox_ua_cm2"]]),
        "ilim_orr_ua_cm2": _safe_stats([metric["ilim_orr_ua_cm2"]]),
        "her_onset_mv": _safe_stats([metric["her_onset_mv"]]),
        "protection_currents_ua_cm2": {k: _safe_stats(v) for k, v in cp_values.items()},
    }

    return {
        "success": True,
        "message": "Analyzed polarization curve (anodic" + (" + cathodic" if cathodic_path_str else "") + ").",
        "files": [file_result],
        "summary": summary,
    }

