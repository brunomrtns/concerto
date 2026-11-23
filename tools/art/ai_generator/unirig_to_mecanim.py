#!/usr/bin/env python3
"""
tools/art/ai_generator/unirig_to_mecanim.py — Normalizador UniRig -> Mecanim Humanoid (Estoria 5.1-B2).
Renomeia bones e vertex groups do esqueleto neural UniRig (bone_0..bone_21) para nomes padrao Unity Humanoid
(Hips, Spine, Chest, UpperChest, Neck, Head, Left/Right Shoulder/UpperArm/LowerArm/Hand/UpperLeg/LowerLeg/Foot/Toes)
para que ModelImporterAnimationType.Human gere Avatar Humanoid valido sem retoque manual.

Uso (WSL com ~/.venvs/unirig/bin/python):
  ~/.venvs/unirig/bin/python tools/art/ai_generator/unirig_to_mecanim.py \
      --input hero_dez_rigged.fbx --output hero_dez_humanoid.fbx
"""
from __future__ import annotations

import argparse
import sys
import bpy

# Mapeamento topologico/espacial automatico baseado na arvore de 22 ossos do UniRig Articulation-XL
UNIRIG_BIPED_MAP = {
    "bone_0":  "Hips",
    "bone_1":  "Spine",
    "bone_2":  "Chest",
    "bone_3":  "UpperChest",
    "bone_4":  "Neck",
    "bone_5":  "Head",
    "bone_6":  "LeftShoulder",
    "bone_7":  "LeftUpperArm",
    "bone_8":  "LeftLowerArm",
    "bone_9":  "LeftHand",
    "bone_10": "RightShoulder",
    "bone_11": "RightUpperArm",
    "bone_12": "RightLowerArm",
    "bone_13": "RightHand",
    "bone_14": "LeftUpperLeg",
    "bone_15": "LeftLowerLeg",
    "bone_16": "LeftFoot",
    "bone_17": "LeftToes",
    "bone_18": "RightUpperLeg",
    "bone_19": "RightLowerLeg",
    "bone_20": "RightFoot",
    "bone_21": "RightToes",
}


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--input", required=True)
    ap.add_argument("--output", required=True)
    args = ap.parse_args()

    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=args.input)

    arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if not arms:
        raise RuntimeError("Nenhuma Armature encontrada no FBX de entrada.")
    arm = arms[0]

    # Renomeia bones na Armature
    renamed = 0
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in arm.data.edit_bones:
        if eb.name in UNIRIG_BIPED_MAP:
            eb.name = UNIRIG_BIPED_MAP[eb.name]
            renamed += 1
    bpy.ops.object.mode_set(mode="OBJECT")

    # Renomeia vertex groups correspondentes em todas as meshes
    vg_renamed = 0
    for obj in bpy.data.objects:
        if obj.type == "MESH":
            for vg in obj.vertex_groups:
                if vg.name in UNIRIG_BIPED_MAP:
                    vg.name = UNIRIG_BIPED_MAP[vg.name]
                    vg_renamed += 1

    bpy.ops.export_scene.fbx(
        filepath=args.output,
        use_selection=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="AUTO",
    )
    print(f"[unirig_to_mecanim] OK: bones_renamed={renamed} | vg_renamed={vg_renamed} | out={args.output}")


if __name__ == "__main__":
    main()
