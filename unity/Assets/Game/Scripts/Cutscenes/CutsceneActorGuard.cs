using System.Collections.Generic;
using Studio.Game.Animation;
using UnityEngine;

namespace Studio.Game.Cutscenes
{
    /// <summary>
    /// Helper pontual de QA de Cutscenes:
    ///   - Durante qualquer cutscene, movimento scriptado e coreografado (NUNCA fisico):
    ///     desabilita CharacterControllers do Dez e NPCs e pausa rotinas de patrulha para eliminar 100% travamentos contra props/quinas.
    ///   - Restaura tudo de forma deterministica ao finalizar ou pular a cutscene.
    /// </summary>
    public sealed class CutsceneActorGuard
    {
        private readonly List<CharacterController> _disabledCcs = new List<CharacterController>();
        private readonly List<AlienNpcRoutineController> _pausedRoutines = new List<AlienNpcRoutineController>();
        private bool _active;

        public void BeginCutscene(GameObject dezGo)
        {
            if (_active) return;
            _active = true;
            _disabledCcs.Clear();
            _pausedRoutines.Clear();

            if (dezGo != null)
            {
                var cc = dezGo.GetComponent<CharacterController>();
                if (cc != null && cc.enabled)
                {
                    cc.enabled = false;
                    _disabledCcs.Add(cc);
                }
            }

            var routines = Object.FindObjectsByType<AlienNpcRoutineController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < routines.Length; i++)
            {
                var r = routines[i];
                if (r == null) continue;
                if (r.enabled)
                {
                    r.enabled = false;
                    _pausedRoutines.Add(r);
                }
                var anim = r.GetComponentInChildren<Animator>(true);
                if (anim != null) anim.SetFloat("Speed", 0f);

                var npcCc = r.GetComponent<CharacterController>();
                if (npcCc != null && npcCc.enabled)
                {
                    npcCc.enabled = false;
                    _disabledCcs.Add(npcCc);
                }
            }
        }

        public void EndCutscene()
        {
            if (!_active) return;
            _active = false;

            for (int i = 0; i < _disabledCcs.Count; i++)
            {
                if (_disabledCcs[i] != null)
                    _disabledCcs[i].enabled = true;
            }
            for (int i = 0; i < _pausedRoutines.Count; i++)
            {
                if (_pausedRoutines[i] != null)
                    _pausedRoutines[i].enabled = true;
            }

            _disabledCcs.Clear();
            _pausedRoutines.Clear();
        }
    }
}
