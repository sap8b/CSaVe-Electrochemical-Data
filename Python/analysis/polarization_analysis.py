from __future__ import annotations

import csv
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
from scipy.optimize import least_squares
from scipy.signal import savgol_filter

# Tafel / BV near-linear region boundaries relative to E_corr (V)
_BV_LOWER_OFFSET_V: float = 0.01
_BV_UPPER_OFFSET_V: float = 0.15
# Floor applied before log10 to avoid log(0) errors
_LOG_FLOOR_A_CM2: float = 1e-20
_EXP_CLIP_MIN: float = -50.0
_EXP_CLIP_MAX: float = 50.0
# Minimum cathodic points retained below E_corr for branch-aware fit before fallback to all points.
_MIN_CATHODIC_POINTS: int = 10
_MIN_HER_ORR_WINDOW_POINTS: int = 20
_MAX_HER_ORR_WINDOW_POINTS: int = 50
_MIN_ILIM_WINDOW_POINTS: int = 10
_MAX_ILIM_WINDOW_POINTS: int = 20
_POLISH_MAX_NFEV: int = 600
_HER_ORR_WINDOW_DIVISOR: int = 4
_ILIM_WINDOW_DIVISOR: int = 8
_SAVGOL_WINDOW_LENGTH: int = 11
_SAVGOL_POLYORDER: int = 3
_ORR_ACTIVATION_UPPER_OFFSET_V: float = 0.02
_EORR_SELECTION_OFFSET_V: float = 0.05
_ANODIC_TAFEL_EXPANDED_UPPER_OFFSET_V: float = 0.20
_ILIM_FALLBACK_WINDOW_BEFORE: int = 1
_ILIM_FALLBACK_WINDOW_AFTER: int = 2


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

    anodic = i0a * np.exp(np.clip((e - ecorr) / ba, _EXP_CLIP_MIN, _EXP_CLIP_MAX))
    cathodic = i0c * np.exp(np.clip(-(e - ecorr) / bc, _EXP_CLIP_MIN, _EXP_CLIP_MAX))
    orr = -ilim_orr / (1.0 + np.exp(np.clip((e - e_orr) / w_orr, -60, 60)))
    her = -i0_her * np.exp(np.clip(-(e - e_her) / b_her, _EXP_CLIP_MIN, _EXP_CLIP_MAX))
    return anodic - cathodic + orr + her


def _estimate_ecorr_from_forward_scan(potential_v: np.ndarray, current_a: np.ndarray) -> float:
    """Estimate E_corr from the forward (anodic) scan data.

    Looks for the first zero crossing of current as potential increases
    (where net current transitions from negative to positive).
    Falls back to the minimum |current| point if no zero crossing is found.
    """
    order = np.argsort(potential_v)
    e = potential_v[order]
    i = current_a[order]

    sign_changes = np.where(np.diff(np.sign(i)))[0]
    if len(sign_changes) > 0:
        idx = sign_changes[0]
        e1, e2 = e[idx], e[idx + 1]
        i1, i2 = i[idx], i[idx + 1]
        if (i2 - i1) != 0:
            return float(e1 - i1 * (e2 - e1) / (i2 - i1))
        return float((e1 + e2) / 2.0)

    return float(e[np.argmin(np.abs(i))])


def _fit_bv_components(e: np.ndarray, current_density: np.ndarray, ecorr_hint: float) -> tuple[np.ndarray, np.ndarray]:
    order = np.argsort(e)
    e_sorted = np.asarray(e[order], dtype=float)
    i_sorted = np.asarray(current_density[order], dtype=float)
    if e_sorted.size == 0 or i_sorted.size == 0 or e_sorted.size != i_sorted.size:
        raise ValueError("Polarization fitting requires non-empty potential/current arrays of equal length.")

    ecorr0 = float(ecorr_hint) if np.isfinite(ecorr_hint) else float(e_sorted[np.argmin(np.abs(i_sorted))])
    idx_ecorr = int(np.argmin(np.abs(e_sorted - ecorr0)))
    icorr0 = max(abs(i_sorted[idx_ecorr]), 1e-10)

    cat_mask = e_sorted < ecorr0
    e_cat = e_sorted[cat_mask]
    i_cat = i_sorted[cat_mask]
    i_abs_cat = np.abs(i_cat)
    log_i_cat = np.log10(np.maximum(i_abs_cat, _LOG_FLOOR_A_CM2))

    def _window_around(center_idx: int, n_points: int, total: int) -> np.ndarray:
        if total <= 0:
            return np.asarray([], dtype=int)
        n = int(np.clip(n_points, 2, total))
        start = max(0, min(center_idx - n // 2, total - n))
        return np.arange(start, start + n, dtype=int)

    def _safe_derivative(x: np.ndarray, y: np.ndarray) -> np.ndarray:
        if x.size < 2:
            return np.zeros_like(y, dtype=float)
        if x.size < 11:
            return np.gradient(y, x)
        dx = np.diff(x)
        delta = float(np.mean(dx)) if dx.size > 0 else 1.0
        if not np.isfinite(delta) or delta == 0:
            delta = 1.0
        window_length = min(21, (x.size // 4) * 2 + 1)
        if window_length % 2 == 0:
            window_length -= 1
        if window_length < 5:
            return np.gradient(y, x)
        polyorder = min(3, window_length - 1)
        return savgol_filter(y, window_length=window_length, polyorder=polyorder, deriv=1, delta=delta)

    # Step 1: skip iR-loss, then fit HER around derivative minimum in middle 60%.
    b_her = 0.12
    i0_her_approx = 1e-9
    e_her_approx = ecorr0 - 0.30
    idx_her = 0
    i_her_contribution = i0_her_approx * np.exp(np.clip(-(e_cat - ecorr0) / b_her, _EXP_CLIP_MIN, _EXP_CLIP_MAX))
    if e_cat.size >= 5:
        try:
            d_log_i_cat_de = _safe_derivative(e_cat, log_i_cat)
            p20, p80 = np.percentile(e_cat, [20.0, 80.0])
            middle_idx = np.where((e_cat >= p20) & (e_cat <= p80))[0]
            if middle_idx.size == 0:
                middle_idx = np.arange(e_cat.size, dtype=int)
            idx_her = int(middle_idx[np.argmin(d_log_i_cat_de[middle_idx])])
            n_her = int(np.clip(e_cat.size // 5, _MIN_HER_ORR_WINDOW_POINTS, _MAX_HER_ORR_WINDOW_POINTS))
            idx_her_win = _window_around(idx_her, n_her, e_cat.size)
            if idx_her_win.size >= 2 and np.ptp(e_cat[idx_her_win]) > 0:
                coeffs_her = np.polyfit(e_cat[idx_her_win], log_i_cat[idx_her_win], 1)
                slope_her = float(coeffs_her[0])
                if np.isfinite(slope_her) and abs(slope_her) > 0:
                    beta_her_v_dec = 1.0 / abs(slope_her)
                    b_her_fit = beta_her_v_dec / np.log10(np.e)
                    i0_her_fit = float(10.0 ** np.polyval(coeffs_her, ecorr0))
                    e_her_fit = float(np.median(e_cat[idx_her_win]))
                    if np.isfinite(b_her_fit) and np.isfinite(i0_her_fit) and np.isfinite(e_her_fit):
                        b_her = float(np.clip(b_her_fit, 0.01, 0.5))
                        i0_her_approx = float(np.clip(i0_her_fit, 1e-12, 1e-1))
                        e_her_approx = e_her_fit
        except Exception:
            b_her = 0.12
            i0_her_approx = 1e-9
            e_her_approx = ecorr0 - 0.30
        i_her_contribution = i0_her_approx * np.exp(np.clip(-(e_cat - ecorr0) / b_her, _EXP_CLIP_MIN, _EXP_CLIP_MAX))

    # Step 2: subtract HER, then estimate i_lim from derivative maximum transition.
    ilim0 = float(np.percentile(i_abs_cat, 75)) if i_abs_cat.size > 0 else icorr0 * 3.0
    idx_lim = 0
    i_residual = np.maximum(i_abs_cat - i_her_contribution, 1e-14)
    if e_cat.size >= 5:
        try:
            d_residual_de = _safe_derivative(e_cat, i_residual)
            lim_candidates = np.where(np.arange(e_cat.size) >= idx_her)[0]
            if lim_candidates.size == 0:
                lim_candidates = np.arange(e_cat.size, dtype=int)
            idx_lim = int(lim_candidates[np.argmax(d_residual_de[lim_candidates])])
            n_lim = int(np.clip(e_cat.size // 8, _MIN_ILIM_WINDOW_POINTS, _MAX_ILIM_WINDOW_POINTS))
            plateau_slice = i_residual[max(0, idx_lim - n_lim):idx_lim]
            if plateau_slice.size >= 3:
                ilim0 = float(np.median(plateau_slice))
            else:
                ilim0 = float(np.percentile(i_abs_cat, 75))
            if not np.isfinite(ilim0) or ilim0 <= 0:
                ilim0 = float(np.percentile(i_abs_cat, 75))
        except Exception:
            ilim0 = float(np.percentile(i_abs_cat, 75))
    if not np.isfinite(ilim0) or ilim0 <= 0:
        ilim0 = float(np.percentile(i_abs_cat, 75)) if i_abs_cat.size > 0 else 1e-10
    ilim0 = float(np.clip(max(ilim0, 1e-12), 1e-10, 1.0))

    # Step 3: fit ORR activation in anodic-of-transition region.
    bc = 0.10
    i0c = float(np.clip((np.max(i_abs_cat) / 2.0) if i_abs_cat.size > 0 else icorr0, 1e-12, 1e-1))
    if e_cat.size >= 5:
        try:
            i_residual = np.maximum(i_abs_cat - i_her_contribution, 1e-14)
            log_residual = np.log10(np.maximum(i_residual, _LOG_FLOOR_A_CM2))
            d_log_residual_de = _safe_derivative(e_cat, log_residual)
            orr_candidates = np.where((np.arange(e_cat.size) > idx_lim) & (e_cat <= ecorr0 - 0.02))[0]
            if orr_candidates.size > 0:
                idx_orr = int(orr_candidates[np.argmin(np.abs(d_log_residual_de[orr_candidates]))])
                n_orr = int(np.clip(e_cat.size // 5, _MIN_HER_ORR_WINDOW_POINTS, _MAX_HER_ORR_WINDOW_POINTS))
                idx_orr_win = _window_around(idx_orr, n_orr, e_cat.size)
                if idx_orr_win.size >= 2 and np.ptp(e_cat[idx_orr_win]) > 0:
                    coeffs_orr = np.polyfit(e_cat[idx_orr_win], log_residual[idx_orr_win], 1)
                    slope_orr = float(coeffs_orr[0])
                    if np.isfinite(slope_orr) and abs(slope_orr) > 0:
                        bc_fit = 1.0 / (abs(slope_orr) * np.log10(np.e))
                        i0c_fit = float(10.0 ** np.polyval(coeffs_orr, ecorr0))
                        if np.isfinite(bc_fit) and np.isfinite(i0c_fit):
                            bc = float(np.clip(bc_fit, 0.01, 0.5))
                            i0c = float(np.clip(i0c_fit, 1e-12, 1e-1))
        except Exception:
            bc = 0.10
            i0c = float(np.clip((np.max(i_abs_cat) / 2.0) if i_abs_cat.size > 0 else icorr0, 1e-12, 1e-1))
    bc = float(np.clip(bc, 0.01, 0.5))
    i0c = float(np.clip(i0c, 1e-12, 1e-1))

    # Step 4: Anodic Tafel fit (unchanged behavior).
    ba = 0.06
    i0a = 1e-8
    an_mask = (e_sorted >= ecorr0 + _BV_LOWER_OFFSET_V) & (e_sorted <= ecorr0 + _BV_UPPER_OFFSET_V)
    if np.sum(an_mask) < 5:
        an_mask = (e_sorted >= ecorr0) & (e_sorted <= ecorr0 + _ANODIC_TAFEL_EXPANDED_UPPER_OFFSET_V)
    if np.sum(an_mask) >= 2:
        try:
            e_an = e_sorted[an_mask]
            log_i_an = np.log10(np.maximum(np.abs(i_sorted[an_mask]), _LOG_FLOOR_A_CM2))
            if np.ptp(e_an) > 0:
                coeffs_an = np.polyfit(e_an, log_i_an, 1)
                slope_an = float(coeffs_an[0])
                if np.isfinite(slope_an) and abs(slope_an) > 0:
                    ba = 1.0 / (abs(slope_an) * np.log10(np.e))
                    i0a = float(10.0 ** np.polyval(coeffs_an, ecorr0))
        except Exception:
            ba = 0.06
            i0a = 1e-8
    ba = float(np.clip(ba, 0.01, 0.5))
    i0a = float(np.clip(i0a, 1e-12, 1e-1))

    # Step 5: constrained polish around ecorr_hint.
    eorr_candidates = e_cat[e_cat < ecorr0 - 0.05]
    eorr0 = float(np.median(eorr_candidates)) if eorr_candidates.size >= 3 else ecorr0 - 0.10
    p0 = np.array([
        i0a, ba,
        i0c, bc,
        ecorr0,
        ilim0,
        eorr0,
        0.04,
        i0_her_approx, b_her, e_her_approx,
    ], dtype=float)

    lb = np.array([1e-12, 0.01, 1e-12, 0.01, ecorr0 - 0.10, 1e-10, -2.0, 0.005, 1e-12, 0.01, -2.0])
    ub = np.array([1e-1, 0.5, 1e-1, 0.5, ecorr0 + 0.05, 1.0, 0.2, 0.2, 1e-1, 0.5, ecorr0 - 0.01])
    p0 = np.clip(p0, lb, ub)

    scale = np.maximum(np.abs(i_sorted), np.percentile(np.abs(i_sorted), 20))

    def residual(params: np.ndarray) -> np.ndarray:
        return (_model_total_current_density(e_sorted, params) - i_sorted) / scale

    result = least_squares(residual, p0, bounds=(lb, ub), max_nfev=_POLISH_MAX_NFEV, loss="soft_l1")
    fitted_sorted = _model_total_current_density(e_sorted, result.x)
    fitted = np.empty_like(fitted_sorted)
    fitted[order] = fitted_sorted
    return result.x, fitted


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


def _compute_component_curves(
    e: np.ndarray,
    params: np.ndarray,
) -> dict[str, list[float]]:
    """Return |i| vs E for each electrochemical component from the fitted BV model.

    Components:
        i_ox   – anodic metal dissolution:  i0a * exp((E-Ecorr)/ba)
        i_orr  – ORR mixed kinetics:        i_act_orr * ilim / (i_act_orr + ilim)
                 where i_act_orr = i0c * exp(-(E-Ecorr)/bc)
        i_her  – HER activation:            i0_her * exp(-(E-e_her)/b_her)
    """
    i0a, ba, i0c, bc, ecorr, ilim_orr, e_orr, w_orr, i0_her, b_her, e_her = params

    i_ox = i0a * np.exp(np.clip((e - ecorr) / ba, _EXP_CLIP_MIN, _EXP_CLIP_MAX))

    i_act_orr = i0c * np.exp(np.clip(-(e - ecorr) / bc, _EXP_CLIP_MIN, _EXP_CLIP_MAX))
    i_orr = i_act_orr * ilim_orr / (i_act_orr + ilim_orr)

    i_her = i0_her * np.exp(np.clip(-(e - e_her) / b_her, _EXP_CLIP_MIN, _EXP_CLIP_MAX))

    return {
        "i_ox_curve_a_cm2": i_ox.tolist(),
        "i_orr_curve_a_cm2": i_orr.tolist(),
        "i_her_curve_a_cm2": i_her.tolist(),
    }


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
    ecorr_hint = _estimate_ecorr_from_forward_scan(anodic_data.potential_v, anodic_data.current_a)

    if cathodic_path_str:
        cathodic_path = Path(cathodic_path_str)
        cathodic_data = _read_polarization_csv(cathodic_path)

        # Full combined data for display (shows hysteresis loop)
        display_potential = np.concatenate([anodic_data.potential_v, cathodic_data.potential_v])
        display_current = np.concatenate([anodic_data.current_a, cathodic_data.current_a])
        disp_order = np.argsort(display_potential)
        display_potential = display_potential[disp_order]
        display_current = display_current[disp_order]

        # Branch-aware fitting dataset (no hysteresis contamination)
        cat_mask = cathodic_data.potential_v < ecorr_hint
        if cat_mask.sum() < _MIN_CATHODIC_POINTS:
            cat_mask = np.ones(len(cathodic_data.potential_v), dtype=bool)
        anodic_mask = anodic_data.potential_v >= ecorr_hint

        fit_potential = np.concatenate([
            cathodic_data.potential_v[cat_mask],
            anodic_data.potential_v[anodic_mask],
        ])
        fit_current = np.concatenate([
            cathodic_data.current_a[cat_mask],
            anodic_data.current_a[anodic_mask],
        ])
        fit_order = np.argsort(fit_potential)
        potential_v = fit_potential[fit_order]
        current_a = fit_current[fit_order]
    else:
        order = np.argsort(anodic_data.potential_v)
        potential_v = anodic_data.potential_v[order]
        current_a = anodic_data.current_a[order]
        display_potential = potential_v
        display_current = current_a

    area_cm2 = float(request.get("exposed_area_cm2", 0.495))
    if area_cm2 <= 0:
        raise ValueError("Exposed area must be > 0.")

    target_mvs = request.get("protection_potentials_mv", [-850.0, -1050.0])
    target_vs = [float(v) / 1000.0 for v in target_mvs]

    current_density = current_a / area_cm2
    display_current_density = display_current / area_cm2

    fit_params, _ = _fit_bv_components(potential_v, current_density, ecorr_hint=ecorr_hint)

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
        val = _interp_current_density_at_potential(display_potential, display_current_density, tv)
        key = f"i_at_{int(mv)}mv_ua_cm2"
        cp_metric[key] = val * 1.0e6
        cp_values[str(int(mv))].append(cp_metric[key])
    metric.update(cp_metric)

    component_curves = _compute_component_curves(display_potential, fit_params)

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
            "potential_v": display_potential.tolist(),
            "current_density_a_cm2": np.abs(display_current_density).tolist(),
            "model_current_density_a_cm2": np.abs(_model_total_current_density(display_potential, fit_params)).tolist(),
            "i_ox_curve_a_cm2": component_curves["i_ox_curve_a_cm2"],
            "i_orr_curve_a_cm2": component_curves["i_orr_curve_a_cm2"],
            "i_her_curve_a_cm2": component_curves["i_her_curve_a_cm2"],
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
