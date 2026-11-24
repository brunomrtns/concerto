using UnityEngine;

namespace Studio.Game.Animation
{
    /// <summary>
    /// FIX 4 (P1): Registry estatico leve para referencia do Player (Dez),
    /// eliminando dependencia fragil de GameObject.Find("Player_Dez_Placeholder").
    /// Mantem fallback temporario com Debug.LogWarning caso algum cenario de teste ainda nao tenha registrado.
    /// </summary>
    public static class PlayerRegistry
    {
        public static GameObject CurrentGameObject { get; private set; }
        public static Transform Current => CurrentGameObject != null ? CurrentGameObject.transform : null;

        public static void Register(GameObject playerGo)
        {
            if (playerGo != null)
                CurrentGameObject = playerGo;
        }

        public static void Unregister(GameObject playerGo)
        {
            if (CurrentGameObject == playerGo)
                CurrentGameObject = null;
        }

        public static GameObject Resolve()
        {
            if (CurrentGameObject != null)
                return CurrentGameObject;

            var fallback = GameObject.Find("Player_Dez_Placeholder");
            if (fallback != null)
            {
                Debug.LogWarning("[PlayerRegistry] Fallback GameObject.Find('Player_Dez_Placeholder') acionado — registre o Player no Awake via PlayerRegistry.Register.");
                CurrentGameObject = fallback;
            }
            return fallback;
        }
    }
}
