"""Check generated Unity skill clips and build one audition reel per character."""

from __future__ import annotations

import wave
from pathlib import Path

import numpy as np


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Game/Assets/Game/Audio/SFX/Skills"
PREVIEWS = Path(__file__).resolve().parent / "previews"
EXPECTED = {"Zelf": 6, "Oboro": 8, "Volbraak": 8, "Rines": 8, "Lieselotte": 8}
ORDER = {letter: index for index, letter in enumerate("PQWER")}
RATE = 48_000


def read(path: Path) -> np.ndarray:
    with wave.open(str(path), "rb") as audio:
        assert (audio.getnchannels(), audio.getsampwidth(), audio.getframerate()) == (1, 2, RATE), path
        frames = audio.readframes(audio.getnframes())
        assert audio.getnframes() > 0, path
    data = np.frombuffer(frames, dtype="<i2")
    assert np.max(np.abs(data.astype(np.int32))) < 29_000, f"Clipping: {path}"
    assert np.sqrt(np.mean((data.astype(np.float64) / 32768) ** 2)) > 0.008, f"Too quiet: {path}"
    assert abs(int(data[-1])) < 64, f"Abrupt ending: {path}"
    return data


def main() -> None:
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    total = 0
    levels = {}
    for character, expected in EXPECTED.items():
        paths = sorted((SOURCE / character).glob("*.wav"),
                       key=lambda path: (ORDER[path.stem[0]], path.stem))
        assert len(paths) == expected, f"{character}: expected {expected}, found {len(paths)}"
        parts = []
        for path in paths:
            samples = read(path)
            parts.append(samples)
            levels[f"{character}/{path.stem}"] = float(np.sqrt(np.mean((samples.astype(np.float64) / 32768) ** 2)))
            parts.append(np.zeros(round(0.27 * RATE), dtype="<i2"))
        preview = np.concatenate(parts)
        with wave.open(str(PREVIEWS / f"{character}-preview.wav"), "wb") as audio:
            audio.setnchannels(1)
            audio.setsampwidth(2)
            audio.setframerate(RATE)
            audio.writeframes(preview.tobytes())
        total += len(paths)
        print(f"{character}: {len(paths)} valid clips, {len(preview) / RATE:.1f}s preview")
    assert total == 38
    for lead, impact in (
        ("Rines/Q_Mark", "Rines/Q_LightningStrike"),
        ("Rines/R_Warning", "Rines/R_ThunderImpact"),
        ("Volbraak/E_Charge", "Volbraak/E_StunHit"),
        ("Lieselotte/R_Dash", "Lieselotte/R_StatSteal"),
    ):
        assert levels[lead] < levels[impact], f"Lead cue masks its impact: {lead}"
    print(f"Validated {total} skill SFX; previews in {PREVIEWS}")


if __name__ == "__main__":
    main()
