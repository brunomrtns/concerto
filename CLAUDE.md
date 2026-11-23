# CLAUDE.md — Regras de Governança do Projeto (`game-concerto`)

> Sincronizado com `AGENTS.md` e `3dprinter-setup-ia/CLAUDE.md`.

## REGRAS INVIOLÁVEIS

1. **TODA geração de asset 3D / personagem usa EXCLUSIVAMENTE TripoSG. Se TripoSG não estiver disponível, PARE e pergunte. Proibido fallback para qualquer outro modelo (Meshy, Luma, TripoSR, Hunyuan3D-2mv, Hunyuan3D-2.1, InstantMesh, SF3D, Rodin, etc.).**
2. **Enforcement automático obrigatório:** o hook `tools/hooks/enforce_triposg_only.py` (registrado em `.claude/settings.json` como `PreToolUse` e no git `pre-commit`) bloqueia (`exit 1` / `exit 2`) qualquer tentativa de gerar malha 3D fora do `TripoSG`.
3. **Pintura PBR / Textura:** após a malha 3D sair exclusivamente do `TripoSG`, o fluxo oficial de pintura PBR é o `pipeline/run_asset.py` do `3dprinter-setup-ia` (`Hunyuan3D-Paint` + `TRELLIS.2 PBR` + `pack_glb`).
4. **Decimate sem rasgar UVs:** antes de qualquer `Decimate` em `.glb` com ilhas UV, é obrigatório rodar `remove_doubles` + `normals_make_consistent(inside=False)` para garantir malha 100% fechada/watertight.

## CHECKLIST OBRIGATÓRIO NO FIM DE CADA TASK

Toda resposta de entrega de task **DEVE** incluir ao final esta tabela preenchida:

| Asset Novo / Alterado | Tipo (3D / Rig / Anim / Cena) | Ferramenta de Geração 3D Usada | Status Gate |
| :--- | :--- | :--- | :--- |
| `<nome_do_asset>` | `<tipo>` | `TripoSG` (ou `N/A — sem geração 3D`) | `PASS / FAIL` |
