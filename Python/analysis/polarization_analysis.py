from __future__ import annotations

import csv
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
from scipy.optimize import least_squares


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

    # Butler-Volmer + transport fitting is underdetermined on very short traces.
    # Require at least ~20 points to ensure enough anodic/cathodic coverage.
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


def _safe_stats(values: list[float]) -> dict[str, float]:
    arr = np.asarray(values, dtype=float)
    if arr.size == 0:
        return {"mean": float("nan"), "std": float("nan")}
    return {"mean": float(np.mean(arr)), "std": float(np.std(arr, ddof=1) if arr.size > 1 else 0.0)}


def run_polarization_analysis(request: dict[str, Any]) -> dict[str, Any]:
    files = [Path(p) for p in request.get("files", [])]
    if not files:
        raise ValueError("No polarization files were provided.")

    area_cm2 = float(request.get("exposed_area_cm2", 0.495))
    if area_cm2 <= 0:
        raise ValueError("Exposed area must be > 0.")

    target_mvs = request.get("protection_potentials_mv", [-850.0, -1050.0])
    target_vs = [float(v) / 1000.0 for v in target_mvs]

    file_results = []
    ecorr_values = []
    icorr_values = []
    ilim_values = []
    her_values = []
    cp_values: dict[str, list[float]] = {str(int(v)): [] for v in target_mvs}

    for path in files:
        data = _read_polarization_csv(path)
        current_density = data.current_a / area_cm2

        fit_params, model_i = _fit_bv_components(data.potential_v, current_density)

        ecorr = float(fit_params[4])
        icorr = float(max(fit_params[0], fit_params[2]))
        ilim = float(fit_params[5])
        her_onset = float(fit_params[10])

        metric = {
            "ecorr_v": ecorr,
            "ecorr_mv": ecorr * 1000.0,
            "icorr_a_cm2": icorr,
            "icorr_ua_cm2": icorr * 1.0e6,
            "ilim_orr_a_cm2": ilim,
            "ilim_orr_ua_cm2": ilim * 1.0e6,
            "her_onset_v": her_onset,
            "her_onset_mv": her_onset * 1000.0,
        }

        cp_metric = {}
        for mv, tv in zip(target_mvs, target_vs):
            val = _interp_current_density_at_potential(data.potential_v, current_density, tv)
            key = f"i_at_{int(mv)}mv_ua_cm2"
            cp_metric[key] = val * 1.0e6
            cp_values[str(int(mv))].append(cp_metric[key])
        metric.update(cp_metric)

        ecorr_values.append(metric["ecorr_mv"])
        icorr_values.append(metric["icorr_ua_cm2"])
        ilim_values.append(metric["ilim_orr_ua_cm2"])
        her_values.append(metric["her_onset_mv"])

        file_results.append(
            {
                "file": str(path),
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
                    "potential_v": data.potential_v.tolist(),
                    "current_density_a_cm2": np.abs(current_density).tolist(),
                    "model_current_density_a_cm2": np.abs(model_i).tolist(),
                },
            }
        )

    summary = {
        "ecorr_mv": _safe_stats(ecorr_values),
        "icorr_ua_cm2": _safe_stats(icorr_values),
        "ilim_orr_ua_cm2": _safe_stats(ilim_values),
        "her_onset_mv": _safe_stats(her_values),
        "protection_currents_ua_cm2": {k: _safe_stats(v) for k, v in cp_values.items()},
    }

    return {
        "success": True,
        "message": f"Analyzed {len(file_results)} polarization file(s).",
        "files": file_results,
        "summary": summary,
    }
