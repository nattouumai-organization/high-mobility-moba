"""Generate the stylized, original skill SFX listed in CUES.md.

Run from any directory with: python tools/skill-sfx/generate.py
Requires NumPy and SciPy. No downloaded samples or API credentials are used.
"""

from __future__ import annotations

import hashlib
import wave
from pathlib import Path

import numpy as np
from scipy import signal


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Game/Assets/Game/Audio/SFX/Skills"
SR = 48_000


def envelope(length: int, attack: float = 0.004, decay: float = 0.12) -> np.ndarray:
    t = np.arange(length) / SR
    shape = np.minimum(1.0, t / max(attack, 1 / SR)) * np.exp(-t / max(decay, 1 / SR))
    shape *= np.clip((length - np.arange(length)) / max(0.012 * SR, 1), 0, 1)
    return shape


class Clip:
    def __init__(self, name: str, duration: float, kind: str = "skill") -> None:
        self.name = name
        self.duration = duration
        self.kind = kind
        self.data = np.zeros(round(duration * SR), dtype=np.float64)
        seed = int.from_bytes(hashlib.sha256(name.encode("utf-8")).digest()[:8], "little")
        self.rng = np.random.default_rng(seed)

    def add(self, sound: np.ndarray, at: float = 0.0, level: float = 1.0) -> None:
        start = round(at * SR)
        if start >= len(self.data):
            return
        count = min(len(sound), len(self.data) - start)
        self.data[start:start + count] += sound[:count] * level

    def air_sweep(self, at: float, duration: float, start_hz: float, end_hz: float,
                  level: float = 0.25, decay: float = 0.15,
                  harmonics: tuple = (1,)) -> None:
        """Move filtered air between two bands without a pitched oscillator."""
        n = round(duration * SR)
        raw = self.rng.standard_normal(n)
        width = 2.1 + 0.25 * (len(harmonics) - 1)
        def band(center: float) -> np.ndarray:
            lo = max(120, center / width)
            hi = min(17_000, max(lo + 350, center * width * 1.5))
            return signal.sosfilt(signal.butter(3, [lo, hi], btype="bandpass",
                                                fs=SR, output="sos"), raw)
        progress = np.linspace(0, 1, n)
        start = band(start_hz)
        end = band(end_hz)
        y = start * np.sqrt(1 - progress) + end * np.sqrt(progress)
        rms = max(np.sqrt(np.mean(y * y)), 1e-9)
        self.add(y / rms * 0.28 * envelope(n, attack=0.0015, decay=decay), at, level)

    def noise(self, at: float, duration: float, lo: float, hi: float,
              level: float = 0.2, attack: float = 0.005, decay: float = 0.15) -> None:
        n = round(duration * SR)
        raw = self.rng.standard_normal(n)
        sos = signal.butter(3, [lo, hi], btype="bandpass", fs=SR, output="sos")
        y = signal.sosfilt(sos, raw)
        rms = max(np.sqrt(np.mean(y * y)), 1e-9)
        self.add(y / rms * 0.22 * envelope(n, attack, decay), at, level)

    def whoosh(self, at: float = 0, duration: float = 0.3,
               level: float = 0.5, bright: bool = True, reverse: bool = False) -> None:
        n = round(duration * SR)
        raw = self.rng.standard_normal(n)
        lo, hi = (750, 10_500) if bright else (100, 2_700)
        sos = signal.butter(3, [lo, hi], btype="bandpass", fs=SR, output="sos")
        y = signal.sosfilt(sos, raw)
        swell = np.sin(np.linspace(0, np.pi, n)) ** 1.8
        if reverse:
            swell *= np.linspace(0.15, 1.0, n)
        else:
            swell *= np.linspace(1.0, 0.25, n)
        rms = max(np.sqrt(np.mean(y * y)), 1e-9)
        self.add(y / rms * 0.23 * swell, at, level)

    def slash(self, at: float, level: float = 0.5, duration: float = 0.15) -> None:
        """Oboro: a dry, grainy cut with a short tearing edge."""
        self.noise(at, duration, 420, 6200, level, attack=0.0007, decay=0.038)
        self.noise(at + 0.012, min(duration, 0.085), 1400, 11_500,
                   level * 0.65, attack=0.0005, decay=0.022)
        for offset in (0.026, 0.044):
            self.noise(at + offset, 0.025, 900, 7400,
                       level * 0.23, attack=0.0005, decay=0.008)

    def bass_hit(self, at: float, level: float = 0.6, duration: float = 0.32) -> None:
        """Volbraak: fixed-pitch sub impact and crushed stone, never a falling boing."""
        n = round(min(duration, 0.24) * SR)
        t = np.arange(n) / SR
        sub = np.sin(2 * np.pi * 56 * t) * envelope(n, 0.001, 0.085)
        self.add(sub, at, level * 0.57)
        self.noise(at, min(duration, 0.28), 48, 470,
                   level * 0.8, attack=0.0008, decay=0.095)
        self.noise(at, 0.09, 420, 3600,
                   level * 0.43, attack=0.0005, decay=0.026)

    def zap(self, at: float, level: float = 0.5, pulses: int = 2) -> None:
        """Rines: close, synthetic electrical snaps rather than rolling thunder."""
        for i in range(pulses):
            offset = i * 0.016
            self.noise(at + offset, 0.043, 1500, 15_000,
                       level * (0.88 ** i), attack=0.0003, decay=0.006)
            self.noise(at + offset, 0.052, 320, 4200,
                       level * 0.55 * (0.85 ** i), attack=0.0003, decay=0.009)

    def glint(self, at: float, level: float = 0.4, duration: float = 0.24) -> None:
        """Lieselotte: a thin, high, polished edge with very little bass."""
        self.air_sweep(at, duration, 4200, 7200,
                       level, decay=min(0.11, duration / 2))
        self.noise(at + 0.013, min(duration, 0.10), 3100, 15_000,
                   level * 0.65, attack=0.0005, decay=0.022)

    def metal(self, at: float = 0, base: float = 490, duration: float = 0.42,
              level: float = 0.4, dark: bool = False) -> None:
        # Scraped edge and a dry click, without ringing pitched partials.
        center = max(350, base * (1.2 if dark else 2.4))
        self.air_sweep(at, duration, center * 1.8, center,
                       level * 0.70, min(0.13, duration / 2))
        self.noise(at, min(0.085, duration), 900 if dark else 2100,
                   7000 if dark else 14_000, level * 0.4,
                   attack=0.0007, decay=0.017)

    def thump(self, at: float = 0, base: float = 130, level: float = 0.65,
              duration: float = 0.38) -> None:
        # A short, granular impact avoids the rounded falling bass note.
        self.noise(at, min(duration, 0.15), 65, max(800, base * 9),
                   level * 0.75, attack=0.0007, decay=0.038)
        self.noise(at, min(duration, 0.10), 330, 5800,
                   level * 0.35, attack=0.0007, decay=0.018)

    def echo(self, delay: float = 0.09, amount: float = 0.2, repeats: int = 2) -> None:
        dry = self.data.copy()
        for i in range(1, repeats + 1):
            samples = round(delay * i * SR)
            self.data[samples:] += dry[:-samples] * (amount ** i)

    def save(self) -> None:
        # Clean subsonic energy and blend a soft parallel saturation path.
        self.data = signal.sosfilt(signal.butter(2, 45, btype="highpass", fs=SR,
                                                output="sos"), self.data)
        self.data = 0.73 * self.data + 0.27 * np.tanh(self.data * 2.7)
        self.data -= np.mean(self.data)
        fade = min(round(0.04 * SR), len(self.data))
        self.data[-fade:] *= np.linspace(1, 0, fade)
        peak = max(np.max(np.abs(self.data)), 1e-9)
        ceiling = {"passive": 0.50, "skill": 0.70, "ultimate": 0.82}[self.kind]
        # A cue that foreshadows or accompanies the action must not mask its hit.
        supporting_gain = {
            "Zelf/E_Dash": 0.75,
            "Zelf/R_BoundarySlow": 0.55,
            "Oboro/W_StealthEnter": 0.78,
            "Oboro/W_StealthExit": 0.66,
            "Oboro/E_Return": 0.58,
            "Volbraak/W_ShieldStart": 0.78,
            "Volbraak/E_Charge": 0.60,
            "Volbraak/R_ChainLaunch": 0.68,
            "Rines/Q_Mark": 0.49,
            "Rines/E_ThunderForm": 0.68,
            "Rines/R_Warning": 0.43,
            "Lieselotte/W_BloodRush": 0.63,
            "Lieselotte/E_BloodPool": 0.65,
            "Lieselotte/R_Dash": 0.68,
            "Lieselotte/R_Release": 0.60,
        }.get(self.name, 1.0)
        pcm = np.int16(np.clip(self.data / peak * ceiling * supporting_gain, -1, 1) * 32767)
        path = OUT / f"{self.name}.wav"
        path.parent.mkdir(parents=True, exist_ok=True)
        with wave.open(str(path), "wb") as stream:
            stream.setnchannels(1)
            stream.setsampwidth(2)
            stream.setframerate(SR)
            stream.writeframes(pcm.tobytes())


def blue(name: str, duration: float, cue: str, kind: str = "skill") -> Clip:
    c = Clip(f"Zelf/{name}", duration, kind)
    if cue == "blink":
        c.whoosh(0, 0.21, 0.65)
        c.air_sweep(0.04, 0.27, 950, 330, 0.26, 0.11, (1, 2, 3))
        c.metal(0.12, 710, 0.41, 0.37)
    elif cue == "guard":
        c.metal(0, 410, 0.62, 0.3)
        c.whoosh(0.06, 0.57, 0.34)
        c.air_sweep(0.10, 0.59, 480, 710, 0.12, 0.48)
        c.echo(0.16, 0.17)
    elif cue == "dash":
        c.whoosh(0, 0.34, 0.65)
        c.air_sweep(0.02, 0.27, 340, 950, 0.24, 0.16, (1, 2))
        c.metal(0.21, 810, 0.24, 0.22)
    elif cue == "wave":
        c.whoosh(0, 0.36, 0.63)
        c.air_sweep(0.04, 0.42, 580, 240, 0.25, 0.25, (1, 2, 3))
        c.metal(0.18, 990, 0.3, 0.19)
    elif cue == "duel":
        c.thump(0.04, 90, 0.55, 0.55)
        c.metal(0.06, 370, 1.05, 0.40)
        c.air_sweep(0.08, 1.0, 300, 620, 0.29, 0.6, (1, 2))
        c.whoosh(0, 0.54, 0.45)
        c.echo(0.21, 0.20)
    elif cue == "boundary":
        c.air_sweep(0, 0.51, 560, 180, 0.26, 0.3, (1, 2))
        c.noise(0, 0.34, 400, 2600, 0.20, decay=0.13)
    return c


def black(name: str, duration: float, cue: str, kind: str = "skill") -> Clip:
    c = Clip(f"Oboro/{name}", duration, kind)
    if cue in ("backstab", "strike", "execute"):
        hit = 0.05 if cue != "execute" else 0.17
        c.slash(hit, 0.70 if cue == "execute" else 0.55,
                0.24 if cue == "execute" else 0.13)
        if cue == "execute":
            c.slash(hit + 0.055, 0.36, 0.19)
            c.noise(hit, 0.16, 110, 1000, 0.22, decay=0.045)
    elif cue == "throw":
        c.slash(0, 0.40, 0.14)
        c.air_sweep(0.015, 0.15, 3200, 6800, 0.24, 0.055)
    elif cue == "teleport":
        c.slash(0.07, 0.52, 0.12)
        c.noise(0, 0.15, 550, 5000, 0.25, decay=0.052)
    elif cue in ("stealth_in", "stealth_out"):
        entering = cue == "stealth_in"
        c.slash(0.07 if entering else 0, 0.34, 0.11)
        c.noise(0, 0.33 if entering else 0.18, 500, 3200,
                0.23, decay=0.11 if entering else 0.045)
    elif cue == "return":
        c.slash(0.05, 0.46, 0.13)
        c.noise(0, 0.16, 500, 4200, 0.20, decay=0.06)
    return c


def red(name: str, duration: float, cue: str, kind: str = "skill") -> Clip:
    c = Clip(f"Volbraak/{name}", duration, kind)
    if cue == "ready":
        c.bass_hit(0, 0.32)
    elif cue == "slam":
        c.noise(0, 0.18, 80, 1000, 0.28, decay=0.10)
        c.bass_hit(0.17, 0.95, 0.45)
        c.noise(0.18, 0.24, 200, 2700, 0.35, decay=0.095)
    elif cue == "shield":
        c.bass_hit(0, 0.62, 0.36)
        c.noise(0.05, 0.23, 100, 800, 0.24, decay=0.10)
    elif cue == "burst":
        c.bass_hit(0, 0.95, 0.48)
        c.noise(0.012, 0.35, 90, 2100, 0.52, decay=0.13)
    elif cue == "charge":
        c.bass_hit(0, 0.60, 0.31)
        c.noise(0.08, 0.33, 75, 950, 0.46, decay=0.15)
    elif cue == "stun":
        c.bass_hit(0, 0.87, 0.42)
        c.noise(0.022, 0.16, 500, 3900, 0.38, decay=0.043)
    elif cue == "chain_launch":
        c.noise(0, 0.30, 140, 1700, 0.36, decay=0.13)
        for at in (0.04, 0.12, 0.22, 0.33):
            c.noise(at, 0.05, 550, 5200, 0.19, decay=0.015)
    elif cue == "chain_latch":
        c.bass_hit(0, 0.68, 0.42)
        c.noise(0.075, 0.08, 500, 3800, 0.25, decay=0.03)
    return c


def yellow(name: str, duration: float, cue: str, kind: str = "skill") -> Clip:
    c = Clip(f"Rines/{name}", duration, kind)
    if cue == "overcharge":
        c.zap(0, 0.30, 2)
    elif cue == "mark":
        c.zap(0, 0.39, 2)
        c.noise(0.06, 0.15, 1900, 11_000, 0.17, decay=0.045)
    elif cue == "strike":
        c.zap(0.08, 0.78, 4)
        c.noise(0.08, 0.14, 750, 7600, 0.32, decay=0.034)
    elif cue == "ring":
        for at in (0, 0.045, 0.09):
            c.zap(at, 0.42, 2)
    elif cue == "form":
        c.noise(0, 0.30, 1100, 11_000, 0.20, decay=0.11)
        for at in (0.04, 0.10, 0.18):
            c.zap(at, 0.36, 2)
    elif cue == "contact":
        c.zap(0, 0.72, 3)
    elif cue == "warning":
        for at in (0, 0.13, 0.26):
            c.zap(at, 0.28, 1)
    elif cue == "thunder":
        c.zap(0, 0.96, 5)
        c.noise(0, 0.20, 1000, 15_000, 0.48, decay=0.047)
        c.zap(0.09, 0.35, 2)
    return c


def white(name: str, duration: float, cue: str, kind: str = "skill") -> Clip:
    c = Clip(f"Lieselotte/{name}", duration, kind)
    if cue == "fourth":
        c.glint(0, 0.36, 0.18)
        c.glint(0.045, 0.20, 0.12)
    elif cue == "bite":
        c.glint(0.08, 0.66, 0.22)
        c.noise(0.08, 0.12, 1200, 4200, 0.15, decay=0.032)
    elif cue == "rush":
        c.glint(0, 0.35, 0.31)
        c.air_sweep(0.04, 0.41, 2800, 6500, 0.25, 0.20)
    elif cue == "blink":
        c.glint(0, 0.55, 0.19)
        c.air_sweep(0.03, 0.21, 6500, 3200, 0.22, 0.07)
    elif cue == "pool":
        c.glint(0, 0.39, 0.26)
        c.noise(0.05, 0.29, 2400, 9000, 0.20, decay=0.11)
    elif cue == "dash":
        c.air_sweep(0, 0.38, 3300, 7100, 0.44, 0.18)
        c.glint(0.06, 0.25, 0.17)
    elif cue == "steal":
        c.glint(0, 0.73, 0.30)
        c.air_sweep(0.04, 0.50, 2900, 7500, 0.38, 0.24)
        c.glint(0.20, 0.30, 0.22)
    elif cue == "release":
        c.glint(0, 0.28, 0.17)
    return c


def generate() -> list[Clip]:
    clips = [
        blue("Q_BlinkHit", 0.53, "blink"),
        blue("W_GuardWhirl", 0.79, "guard"),
        blue("E_Dash", 0.57, "dash"),
        blue("E_WaveHit", 0.62, "wave"),
        blue("R_DuelField", 1.24, "duel", "ultimate"),
        blue("R_BoundarySlow", 0.56, "boundary"),
        black("P_Backstab", 0.44, "backstab", "passive"),
        black("Q_ShurikenThrow", 0.42, "throw"),
        black("Q_Teleport", 0.48, "teleport"),
        black("W_StealthEnter", 0.63, "stealth_in"),
        black("W_StealthExit", 0.38, "stealth_out"),
        black("E_BackAttack", 0.58, "strike"),
        black("E_Return", 0.46, "return"),
        black("R_Execute", 0.91, "execute", "ultimate"),
        red("P_ShieldReady", 0.68, "ready", "passive"),
        red("Q_GroundSlam", 0.85, "slam"),
        red("W_ShieldStart", 0.83, "shield"),
        red("W_Detonation", 0.73, "burst"),
        red("E_Charge", 0.69, "charge"),
        red("E_StunHit", 0.66, "stun"),
        red("R_ChainLaunch", 0.66, "chain_launch", "ultimate"),
        red("R_ChainLatch", 0.88, "chain_latch", "ultimate"),
        yellow("P_Overcharge", 0.31, "overcharge", "passive"),
        yellow("Q_Mark", 0.54, "mark"),
        yellow("Q_LightningStrike", 0.67, "strike"),
        yellow("W_RingPulse", 0.62, "ring"),
        yellow("E_ThunderForm", 0.82, "form"),
        yellow("E_Contact", 0.56, "contact"),
        yellow("R_Warning", 0.72, "warning", "ultimate"),
        yellow("R_ThunderImpact", 1.10, "thunder", "ultimate"),
        white("P_FourthShot", 0.46, "fourth", "passive"),
        white("Q_Bite", 0.60, "bite"),
        white("W_BloodRush", 0.67, "rush"),
        white("E_Blink", 0.59, "blink"),
        white("E_BloodPool", 0.74, "pool"),
        white("R_Dash", 0.69, "dash", "ultimate"),
        white("R_StatSteal", 0.99, "steal", "ultimate"),
        white("R_Release", 0.60, "release", "ultimate"),
    ]
    for clip in clips:
        clip.save()
    return clips


if __name__ == "__main__":
    made = generate()
    print(f"Generated {len(made)} WAV clips at {OUT}")
