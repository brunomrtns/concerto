#!/usr/bin/env python3
"""
Gera texturas PBR 100% tileable (sem costura) de cimento/concreto industrial gasto
para o piso do Caldeiral (1024x1024 Albedo + Normal Map).
"""
import os
import numpy as np
from PIL import Image

OUT_DIR = (
    r"C:\Users\Bruno\workspace\game-concerto\unity\Assets\Game\Art\Textures\Generated"
    if os.name == "nt"
    else "/mnt/c/Users/Bruno/workspace/game-concerto/unity/Assets/Game/Art/Textures/Generated"
)
os.makedirs(OUT_DIR, exist_ok=True)


def periodic_noise(size: int, grid: int, rng: np.random.Generator) -> np.ndarray:
    """Ruido de valor 2D estritamente periodico (tileable sem emendas) com interpolacao suave."""
    base = rng.random((grid, grid), dtype=np.float32)
    coords = np.linspace(0, grid, size, endpoint=False, dtype=np.float32)
    x0 = np.floor(coords).astype(np.int32) % grid
    x1 = (x0 + 1) % grid
    fx = coords - np.floor(coords)
    # Smoothstep (quintic)
    ux = fx * fx * fx * (fx * (fx * 6.0 - 15.0) + 10.0)

    # Interpolacao bilinear periodica
    c00 = base[x0[:, None], x0[None, :]]
    c10 = base[x0[:, None], x1[None, :]]
    c01 = base[x1[:, None], x0[None, :]]
    c11 = base[x1[:, None], x1[None, :]]

    nx0 = c00 * (1.0 - ux[None, :]) + c10 * ux[None, :]
    nx1 = c01 * (1.0 - ux[None, :]) + c11 * ux[None, :]
    return nx0 * (1.0 - ux[:, None]) + nx1 * ux[:, None]


def generate_concrete_pbr(size: int = 1024, seed: int = 2026):
    rng = np.random.default_rng(seed)

    # 1. fBm multi-escala (manchas amplas de concreto + textura de desempenadeira + grao fino)
    n_macro = periodic_noise(size, 4, rng)
    n_med1  = periodic_noise(size, 8, rng)
    n_med2  = periodic_noise(size, 16, rng)
    n_fine1 = periodic_noise(size, 32, rng)
    n_fine2 = periodic_noise(size, 64, rng)
    n_grain = periodic_noise(size, 128, rng)
    n_micro = rng.normal(0.0, 1.0, (size, size)).astype(np.float32)

    fbm = (
        0.30 * n_macro
        + 0.22 * n_med1
        + 0.18 * n_med2
        + 0.13 * n_fine1
        + 0.09 * n_fine2
        + 0.08 * n_grain
    )
    fbm = (fbm - fbm.min()) / max(1e-6, (fbm.max() - fbm.min()))

    # 2. Juntas de dilatacao sutis de placas de concreto (grade 2x2 no tile de ~2.5m)
    ys, xs = np.mgrid[0:size, 0:size]
    # Perturbacao organica leve nas juntas para nao parecer linha digital dura
    jitter_x = (periodic_noise(size, 16, rng) - 0.5) * 3.0
    jitter_y = (periodic_noise(size, 16, rng) - 0.5) * 3.0

    half = size // 2
    dx0 = np.minimum(xs, size - xs).astype(np.float32)
    dx1 = np.abs(xs - half).astype(np.float32)
    dy0 = np.minimum(ys, size - ys).astype(np.float32)
    dy1 = np.abs(ys - half).astype(np.float32)

    dist_x = np.minimum(dx0, dx1) + jitter_x * 0.35
    dist_y = np.minimum(dy0, dy1) + jitter_y * 0.35
    dist_seam = np.minimum(np.abs(dist_x), np.abs(dist_y))

    # Sulco fino (largura ~3.5 px) + leve chanfro (~9 px)
    seam_groove = np.clip(1.0 - dist_seam / 3.5, 0.0, 1.0)
    seam_bevel = np.clip(1.0 - dist_seam / 9.5, 0.0, 1.0) * 0.28

    # Pequenos poros/falhas de concreto (pitting)
    pitting_raw = periodic_noise(size, 96, rng)
    pitting = np.clip((0.14 - pitting_raw) / 0.14, 0.0, 1.0) ** 2.0

    # 3. Heightfield final (0..1)
    height = (
        0.55
        + (fbm - 0.5) * 0.32
        + n_micro * 0.018
        - seam_groove * 0.38
        - seam_bevel * 0.10
        - pitting * 0.18
    )
    height = np.clip(height, 0.0, 1.0)

    # 4. Albedo de cimento industrial cinza-quente (harmoniza com oficina de cobre/latao sem ser marrom)
    # Base cinza concreto medio-claro (RGB ~ 0.44..0.58) com leve poeira quente nas manchas
    warm_stain = np.clip((periodic_noise(size, 6, rng) - 0.55) / 0.45, 0.0, 1.0)
    base_luma = 0.43 + fbm * 0.17 + n_micro * 0.012
    base_luma -= seam_groove * 0.22 + seam_bevel * 0.06 + pitting * 0.09

    r = base_luma * 1.02 + warm_stain * 0.028
    g = base_luma * 1.00 + warm_stain * 0.016
    b = base_luma * 0.96 - warm_stain * 0.008

    albedo = np.stack([r, g, b], axis=-1)
    albedo_u8 = np.clip(albedo * 255.0, 0, 255).astype(np.uint8)

    # 5. Normal Map (Sobel periodico via np.roll -> 100% tileable!)
    strength = 4.2
    h_l = np.roll(height,  1, axis=1)
    h_r = np.roll(height, -1, axis=1)
    h_u = np.roll(height,  1, axis=0)
    h_d = np.roll(height, -1, axis=0)

    dx = (h_r - h_l) * strength
    dy = (h_d - h_u) * strength
    dz = np.ones_like(height, dtype=np.float32)

    inv_len = 1.0 / np.sqrt(dx * dx + dy * dy + dz * dz)
    nx = -dx * inv_len
    ny =  dy * inv_len
    nz =  dz * inv_len

    normal = np.stack([nx * 0.5 + 0.5, ny * 0.5 + 0.5, nz * 0.5 + 0.5], axis=-1)
    normal_u8 = np.clip(normal * 255.0, 0, 255).astype(np.uint8)

    alb_path = os.path.join(OUT_DIR, "caldeiral_piso_cimento_albedo.png")
    nrm_path = os.path.join(OUT_DIR, "caldeiral_piso_cimento_normal.png")
    Image.fromarray(albedo_u8, mode="RGB").save(alb_path)
    Image.fromarray(normal_u8, mode="RGB").save(nrm_path)
    print(f"CONCRETE_PBR_OK: {alb_path} | {nrm_path}")


if __name__ == "__main__":
    generate_concrete_pbr()
