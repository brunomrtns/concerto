#!/usr/bin/env python3
"""
tools/art/ai_generator/unirig_to_mecanim.py (v2 — RUN mega_v6 / ADR-012):
Normaliza esqueleto neural UniRig -> Mecanim Humanoid (22 ossos) E adiciona ossos secundarios
filhos de Head (Ear_L, Ear_R, Antenna_Top) com pesos suaves na malha para SpringBone C# no Unity!
"""
from __future__ import annotations

import argparse
import bpy
from mathutils import Vector

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


def add_secondary_head_bones(arm_obj, mesh_obj) -> dict:
    """Adiciona Ear_L, Ear_R e Antenna_Top filhos de Head e transfere peso suave dos vertices das pontas."""
    verts = mesh_obj.data.vertices
    # Descobre bounds reais da cabeca (vertices com peso em Head ou Neck acima do centro)
    head_vg = mesh_obj.vertex_groups.get("Head")
    if not head_vg:
        return {"added": 0}

    # Coleta posicoes em espaco do objeto (no Blender, Z = altura, X = largura)
    head_pts = []
    for v in verts:
        for g in v.groups:
            if g.group == head_vg.index and g.weight > 0.25:
                head_pts.append(v.co)
                break
    if not head_pts:
        return {"added": 0}

    min_x = min(p.x for p in head_pts)
    max_x = max(p.x for p in head_pts)
    min_z = min(p.z for p in head_pts)
    max_z = max(p.z for p in head_pts)
    avg_y = sum(p.y for p in head_pts) / len(head_pts)
    mid_z = 0.5 * (min_z + max_z)

    # Cria os 3 ossos secundarios em EDIT mode
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode="EDIT")
    eb_head = arm_obj.data.edit_bones.get("Head")
    if not eb_head:
        bpy.ops.object.mode_set(mode="OBJECT")
        return {"added": 0}

    specs = [
        ("Ear_L",       Vector((max_x * 0.42, avg_y, mid_z)), Vector((max_x * 0.95, avg_y, mid_z + 0.04))),
        ("Ear_R",       Vector((min_x * 0.42, avg_y, mid_z)), Vector((min_x * 0.95, avg_y, mid_z + 0.04))),
        ("Antenna_Top", Vector((0.0, avg_y, max_z - 0.14)),   Vector((0.0, avg_y, max_z))),
    ]
    for bname, h_pos, t_pos in specs:
        if bname in arm_obj.data.edit_bones:
            arm_obj.data.edit_bones.remove(arm_obj.data.edit_bones[bname])
        nb = arm_obj.data.edit_bones.new(bname)
        nb.parent = eb_head
        nb.use_connect = False
        nb.head = h_pos
        nb.tail = t_pos

    bpy.ops.object.mode_set(mode="OBJECT")

    # Cria vertex groups e pinta pesos suaves (transferindo da Head para Ear_L / Ear_R / Antenna_Top)
    vg_ear_l = mesh_obj.vertex_groups.get("Ear_L") or mesh_obj.vertex_groups.new(name="Ear_L")
    vg_ear_r = mesh_obj.vertex_groups.get("Ear_R") or mesh_obj.vertex_groups.new(name="Ear_R")
    vg_ant   = mesh_obj.vertex_groups.get("Antenna_Top") or mesh_obj.vertex_groups.new(name="Antenna_Top")

    ear_thresh_l = max_x * 0.40
    ear_thresh_r = min_x * 0.40
    ant_thresh_z = max_z - 0.14

    counts = {"Ear_L": 0, "Ear_R": 0, "Antenna_Top": 0}
    for v in verts:
        # Apenas vertices pertencentes a cabeca
        hw = 0.0
        for g in v.groups:
            if g.group == head_vg.index:
                hw = g.weight
                break
        if hw < 0.15:
            continue

        if v.co.x > ear_thresh_l and v.co.z > (min_z + 0.05):
            t = min(1.0, max(0.0, (v.co.x - ear_thresh_l) / max(1e-4, max_x - ear_thresh_l)))
            w = 0.85 * (t * t)
            if w > 0.02:
                vg_ear_l.add([v.index], w * hw, "REPLACE")
                head_vg.add([v.index], max(0.05, (1.0 - w) * hw), "REPLACE")
                counts["Ear_L"] += 1
        elif v.co.x < ear_thresh_r and v.co.z > (min_z + 0.05):
            t = min(1.0, max(0.0, (ear_thresh_r - v.co.x) / max(1e-4, abs(min_x - ear_thresh_r))))
            w = 0.85 * (t * t)
            if w > 0.02:
                vg_ear_r.add([v.index], w * hw, "REPLACE")
                head_vg.add([v.index], max(0.05, (1.0 - w) * hw), "REPLACE")
                counts["Ear_R"] += 1
        elif v.co.z > ant_thresh_z and abs(v.co.x) < max_x * 0.35:
            t = min(1.0, max(0.0, (v.co.z - ant_thresh_z) / max(1e-4, max_z - ant_thresh_z)))
            w = 0.90 * t
            if w > 0.02:
                vg_ant.add([v.index], w * hw, "REPLACE")
                head_vg.add([v.index], max(0.05, (1.0 - w) * hw), "REPLACE")
                counts["Antenna_Top"] += 1

    return counts


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--input", required=True)
    ap.add_argument("--output", required=True)
    ap.add_argument("--add-secondary-head", action="store_true", help="Adiciona Ear_L, Ear_R e Antenna_Top para SpringBone")
    args = ap.parse_args()

    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=args.input)

    arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if not arms:
        raise RuntimeError("Nenhuma Armature encontrada no FBX de entrada.")
    arm = arms[0]

    renamed = 0
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in arm.data.edit_bones:
        if eb.name in UNIRIG_BIPED_MAP:
            eb.name = UNIRIG_BIPED_MAP[eb.name]
            renamed += 1
    bpy.ops.object.mode_set(mode="OBJECT")

    sec_info = {}
    if args.add_secondary_head:
        meshes = [o for o in bpy.data.objects if o.type == "MESH"]
        if meshes:
            sec_info = add_secondary_head_bones(arm, meshes[0])

    bpy.ops.export_scene.fbx(
        filepath=args.output,
        use_selection=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="AUTO",
    )
    print(f"[unirig_to_mecanim] OK: bones_renamed={renamed} | secondary={sec_info} | out={args.output}")


if __name__ == "__main__":
    main()
