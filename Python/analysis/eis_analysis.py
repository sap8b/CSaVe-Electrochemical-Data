from __future__ import annotations

import csv
from pathlib import Path
from typing import Any

import numpy as np
from scipy.optimize import least_squares


def _read_eis_csv(path: Path) -> tuple[np.ndarray, np.ndarray]:
    with path.open("r", encoding="utf-8-sig", newline="") as f:
        reader = csv.DictReader(f)
        if not reader.fieldnames:
            raise ValueError(f"No header row in {path}")
        fields = {k.strip().lower(): k for k in reader.fieldnames}

        def pick(*candidates: str) -> str:
            for c in candidates:
                if c in fields:
                    return fields[c]
            raise ValueError(f"Could not find columns {candidates} in {path}")

        freq_key = pick("freq", "frequency", "f")
        zr_key = pick("zreal", "z'", "zre", "z_real")
        zi_key = pick("zimag", "zimaginary", "z''", "zim", "z_imag")

        freq = []
        zre = []
        zim = []
        for row in reader:
            try:
                fval = float(row[freq_key])
                r = float(row[zr_key])
                im = float(row[zi_key])
            except (ValueError, TypeError, KeyError):
                continue
            if fval > 0:
                freq.append(fval)
                zre.append(r)
                zim.append(im)

    if len(freq) < 8:
        raise ValueError(f"Insufficient EIS rows in {path}")

    freq_a = np.asarray(freq, dtype=float)
    z = np.asarray(zre, dtype=float) + 1j * np.asarray(zim, dtype=float)

    order = np.argsort(freq_a)[::-1]
    return freq_a[order], z[order]


def _z_element(kind: str, params: np.ndarray, freq: np.ndarray) -> np.ndarray:
    w = 2.0 * np.pi * freq
    jw = 1j * w
    if kind == "R":
        return np.full_like(freq, params[0], dtype=complex)
    if kind == "C":
        return 1.0 / (jw * params[0])
    if kind == "CPE":
        q, alpha = params
        return 1.0 / (q * np.power(jw, alpha))
    if kind == "W":
        sigma = params[0]
        return sigma / np.sqrt(jw)
    raise ValueError(f"Unknown element kind: {kind}")


def _matrix_equivalent_impedance(freq: np.ndarray, n_nodes: int, source_node: int, branches: list[dict[str, Any]], p: np.ndarray) -> np.ndarray:
    z_out = np.zeros_like(freq, dtype=complex)
    for idx_f, f in enumerate(freq):
        y = np.zeros((n_nodes - 1, n_nodes - 1), dtype=complex)

        for br in branches:
            n1 = br["n1"]
            n2 = br["n2"]
            sl = br["slice"]
            z_elem = _z_element(br["kind"], p[sl], np.asarray([f], dtype=float))[0]
            y_elem = 1.0 / z_elem

            def map_node(n: int) -> int:
                return n - 1

            if n1 != 0:
                i = map_node(n1)
                y[i, i] += y_elem
            if n2 != 0:
                j = map_node(n2)
                y[j, j] += y_elem
            if n1 != 0 and n2 != 0:
                i = map_node(n1)
                j = map_node(n2)
                y[i, j] -= y_elem
                y[j, i] -= y_elem

        inj = np.zeros(n_nodes - 1, dtype=complex)
        inj[source_node - 1] = 1.0
        v = np.linalg.solve(y, inj)
        z_out[idx_f] = v[source_node - 1]

    return z_out


def _model_spec(model_name: str) -> tuple[list[dict[str, Any]], np.ndarray, tuple[np.ndarray, np.ndarray], int, int]:
    name = model_name.lower().strip()
    if name == "randles_rc":
        branches = [
            {"n1": 1, "n2": 2, "kind": "R", "slice": slice(0, 1)},
            {"n1": 2, "n2": 0, "kind": "R", "slice": slice(1, 2)},
            {"n1": 2, "n2": 0, "kind": "C", "slice": slice(2, 3)},
        ]
        p0 = np.array([5.0, 100.0, 1e-4])
        lb = np.array([1e-6, 1e-6, 1e-9])
        ub = np.array([1e4, 1e6, 1.0])
        return branches, p0, (lb, ub), 3, 1

    if name == "randles_cpe_w":
        branches = [
            {"n1": 1, "n2": 2, "kind": "R", "slice": slice(0, 1)},
            {"n1": 2, "n2": 0, "kind": "R", "slice": slice(1, 2)},
            {"n1": 2, "n2": 0, "kind": "CPE", "slice": slice(2, 4)},
            {"n1": 2, "n2": 0, "kind": "W", "slice": slice(4, 5)},
        ]
        p0 = np.array([5.0, 150.0, 1e-4, 0.85, 20.0])
        lb = np.array([1e-6, 1e-6, 1e-9, 0.2, 1e-6])
        ub = np.array([1e4, 1e7, 1.0, 1.0, 1e5])
        return branches, p0, (lb, ub), 3, 1

    if name == "coating_two_time_constants":
        branches = [
            {"n1": 1, "n2": 2, "kind": "R", "slice": slice(0, 1)},
            {"n1": 2, "n2": 3, "kind": "R", "slice": slice(1, 2)},
            {"n1": 2, "n2": 3, "kind": "CPE", "slice": slice(2, 4)},
            {"n1": 3, "n2": 0, "kind": "R", "slice": slice(4, 5)},
            {"n1": 3, "n2": 0, "kind": "CPE", "slice": slice(5, 7)},
        ]
        p0 = np.array([2.0, 2e3, 1e-6, 0.85, 200.0, 1e-4, 0.9])
        lb = np.array([1e-6, 1e-3, 1e-10, 0.2, 1e-6, 1e-10, 0.2])
        ub = np.array([1e4, 1e8, 1.0, 1.0, 1e8, 1.0, 1.0])
        return branches, p0, (lb, ub), 4, 1

    raise ValueError(f"Unsupported EIS model: {model_name}")


def _fit_model(freq: np.ndarray, z_data: np.ndarray, model_name: str) -> tuple[np.ndarray, np.ndarray, list[str]]:
    branches, p0, bounds, n_nodes, source_node = _model_spec(model_name)

    scale = np.maximum(np.abs(z_data), np.percentile(np.abs(z_data), 30))

    def residual(p: np.ndarray) -> np.ndarray:
        z_fit = _matrix_equivalent_impedance(freq, n_nodes, source_node, branches, p)
        d = (z_fit - z_data) / scale
        return np.hstack([d.real, d.imag])

    result = least_squares(residual, p0, bounds=bounds, max_nfev=2500, loss="soft_l1")
    z_fit = _matrix_equivalent_impedance(freq, n_nodes, source_node, branches, result.x)

    if model_name.lower() == "randles_rc":
        names = ["Rs_ohm", "Rct_ohm", "Cdl_f"]
    elif model_name.lower() == "randles_cpe_w":
        names = ["Rs_ohm", "Rct_ohm", "Q_cpe", "alpha_cpe", "sigma_w"]
    else:
        names = ["Rs_ohm", "Rcoat_ohm", "Qcoat", "alphacoat", "Rct_ohm", "Qdl", "alphadl"]

    return result.x, z_fit, names


def run_eis_analysis(request: dict[str, Any]) -> dict[str, Any]:
    files = [Path(p) for p in request.get("files", [])]
    if not files:
        raise ValueError("No EIS files were provided.")

    model = str(request.get("model", "randles_cpe_w"))
    file_results = []

    for path in files:
        freq, z_data = _read_eis_csv(path)
        p_fit, z_fit, names = _fit_model(freq, z_data, model)

        params = {name: float(val) for name, val in zip(names, p_fit)}

        file_results.append(
            {
                "file": str(path),
                "model": model,
                "fit_parameters": params,
                "plot": {
                    "freq_hz": freq.tolist(),
                    "zreal_ohm": z_data.real.tolist(),
                    "zimag_ohm": z_data.imag.tolist(),
                    "zreal_fit_ohm": z_fit.real.tolist(),
                    "zimag_fit_ohm": z_fit.imag.tolist(),
                    "zmod_ohm": np.abs(z_data).tolist(),
                    "phase_deg": np.degrees(np.angle(z_data)).tolist(),
                    "zmod_fit_ohm": np.abs(z_fit).tolist(),
                    "phase_fit_deg": np.degrees(np.angle(z_fit)).tolist(),
                },
            }
        )

    return {
        "success": True,
        "message": f"Analyzed {len(file_results)} EIS file(s) using {model}.",
        "files": file_results,
    }
