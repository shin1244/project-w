"""Synthesize the game's sound effects into audio/sfx/*.wav.

Short, loud and gritty like late-90s RTS samples (22 kHz, saturated), with escaping steam as the
signature: most weapons, machines and buildings end in a pressure-release hiss. Every sound is
built from a few parts: a boom with a falling pitch, a noise crack for the transient,
inharmonic metal rings, plucked strings and band-passed hiss. Seeds are fixed, so running this
again writes the same files. Needs numpy.

    python tools/synth_sfx.py            # writes audio/sfx/
    python tools/synth_sfx.py --preview  # also writes audio/sfx-preview.html (not committed)
"""
import argparse
import base64
import os
import wave
import zlib

import numpy as np

SR = 44100
OUT_SR = 22050
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


class Synth:
    def __init__(self, seed):
        self.rng = np.random.default_rng(seed)

    # ── Building blocks ──

    @staticmethod
    def n(d):
        return int(SR * d)

    def t(self, d):
        return np.arange(self.n(d)) / SR

    def env(self, d, attack, decay):
        x = self.t(d)
        return np.minimum(x / max(attack, 1e-4), 1) * np.exp(-np.maximum(x - attack, 0) / decay)

    def noise(self, d):
        return self.rng.standard_normal(self.n(d))

    @staticmethod
    def norm(x):
        return x / (np.abs(x).max() + 1e-9)

    def place(self, buf, sound, at, gain=1.0):
        i = self.n(at)
        end = min(len(buf), i + len(sound))
        if end > i:
            buf[i:end] += gain * sound[: end - i]

    @staticmethod
    def filt(x, lo=None, hi=None):
        spectrum = np.fft.rfft(x)
        f = np.maximum(np.fft.rfftfreq(len(x), 1 / SR), 1)
        mask = np.ones_like(f)
        if lo:
            mask /= 1 + (lo / f) ** 4
        if hi:
            mask /= 1 + (f / hi) ** 4
        return np.fft.irfft(spectrum * mask, len(x))

    @staticmethod
    def band(x, centers, q):
        """State-variable band-pass with a per-sample centre frequency."""
        out = np.empty_like(x)
        low = bp = 0.0
        fs = 2 * np.sin(np.pi * np.minimum(centers, SR / 6) / SR)
        for i in range(len(x)):
            low += fs[i] * bp
            high = x[i] - low - q * bp
            bp += fs[i] * high
            out[i] = bp
        return out

    def flutter(self, d, rate=30, depth=0.35):
        raw = self.filt(self.noise(d), hi=rate)
        return 1 + depth * self.norm(raw)

    def hiss(self, d, start_hz, end_hz, attack, decay, q=0.7, wobble=0.35):
        """Steam escaping a valve: band-passed noise whose centre settles from start to end."""
        x = self.t(d)
        centers = end_hz + (start_hz - end_hz) * np.exp(-x / (d / 2.5))
        s = self.band(self.noise(d), centers, q) + 0.35 * self.filt(self.noise(d), lo=6000)
        return self.norm(s * self.env(d, attack, decay) * self.flutter(d, depth=wobble))

    def boom(self, d, f0, f1, tau, decay, drive=1.5):
        x = self.t(d)
        f = f1 + (f0 - f1) * np.exp(-x / tau)
        ph = 2 * np.pi * np.cumsum(f) / SR
        s = np.sin(ph) + 0.35 * np.sin(2 * ph) + 0.12 * np.sin(3 * ph)
        return self.norm(np.tanh(drive * s) * self.env(d, 0.002, decay))

    def crack(self, d, lo=1200, hi=9000, decay=0.006):
        return self.norm(self.filt(self.noise(d), lo=lo, hi=hi) * self.env(d, 0.0005, decay))

    def ring(self, d, base, decay, partials=(1, 2.76, 5.40, 8.93, 13.3), amps=(1, 0.6, 0.4, 0.25, 0.15)):
        """Struck metal: inharmonic partials, higher ones dying faster."""
        x = self.t(d)
        s = np.zeros(self.n(d))
        for k, (p, a) in enumerate(zip(partials, amps)):
            f = base * p * (1 + self.rng.uniform(-0.01, 0.01))
            s += a * np.sin(2 * np.pi * f * x + self.rng.uniform(0, 6.28)) * np.exp(-x / (decay / (1 + 0.6 * k)))
        return self.norm(s * np.minimum(x / 0.0008, 1))

    def clank(self, base, decay=0.12, d=0.4):
        return self.norm(self.ring(d, base, decay) + 0.6 * self.crack(d, lo=800, hi=7000, decay=0.004))

    def rumble(self, d, cutoff, attack, decay):
        return self.norm(self.filt(self.noise(d), hi=cutoff) * self.env(d, attack, decay))

    def pluck(self, d, freq, damping=0.996):
        """Karplus-Strong string."""
        period = int(SR / freq)
        line = self.rng.uniform(-1, 1, period)
        out = np.empty(self.n(d))
        for i in range(len(out)):
            j = i % period
            out[i] = line[j]
            line[j] = damping * 0.5 * (line[j] + line[(j + 1) % period])
        return self.norm(out)

    def whistle(self, d, tones, attack=0.05, release=0.25, breath=0.3):
        x = self.t(d)
        s = np.zeros(self.n(d))
        glide = 1 - 0.08 * np.exp(-x / 0.05)
        vibrato = 1 + 0.004 * np.sin(2 * np.pi * 5.5 * x)
        for k, tone in enumerate(tones):
            ph = 2 * np.pi * np.cumsum(tone * glide * vibrato * (1 + 0.002 * k)) / SR
            s += np.sin(ph) + 0.25 * np.sin(2 * ph)
            s += breath * self.band(self.noise(d), np.full(self.n(d), tone), 0.08)
        shape = np.minimum(x / attack, 1) * np.clip((d - x) / release, 0, 1)
        return self.norm(s * shape * self.flutter(d, rate=12, depth=0.08))

    def sweep(self, d, f0, f1, decay):
        x = self.t(d)
        f = f0 + (f1 - f0) * (x / d) ** 0.6
        return self.norm(np.sin(2 * np.pi * np.cumsum(f) / SR) * self.env(d, 0.002, decay))

    def clicks(self, buf, count, start, span, lo_hz, hi_hz, gain, decay=0.02, fade=True):
        """Scattered debris or ratchet teeth."""
        for _ in range(count):
            at = start + self.rng.uniform(0, span)
            g = gain * (1 - 0.8 * (at - start) / max(span, 1e-3)) if fade else gain
            self.place(buf, self.ring(0.08, self.rng.uniform(lo_hz, hi_hz), decay), at, g)

    def j(self, value, spread=0.06):
        """Per-variant jitter of a design value."""
        return value * (1 + self.rng.uniform(-spread, spread))

    def master(self, buf, drive=2.2):
        buf = self.filt(buf, lo=35)
        buf = self.norm(buf)
        buf = np.tanh(drive * buf) / np.tanh(drive)  # loud and gritty, never clipping
        fade = min(len(buf), self.n(0.015))
        buf[-fade:] *= np.linspace(1, 0, fade)
        down = self.filt(buf, hi=9500)[::2]  # 44.1 -> 22.05 kHz
        return 0.94 * self.norm(down)


def blank(s, d):
    return np.zeros(s.n(d))


# ── Weapons ──

def rifle(s):
    b = blank(s, 0.45)
    s.place(b, s.crack(0.03, lo=900, hi=7000, decay=0.004), 0, 1.0)
    s.place(b, s.boom(0.12, s.j(420), 140, 0.012, 0.03, drive=2.0), 0, 0.8)
    s.place(b, s.filt(s.noise(0.15), lo=2200) * s.env(0.15, 0.001, 0.035), 0.002, 0.7)
    s.place(b, s.hiss(0.3, 7500, 5000, 0.01, 0.07, wobble=0.2), 0.02, 0.22)
    s.place(b, s.ring(0.06, s.j(1750), 0.018), 0.13, 0.22)
    return s.master(b, 2.0)


def pistol(s):
    b = blank(s, 0.35)
    s.place(b, s.crack(0.025, lo=1500, hi=9000, decay=0.003), 0, 1.0)
    s.place(b, s.boom(0.1, s.j(600), 200, 0.01, 0.025, drive=2.2), 0, 0.7)
    s.place(b, s.ring(0.12, s.j(2200), 0.03), 0.003, 0.25)
    s.place(b, s.hiss(0.25, 6000, 4000, 0.008, 0.05, wobble=0.2), 0.03, 0.18)
    return s.master(b, 2.2)


def tower_shot(s):
    b = blank(s, 0.8)
    s.place(b, s.crack(0.04, lo=900, decay=0.006), 0, 1.0)
    s.place(b, s.boom(0.4, s.j(220), 70, 0.025, 0.08, drive=2.4), 0, 1.0)
    s.place(b, s.ring(0.5, s.j(600), 0.2), 0.003, 0.3)
    s.place(b, s.hiss(0.65, 5500, 3000, 0.02, 0.2), 0.06, 0.45)
    return s.master(b, 2.4)


def bow(s):
    b = blank(s, 0.45)
    string = s.pluck(0.35, s.j(170, 0.08), 0.993) * s.env(0.35, 0.001, 0.09)
    s.place(b, string, 0, 0.7)
    whoosh = s.band(s.noise(0.3), np.linspace(1500, 4200, s.n(0.3)), 0.5) * s.env(0.3, 0.04, 0.08)
    s.place(b, s.norm(whoosh), 0.01, 0.6)
    s.place(b, s.crack(0.02, lo=2000, decay=0.003), 0, 0.3)
    return s.master(b, 1.8)


def sword(s):
    b = blank(s, 0.5)
    s.place(b, s.ring(0.45, s.j(1050, 0.12), 0.14), 0, 0.8)
    s.place(b, s.crack(0.03, lo=1200, decay=0.005), 0, 0.8)
    s.place(b, s.boom(0.12, 160, 90, 0.015, 0.04), 0, 0.6)
    s.place(b, s.hiss(0.25, 6500, 4500, 0.005, 0.05, wobble=0.2), 0.04, 0.2)
    return s.master(b, 2.3)


def claw(s):
    b = blank(s, 0.4)
    swish = s.band(s.noise(0.2), np.linspace(3200, 900, s.n(0.2)), 0.45) * s.env(0.2, 0.01, 0.05)
    s.place(b, s.norm(swish), 0, 0.8)
    s.place(b, s.boom(0.15, s.j(140), 70, 0.02, 0.05, drive=2.0), 0.05, 0.8)
    s.place(b, s.filt(s.noise(0.12), lo=600, hi=2500) * s.env(0.12, 0.001, 0.025), 0.05, 0.6)
    return s.master(b, 2.2)


def stone(s):
    b = blank(s, 0.65)
    s.place(b, s.boom(0.6, s.j(95), 38, 0.03, 0.18, drive=3.0), 0, 1.2)
    s.place(b, s.rumble(0.35, 1400, 0.001, 0.07), 0, 0.8)
    s.clicks(b, 6, 0.03, 0.3, 250, 700, 0.3, decay=0.03)
    return s.master(b, 2.6)


def flame(s):
    b = blank(s, 0.8)
    roar = s.rumble(0.75, 1300, 0.04, 0.3) * s.flutter(0.75, rate=40, depth=0.5)
    s.place(b, s.norm(roar), 0, 1.0)
    s.place(b, s.boom(0.4, 70, 45, 0.05, 0.15), 0, 0.5)
    s.place(b, s.hiss(0.6, 3500, 2200, 0.03, 0.2, q=0.5), 0.02, 0.35)
    for _ in range(14):
        s.place(b, s.crack(0.01, lo=2500, decay=0.0015), s.rng.uniform(0.05, 0.6), s.rng.uniform(0.1, 0.35))
    return s.master(b, 2.4)


def artillery_fire(s):
    b = blank(s, 1.9)
    s.place(b, s.crack(0.05, lo=1500, decay=0.008), 0, 1.0)
    s.place(b, s.boom(0.9, s.j(150), 40, 0.045, 0.32, drive=2.5), 0, 1.3)
    s.place(b, s.filt(s.noise(0.6), hi=380) * s.env(0.6, 0.004, 0.18), 0, 3.0)
    s.place(b, s.ring(0.8, s.j(175), 0.5), 0.004, 0.32)
    s.place(b, s.hiss(1.6, 5200, 2100, 0.025, 0.55), 0.11, 0.62)
    s.place(b, s.hiss(0.7, 6500, 3500, 0.01, 0.18, wobble=0.5), 0.62, 0.3)
    return s.master(b, 2.6)


def shell_impact(s):
    b = blank(s, 1.4)
    s.place(b, s.crack(0.04, lo=1000, decay=0.01), 0, 1.0)
    s.place(b, s.boom(1.0, s.j(115), 28, 0.06, 0.45, drive=2.8), 0, 1.4)
    s.place(b, s.filt(s.noise(0.9), hi=600) * s.env(0.9, 0.003, 0.3), 0, 2.5)
    s.place(b, s.filt(s.noise(0.7), lo=300, hi=2500) * s.env(0.7, 0.005, 0.22), 0.01, 0.9)
    s.clicks(b, 9, 0.12, 0.6, 600, 1900, 0.25, decay=0.015, fade=False)
    return s.master(b, 2.8)


def artillery_deploy(s):
    b = blank(s, 1.75)
    s.place(b, s.clank(320, 0.09), 0, 0.7)
    s.place(b, s.boom(0.3, 95, 55, 0.03, 0.07), 0, 0.8)
    for k in range(8):
        s.place(b, s.ring(0.05, 2300 + 90 * (k % 3), 0.012), 0.18 + 0.027 * k, 0.28)
    s.place(b, s.boom(0.5, 75, 38, 0.04, 0.15, drive=2.2), 0.43, 1.1)
    s.place(b, s.clank(140, 0.3, 0.6), 0.43, 0.7)
    s.place(b, s.hiss(1.15, 6500, 2800, 0.012, 0.45, q=0.6), 0.48, 0.85)
    s.place(b, s.clank(520, 0.05, 0.2), 1.12, 0.35)
    return s.master(b, 2.4)


def artillery_pack(s):
    b = blank(s, 1.4)
    s.place(b, s.hiss(0.7, 5500, 3200, 0.01, 0.25, q=0.6), 0, 0.75)
    s.place(b, s.clank(160, 0.2, 0.5), 0.35, 0.6)
    s.place(b, s.boom(0.3, 85, 50, 0.03, 0.08), 0.35, 0.7)
    for k in range(7):
        s.place(b, s.ring(0.05, 2500 - 80 * (k % 3), 0.012), 0.6 + 0.03 * k, 0.26)
    s.place(b, s.clank(380, 0.08), 0.95, 0.6)
    return s.master(b, 2.3)


def knight_charge(s):
    b = blank(s, 1.2)
    s.place(b, s.sweep(0.18, 900, 2400, 0.08), 0, 0.35)
    s.place(b, s.crack(0.02, lo=2500, decay=0.003), 0, 0.5)
    s.place(b, s.hiss(0.9, 2600, 5200, 0.04, 0.5, q=0.5, wobble=0.5), 0.08, 0.9)
    for k, at in enumerate((0.12, 0.27, 0.39, 0.48, 0.55)):
        s.place(b, s.boom(0.12, 130, 70, 0.015, 0.04), at, 0.55 - 0.05 * k)
    s.place(b, s.clank(260, 0.12), 0.86, 0.55)
    return s.master(b, 2.4)


def skill_cast(s):
    b = blank(s, 0.8)
    rise = s.band(s.noise(0.5), np.geomspace(700, 5200, s.n(0.5)), 0.4) * s.env(0.5, 0.15, 0.2)
    s.place(b, s.norm(rise), 0, 0.8)
    s.place(b, s.ring(0.6, s.j(1150, 0.1), 0.25, partials=(1, 2.0, 3.01, 4.2), amps=(1, 0.5, 0.35, 0.2)), 0.12, 0.45)
    s.place(b, s.hiss(0.5, 5500, 3500, 0.01, 0.15), 0.2, 0.4)
    return s.master(b, 1.9)


def skill_ultimate(s):
    b = blank(s, 1.5)
    s.place(b, s.boom(1.0, 120, 35, 0.06, 0.4, drive=2.6), 0, 1.0)
    s.place(b, s.whistle(0.9, (s.j(523.3, 0.02), 659.3, 784.0), attack=0.25, release=0.3, breath=0.2), 0.05, 0.6)
    s.place(b, s.hiss(1.2, 4500, 2000, 0.02, 0.45, wobble=0.45), 0.1, 0.75)
    s.place(b, s.ring(1.0, 880, 0.4, partials=(1, 2.0, 3.01, 4.2), amps=(1, 0.5, 0.35, 0.2)), 0.08, 0.35)
    return s.master(b, 2.3)


def ram_impact(s):
    b = blank(s, 1.5)
    s.place(b, s.boom(1.2, 75, 24, 0.05, 0.5, drive=3.2), 0, 1.5)
    for k in range(3):
        s.place(b, s.filt(s.noise(0.08), lo=400, hi=2600) * s.env(0.08, 0.001, 0.02), 0.01 * k, 0.8)
    s.place(b, s.rumble(1.0, 500, 0.005, 0.35), 0, 1.4)
    s.clicks(b, 10, 0.1, 0.8, 300, 1200, 0.3, decay=0.025)
    return s.master(b, 3.0)


# ── Work ──

def chop(s):
    b = blank(s, 0.28)
    s.place(b, s.boom(0.1, s.j(230), 160, 0.01, 0.03), 0, 0.9)
    s.place(b, s.filt(s.noise(0.06), lo=800, hi=2600) * s.env(0.06, 0.0005, 0.018), 0, 0.8)
    s.place(b, s.ring(0.08, s.j(520), 0.03), 0, 0.25)
    return s.master(b, 1.8)


def hammer(s):
    b = blank(s, 0.35)
    s.place(b, s.ring(0.3, s.j(1800, 0.08), 0.08), 0, 0.8)
    s.place(b, s.crack(0.02, lo=1500, decay=0.003), 0, 0.6)
    s.place(b, s.boom(0.08, 200, 120, 0.01, 0.025), 0, 0.5)
    return s.master(b, 2.0)


# ── Deaths ──

def death_soldier(s):
    b = blank(s, 0.85)
    for _ in range(4):
        s.place(b, s.ring(0.12, s.rng.uniform(600, 1800), 0.05), s.rng.uniform(0, 0.22), 0.35)
    s.place(b, s.boom(0.2, s.j(120), 60, 0.02, 0.06, drive=2.0), 0.18, 0.9)
    s.place(b, s.rumble(0.15, 1200, 0.001, 0.04), 0.18, 0.5)
    s.place(b, s.hiss(0.5, 5000, 2600, 0.005, 0.15, wobble=0.5), 0.05, 0.55)
    return s.master(b, 2.3)


def death_machine(s):
    b = blank(s, 1.5)
    for base, at in ((210, 0.0), (330, 0.03), (470, 0.07), (260, 0.12)):
        s.place(b, s.clank(s.j(base), 0.18, 0.5), at, 0.5)
    s.place(b, s.filt(s.noise(0.3), hi=3000) * s.env(0.3, 0.001, 0.08), 0, 0.8)
    s.place(b, s.boom(0.7, 105, 34, 0.05, 0.28, drive=2.4), 0.05, 1.1)
    s.place(b, s.hiss(1.25, 3800, 1500, 0.015, 0.6, q=0.55, wobble=0.45), 0.06, 0.8)
    s.clicks(b, 11, 0.3, 0.85, 800, 2600, 0.3)
    return s.master(b, 2.5)


def death_beast(s):
    b = blank(s, 0.9)
    s.place(b, s.boom(0.3, s.j(110), 50, 0.03, 0.08, drive=2.2), 0, 0.9)
    howl = s.band(s.noise(0.7), np.geomspace(900, 180, s.n(0.7)), 0.25) * s.env(0.7, 0.02, 0.25)
    s.place(b, s.norm(howl * s.flutter(0.7, rate=18, depth=0.6)), 0.02, 0.9)
    s.place(b, s.boom(0.25, 90, 45, 0.02, 0.07), 0.4, 0.6)
    return s.master(b, 2.4)


def death_heavy(s):
    b = blank(s, 1.3)
    s.place(b, s.boom(0.8, 70, 28, 0.05, 0.3, drive=2.8), 0, 1.1)
    s.place(b, s.rumble(1.2, 900, 0.01, 0.4), 0, 1.0)
    s.clicks(b, 16, 0.05, 1.0, 200, 650, 0.35, decay=0.03)
    return s.master(b, 2.5)


def building_collapse(s):
    b = blank(s, 2.3)
    s.place(b, s.boom(1.6, 55, 22, 0.08, 0.8, drive=3.0), 0, 1.4)
    s.place(b, s.rumble(2.0, 450, 0.02, 0.7), 0, 1.6)
    x = s.t(1.2)
    groan = np.sin(2 * np.pi * np.cumsum(120 - 45 * x / 1.2) / SR) * (0.6 + 0.4 * np.sin(2 * np.pi * 7 * x))
    s.place(b, s.norm(groan * s.env(1.2, 0.1, 0.5)), 0.05, 0.3)
    s.clicks(b, 24, 0.15, 1.7, 300, 2200, 0.35, decay=0.03)
    s.place(b, s.hiss(1.5, 4200, 1800, 0.05, 0.6, wobble=0.5), 0.35, 0.7)
    return s.master(b, 2.8)


# ── Construction and production ──

def build_start(s):
    b = blank(s, 0.85)
    s.place(b, s.clank(300, 0.1), 0, 0.6)
    s.place(b, s.boom(0.25, 100, 60, 0.02, 0.06), 0, 0.7)
    for k in range(4):
        s.place(b, s.ring(0.05, 2300 + 120 * k, 0.012), 0.15 + 0.035 * k, 0.3)
    s.place(b, s.hiss(0.5, 5500, 3500, 0.01, 0.15), 0.32, 0.5)
    return s.master(b, 2.0)


def build_complete(s):
    b = blank(s, 1.7)
    s.place(b, s.clank(300, 0.1), 0, 0.45)
    s.place(b, s.whistle(1.15, (466.2, 587.3, 698.5)), 0.06, 0.85)
    s.place(b, s.hiss(0.6, 5000, 3000, 0.01, 0.2, wobble=0.3), 1.08, 0.35)
    return s.master(b, 1.6)


def unit_ready(s):
    b = blank(s, 0.55)
    s.place(b, s.ring(0.05, 2600, 0.008), 0, 0.4)
    s.place(b, s.whistle(0.14, (784.0,), attack=0.015, release=0.04, breath=0.15), 0.03, 0.7)
    s.place(b, s.whistle(0.2, (987.8,), attack=0.015, release=0.06, breath=0.15), 0.19, 0.7)
    s.place(b, s.hiss(0.25, 6000, 4000, 0.005, 0.07), 0.38, 0.25)
    return s.master(b, 1.5)


# ── Interface ──

def ui_click(s):
    b = blank(s, 0.09)
    s.place(b, s.ring(0.06, 2400, 0.012), 0, 0.8)
    s.place(b, s.crack(0.01, lo=3000, decay=0.0012), 0, 0.5)
    return s.master(b, 1.4)


def ui_error(s):
    b = blank(s, 0.32)
    x = s.t(0.28)
    tone = sum(np.tanh(3 * np.sin(2 * np.pi * f * x)) for f in (196.0, 207.7))
    s.place(b, s.norm(s.filt(tone, hi=2200) * np.minimum(x / 0.01, 1) * np.clip((0.28 - x) / 0.05, 0, 1)), 0, 1.0)
    return s.master(b, 1.6)


def select(s):
    b = blank(s, 0.22)
    s.place(b, s.ring(0.04, 3000, 0.006), 0, 0.5)
    pitch = s.j(1300, 0.12)
    s.place(b, s.sweep(0.07, pitch, pitch * 1.25, 0.04), 0.015, 0.6)
    s.place(b, s.hiss(0.12, 7000, 5000, 0.004, 0.03, wobble=0.1), 0.06, 0.25)
    return s.master(b, 1.5)


def order_move(s):
    b = blank(s, 0.16)
    s.place(b, s.ring(0.04, s.j(2800), 0.006), 0, 0.5)
    s.place(b, s.hiss(0.12, 7500, 5500, 0.002, 0.03, wobble=0.1), 0.005, 0.7)
    return s.master(b, 1.5)


def order_attack(s):
    b = blank(s, 0.26)
    s.place(b, s.ring(0.2, s.j(700), 0.06), 0, 0.8)
    s.place(b, s.crack(0.015, lo=1500, decay=0.002), 0, 0.6)
    s.place(b, s.hiss(0.15, 6500, 4500, 0.003, 0.04, wobble=0.1), 0.03, 0.4)
    return s.master(b, 1.8)


# name: (builder, variants). File names are <name>-<n>.wav; audio/SoundBank.cs lists the same names.
SOUNDS = {
    "rifle": (rifle, 3), "pistol": (pistol, 2), "tower-shot": (tower_shot, 2), "bow": (bow, 2),
    "sword": (sword, 3), "claw": (claw, 2), "stone": (stone, 2), "flame": (flame, 2),
    "artillery-fire": (artillery_fire, 2), "shell-impact": (shell_impact, 2),
    "artillery-deploy": (artillery_deploy, 1), "artillery-pack": (artillery_pack, 1),
    "knight-charge": (knight_charge, 1), "skill-cast": (skill_cast, 2), "skill-ultimate": (skill_ultimate, 1),
    "ram-impact": (ram_impact, 1), "chop": (chop, 3), "hammer": (hammer, 2),
    "death-soldier": (death_soldier, 3), "death-machine": (death_machine, 2), "death-beast": (death_beast, 2),
    "death-heavy": (death_heavy, 2), "building-collapse": (building_collapse, 1),
    "build-start": (build_start, 1), "build-complete": (build_complete, 1), "unit-ready": (unit_ready, 1),
    "ui-click": (ui_click, 1), "ui-error": (ui_error, 1), "select": (select, 3),
    "order-move": (order_move, 2), "order-attack": (order_attack, 2),
}


def wav_bytes(samples):
    import io
    out = io.BytesIO()
    with wave.open(out, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(OUT_SR)
        w.writeframes((np.clip(samples, -1, 1) * 32767).astype("<i2").tobytes())
    return out.getvalue()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", action="store_true")
    args = parser.parse_args()
    folder = os.path.join(ROOT, "audio", "sfx")
    os.makedirs(folder, exist_ok=True)
    cards = []
    for name, (build, variants) in SOUNDS.items():
        for k in range(1, variants + 1):
            data = wav_bytes(build(Synth(zlib.crc32(f"{name}-{k}".encode()))))
            with open(os.path.join(folder, f"{name}-{k}.wav"), "wb") as f:
                f.write(data)
            cards.append((f"{name}-{k}", base64.b64encode(data).decode()))
        print(f"{name}: {variants}")
    if args.preview:
        rows = "\n".join(f'<div><code>{key}</code><audio controls src="data:audio/wav;base64,{b64}"></audio></div>'
                         for key, b64 in cards)
        with open(os.path.join(ROOT, "audio", "sfx-preview.html"), "w", encoding="utf-8") as f:
            f.write(PREVIEW.replace("{rows}", rows))


PREVIEW = """<!doctype html><html lang="ko"><head><meta charset="utf-8"><title>효과음 목록</title>
<style>body{font:14px system-ui,sans-serif;margin:24px;background:#1b1915;color:#eee}
div{display:flex;gap:12px;align-items:center;margin:6px 0}code{width:180px}</style></head>
<body><h1>효과음 목록</h1>{rows}</body></html>"""

if __name__ == "__main__":
    main()
