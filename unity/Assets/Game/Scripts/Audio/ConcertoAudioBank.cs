using UnityEngine;
using UnityEngine.Audio;

namespace Studio.Game.Audio
{
    /// <summary>
    /// Banco centralizado de referencias de audio de CONCERTO (carregado via Resources/Audio/ConcertoAudioBank).
    /// Mantem o mix global, variacoes de passos por superficie (min. 3/superficie), UI SFX e ambiencias por zona.
    /// </summary>
    [CreateAssetMenu(fileName = "ConcertoAudioBank", menuName = "Concerto/Audio Bank")]
    public sealed class ConcertoAudioBank : ScriptableObject
    {
        [Header("Global AudioMixer & Buses")]
        public AudioMixer masterMixer;
        public AudioMixerGroup masterGroup;
        public AudioMixerGroup musicGroup;
        public AudioMixerGroup sfxGroup;
        public AudioMixerGroup ambienceGroup;
        public AudioMixerGroup uiGroup;

        [Header("UI SFX (Abrir / Fechar / Confirmar / Negar)")]
        public AudioClip uiOpenClip;
        public AudioClip uiCloseClip;
        public AudioClip uiConfirmClip;
        public AudioClip uiDenyClip;

        [Header("Locomocao — 3 Variacoes por Superficie + Pulo/Pouso")]
        public AudioClip[] footstepFloorClips;
        public AudioClip[] footstepMetalClips;
        public AudioClip[] footstepPuddleClips;
        public AudioClip jumpTakeoffClip;
        public AudioClip landingThudClip;

        [Header("Ambiencia por Area (Crossfade Continuo)")]
        public AudioClip workshopAmbienceClip;
        public AudioClip sectorAmbienceClip;

        [Header("Musica de Gameplay por Area (Com Ducking em Cutscene/Dialogo)")]
        public AudioClip caldeiralMusicClip;
        public AudioClip sectorMusicClip;
    }
}
