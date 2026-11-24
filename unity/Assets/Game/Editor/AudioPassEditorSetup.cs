#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using Studio.Game.Animation;
using Studio.Game.Audio;
using Studio.Game.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace Studio.Game.Editor
{
    public static class AudioPassEditorSetup
    {
        private const string ResourcesAudioDir = "Assets/Resources/Audio";
        private const string MixerAssetPath    = ResourcesAudioDir + "/ConcertoMasterMixer.mixer";
        private const string BankAssetPath     = ResourcesAudioDir + "/ConcertoAudioBank.asset";

        [MenuItem("Concerto/Audio Pass/Build Global AudioMixer, Bank & Wire Scenes")]
        public static void RunFullSetup()
        {
            EnsureDir(ResourcesAudioDir);
            AssetDatabase.Refresh();

            var mixer = CreateOrRebuildAudioMixer();
            if (mixer == null)
                throw new InvalidOperationException("Falha ao criar ConcertoMasterMixer.mixer");

            var bank = CreateOrUpdateAudioBank(mixer);
            if (bank == null)
                throw new InvalidOperationException("Falha ao criar ConcertoAudioBank.asset");

            WireScene("Assets/Game/Scenes/MainMenu.unity", bank);
            WireScene("Assets/Game/Scenes/Caldeiral.unity", bank);
            WireScene("Assets/Game/Scenes/SetorExterno.unity", bank);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AudioPassEditorSetup] Setup global de audio concluido com sucesso!");
        }

        private static void EnsureDir(string assetDir)
        {
            if (!Directory.Exists(assetDir))
                Directory.CreateDirectory(assetDir);
        }

        private static AudioMixer CreateOrRebuildAudioMixer()
        {
            // Usa UnityEditor.Audio.AudioMixerController via reflexao (API nativa do Editor para criar .mixer valido)
            var editorAsm = typeof(UnityEditor.Editor).Assembly;
            var ctrlType = editorAsm.GetType("UnityEditor.Audio.AudioMixerController");
            if (ctrlType == null)
                throw new InvalidOperationException("AudioMixerController nao encontrado no UnityEditor assembly.");

            if (File.Exists(MixerAssetPath))
                AssetDatabase.DeleteAsset(MixerAssetPath);

            var createMethod = ctrlType.GetMethod("CreateMixerControllerAtPath", BindingFlags.Public | BindingFlags.Static);
            if (createMethod == null)
                throw new InvalidOperationException("CreateMixerControllerAtPath nao encontrado.");

            var controller = createMethod.Invoke(null, new object[] { MixerAssetPath }) as AudioMixer;
            if (controller == null)
                throw new InvalidOperationException("CreateMixerControllerAtPath retornou null.");

            var masterProp = ctrlType.GetProperty("masterGroup", BindingFlags.Public | BindingFlags.Instance);
            var masterGroup = masterProp.GetValue(controller);

            var createNewGroup = ctrlType.GetMethod("CreateNewGroup", BindingFlags.Public | BindingFlags.Instance);
            var addChildToParent = ctrlType.GetMethod("AddChildToParent", BindingFlags.Public | BindingFlags.Instance);
            var addExposedParam = ctrlType.GetMethod("AddExposedParameter", BindingFlags.Public | BindingFlags.Instance);

            var musicGroup    = CreateChildGroup(controller, masterGroup, "Music",    createNewGroup, addChildToParent);
            var sfxGroup      = CreateChildGroup(controller, masterGroup, "SFX",      createNewGroup, addChildToParent);
            var ambienceGroup = CreateChildGroup(controller, masterGroup, "Ambience", createNewGroup, addChildToParent);
            var uiGroup       = CreateChildGroup(controller, masterGroup, "UI",       createNewGroup, addChildToParent);

            ExposeGroupVolume(controller, masterGroup,   AudioMixController.ParamMasterVol,   addExposedParam);
            ExposeGroupVolume(controller, musicGroup,    AudioMixController.ParamMusicVol,    addExposedParam);
            ExposeGroupVolume(controller, sfxGroup,      AudioMixController.ParamSfxVol,      addExposedParam);
            ExposeGroupVolume(controller, ambienceGroup, AudioMixController.ParamAmbienceVol, addExposedParam);
            ExposeGroupVolume(controller, uiGroup,       AudioMixController.ParamUiVol,       addExposedParam);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MixerAssetPath, ImportAssetOptions.ForceSynchronousImport);

            var loaded = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
            Debug.Log($"[AudioPassEditorSetup] AudioMixer criado em {MixerAssetPath} com 5 buses expostos.");
            return loaded;
        }

        private static object CreateChildGroup(
            object controller,
            object parentGroup,
            string groupName,
            MethodInfo createNewGroup,
            MethodInfo addChildToParent)
        {
            var group = createNewGroup.Invoke(controller, new object[] { groupName, false });
            addChildToParent.Invoke(controller, new object[] { group, parentGroup });
            return group;
        }

        private static void ExposeGroupVolume(object controller, object group, string exposedName, MethodInfo addExposedParam)
        {
            var groupType = group.GetType();
            var getGuidMethod = groupType.GetMethod("GetGUIDForVolume", BindingFlags.Public | BindingFlags.Instance);
            var volGuid = getGuidMethod.Invoke(group, null);

            var editorAsm = typeof(UnityEditor.Editor).Assembly;
            var paramPathType = editorAsm.GetType("UnityEditor.Audio.AudioMixerGroupController");
            var exposedType = editorAsm.GetType("UnityEditor.Audio.ExposedAudioParameter");
            var paramPath = editorAsm.GetType("UnityEditor.Audio.ParameterPath");
            var audioGroupParamPathType = editorAsm.GetType("UnityEditor.Audio.AudioGroupParameterPath");

            if (audioGroupParamPathType != null && exposedType != null && addExposedParam != null)
            {
                // Construtor: AudioGroupParameterPath(AudioMixerGroupController group, GUID parameter)
                var ctor = audioGroupParamPathType.GetConstructor(new[] { paramPathType, volGuid.GetType() });
                if (ctor != null)
                {
                    var pathObj = ctor.Invoke(new[] { group, volGuid });
                    addExposedParam.Invoke(controller, new[] { pathObj });

                    // Renomeia o parametro exposto cujo guid corresponde a volGuid para exposedName
                    var exposedProp = controller.GetType().GetProperty("exposedParameters", BindingFlags.Public | BindingFlags.Instance);
                    var arr = exposedProp.GetValue(controller) as Array;
                    var guidField = exposedType.GetField("guid", BindingFlags.Public | BindingFlags.Instance);
                    var nameField = exposedType.GetField("name", BindingFlags.Public | BindingFlags.Instance);
                    if (arr != null && guidField != null && nameField != null)
                    {
                        for (int i = 0; i < arr.Length; i++)
                        {
                            var item = arr.GetValue(i);
                            var g = guidField.GetValue(item);
                            if (g != null && g.Equals(volGuid))
                            {
                                nameField.SetValue(item, exposedName);
                                arr.SetValue(item, i);
                                exposedProp.SetValue(controller, arr);
                                break;
                            }
                        }
                    }
                    return;
                }
            }

            Debug.LogWarning($"[AudioPassEditorSetup] Nao foi possivel expor {exposedName} automaticamente.");
        }

        private static ConcertoAudioBank CreateOrUpdateAudioBank(AudioMixer mixer)
        {
            var bank = AssetDatabase.LoadAssetAtPath<ConcertoAudioBank>(BankAssetPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<ConcertoAudioBank>();
                AssetDatabase.CreateAsset(bank, BankAssetPath);
            }

            bank.masterMixer   = mixer;
            bank.masterGroup   = FindGroup(mixer, "Master");
            bank.musicGroup    = FindGroup(mixer, "Music");
            bank.sfxGroup      = FindGroup(mixer, "SFX");
            bank.ambienceGroup = FindGroup(mixer, "Ambience");
            bank.uiGroup       = FindGroup(mixer, "UI");

            bank.uiOpenClip    = LoadClip("Assets/Game/Audio/SFX/UI/ui_open_TEMP.wav");
            bank.uiCloseClip   = LoadClip("Assets/Game/Audio/SFX/UI/ui_close_TEMP.wav");
            bank.uiConfirmClip = LoadClip("Assets/Game/Audio/SFX/UI/ui_confirm_TEMP.wav");
            bank.uiDenyClip    = LoadClip("Assets/Game/Audio/SFX/UI/ui_deny_TEMP.wav");

            bank.footstepFloorClips = new[]
            {
                LoadClip("Assets/Game/Audio/SFX/Movement/footstep_floor_TEMP.wav"),
                LoadClip("Assets/Game/Audio/SFX/Movement/footstep_floor_02_TEMP.wav"),
                LoadClip("Assets/Game/Audio/SFX/Movement/footstep_floor_03_TEMP.wav"),
            };
            bank.footstepMetalClips = new[]
            {
                LoadClip("Assets/Game/Audio/SFX/Movement/footstep_metal_TEMP.wav"),
                LoadClip("Assets/Game/Audio/SFX/Movement/footstep_metal_02_TEMP.wav"),
                LoadClip("Assets/Game/Audio/SFX/Movement/footstep_metal_03_TEMP.wav"),
            };
            bank.footstepPuddleClips = new[]
            {
                LoadClip("Assets/Game/Audio/SFX/Movement/footstep_puddle_TEMP.wav"),
                LoadClip("Assets/Game/Audio/SFX/Movement/footstep_puddle_02_TEMP.wav"),
                LoadClip("Assets/Game/Audio/SFX/Movement/footstep_puddle_03_TEMP.wav"),
            };
            bank.jumpTakeoffClip = LoadClip("Assets/Game/Audio/SFX/Movement/jump_takeoff_TEMP.wav");
            bank.landingThudClip = LoadClip("Assets/Game/Audio/SFX/Movement/landing_thud_TEMP.wav");

            bank.workshopAmbienceClip = LoadClip("Assets/Game/Audio/Ambience/workshop_warm_ambience_TEMP.wav");
            bank.sectorAmbienceClip   = LoadClip("Assets/Game/Audio/SFX/Sector/sector_cold_wind_ambience_TEMP.wav");

            bank.caldeiralMusicClip = LoadClip("Assets/Game/Audio/Music/concerto_bgm/caldeiral_sync_loop.ogg");
            bank.sectorMusicClip    = LoadClip("Assets/Game/Audio/Music/concerto_bgm/concerto_tema_quebrado.ogg");

            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AudioPassEditorSetup] ConcertoAudioBank salvo em {BankAssetPath}.");
            return bank;
        }

        private static AudioMixerGroup FindGroup(AudioMixer mixer, string name)
        {
            var groups = mixer.FindMatchingGroups(name);
            return (groups != null && groups.Length > 0) ? groups[0] : null;
        }

        private static AudioClip LoadClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
                Debug.LogWarning($"[AudioPassEditorSetup] AudioClip nao encontrado: {path}");
            return clip;
        }

        private static void WireScene(string scenePath, ConcertoAudioBank bank)
        {
            if (!File.Exists(scenePath)) return;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. Wire DezLocomotionController footsteps (3 variations/surface) + jump clip
            var locos = UnityEngine.Object.FindObjectsByType<DezLocomotionController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var loco in locos)
            {
                loco.footstepFloorClips  = bank.footstepFloorClips;
                loco.footstepMetalClips  = bank.footstepMetalClips;
                loco.footstepPuddleClips = bank.footstepPuddleClips;
                loco.jumpTakeoffClip     = bank.jumpTakeoffClip;
                if (loco.landingThudClip == null) loco.landingThudClip = bank.landingThudClip;
                EditorUtility.SetDirty(loco);
            }

            // 2. Wire Ambience & UI sliders into OptionsPanelUI (reusing existing SFX slider row as template)
            var panels = UnityEngine.Object.FindObjectsByType<OptionsPanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var panel in panels)
            {
                EnsureSliderRow(panel, ref panel.ambienceSlider, ref panel.ambienceValueText, "AmbienceRow", "AMBIENCIA");
                EnsureSliderRow(panel, ref panel.uiSlider, ref panel.uiValueText, "UiRow", "UI");
                EditorUtility.SetDirty(panel);
            }

            // 3. Route all AudioSources in scene to ConcertoMasterMixer groups
            var sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int routed = 0;
            foreach (var src in sources)
            {
                string n = src.gameObject.name;
                if (ContainsAny(n, "Music", "BGM", "Motif"))
                    src.outputAudioMixerGroup = bank.musicGroup;
                else if (ContainsAny(n, "Ambience", "Wind", "Hum"))
                    src.outputAudioMixerGroup = bank.ambienceGroup;
                else if (ContainsAny(n, "UI", "Menu", "Canvas"))
                    src.outputAudioMixerGroup = bank.uiGroup;
                else
                    src.outputAudioMixerGroup = bank.sfxGroup;
                EditorUtility.SetDirty(src);
                routed++;
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[AudioPassEditorSetup] Cena {scenePath} atualizada (AudioSources roteados: {routed}).");
        }

        private static void EnsureSliderRow(
            OptionsPanelUI panel,
            ref Slider targetSlider,
            ref Text targetValueText,
            string rowName,
            string labelText)
        {
            if (targetSlider != null) return;
            if (panel.sfxSlider == null) return;

            var sfxRow = panel.sfxSlider.transform.parent;
            if (sfxRow == null || sfxRow.parent == null) return;

            var existing = sfxRow.parent.Find(rowName);
            GameObject rowGo;
            if (existing != null)
            {
                rowGo = existing.gameObject;
            }
            else
            {
                rowGo = UnityEngine.Object.Instantiate(sfxRow.gameObject, sfxRow.parent);
                rowGo.name = rowName;
                int sfxIdx = sfxRow.GetSiblingIndex();
                rowGo.transform.SetSiblingIndex(sfxIdx + 1);
            }

            targetSlider = rowGo.GetComponentInChildren<Slider>(true);
            var texts = rowGo.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].text.Contains("%"))
                    targetValueText = texts[i];
                else
                    texts[i].text = labelText;
            }
        }

        private static bool ContainsAny(string text, string a, string b, string c)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif
