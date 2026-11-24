#!/usr/bin/env python3
"""
Gera 4 arquivos WAV TEMP (royalty-free procedural local) para a Cutscene 1 ("Concerto")
e a textura 2D do Quadro de Checklist de Pecas ("PROJETO ECHO - CONFIDENCIAL").
Marcados explicitamente como TEMP ate substituicao pela trilha final.
"""
import os
import wave
import numpy as np
from PIL import Image, ImageDraw, ImageFont

AUDIO_OUT = "/mnt/c/Users/Bruno/workspace/game-concerto/unity/Assets/Game/Audio/Cutscenes/CS01"
TEX_OUT   = "/mnt/c/Users/Bruno/workspace/game-concerto/unity/Assets/Game/Art/Textures/Cutscenes"
os.makedirs(AUDIO_OUT, exist_ok=True)
os.makedirs(TEX_OUT, exist_ok=True)

SR = 44100


def save_wav(path: str, samples: np.ndarray):
    s = np.clip(samples, -0.95, 0.95)
    pcm = (s * 32767.0).astype(np.int16)
    with wave.open(path, "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(SR)
        wf.writeframes(pcm.tobytes())
    print(f"WAV_OK: {path} ({len(samples)/SR:.2f}s)")


def musicbox_note(freq: float, dur: float) -> np.ndarray:
    t = np.linspace(0, dur, int(SR * dur), endpoint=False, dtype=np.float32)
    # Timbre cristalino de caixinha de musica (fundamental + 2o e 3o harmonicos + ataque suave)
    env = np.minimum(1.0, t / 0.008) * np.exp(-t * 2.6)
    sig = (
        0.65 * np.sin(2 * np.pi * freq * t)
        + 0.22 * np.sin(2 * np.pi * freq * 2.0 * t) * np.exp(-t * 4.5)
        + 0.09 * np.sin(2 * np.pi * freq * 3.0 * t) * np.exp(-t * 7.0)
    )
    return sig * env * 0.45


def gen_musicbox_motif():
    """Tema calmo e nostalgico (La menor -> Fa -> Do -> Sol) — motif de CONCERTO (TEMP)."""
    # Frequencias (Hz): A4=440, B4=493.88, C5=523.25, D5=587.33, E5=659.25, F5=698.46, G5=783.99, A5=880
    melody = [
        (0.0, 523.25), (0.65, 659.25), (1.30, 880.00), (2.10, 783.99),
        (2.80, 659.25), (3.55, 587.33), (4.30, 523.25), (5.20, 440.00),
        (6.00, 349.23), (6.65, 523.25), (7.30, 698.46), (8.10, 659.25),
        (8.90, 587.33), (9.75, 523.25), (10.60, 493.88), (11.50, 440.00),
    ]
    total_dur = 14.5
    buf = np.zeros(int(SR * total_dur), dtype=np.float32)
    for start, freq in melody:
        note = musicbox_note(freq, 2.4)
        idx = int(start * SR)
        end = min(len(buf), idx + len(note))
        buf[idx:end] += note[: end - idx]
    # Reverb leve por delay curto
    delay = int(0.18 * SR)
    buf[delay:] += buf[:-delay] * 0.25
    save_wav(os.path.join(AUDIO_OUT, "cs01_musicbox_motif_TEMP.wav"), buf)


def gen_machine_hum():
    """Zumbido grave e misterioso da maquina sob a lona (55Hz + 110Hz + pulso lento)."""
    dur = 16.0
    t = np.linspace(0, dur, int(SR * dur), endpoint=False, dtype=np.float32)
    lfo = 0.75 + 0.25 * np.sin(2 * np.pi * 0.35 * t)
    sig = (
        0.38 * np.sin(2 * np.pi * 55.0 * t)
        + 0.18 * np.sin(2 * np.pi * 110.0 * t)
        + 0.06 * np.sin(2 * np.pi * 220.5 * t) * lfo
    )
    fade = np.minimum(1.0, t / 1.5) * np.minimum(1.0, (dur - t) / 1.5)
    save_wav(os.path.join(AUDIO_OUT, "cs01_machine_hum_TEMP.wav"), sig * fade * 0.55)


def gen_radio_chime():
    """Vinheta institucional curta do radio/holograma ('Projeto Echo')."""
    dur = 2.2
    buf = np.zeros(int(SR * dur), dtype=np.float32)
    for st, fr in [(0.0, 440.0), (0.22, 554.37), (0.44, 659.25)]:
        t = np.linspace(0, 0.9, int(SR * 0.9), endpoint=False, dtype=np.float32)
        env = np.minimum(1.0, t / 0.01) * np.exp(-t * 3.8)
        s = np.sin(2 * np.pi * fr * t) * env * 0.32
        idx = int(st * SR)
        buf[idx : idx + len(s)] += s
    save_wav(os.path.join(AUDIO_OUT, "cs01_radio_chime_TEMP.wav"), buf)


def gen_memory_echo():
    """Ressonancia eterea abafada para o beat emocional da foto."""
    dur = 6.5
    t = np.linspace(0, dur, int(SR * dur), endpoint=False, dtype=np.float32)
    env = np.minimum(1.0, t / 1.0) * np.exp(-t * 0.45)
    sig = (
        0.28 * np.sin(2 * np.pi * 220.0 * t)
        + 0.22 * np.sin(2 * np.pi * 329.63 * t)
        + 0.18 * np.sin(2 * np.pi * 440.0 * t)
        + 0.12 * np.sin(2 * np.pi * 523.25 * t)
    )
    save_wav(os.path.join(AUDIO_OUT, "cs01_echo_memory_TEMP.wav"), sig * env * 0.6)


def gen_checklist_board_texture():
    """Textura 2D nitida (1024x768) do quadro de checklist de pecas ('PROJETO ECHO - CONFIDENCIAL')."""
    W, H = 1024, 768
    img = Image.new("RGB", (W, H), (28, 34, 42))
    dr = ImageDraw.Draw(img)

    # Moldura metalica rebitada
    dr.rectangle([10, 10, W - 10, H - 10], outline=(168, 128, 64), width=14)
    dr.rectangle([30, 30, W - 30, H - 30], outline=(70, 95, 115), width=3)

    # Grade sutil de blueprint
    for x in range(48, W - 48, 48):
        dr.line([(x, 40), (x, H - 40)], fill=(36, 48, 60), width=1)
    for y in range(48, H - 48, 48):
        dr.line([(40, y), (W - 40, y)], fill=(36, 48, 60), width=1)

    # Selo vermelho CONFIDENCIAL no canto superior direito
    dr.rectangle([670, 52, 960, 118], outline=(210, 58, 48), width=5)
    dr.text((700, 72), "CONFIDENCIAL", fill=(225, 72, 60))

    # Titulo
    dr.text((64, 62), "PROJETO ECHO  //  CHECKLIST DE COMPONENTES", fill=(230, 210, 160))
    dr.line([(60, 128), (960, 128)], fill=(168, 128, 64), width=3)

    # 4 slots de pecas (2x2) — 3 VAZIOS (pendentes) e 1 instalado
    slots = [
        (70,  165, "01. NUCLEO DE RESSONANCIA", "STATUS: PENDENTE", (220, 95, 75),  False),
        (530, 165, "02. BOBINA DE FASE BETA",   "STATUS: PENDENTE", (220, 95, 75),  False),
        (70,  430, "03. CRISTAL ESTABILIZADOR", "STATUS: PENDENTE", (220, 95, 75),  False),
        (530, 430, "04. CHASSI DE CONTENCAO",   "STATUS: OK",       (70, 215, 195), True),
    ]
    for sx, sy, title, st, col, done in slots:
        dr.rectangle([sx, sy, sx + 420, sy + 220], fill=(22, 28, 36), outline=col, width=4)
        dr.text((sx + 20, sy + 20), title, fill=(220, 228, 235))
        dr.text((sx + 20, sy + 52), st, fill=col)
        # Area visual do slot vazio (silhueta tracejada)
        dr.rectangle([sx + 25, sy + 92, sx + 395, sy + 195], outline=(90, 110, 130), width=2)
        if not done:
            dr.text((sx + 135, sy + 134), "[ SLOT VAZIO ]", fill=(140, 155, 170))
        else:
            dr.rectangle([sx + 38, sy + 104, sx + 382, sy + 184], fill=(32, 92, 95), outline=(90, 230, 220), width=2)
            dr.text((sx + 125, sy + 134), "[ INSTALADO ]", fill=(160, 250, 245))

    out_path = os.path.join(TEX_OUT, "cs01_quadro_checklist.png")
    img.save(out_path)
    print(f"CHECKLIST_TEX_OK: {out_path}")


if __name__ == "__main__":
    gen_musicbox_motif()
    gen_machine_hum()
    gen_radio_chime()
    gen_memory_echo()
    gen_checklist_board_texture()
