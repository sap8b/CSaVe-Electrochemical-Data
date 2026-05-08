import json
import sys
from pathlib import Path

from polarization_analysis import run_polarization_analysis
from eis_analysis import run_eis_analysis


def main() -> int:
    if len(sys.argv) != 4:
        print("Usage: run_analysis.py <polarization|eis> <input_json> <output_json>", file=sys.stderr)
        return 2

    mode = sys.argv[1].strip().lower()
    input_path = Path(sys.argv[2])
    output_path = Path(sys.argv[3])

    try:
        request = json.loads(input_path.read_text(encoding="utf-8"))
        if mode == "polarization":
            response = run_polarization_analysis(request)
        elif mode == "eis":
            response = run_eis_analysis(request)
        else:
            raise ValueError(f"Unsupported mode: {mode}")

        output_path.write_text(json.dumps(response), encoding="utf-8")
        return 0
    except Exception as exc:  # surface clear errors back to C#
        output_path.write_text(json.dumps({"success": False, "message": str(exc)}), encoding="utf-8")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
