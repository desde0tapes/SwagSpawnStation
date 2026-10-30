using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using Reptile;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using System;
using System.Collections;

namespace SwagSpawnStation
{
    [BepInPlugin("com.desdezerotapes.swagspawnstation", "Swag Spawn Station", "1.3.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;

        private string _baseBundlesFolder;
        private Harmony _harmony;

        // Enums
        private enum EffectCategory
        {
            ObjectSpawner,
            Static,
            Static2,
            Phone,
            Manual,
            BoostTrick,
            MoveStyle,
            MoveStyleBoost
        }

        // Data
        private Dictionary<GameObject, EffectCategory> _prefabCategoryMap = new Dictionary<GameObject, EffectCategory>();
        private List<GameObject> _activePuppets = new List<GameObject>();
        private Dictionary<GameObject, EffectCategory> _puppetCategoryMap = new Dictionary<GameObject, EffectCategory>();
        private Dictionary<GameObject, bool> _puppetStateCache = new Dictionary<GameObject, bool>();

        private Dictionary<Transform, Transform> _boneMap = new Dictionary<Transform, Transform>();
        private Dictionary<Transform, EffectCategory> _boneCategoryMap = new Dictionary<Transform, EffectCategory>();

        // Camera tracking and compatibility
        private List<Transform> _mainCameraFollowers = new List<Transform>();
        private Transform _lastVisualRoot;

        private class TransformData
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public TransformData(Vector3 p, Quaternion r) { Position = p; Rotation = r; }
        }
        private Dictionary<GameObject, TransformData> _stationaryPuppets = new Dictionary<GameObject, TransformData>();

        // Reflection
        private FieldInfo _characterVisualField;
        private FieldInfo _phoneActiveField;

        // Boost trick reflection
        private FieldInfo _abilityField;
        private FieldInfo _boostingField;
        private MethodInfo _checkBoostTrickMethod;
        private FieldInfo _curTrickBoostField;
        private FieldInfo _grindTrickTimerField;

        // Movestyle reflection
        private FieldInfo _moveStyleField;

        // Configs
        public ConfigEntry<bool> IsEnabled;
        public ConfigEntry<string> EffectsBundlesLocation;
        public ConfigEntry<bool> BlockLockedCombo;
        public ConfigEntry<bool> BlockShieldBashKnockback;

        public ConfigEntry<bool> ObjectSpawnerEnabled;
        public ConfigEntry<string> ObjectSpawnerFolderName;
        public ConfigEntry<float> ObjectSpawnerRotX, ObjectSpawnerRotY, ObjectSpawnerRotZ;
        public ConfigEntry<float> ObjectSpawnerPosX, ObjectSpawnerPosY, ObjectSpawnerPosZ;
        public ConfigEntry<bool> ObjectSpawnerUseBoneRot;

        public ConfigEntry<bool> StaticEnabled;
        public ConfigEntry<string> StaticFolderName;
        public ConfigEntry<float> StaticRotX, StaticRotY, StaticRotZ;
        public ConfigEntry<float> StaticPosX, StaticPosY, StaticPosZ;
        public ConfigEntry<bool> StaticUseBoneRot;
        public ConfigEntry<float> StaticLag;

        public ConfigEntry<bool> Static2Enabled;
        public ConfigEntry<string> Static2FolderName;
        public ConfigEntry<float> Static2RotX, Static2RotY, Static2RotZ;
        public ConfigEntry<float> Static2PosX, Static2PosY, Static2PosZ;
        public ConfigEntry<bool> Static2UseBoneRot;
        public ConfigEntry<float> Static2Lag;

        public ConfigEntry<bool> PhoneEnabled;
        public ConfigEntry<string> PhoneFolderName;
        public ConfigEntry<float> PhoneRotX, PhoneRotY, PhoneRotZ;
        public ConfigEntry<float> PhonePosX, PhonePosY, PhonePosZ;
        public ConfigEntry<bool> PhoneUseBoneRot;
        public ConfigEntry<float> PhoneLag;

        public ConfigEntry<bool> ManualToggleEnabled;
        public ConfigEntry<string> ManualFolderName;
        public ConfigEntry<KeyCode> ManualToggleKey;
        public ConfigEntry<bool> ManualStayEnabled;
        public ConfigEntry<bool> ManualHoldEnabled;
        public ConfigEntry<float> ManualRotX, ManualRotY, ManualRotZ;
        public ConfigEntry<float> ManualPosX, ManualPosY, ManualPosZ;
        public ConfigEntry<bool> ManualUseBoneRot;
        public ConfigEntry<float> ManualLag;

        // Boost Trick Configs
        public ConfigEntry<bool> BoostTrickEnabled;
        public ConfigEntry<string> BoostTrickFolderName;
        public ConfigEntry<float> BoostTrickDelay;
        public ConfigEntry<bool> BoostTrickOverrideByBoosting;
        public ConfigEntry<float> BoostTrickRotX, BoostTrickRotY, BoostTrickRotZ;
        public ConfigEntry<float> BoostTrickPosX, BoostTrickPosY, BoostTrickPosZ;
        public ConfigEntry<bool> BoostTrickUseBoneRot;
        public ConfigEntry<float> BoostTrickLag;

        // MoveStyle Configs
        public ConfigEntry<bool> MoveStyleEnabled;
        public ConfigEntry<string> MoveStyleFolderName;
        public ConfigEntry<float> MoveStyleDelay;
        public ConfigEntry<bool> MoveStyleFreezeParticles;
        public ConfigEntry<float> MoveStyleRotX, MoveStyleRotY, MoveStyleRotZ;
        public ConfigEntry<float> MoveStylePosX, MoveStylePosY, MoveStylePosZ;
        public ConfigEntry<bool> MoveStyleUseBoneRot;
        public ConfigEntry<float> MoveStyleLag;

        // MoveStyle Boost Configs
        public ConfigEntry<bool> MoveStyleBoostEnabled;
        public ConfigEntry<string> MoveStyleBoostFolderName;
        public ConfigEntry<float> MoveStyleBoostDelay;
        public ConfigEntry<bool> MoveStyleBoostFreezeParticles;
        public ConfigEntry<float> MoveStyleBoostRotX, MoveStyleBoostRotY, MoveStyleBoostRotZ;
        public ConfigEntry<float> MoveStyleBoostPosX, MoveStyleBoostPosY, MoveStyleBoostPosZ;
        public ConfigEntry<bool> MoveStyleBoostUseBoneRot;
        public ConfigEntry<float> MoveStyleBoostLag;

        // State tracking
        private string _lastEffectsBundlesLocation = "";

        private string _lastLoadedObjectSpawner = "";
        private string _lastLoadedStatic = "";
        private string _lastLoadedStatic2 = "";
        private string _lastLoadedPhone = "";
        private string _lastLoadedManual = "";
        private string _lastLoadedBoostTrick = "";
        private string _lastLoadedMoveStyle = "";
        private string _lastLoadedMoveStyleBoost = "";

        private bool _lastObjectSpawnerState = false;
        private bool _lastStaticState = false;
        private bool _lastStatic2State = false;
        private bool _lastPhoneState = false;
        private bool _lastManualConfigState = false;
        private bool _lastManualStayEnabledState = false;
        private bool _lastManualHoldEnabledState = false;
        private bool _lastBoostTrickState = false;
        private bool _lastMoveStyleState = false;
        private bool _lastMoveStyleBoostState = false;

        private Vector3 _lastRotObjectSpawner, _lastPosObjectSpawner;
        private Vector3 _lastRotStatic, _lastPosStatic;
        private Vector3 _lastRotStatic2, _lastPosStatic2;
        private Vector3 _lastRotPhone, _lastPosPhone;
        private Vector3 _lastRotManual, _lastPosManual;
        private Vector3 _lastRotBoostTrick, _lastPosBoostTrick;
        private Vector3 _lastRotMoveStyle, _lastPosMoveStyle;
        private Vector3 _lastRotMoveStyleBoost, _lastPosMoveStyleBoost;

        private bool _lastUseBoneRotObjectSpawner, _lastUseBoneRotStatic, _lastUseBoneRotStatic2, _lastUseBoneRotPhone, _lastUseBoneRotManual, _lastUseBoneRotBoostTrick, _lastUseBoneRotMoveStyle, _lastUseBoneRotMoveStyleBoost;
        private float _lastLagStatic, _lastLagStatic2, _lastLagPhone, _lastLagManual, _lastLagBoostTrick, _lastLagMoveStyle, _lastLagMoveStyleBoost;

        private float _lastTrickTime = -100f;
        private float _lastBoostTrickTime = -100f;
        private float _lastRegularBoostTime = -100f;
        private float _lastMoveStyleTime = -100f;
        private float _lastMoveStyleBoostTime = -100f;

        private bool _needsRefresh = false;
        private bool _manualToggleActive = false;
        private bool _forceManualRetrigger = false;

        private bool _moveStyleFrozen = false;
        private bool _moveStyleBoostFrozen = false;

        private void Awake()
        {
            Instance = this;

            // Register camera render callback
            Application.onBeforeRender += OnBeforeRender;

            IsEnabled = Config.Bind("1. General", "Enabled", true, "Master switch for the entire mod.");
            EffectsBundlesLocation = Config.Bind("1. General", "Effects Bundles Path", Path.Combine(Paths.BepInExRootPath, "EffectsBundles"), "Directory where the effects bundles are located.");
            BlockLockedCombo = Config.Bind("1. General", "Block Locked Combo", false, "Prevents teleporting and forced melee animations when hitting props/cops from afar.");
            BlockShieldBashKnockback = Config.Bind("1. General", "Block Shield Bash Knockback", false, "Prevents the shield bash knockback animation when hitting a shield cop (useful for ranged custom hitboxes).");

            ObjectSpawnerEnabled = Config.Bind("2. Object Spawner", "Enable Object Spawner", false, "Load objects that spawn at the player position but remain independent.");
            ObjectSpawnerFolderName = Config.Bind("2. Object Spawner", "Object Spawner Folder", "ObjectSpawner", "Folder for Object Spawner assets.");
            ObjectSpawnerRotX = Config.Bind("2. Object Spawner", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            ObjectSpawnerRotY = Config.Bind("2. Object Spawner", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            ObjectSpawnerRotZ = Config.Bind("2. Object Spawner", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            ObjectSpawnerPosX = Config.Bind("2. Object Spawner", "Position X", 0f, "X axis Offset");
            ObjectSpawnerPosY = Config.Bind("2. Object Spawner", "Position Y", 0f, "Y axis Offset");
            ObjectSpawnerPosZ = Config.Bind("2. Object Spawner", "Position Z", 0f, "Z axis Offset");
            ObjectSpawnerUseBoneRot = Config.Bind("2. Object Spawner", "Use Bone Rotation", true, "If True, object aligns with bone. If False, aligns with Player Root.");

            StaticEnabled = Config.Bind("3. Static Effects", "Enable Static", false, "Load always-on effects.");
            StaticFolderName = Config.Bind("3. Static Effects", "Static Folder", "StaticEffects", "Static Effect Folder name.");
            StaticRotX = Config.Bind("3. Static Effects", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            StaticRotY = Config.Bind("3. Static Effects", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            StaticRotZ = Config.Bind("3. Static Effects", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            StaticPosX = Config.Bind("3. Static Effects", "Position X", 0f, "X axis Offset");
            StaticPosY = Config.Bind("3. Static Effects", "Position Y", 0f, "Y axis Offset");
            StaticPosZ = Config.Bind("3. Static Effects", "Position Z", 0f, "Z axis Offset");
            StaticUseBoneRot = Config.Bind("3. Static Effects", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            StaticLag = Config.Bind("3. Static Effects", "Position Lag", 0f, "Dragging smooth delayed position effect. 0 = Instant.");

            Static2Enabled = Config.Bind("4. Static Effects #2", "Enable Static 2", false, "Load a second set of always-on effects.");
            Static2FolderName = Config.Bind("4. Static Effects #2", "Static 2 Folder", "StaticEffects2", "Static Effect #2 Folder name.");
            Static2RotX = Config.Bind("4. Static Effects #2", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            Static2RotY = Config.Bind("4. Static Effects #2", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            Static2RotZ = Config.Bind("4. Static Effects #2", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            Static2PosX = Config.Bind("4. Static Effects #2", "Position X", 0f, "X axis Offset");
            Static2PosY = Config.Bind("4. Static Effects #2", "Position Y", 0f, "Y axis Offset");
            Static2PosZ = Config.Bind("4. Static Effects #2", "Position Z", 0f, "Z axis Offset");
            Static2UseBoneRot = Config.Bind("4. Static Effects #2", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            Static2Lag = Config.Bind("4. Static Effects #2", "Position Lag", 0f, "Dragging smooth delayed position effect. 0 = Instant.");

            PhoneEnabled = Config.Bind("5. Phone", "Enable Phone", false, "Load effects when the player has the phone out.");
            PhoneFolderName = Config.Bind("5. Phone", "Phone Folder", "PhoneEffects", "Folder for Phone assets.");
            PhoneRotX = Config.Bind("5. Phone", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            PhoneRotY = Config.Bind("5. Phone", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            PhoneRotZ = Config.Bind("5. Phone", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            PhonePosX = Config.Bind("5. Phone", "Position X", 0f, "X axis Offset");
            PhonePosY = Config.Bind("5. Phone", "Position Y", 0f, "Y axis Offset");
            PhonePosZ = Config.Bind("5. Phone", "Position Z", 0f, "Z axis Offset");
            PhoneUseBoneRot = Config.Bind("5. Phone", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            PhoneLag = Config.Bind("5. Phone", "Position Lag", 0f, "Dragging smooth delayed position effect. 0 = Instant.");

            ManualToggleEnabled = Config.Bind("6. Manual Toggle", "Enable Key Toggle", false, "Allow toggling effects with a key.");
            ManualFolderName = Config.Bind("6. Manual Toggle", "Manual Folder", "ManualEffects", "Folder for Manual Toggle effects.");
            ManualToggleKey = Config.Bind("6. Manual Toggle", "Toggle Key", KeyCode.Keypad5, "Key to toggle Manual effects.");
            ManualStayEnabled = Config.Bind("6. Manual Toggle", "Toggles Stay Enabled", false, "If true, hitting the key reloads/retriggers the effect instead of turning it off, maintaining it on.");
            ManualHoldEnabled = Config.Bind("6. Manual Toggle", "Hold Mode Enabled", false, "If true, the effect is only active while the key is held down (overrides standard toggle).");
            ManualRotX = Config.Bind("6. Manual Toggle", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            ManualRotY = Config.Bind("6. Manual Toggle", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            ManualRotZ = Config.Bind("6. Manual Toggle", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            ManualPosX = Config.Bind("6. Manual Toggle", "Position X", 0f, "X axis Offset");
            ManualPosY = Config.Bind("6. Manual Toggle", "Position Y", 0f, "Y axis Offset");
            ManualPosZ = Config.Bind("6. Manual Toggle", "Position Z", 0f, "Z axis Offset");
            ManualUseBoneRot = Config.Bind("6. Manual Toggle", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            ManualLag = Config.Bind("6. Manual Toggle", "Position Lag", 0f, "Dragging smooth delayed position effect. 0 = Instant.");

            // Boost Trick Configs
            BoostTrickEnabled = Config.Bind("7. Boost Trick Glow", "Enable BoostTrickGlow", false, "Load effects/hitboxes when doing a Boost Trick.");
            BoostTrickFolderName = Config.Bind("7. Boost Trick Glow", "BoostTrick Folder", "BoostTrickGlow", "Boost Trick Folder name.");
            BoostTrickDelay = Config.Bind("7. Boost Trick Glow", "BoostTrick Delay", 1.5f, "Grace period for Boost Tricks.");
            BoostTrickOverrideByBoosting = Config.Bind("7. Boost Trick Glow", "Overriden by Boosting", false, "If true, Boost Trick effects disappear permanently if you boost.");
            BoostTrickRotX = Config.Bind("7. Boost Trick Glow", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            BoostTrickRotY = Config.Bind("7. Boost Trick Glow", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            BoostTrickRotZ = Config.Bind("7. Boost Trick Glow", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            BoostTrickPosX = Config.Bind("7. Boost Trick Glow", "Position X", 0f, "X axis Offset");
            BoostTrickPosY = Config.Bind("7. Boost Trick Glow", "Position Y", 0f, "Y axis Offset");
            BoostTrickPosZ = Config.Bind("7. Boost Trick Glow", "Position Z", 0f, "Z axis Offset");
            BoostTrickUseBoneRot = Config.Bind("7. Boost Trick Glow", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            BoostTrickLag = Config.Bind("7. Boost Trick Glow", "Position Lag", 0f, "Dragging smooth delayed position effect. 0 = Instant.");

            // MoveStyle Configs
            MoveStyleEnabled = Config.Bind("8. MoveStyle Effects", "Enable MoveStyle", false, "Load effects when player is using a MoveStyle (not on foot).");
            MoveStyleFolderName = Config.Bind("8. MoveStyle Effects", "MoveStyle Folder", "MoveStyleEffects", "Folder for MoveStyle assets.");
            MoveStyleDelay = Config.Bind("8. MoveStyle Effects", "MoveStyle Delay", 0f, "Grace period: how long effects stay after using MoveStyle.");
            MoveStyleFreezeParticles = Config.Bind("8. MoveStyle Effects", "Freeze Particles on Unequip", true, "Halts bone updates when unequipped during the delay period to allow particles to naturally dissipate.");
            MoveStyleRotX = Config.Bind("8. MoveStyle Effects", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            MoveStyleRotY = Config.Bind("8. MoveStyle Effects", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            MoveStyleRotZ = Config.Bind("8. MoveStyle Effects", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            MoveStylePosX = Config.Bind("8. MoveStyle Effects", "Position X", 0f, "X axis Offset");
            MoveStylePosY = Config.Bind("8. MoveStyle Effects", "Position Y", 0f, "Y axis Offset");
            MoveStylePosZ = Config.Bind("8. MoveStyle Effects", "Position Z", 0f, "Z axis Offset");
            MoveStyleUseBoneRot = Config.Bind("8. MoveStyle Effects", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            MoveStyleLag = Config.Bind("8. MoveStyle Effects", "Position Lag", 0f, "Dragging smooth delayed position effect. 0 = Instant. Higher = More Delay.");

            // MoveStyle Boost Configs
            MoveStyleBoostEnabled = Config.Bind("9. MoveStyle Boost Effects", "Enable MoveStyle Boost", false, "Load effects when player is boosting while using a MoveStyle.");
            MoveStyleBoostFolderName = Config.Bind("9. MoveStyle Boost Effects", "MoveStyle Boost Folder", "MoveStyleBoostEffects", "Folder for MoveStyle Boost assets.");
            MoveStyleBoostDelay = Config.Bind("9. MoveStyle Boost Effects", "MoveStyle Boost Delay", 0f, "Grace period: how long effects stay after boost ends.");
            MoveStyleBoostFreezeParticles = Config.Bind("9. MoveStyle Boost Effects", "Freeze Particles on Unequip", true, "Halts bone updates when boost ends during the delay period.");
            MoveStyleBoostRotX = Config.Bind("9. MoveStyle Boost Effects", "Rotation X", 0f, "Adjust X axis Rotation Pitch.");
            MoveStyleBoostRotY = Config.Bind("9. MoveStyle Boost Effects", "Rotation Y", 0f, "Adjust Y axis Rotation Pitch.");
            MoveStyleBoostRotZ = Config.Bind("9. MoveStyle Boost Effects", "Rotation Z", 0f, "Adjust Z axis Rotation Pitch.");
            MoveStyleBoostPosX = Config.Bind("9. MoveStyle Boost Effects", "Position X", 0f, "X axis Offset");
            MoveStyleBoostPosY = Config.Bind("9. MoveStyle Boost Effects", "Position Y", 0f, "Y axis Offset");
            MoveStyleBoostPosZ = Config.Bind("9. MoveStyle Boost Effects", "Position Z", 0f, "Z axis Offset");
            MoveStyleBoostUseBoneRot = Config.Bind("9. MoveStyle Boost Effects", "Use Bone Rotation", true, "True=Attach to bone, False=Stay Upright.");
            MoveStyleBoostLag = Config.Bind("9. MoveStyle Boost Effects", "Position Lag", 0f, "Dragging smooth delayed position effect. 0 = Instant. Higher = More Delay.");

            IsEnabled.SettingChanged += (sender, args) => { if (IsEnabled.Value) _needsRefresh = true; };

            _baseBundlesFolder = EffectsBundlesLocation.Value;
            if (!Directory.Exists(_baseBundlesFolder)) Directory.CreateDirectory(_baseBundlesFolder);

            string[] subfolders = {
                ObjectSpawnerFolderName.Value,
                StaticFolderName.Value,
                Static2FolderName.Value,
                PhoneFolderName.Value,
                ManualFolderName.Value,
                BoostTrickFolderName.Value,
                MoveStyleFolderName.Value,
                MoveStyleBoostFolderName.Value
            };

            foreach (string folder in subfolders)
            {
                if (!string.IsNullOrEmpty(folder))
                {
                    string newSub = Path.Combine(_baseBundlesFolder, folder.Trim());
                    if (!Directory.Exists(newSub)) Directory.CreateDirectory(newSub);
                }
            }

            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            _characterVisualField = typeof(Player).GetField("characterVisual", flags);
            _phoneActiveField = typeof(CharacterVisual).GetField("phoneActive", flags);

            // Boost trick reflection
            _abilityField = typeof(Player).GetField("ability", flags);
            _boostingField = typeof(Player).GetField("boosting", flags);
            _checkBoostTrickMethod = typeof(Player).GetMethod("CheckBoostTrick", flags);
            _curTrickBoostField = typeof(GrindAbility).GetField("curTrickBoost", flags);
            _grindTrickTimerField = typeof(GrindAbility).GetField("trickTimer", flags);

            // Movestyle reflection
            _moveStyleField = typeof(Player).GetField("moveStyle", flags);

            try
            {
                _harmony = new Harmony("com.desdezerotapes.swagspawnstation");
                _harmony.PatchAll();
            }
            catch (Exception ex)
            {
                Logger.LogError($"[SwagSpawnStation] Harmony patch failed. Core visuals will continue. Error: {ex.Message}");
            }

            RefreshEffects();
            _needsRefresh = true;
            Logger.LogInfo("Swag Spawn Station 1.3.0 Initialized with Dynamic Teleporters, Full Compatibility Protocol, Boost Tricks, MoveStyle, MoveStyle Boosts, and AmbientTriggers.");
        }

        private void Update()
        {
            if (ManualToggleEnabled.Value)
            {
                if (ManualHoldEnabled.Value)
                {
                    _manualToggleActive = Input.GetKey(ManualToggleKey.Value);
                }
                else if (Input.GetKeyDown(ManualToggleKey.Value))
                {
                    if (ManualStayEnabled.Value)
                    {
                        _manualToggleActive = true;
                        _forceManualRetrigger = true;
                    }
                    else
                    {
                        _manualToggleActive = !_manualToggleActive;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(HitBounceAbility))]
        public static class HitBounceTriggerPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(HitBounceAbility.Trigger), new Type[] { typeof(Vector3), typeof(BoxCollider), typeof(HitBounceAbility.BounceType) })]
            [HarmonyPatch(nameof(HitBounceAbility.Trigger), new Type[] { typeof(GameObject), typeof(Collider), typeof(Vector3), typeof(HitBounceAbility.BounceType) })]
            [HarmonyPatch(nameof(HitBounceAbility.Trigger), new Type[] { typeof(Vector3), typeof(float), typeof(Vector3), typeof(HitBounceAbility.BounceType) })]
            public static bool PrefixTrigger(HitBounceAbility __instance, ref bool __result)
            {
                if (Plugin.Instance != null && Plugin.Instance.BlockLockedCombo != null && Plugin.Instance.BlockLockedCombo.Value)
                {
                    try { __result = true; }
                    catch (Exception ex)
                    {
                        Plugin.Instance.Logger.LogError($"[SwagSpawnStation] Error executing hitbounce block: {ex.Message}");
                        __result = true;
                    }
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Player))]
        public static class PlayerAbilityPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(Player.ActivateAbility))]
            public static bool Prefix(Player __instance, Ability __0)
            {
                if (Plugin.Instance != null && __0 != null)
                {
                    string typeName = __0.GetType().Name;
                    if (Plugin.Instance.BlockLockedCombo != null && Plugin.Instance.BlockLockedCombo.Value)
                    {
                        if (typeName.Contains("SprayEnemiesAbility")) return false;
                    }

                    if (Plugin.Instance.BlockShieldBashKnockback != null && Plugin.Instance.BlockShieldBashKnockback.Value)
                    {
                        if (__0 is KnockbackAbility kbAbility && kbAbility.knockbackType == KnockbackAbility.KnockbackType.SHIELDBASH)
                        {
                            Plugin.Instance.Logger.LogInfo("[SwagSpawnStation] Shield Bash Knockback blocked at ActivateAbility.");
                            return false;
                        }
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(KnockbackAbility))]
        public static class KnockbackAbilityStartPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(KnockbackAbility.OnStartAbility))]
            public static bool Prefix(KnockbackAbility __instance)
            {
                if (Plugin.Instance != null && Plugin.Instance.BlockShieldBashKnockback != null && Plugin.Instance.BlockShieldBashKnockback.Value)
                {
                    if (__instance.knockbackType == KnockbackAbility.KnockbackType.SHIELDBASH)
                    {
                        Plugin.Instance.Logger.LogInfo("[SwagSpawnStation] Shield Bash Knockback blocked at OnStartAbility. Force stopping.");
                        __instance.p.StopCurrentAbility();
                        return false;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "SetCharacter")]
        public static class PlayerSetCharacterPatch
        {
            [HarmonyPostfix]
            public static void Postfix(Player __instance)
            {
                if (Traverse.Create(__instance).Field("isAI").GetValue<bool>()) return;
                Instance._needsRefresh = true;
                Instance.Logger.LogInfo("[PA] Character Changed. Refreshing...");
            }
        }

        private void RefreshEffects()
        {
            _baseBundlesFolder = EffectsBundlesLocation.Value;
            _prefabCategoryMap.Clear();

            if (ObjectSpawnerEnabled.Value) LoadBundlesIntoCategory(ObjectSpawnerFolderName.Value, EffectCategory.ObjectSpawner);
            if (StaticEnabled.Value) LoadBundlesIntoCategory(StaticFolderName.Value, EffectCategory.Static);
            if (Static2Enabled.Value) LoadBundlesIntoCategory(Static2FolderName.Value, EffectCategory.Static2);
            if (PhoneEnabled.Value) LoadBundlesIntoCategory(PhoneFolderName.Value, EffectCategory.Phone);
            if (ManualToggleEnabled.Value) LoadBundlesIntoCategory(ManualFolderName.Value, EffectCategory.Manual);
            if (BoostTrickEnabled.Value) LoadBundlesIntoCategory(BoostTrickFolderName.Value, EffectCategory.BoostTrick);
            if (MoveStyleEnabled.Value) LoadBundlesIntoCategory(MoveStyleFolderName.Value, EffectCategory.MoveStyle);
            if (MoveStyleBoostEnabled.Value) LoadBundlesIntoCategory(MoveStyleBoostFolderName.Value, EffectCategory.MoveStyleBoost);

            _lastEffectsBundlesLocation = EffectsBundlesLocation.Value;
            _lastLoadedObjectSpawner = ObjectSpawnerFolderName.Value;
            _lastLoadedStatic = StaticFolderName.Value;
            _lastLoadedStatic2 = Static2FolderName.Value;
            _lastLoadedPhone = PhoneFolderName.Value;
            _lastLoadedManual = ManualFolderName.Value;
            _lastLoadedBoostTrick = BoostTrickFolderName.Value;
            _lastLoadedMoveStyle = MoveStyleFolderName.Value;
            _lastLoadedMoveStyleBoost = MoveStyleBoostFolderName.Value;

            _lastObjectSpawnerState = ObjectSpawnerEnabled.Value;
            _lastStaticState = StaticEnabled.Value;
            _lastStatic2State = Static2Enabled.Value;
            _lastPhoneState = PhoneEnabled.Value;
            _lastManualConfigState = ManualToggleEnabled.Value;
            _lastManualStayEnabledState = ManualStayEnabled.Value;
            _lastManualHoldEnabledState = ManualHoldEnabled.Value;
            _lastBoostTrickState = BoostTrickEnabled.Value;
            _lastMoveStyleState = MoveStyleEnabled.Value;
            _lastMoveStyleBoostState = MoveStyleBoostEnabled.Value;

            _lastRotObjectSpawner = new Vector3(ObjectSpawnerRotX.Value, ObjectSpawnerRotY.Value, ObjectSpawnerRotZ.Value);
            _lastPosObjectSpawner = new Vector3(ObjectSpawnerPosX.Value, ObjectSpawnerPosY.Value, ObjectSpawnerPosZ.Value);
            _lastUseBoneRotObjectSpawner = ObjectSpawnerUseBoneRot.Value;

            _lastRotStatic = new Vector3(StaticRotX.Value, StaticRotY.Value, StaticRotZ.Value);
            _lastPosStatic = new Vector3(StaticPosX.Value, StaticPosY.Value, StaticPosZ.Value);
            _lastUseBoneRotStatic = StaticUseBoneRot.Value;
            _lastLagStatic = StaticLag.Value;

            _lastRotStatic2 = new Vector3(Static2RotX.Value, Static2RotY.Value, Static2RotZ.Value);
            _lastPosStatic2 = new Vector3(Static2PosX.Value, Static2PosY.Value, Static2PosZ.Value);
            _lastUseBoneRotStatic2 = Static2UseBoneRot.Value;
            _lastLagStatic2 = Static2Lag.Value;

            _lastRotPhone = new Vector3(PhoneRotX.Value, PhoneRotY.Value, PhoneRotZ.Value);
            _lastPosPhone = new Vector3(PhonePosX.Value, PhonePosY.Value, PhonePosZ.Value);
            _lastUseBoneRotPhone = PhoneUseBoneRot.Value;
            _lastLagPhone = PhoneLag.Value;

            _lastRotManual = new Vector3(ManualRotX.Value, ManualRotY.Value, ManualRotZ.Value);
            _lastPosManual = new Vector3(ManualPosX.Value, ManualPosY.Value, ManualPosZ.Value);
            _lastUseBoneRotManual = ManualUseBoneRot.Value;
            _lastLagManual = ManualLag.Value;

            _lastRotBoostTrick = new Vector3(BoostTrickRotX.Value, BoostTrickRotY.Value, BoostTrickRotZ.Value);
            _lastPosBoostTrick = new Vector3(BoostTrickPosX.Value, BoostTrickPosY.Value, BoostTrickPosZ.Value);
            _lastUseBoneRotBoostTrick = BoostTrickUseBoneRot.Value;
            _lastLagBoostTrick = BoostTrickLag.Value;

            _lastRotMoveStyle = new Vector3(MoveStyleRotX.Value, MoveStyleRotY.Value, MoveStyleRotZ.Value);
            _lastPosMoveStyle = new Vector3(MoveStylePosX.Value, MoveStylePosY.Value, MoveStylePosZ.Value);
            _lastUseBoneRotMoveStyle = MoveStyleUseBoneRot.Value;
            _lastLagMoveStyle = MoveStyleLag.Value;

            _lastRotMoveStyleBoost = new Vector3(MoveStyleBoostRotX.Value, MoveStyleBoostRotY.Value, MoveStyleBoostRotZ.Value);
            _lastPosMoveStyleBoost = new Vector3(MoveStyleBoostPosX.Value, MoveStyleBoostPosY.Value, MoveStyleBoostPosZ.Value);
            _lastUseBoneRotMoveStyleBoost = MoveStyleBoostUseBoneRot.Value;
            _lastLagMoveStyleBoost = MoveStyleBoostLag.Value;
        }

        private void LoadBundlesIntoCategory(string subFolder, EffectCategory category)
        {
            string targetPath = _baseBundlesFolder;
            if (!string.IsNullOrEmpty(subFolder))
            {
                string combined = Path.Combine(_baseBundlesFolder, subFolder.Trim());
                if (Directory.Exists(combined)) targetPath = combined;
                else return;
            }

            if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);
            if (Directory.Exists(targetPath))
            {
                foreach (var file in Directory.GetFiles(targetPath, "*.cbb", SearchOption.TopDirectoryOnly))
                {
                    AssetBundle bundle = AssetBundle.LoadFromFile(file);
                    if (bundle == null) continue;

                    var assets = bundle.LoadAllAssets<GameObject>();
                    foreach (var go in assets)
                    {
                        if (!_prefabCategoryMap.ContainsKey(go))
                        {
                            _prefabCategoryMap.Add(go, category);
                        }
                    }
                    bundle.Unload(false);
                }
            }
        }

        private void OnBeforeRender()
        {
            if (Instance == null || !IsEnabled.Value || _mainCameraFollowers.Count == 0 || _lastVisualRoot == null) return;

            // Update camera followers without interpolation
            UpdateCameraFollowers(_lastVisualRoot, false);
        }

        private void LateUpdate()
        {
            var player = WorldHandler.instance?.GetCurrentPlayer();
            if (player == null) return;

            object visualObj = _characterVisualField?.GetValue(player);
            if (visualObj == null) return;
            Transform visualRoot = ExtractTransform(visualObj);
            if (visualRoot == null) return;

            // Crewboom and stage load compatibility
            if (_lastVisualRoot != visualRoot)
            {
                _lastVisualRoot = visualRoot;
                _needsRefresh = true;
                Logger.LogInfo("[SwagSpawnStation] Visual root mismatch detected (Late CrewBoom / Scene transition). Forcing refresh.");
            }

            // Check destroyed puppets
            foreach (var puppet in _activePuppets)
            {
                if (puppet == null)
                {
                    _needsRefresh = true;
                    Logger.LogInfo("[SwagSpawnStation] Orphaned/Destroyed puppet detected. Forcing refresh.");
                    break;
                }
            }

            bool folderChanged =
                EffectsBundlesLocation.Value != _lastEffectsBundlesLocation ||
                ObjectSpawnerFolderName.Value != _lastLoadedObjectSpawner ||
                StaticFolderName.Value != _lastLoadedStatic ||
                Static2FolderName.Value != _lastLoadedStatic2 ||
                PhoneFolderName.Value != _lastLoadedPhone ||
                ManualFolderName.Value != _lastLoadedManual ||
                BoostTrickFolderName.Value != _lastLoadedBoostTrick ||
                MoveStyleFolderName.Value != _lastLoadedMoveStyle ||
                MoveStyleBoostFolderName.Value != _lastLoadedMoveStyleBoost;

            bool stateChanged =
                ObjectSpawnerEnabled.Value != _lastObjectSpawnerState ||
                StaticEnabled.Value != _lastStaticState ||
                Static2Enabled.Value != _lastStatic2State ||
                PhoneEnabled.Value != _lastPhoneState ||
                ManualToggleEnabled.Value != _lastManualConfigState ||
                ManualStayEnabled.Value != _lastManualStayEnabledState ||
                ManualHoldEnabled.Value != _lastManualHoldEnabledState ||
                BoostTrickEnabled.Value != _lastBoostTrickState ||
                MoveStyleEnabled.Value != _lastMoveStyleState ||
                MoveStyleBoostEnabled.Value != _lastMoveStyleBoostState;

            bool objectSpawnerChanged =
                _lastRotObjectSpawner != new Vector3(ObjectSpawnerRotX.Value, ObjectSpawnerRotY.Value, ObjectSpawnerRotZ.Value) ||
                _lastPosObjectSpawner != new Vector3(ObjectSpawnerPosX.Value, ObjectSpawnerPosY.Value, ObjectSpawnerPosZ.Value) ||
                _lastUseBoneRotObjectSpawner != ObjectSpawnerUseBoneRot.Value;

            bool staticChanged =
                _lastRotStatic != new Vector3(StaticRotX.Value, StaticRotY.Value, StaticRotZ.Value) ||
                _lastPosStatic != new Vector3(StaticPosX.Value, StaticPosY.Value, StaticPosZ.Value) ||
                _lastUseBoneRotStatic != StaticUseBoneRot.Value ||
                _lastLagStatic != StaticLag.Value;

            bool static2Changed =
                _lastRotStatic2 != new Vector3(Static2RotX.Value, Static2RotY.Value, Static2RotZ.Value) ||
                _lastPosStatic2 != new Vector3(Static2PosX.Value, Static2PosY.Value, Static2PosZ.Value) ||
                _lastUseBoneRotStatic2 != Static2UseBoneRot.Value ||
                _lastLagStatic2 != Static2Lag.Value;

            bool phoneChanged =
                _lastRotPhone != new Vector3(PhoneRotX.Value, PhoneRotY.Value, PhoneRotZ.Value) ||
                _lastPosPhone != new Vector3(PhonePosX.Value, PhonePosY.Value, PhonePosZ.Value) ||
                _lastUseBoneRotPhone != PhoneUseBoneRot.Value ||
                _lastLagPhone != PhoneLag.Value;

            bool manualChanged =
                _lastRotManual != new Vector3(ManualRotX.Value, ManualRotY.Value, ManualRotZ.Value) ||
                _lastPosManual != new Vector3(ManualPosX.Value, ManualPosY.Value, ManualPosZ.Value) ||
                _lastUseBoneRotManual != ManualUseBoneRot.Value ||
                _lastLagManual != ManualLag.Value;

            bool boostTrickChanged =
                _lastRotBoostTrick != new Vector3(BoostTrickRotX.Value, BoostTrickRotY.Value, BoostTrickRotZ.Value) ||
                _lastPosBoostTrick != new Vector3(BoostTrickPosX.Value, BoostTrickPosY.Value, BoostTrickPosZ.Value) ||
                _lastUseBoneRotBoostTrick != BoostTrickUseBoneRot.Value ||
                _lastLagBoostTrick != BoostTrickLag.Value;

            bool moveStyleChanged =
                _lastRotMoveStyle != new Vector3(MoveStyleRotX.Value, MoveStyleRotY.Value, MoveStyleRotZ.Value) ||
                _lastPosMoveStyle != new Vector3(MoveStylePosX.Value, MoveStylePosY.Value, MoveStylePosZ.Value) ||
                _lastUseBoneRotMoveStyle != MoveStyleUseBoneRot.Value ||
                _lastLagMoveStyle != MoveStyleLag.Value;

            bool moveStyleBoostChanged =
                _lastRotMoveStyleBoost != new Vector3(MoveStyleBoostRotX.Value, MoveStyleBoostRotY.Value, MoveStyleBoostRotZ.Value) ||
                _lastPosMoveStyleBoost != new Vector3(MoveStyleBoostPosX.Value, MoveStyleBoostPosY.Value, MoveStyleBoostPosZ.Value) ||
                _lastUseBoneRotMoveStyleBoost != MoveStyleBoostUseBoneRot.Value ||
                _lastLagMoveStyleBoost != MoveStyleBoostLag.Value;

            bool transformChanged = objectSpawnerChanged ||
                staticChanged || static2Changed || phoneChanged || manualChanged || boostTrickChanged || moveStyleChanged || moveStyleBoostChanged;

            if (IsEnabled.Value && (folderChanged || stateChanged || transformChanged || _needsRefresh))
            {
                bool wasRefreshRequested = _needsRefresh;
                _needsRefresh = false;

                if (folderChanged || stateChanged || wasRefreshRequested)
                {
                    DestroyPuppets();
                    RefreshEffects();
                    SpawnPuppets(visualRoot);
                }
                else if (transformChanged)
                {
                    _lastRotObjectSpawner = new Vector3(ObjectSpawnerRotX.Value, ObjectSpawnerRotY.Value, ObjectSpawnerRotZ.Value);
                    _lastPosObjectSpawner = new Vector3(ObjectSpawnerPosX.Value, ObjectSpawnerPosY.Value, ObjectSpawnerPosZ.Value);
                    _lastUseBoneRotObjectSpawner = ObjectSpawnerUseBoneRot.Value;

                    _lastRotStatic = new Vector3(StaticRotX.Value, StaticRotY.Value, StaticRotZ.Value);
                    _lastPosStatic = new Vector3(StaticPosX.Value, StaticPosY.Value, StaticPosZ.Value);
                    _lastUseBoneRotStatic = StaticUseBoneRot.Value;
                    _lastLagStatic = StaticLag.Value;

                    _lastRotStatic2 = new Vector3(Static2RotX.Value, Static2RotY.Value, Static2RotZ.Value);
                    _lastPosStatic2 = new Vector3(Static2PosX.Value, Static2PosY.Value, Static2PosZ.Value);
                    _lastUseBoneRotStatic2 = Static2UseBoneRot.Value;
                    _lastLagStatic2 = Static2Lag.Value;

                    _lastRotPhone = new Vector3(PhoneRotX.Value, PhoneRotY.Value, PhoneRotZ.Value);
                    _lastPosPhone = new Vector3(PhonePosX.Value, PhonePosY.Value, PhonePosZ.Value);
                    _lastUseBoneRotPhone = PhoneUseBoneRot.Value;
                    _lastLagPhone = PhoneLag.Value;

                    _lastRotManual = new Vector3(ManualRotX.Value, ManualRotY.Value, ManualRotZ.Value);
                    _lastPosManual = new Vector3(ManualPosX.Value, ManualPosY.Value, ManualPosZ.Value);
                    _lastUseBoneRotManual = ManualUseBoneRot.Value;
                    _lastLagManual = ManualLag.Value;

                    _lastRotBoostTrick = new Vector3(BoostTrickRotX.Value, BoostTrickRotY.Value, BoostTrickRotZ.Value);
                    _lastPosBoostTrick = new Vector3(BoostTrickPosX.Value, BoostTrickPosY.Value, BoostTrickPosZ.Value);
                    _lastUseBoneRotBoostTrick = BoostTrickUseBoneRot.Value;
                    _lastLagBoostTrick = BoostTrickLag.Value;

                    _lastRotMoveStyle = new Vector3(MoveStyleRotX.Value, MoveStyleRotY.Value, MoveStyleRotZ.Value);
                    _lastPosMoveStyle = new Vector3(MoveStylePosX.Value, MoveStylePosY.Value, MoveStylePosZ.Value);
                    _lastUseBoneRotMoveStyle = MoveStyleUseBoneRot.Value;
                    _lastLagMoveStyle = MoveStyleLag.Value;

                    _lastRotMoveStyleBoost = new Vector3(MoveStyleBoostRotX.Value, MoveStyleBoostRotY.Value, MoveStyleBoostRotZ.Value);
                    _lastPosMoveStyleBoost = new Vector3(MoveStyleBoostPosX.Value, MoveStyleBoostPosY.Value, MoveStyleBoostPosZ.Value);
                    _lastUseBoneRotMoveStyleBoost = MoveStyleBoostUseBoneRot.Value;
                    _lastLagMoveStyleBoost = MoveStyleBoostLag.Value;
                }
            }
            else if (!IsEnabled.Value && _activePuppets.Count > 0)
            {
                DestroyPuppets();
            }

            UpdatePuppetLogic(player, visualRoot, visualObj);
        }

        private void SpawnPuppets(Transform playerVisual)
        {
            _boneMap.Clear();
            _boneCategoryMap.Clear();
            _puppetStateCache.Clear();
            _puppetCategoryMap.Clear();
            _stationaryPuppets.Clear();
            _mainCameraFollowers.Clear();

            bool syncRefreshNeeded = false;
            Logger.LogInfo("--- Spawning Puppets ---");

            foreach (var kvp in _prefabCategoryMap)
            {
                GameObject prefab = kvp.Key;
                EffectCategory category = kvp.Value;

                if (prefab == null) continue;

                GameObject puppet = Instantiate(prefab);
                puppet.name = $"{prefab.name}_{category}_StationPuppet";
                puppet.transform.position = playerVisual.position;
                puppet.transform.rotation = playerVisual.rotation;
                puppet.transform.localScale = Vector3.one;

                InjectGrinds(puppet);
                InjectAmbientTriggers(puppet);
                TeleporterSetup.SetupTeleportersInHierarchy(puppet.transform);

                var allChildren = puppet.GetComponentsInChildren<Transform>(true).ToList();
                bool hasAuthKey = false;
                List<Transform> tvTransforms = new List<Transform>();

                foreach (var child in allChildren)
                {
                    if (child.name.Equals("AuthKey", StringComparison.OrdinalIgnoreCase)) hasAuthKey = true;
                    if (child.name.Contains("TV")) tvTransforms.Add(child);
                }

                if (!hasAuthKey)
                {
                    DestroyImmediate(puppet);
                    continue;
                }

                _activePuppets.Add(puppet);
                _puppetCategoryMap[puppet] = category;
                _puppetStateCache[puppet] = false;

                int hitboxCount = 0;

                foreach (var col in puppet.GetComponentsInChildren<Collider>(true))
                {
                    if (col.name.IndexOf("Hitbox", StringComparison.OrdinalIgnoreCase) >= 0 || col.gameObject.layer == 18)
                    {
                        col.gameObject.layer = 18;
                        col.isTrigger = true;
                        hitboxCount++;
                    }
                }

                if (category == EffectCategory.ObjectSpawner)
                {
                    MapBonesRecursive(puppet.transform, playerVisual, oneShot: true, category);
                }
                else
                {
                    MapBonesRecursive(puppet.transform, playerVisual, oneShot: false, category);
                }

                if (hitboxCount > 0)
                {
                    puppet.transform.SetParent(playerVisual, false);
                    Logger.LogInfo($"[Hitbox Bridge] Registered {hitboxCount} custom hitboxes on '{prefab.name}'. Parented to Player.");
                    if (category == EffectCategory.ObjectSpawner)
                    {
                        _stationaryPuppets[puppet] = new TransformData(puppet.transform.position, puppet.transform.rotation);
                    }
                }

                if (tvTransforms.Count > 0)
                {
                    foreach (var tvTransform in tvTransforms)
                    {
                        Logger.LogInfo($"[SyncVideo Bridge] Found TV prop on '{prefab.name}'. Renaming exactly to 'TV' and injecting scripts...");
                        tvTransform.name = "TV";
                        tvTransform.gameObject.layer = 21;
                        syncRefreshNeeded = true;

                        if (tvTransform.GetComponent<MeshRenderer>() == null)
                        {
                            Logger.LogWarning("[SyncVideo Bridge] WARNING: 'TV' object does not have a MeshRenderer!");
                        }

                        try
                        {
                            Type junkType = Type.GetType("Reptile.Junk, Assembly-CSharp");
                            if (junkType != null && tvTransform.gameObject.GetComponent(junkType) == null)
                            {
                                tvTransform.gameObject.AddComponent(junkType);
                            }

                            Type stageObjectType = Type.GetType("MapStation.Common.StageObject, MapStation.Common");
                            if (stageObjectType != null && tvTransform.gameObject.GetComponent(stageObjectType) == null)
                            {
                                tvTransform.gameObject.AddComponent(stageObjectType);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"[SyncVideo Bridge] Failed to inject components via reflection: {ex.Message}");
                        }
                    }
                }
            }

            if (syncRefreshNeeded)
            {
                TryForceSyncVideoRefresh();
            }
        }

        private void InjectGrinds(GameObject root)
        {
            Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                if (t.name.StartsWith("GrindPath", StringComparison.OrdinalIgnoreCase) || t.name.StartsWith("Grind", StringComparison.OrdinalIgnoreCase))
                {
                    Transform nodesContainer = t.Find("Nodes");
                    Transform linesContainer = t.Find("Line") ?? t.Find("Lines");

                    if (nodesContainer == null || linesContainer == null) continue;

                    var grindPath = t.gameObject.GetComponent<GrindPath>();
                    if (grindPath == null) grindPath = t.gameObject.AddComponent<GrindPath>();

                    try
                    {
                        grindPath.sfxCollection = SfxCollectionID.GrindMetalSfx;
                    }
                    catch { }

                    Dictionary<int, GrindNode> nodeDict = new Dictionary<int, GrindNode>();
                    foreach (Transform nodeTransform in nodesContainer)
                    {
                        var node = nodeTransform.gameObject.AddComponent<GrindNode>();
                        node.PathReference = grindPath;
                        node.grindLines = new List<GrindLine>();

                        string name = nodeTransform.name.Replace(" ", "");
                        string numPart = new string(name.TakeWhile(char.IsDigit).ToArray());
                        if (int.TryParse(numPart, out int index))
                        {
                            nodeDict[index] = node;
                        }

                        if (name.IndexOf("Retour", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            node.retour = true;
                        }
                    }

                    foreach (Transform lineTransform in linesContainer)
                    {
                        string name = lineTransform.name.Replace(" ", "");
                        string numPart = new string(name.TakeWhile(char.IsDigit).ToArray());

                        if (int.TryParse(numPart, out int index))
                        {
                            var line = lineTransform.gameObject.AddComponent<GrindLine>();
                            line.PathReference = grindPath;
                            try { line.sfxCollection = SfxCollectionID.GrindMetalSfx; } catch { }
                            line.cornerBoost = true;
                            line.upwardsGrindJump = true;

                            line.nodes = new GrindNode[2];
                            lineTransform.gameObject.layer = 11;
                            if (nodeDict.TryGetValue(index, out GrindNode n0) && nodeDict.TryGetValue(index + 1, out GrindNode n1))
                            {
                                line.nodes[0] = n0;
                                line.nodes[1] = n1;
                                n0.grindLines.Add(line);
                                n1.grindLines.Add(line);

                                var col = lineTransform.gameObject.GetComponent<CapsuleCollider>();
                                if (col == null) col = lineTransform.gameObject.AddComponent<CapsuleCollider>();

                                float radius = 0.84f;
                                col.radius = radius;
                                col.direction = 2;

                                Vector3 dirVector = n1.transform.position - n0.transform.position;
                                float dist = dirVector.magnitude;
                                lineTransform.position = n0.transform.position + (dirVector.normalized * (dist * 0.5f));
                                if (dist > 0.001f)
                                {
                                    lineTransform.rotation = Quaternion.LookRotation(dirVector.normalized);
                                }

                                col.height = dist + (radius * 2f);
                                col.center = Vector3.zero;
                                col.isTrigger = true;
                            }

                            foreach (Transform child in lineTransform)
                            {
                                child.gameObject.layer = 12;
                            }
                        }
                    }
                }
            }
        }

        private void InjectAmbientTriggers(GameObject root)
        {
            Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                if (t.name.StartsWith("AmbientTrigger", StringComparison.OrdinalIgnoreCase))
                {
                    t.gameObject.layer = 19;

                    Collider col = t.GetComponent<Collider>();
                    if (col == null)
                    {
                        col = t.gameObject.AddComponent<BoxCollider>();
                    }
                    col.isTrigger = true;

                    AmbientTrigger at = t.gameObject.GetComponent<AmbientTrigger>();
                    if (at == null)
                    {
                        at = t.gameObject.AddComponent<AmbientTrigger>();
                    }

                    Color lightColor = Color.white;
                    Color shadowColor = Color.black;

                    string hexPart = t.name.Substring("AmbientTrigger".Length).Trim();
                    hexPart = hexPart.Split(' ')[0].Split('(')[0];

                    if (!string.IsNullOrEmpty(hexPart) && hexPart.Contains(","))
                    {
                        string[] hexes = hexPart.Split(',');
                        if (hexes.Length >= 2)
                        {
                            string hexLight = hexes[0].Trim();
                            string hexShadow = hexes[1].Trim();

                            if (!hexLight.StartsWith("#")) hexLight = "#" + hexLight;
                            if (!hexShadow.StartsWith("#")) hexShadow = "#" + hexShadow;

                            if (ColorUtility.TryParseHtmlString(hexLight, out Color parsedLight))
                                lightColor = parsedLight;

                            if (ColorUtility.TryParseHtmlString(hexShadow, out Color parsedShadow))
                                shadowColor = parsedShadow;
                        }
                    }

                    at.AmbientEnvLight = lightColor;
                    at.AmbientEnvShadow = shadowColor;
                    at.transitionDuration = 3f;
                    at.revertTransitionDuration = 3f;

                    Logger.LogInfo($"[AmbientTrigger Bridge] Injected AmbientTrigger on {t.name}. Light: {lightColor}, Shadow: {shadowColor}");
                }
            }
        }

        private void TryForceSyncVideoRefresh()
        {
            try
            {
                Type syncPluginType = Type.GetType("SyncVideo.SyncVideoPlugin, SyncVideo");
                if (syncPluginType != null)
                {
                    PropertyInfo screenManagerProp = syncPluginType.GetProperty("ScreenManager", BindingFlags.Public | BindingFlags.Static);
                    if (screenManagerProp != null)
                    {
                        object screenManager = screenManagerProp.GetValue(null);
                        if (screenManager != null)
                        {
                            MethodInfo[] methods = screenManager.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            MethodInfo targetMethod = null;

                            foreach (var m in methods)
                            {
                                if (m.GetParameters().Length == 0 && m.DeclaringType == screenManager.GetType())
                                {
                                    if (m.Name.IndexOf("Refresh", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        m.Name.IndexOf("Attach", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        m.Name.IndexOf("Find", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        m.Name.IndexOf("Scan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        m.Name.IndexOf("Rebind", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        targetMethod = m;
                                    }
                                }
                            }

                            if (targetMethod != null)
                            {
                                targetMethod.Invoke(screenManager, null);
                                Logger.LogInfo($"[SyncVideo Bridge] Successfully triggered SyncVideo via {targetMethod.Name}()!");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[SyncVideo Bridge] Reflection failure: {ex.Message}");
            }
        }

        private Transform GetActiveCameraTransform()
        {
            if (Camera.main != null && Camera.main.gameObject.activeInHierarchy)
            {
                return Camera.main.transform;
            }

            var allCams = Camera.allCameras;
            for (int i = 0; i < allCams.Length; i++)
            {
                var cam = allCams[i];
                if (cam.isActiveAndEnabled && cam.targetTexture == null)
                {
                    return cam.transform;
                }
            }
            return null;
        }

        private void MapBonesRecursive(Transform puppetPart, Transform playerPart, bool oneShot, EffectCategory category)
        {
            if (puppetPart == null) return;

            // NPC bypass check
            if (puppetPart.name.EndsWith("_NPC", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (puppetPart.name.Equals("MainCamera", StringComparison.OrdinalIgnoreCase))
            {
                GetCategorySettings(category, out Vector3 cPosOffset, out Quaternion cRotOffset, out bool cUseBoneRot, out float cLag);
                if (oneShot)
                {
                    Transform cam = GetActiveCameraTransform();
                    if (cam != null)
                    {
                        if (cUseBoneRot)
                        {
                            puppetPart.position = cam.position + (cam.rotation * cPosOffset);
                            puppetPart.rotation = cam.rotation * cRotOffset;
                        }
                        else
                        {
                            puppetPart.position = cam.position + (Quaternion.Euler(0, playerPart.root.eulerAngles.y, 0) * cPosOffset);
                            puppetPart.rotation = Quaternion.Euler(0, playerPart.root.eulerAngles.y, 0) * cRotOffset;
                        }
                    }
                }
                else
                {
                    _mainCameraFollowers.Add(puppetPart);
                    _boneCategoryMap[puppetPart] = category;
                }

                foreach (Transform child in puppetPart) MapBonesRecursive(child, playerPart, oneShot, category);
                return;
            }

            Transform match = FindBone(playerPart, puppetPart.name);
            if (match != null)
            {
                GetCategorySettings(category, out Vector3 posOffset, out Quaternion rotOffset, out bool useBoneRot, out float lag);
                if (oneShot)
                {
                    if (useBoneRot)
                    {
                        puppetPart.position = match.position + (match.rotation * posOffset);
                        puppetPart.rotation = match.rotation * rotOffset;
                    }
                    else
                    {
                        puppetPart.position = match.position + (Quaternion.Euler(0, playerPart.root.eulerAngles.y, 0) * posOffset);
                        puppetPart.rotation = Quaternion.Euler(0, playerPart.root.eulerAngles.y, 0) * rotOffset;
                    }
                }
                else
                {
                    _boneMap[puppetPart] = match;
                    _boneCategoryMap[puppetPart] = category;
                }
            }

            foreach (Transform child in puppetPart) MapBonesRecursive(child, playerPart, oneShot, category);
        }

        private void GetCategorySettings(EffectCategory category, out Vector3 posOffset, out Quaternion rotOffset, out bool useBoneRot, out float lag)
        {
            switch (category)
            {
                case EffectCategory.ObjectSpawner:
                    posOffset = new Vector3(ObjectSpawnerPosX.Value, ObjectSpawnerPosY.Value, ObjectSpawnerPosZ.Value);
                    rotOffset = Quaternion.Euler(ObjectSpawnerRotX.Value, ObjectSpawnerRotY.Value, ObjectSpawnerRotZ.Value);
                    useBoneRot = ObjectSpawnerUseBoneRot.Value;
                    lag = 0f;
                    break;
                case EffectCategory.Static:
                    posOffset = new Vector3(StaticPosX.Value, StaticPosY.Value, StaticPosZ.Value);
                    rotOffset = Quaternion.Euler(StaticRotX.Value, StaticRotY.Value, StaticRotZ.Value);
                    useBoneRot = StaticUseBoneRot.Value;
                    lag = StaticLag.Value;
                    break;
                case EffectCategory.Static2:
                    posOffset = new Vector3(Static2PosX.Value, Static2PosY.Value, Static2PosZ.Value);
                    rotOffset = Quaternion.Euler(Static2RotX.Value, Static2RotY.Value, Static2RotZ.Value);
                    useBoneRot = Static2UseBoneRot.Value;
                    lag = Static2Lag.Value;
                    break;
                case EffectCategory.Phone:
                    posOffset = new Vector3(PhonePosX.Value, PhonePosY.Value, PhonePosZ.Value);
                    rotOffset = Quaternion.Euler(PhoneRotX.Value, PhoneRotY.Value, PhoneRotZ.Value);
                    useBoneRot = PhoneUseBoneRot.Value;
                    lag = PhoneLag.Value;
                    break;
                case EffectCategory.Manual:
                    posOffset = new Vector3(ManualPosX.Value, ManualPosY.Value, ManualPosZ.Value);
                    rotOffset = Quaternion.Euler(ManualRotX.Value, ManualRotY.Value, ManualRotZ.Value);
                    useBoneRot = ManualUseBoneRot.Value;
                    lag = ManualLag.Value;
                    break;
                case EffectCategory.BoostTrick:
                    posOffset = new Vector3(BoostTrickPosX.Value, BoostTrickPosY.Value, BoostTrickPosZ.Value);
                    rotOffset = Quaternion.Euler(BoostTrickRotX.Value, BoostTrickRotY.Value, BoostTrickRotZ.Value);
                    useBoneRot = BoostTrickUseBoneRot.Value;
                    lag = BoostTrickLag.Value;
                    break;
                case EffectCategory.MoveStyle:
                    posOffset = new Vector3(MoveStylePosX.Value, MoveStylePosY.Value, MoveStylePosZ.Value);
                    rotOffset = Quaternion.Euler(MoveStyleRotX.Value, MoveStyleRotY.Value, MoveStyleRotZ.Value);
                    useBoneRot = MoveStyleUseBoneRot.Value;
                    lag = MoveStyleLag.Value;
                    break;
                case EffectCategory.MoveStyleBoost:
                    posOffset = new Vector3(MoveStyleBoostPosX.Value, MoveStyleBoostPosY.Value, MoveStyleBoostPosZ.Value);
                    rotOffset = Quaternion.Euler(MoveStyleBoostRotX.Value, MoveStyleBoostRotY.Value, MoveStyleBoostRotZ.Value);
                    useBoneRot = MoveStyleBoostUseBoneRot.Value;
                    lag = MoveStyleBoostLag.Value;
                    break;
                default:
                    posOffset = Vector3.zero;
                    rotOffset = Quaternion.identity;
                    useBoneRot = true;
                    lag = 0f;
                    break;
            }
        }

        private void UpdatePuppetLogic(Player player, Transform playerVisual, object visualObj)
        {
            if (_activePuppets.Count == 0) return;

            bool isPhoneActive = false;
            if (_phoneActiveField != null && visualObj != null)
            {
                try { isPhoneActive = (bool)_phoneActiveField.GetValue(visualObj); } catch { }
            }

            // Boost trick detection
            bool currentlyBoosting = false;
            if (_boostingField != null)
            {
                try { currentlyBoosting = (bool)_boostingField.GetValue(player); } catch { }
            }

            object currentAbility = _abilityField?.GetValue(player);
            bool currentlyTricking = CheckIfTricking(player);
            if (currentlyTricking) _lastTrickTime = Time.time;

            bool currentlyBoostTricking = false;
            if (_checkBoostTrickMethod != null)
            {
                try { currentlyBoostTricking = (bool)_checkBoostTrickMethod.Invoke(player, null); } catch { }
            }

            // False positive check
            if (!currentlyTricking)
            {
                currentlyBoostTricking = false;
            }

            if (currentAbility != null)
            {
                string abilityName = currentAbility.GetType().Name;
                if (abilityName == "BoostAbility")
                {
                    currentlyBoostTricking = false;
                }
                else if (abilityName == "GrindAbility" || currentAbility is GrindAbility)
                {
                    currentlyBoostTricking = false;
                }
            }

            if (!currentlyBoostTricking && currentAbility != null && currentlyBoosting)
            {
                if (currentAbility.GetType().Name == "GrindAbility" || currentAbility is GrindAbility)
                {
                    try
                    {
                        bool isGrindBoostTrick = (bool)_curTrickBoostField.GetValue(currentAbility);
                        float gTime = (float)_grindTrickTimerField.GetValue(currentAbility);
                        if (isGrindBoostTrick && gTime > 0f) currentlyBoostTricking = true;
                    }
                    catch { }
                }
            }

            if (currentlyBoostTricking)
            {
                _lastBoostTrickTime = Time.time;
                _lastRegularBoostTime = -100f;
            }

            bool isRegularBoosting = currentlyBoosting && !currentlyBoostTricking;
            if (isRegularBoosting)
            {
                _lastRegularBoostTime = Time.time;
            }

            // Movestyle detection
            MoveStyle currentMoveStyle = MoveStyle.ON_FOOT;
            if (_moveStyleField != null)
            {
                try { currentMoveStyle = (MoveStyle)_moveStyleField.GetValue(player); } catch { }
            }

            bool isMoveStyleActive = currentMoveStyle != MoveStyle.ON_FOOT;
            if (isMoveStyleActive) _lastMoveStyleTime = Time.time;
            bool finalMoveStyleActive = (Time.time - _lastMoveStyleTime <= MoveStyleDelay.Value);

            _moveStyleFrozen = finalMoveStyleActive && !isMoveStyleActive && MoveStyleFreezeParticles.Value;

            // Movestyle boost detection
            bool isMoveStyleBoostActive = isMoveStyleActive && currentlyBoosting;
            if (isMoveStyleBoostActive) _lastMoveStyleBoostTime = Time.time;
            bool finalMoveStyleBoostActive = (Time.time - _lastMoveStyleBoostTime <= MoveStyleBoostDelay.Value);

            _moveStyleBoostFrozen = finalMoveStyleBoostActive && !isMoveStyleBoostActive && MoveStyleBoostFreezeParticles.Value;

            // Boost trick state
            float timeSinceBoostTrick = Time.time - _lastBoostTrickTime;
            bool boostTrickInTimeWindow = (timeSinceBoostTrick <= BoostTrickDelay.Value);
            bool boostTrickVisualReady = (timeSinceBoostTrick >= 0.2f);
            bool boostTrickActive = boostTrickInTimeWindow && boostTrickVisualReady;

            // Boost override check
            if (BoostTrickOverrideByBoosting.Value)
            {
                float timeSinceRegularBoost = Time.time - _lastRegularBoostTime;
                bool blockedByRegularBoost = (timeSinceRegularBoost <= BoostTrickDelay.Value);

                if (blockedByRegularBoost && !currentlyBoostTricking)
                {
                    boostTrickActive = false;
                    _lastBoostTrickTime = -100f;
                }
            }

            foreach (var puppet in _activePuppets)
            {
                if (puppet == null) continue;
                EffectCategory cat = _puppetCategoryMap[puppet];
                bool shouldBeActive = false;

                switch (cat)
                {
                    case EffectCategory.ObjectSpawner:
                    case EffectCategory.Static:
                    case EffectCategory.Static2:
                        shouldBeActive = true;
                        break;
                    case EffectCategory.Phone:
                        shouldBeActive = isPhoneActive;
                        break;
                    case EffectCategory.BoostTrick:
                        shouldBeActive = boostTrickActive;
                        break;
                    case EffectCategory.MoveStyle:
                        shouldBeActive = finalMoveStyleActive;
                        break;
                    case EffectCategory.MoveStyleBoost:
                        shouldBeActive = finalMoveStyleBoostActive;
                        break;
                    case EffectCategory.Manual:
                        shouldBeActive = _manualToggleActive;
                        if (_forceManualRetrigger)
                        {
                            _puppetStateCache[puppet] = false;
                        }
                        break;
                }

                if (cat != EffectCategory.ObjectSpawner && !_boneMap.ContainsKey(puppet.transform) && !_mainCameraFollowers.Contains(puppet.transform))
                {
                    bool freezeTransform = (cat == EffectCategory.MoveStyle && _moveStyleFrozen) ||
                                           (cat == EffectCategory.MoveStyleBoost && _moveStyleBoostFrozen);

                    if (!freezeTransform)
                    {
                        GetCategorySettings(cat, out Vector3 posOffset, out Quaternion rotOffset, out bool useBoneRot, out float lag);
                        Vector3 targetPos;
                        Quaternion targetRot;

                        if (useBoneRot)
                        {
                            targetPos = playerVisual.position + (playerVisual.rotation * posOffset);
                            targetRot = playerVisual.rotation * rotOffset;
                        }
                        else
                        {
                            targetPos = playerVisual.position + (Quaternion.Euler(0, playerVisual.eulerAngles.y, 0) * posOffset);
                            targetRot = Quaternion.Euler(0, playerVisual.eulerAngles.y, 0) * rotOffset;
                        }

                        if (lag > 0.01f)
                        {
                            float speed = 20f / (1f + lag);
                            puppet.transform.position = Vector3.Lerp(puppet.transform.position, targetPos, Time.deltaTime * speed);
                            puppet.transform.rotation = Quaternion.Slerp(puppet.transform.rotation, targetRot, Time.deltaTime * speed);
                        }
                        else
                        {
                            puppet.transform.position = targetPos;
                            puppet.transform.rotation = targetRot;
                        }
                    }
                }

                UpdateEffectState(puppet, shouldBeActive);
            }

            if (_forceManualRetrigger) _forceManualRetrigger = false;

            // Standard bone updates
            foreach (var pair in _boneMap)
            {
                if (pair.Key == null || pair.Value == null) continue;

                if (_boneCategoryMap.TryGetValue(pair.Key, out EffectCategory cat))
                {
                    bool freezeTransform = (cat == EffectCategory.MoveStyle && _moveStyleFrozen) ||
                                           (cat == EffectCategory.MoveStyleBoost && _moveStyleBoostFrozen);

                    if (freezeTransform) continue;

                    GetCategorySettings(cat, out Vector3 posOffset, out Quaternion rotOffset, out bool useBoneRot, out float lag);
                    Vector3 targetPos;
                    Quaternion targetRot;

                    if (useBoneRot)
                    {
                        targetPos = pair.Value.position + (pair.Value.rotation * posOffset);
                        targetRot = pair.Value.rotation * rotOffset;
                    }
                    else
                    {
                        Quaternion playerFacing = Quaternion.Euler(0, playerVisual.eulerAngles.y, 0);
                        targetPos = pair.Value.position + (playerFacing * posOffset);
                        targetRot = playerFacing * rotOffset;
                    }

                    if (lag > 0.01f)
                    {
                        float speed = 20f / (1f + lag);
                        pair.Key.position = Vector3.Lerp(pair.Key.position, targetPos, Time.deltaTime * speed);
                        pair.Key.rotation = Quaternion.Slerp(pair.Key.rotation, targetRot, Time.deltaTime * speed);
                    }
                    else
                    {
                        pair.Key.position = targetPos;
                        pair.Key.rotation = targetRot;
                    }
                }
                else
                {
                    pair.Key.position = pair.Value.position;
                    pair.Key.rotation = pair.Value.rotation;
                }
            }

            // Camera follower updates
            UpdateCameraFollowers(playerVisual, true);

            foreach (var kvp in _stationaryPuppets)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.transform.position = kvp.Value.Position;
                    kvp.Key.transform.rotation = kvp.Value.Rotation;
                }
            }
        }

        private void UpdateCameraFollowers(Transform playerVisual, bool doLerp)
        {
            Transform activeCam = GetActiveCameraTransform();
            if (activeCam != null)
            {
                foreach (var camFollower in _mainCameraFollowers)
                {
                    if (camFollower == null) continue;

                    if (_boneCategoryMap.TryGetValue(camFollower, out EffectCategory cat))
                    {
                        bool freezeTransform = (cat == EffectCategory.MoveStyle && _moveStyleFrozen) ||
                                               (cat == EffectCategory.MoveStyleBoost && _moveStyleBoostFrozen);

                        if (freezeTransform) continue;

                        GetCategorySettings(cat, out Vector3 posOffset, out Quaternion rotOffset, out bool useBoneRot, out float lag);
                        Vector3 targetPos;
                        Quaternion targetRot;

                        if (useBoneRot)
                        {
                            targetPos = activeCam.position + (activeCam.rotation * posOffset);
                            targetRot = activeCam.rotation * rotOffset;
                        }
                        else
                        {
                            Quaternion playerFacing = Quaternion.Euler(0, playerVisual.eulerAngles.y, 0);
                            targetPos = activeCam.position + (playerFacing * posOffset);
                            targetRot = playerFacing * rotOffset;
                        }

                        if (lag > 0.01f)
                        {
                            if (doLerp)
                            {
                                float speed = 20f / (1f + lag);
                                camFollower.position = Vector3.Lerp(camFollower.position, targetPos, Time.deltaTime * speed);
                                camFollower.rotation = Quaternion.Slerp(camFollower.rotation, targetRot, Time.deltaTime * speed);
                            }
                        }
                        else
                        {
                            camFollower.position = targetPos;
                            camFollower.rotation = targetRot;
                        }
                    }
                }
            }
        }

        private void UpdateEffectState(GameObject fx, bool active)
        {
            if (fx == null) return;
            bool wasActive = _puppetStateCache.ContainsKey(fx) ? _puppetStateCache[fx] : false;

            if (active && !wasActive)
            {
                var animators = fx.GetComponentsInChildren<Animator>(true);
                foreach (var anim in animators) { anim.Rebind(); anim.Update(0f); }

                var legacyAnims = fx.GetComponentsInChildren<Animation>(true);
                foreach (var anim in legacyAnims) { anim.Rewind(); anim.Play(); }
            }

            _puppetStateCache[fx] = active;

            var systems = fx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                var main = ps.main;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                var emission = ps.emission;
                if (emission.enabled != active) emission.enabled = active;
                if (active && !wasActive) ps.Play();
            }

            var trails = fx.GetComponentsInChildren<TrailRenderer>(true);
            foreach (var trail in trails) if (trail.emitting != active) trail.emitting = active;

            var sounds = fx.GetComponentsInChildren<AudioSource>(true);
            foreach (var source in sounds)
            {
                if (active)
                {
                    if (!wasActive) source.Play();
                }
                else
                {
                    if (source.isPlaying) source.Stop();
                }
            }

            var meshes = fx.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var m in meshes) if (m.enabled != active) m.enabled = active;

            var skinnedMeshes = fx.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var sm in skinnedMeshes) if (sm.enabled != active) sm.enabled = active;

            var canvases = fx.GetComponentsInChildren<Canvas>(true);
            foreach (var c in canvases) if (c.enabled != active) c.enabled = active;

            var cameras = fx.GetComponentsInChildren<Camera>(true);
            foreach (var cam in cameras) if (cam.enabled != active) cam.enabled = active;

            var colliders = fx.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders) if (col.enabled != active) col.enabled = active;
        }

        private void DestroyPuppets()
        {
            foreach (var p in _activePuppets) if (p != null) Destroy(p);
            _activePuppets.Clear();
            _puppetStateCache.Clear();
            _puppetCategoryMap.Clear();
            _boneMap.Clear();
            _boneCategoryMap.Clear();
            _stationaryPuppets.Clear();
            _mainCameraFollowers.Clear();
        }

        private Transform ExtractTransform(object visualObj)
        {
            try
            {
                return ((GameObject)visualObj.GetType().GetProperty("gameObject").GetValue(visualObj, null)).transform;
            }
            catch
            {
                return null;
            }
        }

        private bool CheckIfTricking(Player p)
        {
            if (p.IsComboing()) return true;
            object abilityObj = _abilityField?.GetValue(p);
            if (abilityObj != null)
            {
                string typeName = abilityObj.GetType().Name;
                return typeName.Contains("Grind") || typeName.Contains("Wallrun") ||
                       typeName.Contains("Slide") || typeName.Contains("Trick");
            }
            return false;
        }

        private Transform FindBone(Transform root, string name)
        {
            string lowerName = root.name.ToLowerInvariant();
            if (lowerName == "hitbox" || lowerName == "hitboxleftleg" || lowerName == "hitboxrightleg" ||
                lowerName == "hitboxupperbody" || lowerName == "airialhitbox" || lowerName == "radialhitbox")
            {
                return null;
            }

            if (lowerName.Contains("puppet")) return null;
            if (root.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return root;

            foreach (Transform child in root)
            {
                Transform found = FindBone(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void OnDisable() => DestroyPuppets();

        private void OnDestroy()
        {
            Application.onBeforeRender -= OnBeforeRender;
            _harmony?.UnpatchSelf();
            DestroyPuppets();
        }
    }

    // Teleporter setup
    public static class TeleporterSetup
    {
        public static void SetupTeleportersInHierarchy(Transform rootTransform)
        {
            List<SwagTeleporter> teleporters = new List<SwagTeleporter>();

            Transform[] allChildren = rootTransform.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allChildren)
            {
                if (t.name.StartsWith("Teleporter", StringComparison.OrdinalIgnoreCase))
                {
                    Transform trigger = null;
                    Transform exitSpawner = null;

                    foreach (Transform child in t.GetComponentsInChildren<Transform>(true))
                    {
                        if (child.name.StartsWith("Trigger", StringComparison.OrdinalIgnoreCase)) trigger = child;
                        if (child.name.StartsWith("ExitSpawner", StringComparison.OrdinalIgnoreCase)) exitSpawner = child;
                    }

                    if (trigger != null && exitSpawner != null)
                    {
                        if (trigger.GetComponent<SwagTeleporter>() != null) continue;

                        trigger.gameObject.layer = 19;
                        Collider col = trigger.GetComponent<Collider>();
                        if (col != null)
                        {
                            col.isTrigger = true;
                        }

                        SwagTeleporter st = trigger.gameObject.AddComponent<SwagTeleporter>();
                        st.exitSpawner = exitSpawner;
                        st.teleporterID = t.name;
                        teleporters.Add(st);
                    }
                }
            }

            foreach (var t1 in teleporters)
            {
                if (t1.targetTeleporter != null) continue;

                string searchBase = t1.teleporterID.Split('_')[0];

                foreach (var t2 in teleporters)
                {
                    if (t1 == t2) continue;
                    string searchBase2 = t2.teleporterID.Split('_')[0];

                    if (searchBase == searchBase2)
                    {
                        t1.targetTeleporter = t2;
                        t2.targetTeleporter = t1;
                        break;
                    }
                }
            }
        }
    }

    // Teleporter component
    public class SwagTeleporter : MonoBehaviour
    {
        public string teleporterID;
        public SwagTeleporter targetTeleporter;
        public Transform exitSpawner;
        public bool isCooldown;

        public void StartTeleport(Reptile.Player player)
        {
            StartCoroutine(CustomTeleportRoutine(player));
        }

        private IEnumerator CustomTeleportRoutine(Reptile.Player player)
        {
            isCooldown = true;
            if (targetTeleporter != null) targetTeleporter.isCooldown = true;

            Core.Instance.GameInput.DisableAllControllerMaps();

            EffectsUI effects = UnityEngine.Object.FindObjectOfType<EffectsUI>();

            float fadeTimeIn = 0.5f;
            float fadeTimeStay = 0.25f;
            float fadeTimeOut = 0.5f;

            if (effects != null)
            {
                MethodInfo fadeMethod = effects.GetType().GetMethod("FadeInAndOutBlack");
                if (fadeMethod != null)
                {
                    ParameterInfo[] parameters = fadeMethod.GetParameters();
                    if (parameters.Length == 4)
                    {
                        object updateTypeNormal = Enum.ToObject(parameters[3].ParameterType, 0);
                        fadeMethod.Invoke(effects, new object[] { fadeTimeIn, fadeTimeStay, fadeTimeOut, updateTypeNormal });
                    }
                    else if (parameters.Length == 3)
                    {
                        fadeMethod.Invoke(effects, new object[] { fadeTimeIn, fadeTimeStay, fadeTimeOut });
                    }
                }
            }

            float timer = 0f;
            while (timer < fadeTimeIn)
            {
                player.motor.velocity = Vector3.zero;
                timer += Time.deltaTime;
                yield return null;
            }

            if (targetTeleporter != null && targetTeleporter.exitSpawner != null)
            {
                ApplyOrientation(player, targetTeleporter.exitSpawner);
            }

            timer = 0f;
            while (timer < (fadeTimeStay + fadeTimeOut))
            {
                player.motor.velocity = Vector3.zero;
                if (targetTeleporter != null && targetTeleporter.exitSpawner != null)
                {
                    ApplyOrientation(player, targetTeleporter.exitSpawner);
                }
                timer += Time.deltaTime;
                yield return null;
            }

            Core.Instance.GameInput.EnableControllerMaps(BaseModule.IN_GAME_INPUT_MAPS);

            yield return new WaitForSeconds(0.5f);
            isCooldown = false;
            if (targetTeleporter != null) targetTeleporter.isCooldown = false;
        }

        private void ApplyOrientation(Reptile.Player player, Transform exitSpawner)
        {
            if (exitSpawner == null || player == null) return;

            Quaternion adjustedExitRotation = exitSpawner.rotation * Quaternion.Euler(0f, 0f, 0f);
            Vector3 exitForward = adjustedExitRotation * Vector3.forward;
            exitForward.y = 0f;
            if (exitForward.sqrMagnitude < 0.0001f)
            {
                exitForward = exitSpawner.forward;
                exitForward.y = 0f;
            }
            if (exitForward.sqrMagnitude < 0.0001f) exitForward = Vector3.forward;
            exitForward.Normalize();

            Quaternion targetRotation = Quaternion.LookRotation(exitForward, Vector3.up);

            player.transform.position = exitSpawner.position;
            player.transform.rotation = targetRotation;

            if (player.motor != null)
            {
                player.motor.RigidbodyMove(exitSpawner.position);
                player.motor.rotation = targetRotation;
                player.motor.velocity = Vector3.zero;
            }

            try
            {
                MethodInfo setRotMethod = player.GetType().GetMethod("SetRotation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(Quaternion) }, null);
                if (setRotMethod != null)
                {
                    setRotMethod.Invoke(player, new object[] { targetRotation });
                }
                else
                {
                    MethodInfo setRotVec = player.GetType().GetMethod("SetRotation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(Vector3) }, null);
                    if (setRotVec != null)
                    {
                        setRotVec.Invoke(player, new object[] { exitForward });
                    }
                }
            }
            catch { }

            try
            {
                MethodInfo setVisRot = player.GetType().GetMethod("SetVisualRot", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(Quaternion) }, null);
                if (setVisRot != null)
                {
                    setVisRot.Invoke(player, new object[] { targetRotation });
                }
            }
            catch { }

            Animator playerAnim = Traverse.Create(player).Field<Animator>("anim").Value;
            if (playerAnim == null)
            {
                playerAnim = player.GetComponentInChildren<Animator>();
            }

            if (playerAnim != null)
            {
                playerAnim.transform.rotation = targetRotation;
                playerAnim.transform.forward = exitForward;
            }
        }
    }

    // Teleporter patch
    [HarmonyPatch(typeof(Reptile.Player))]
    internal class PlayerTeleportMonitor
    {
        [HarmonyPatch("OnTriggerEnter")]
        [HarmonyPrefix]
        public static bool Prefix_OnTriggerEnter(Reptile.Player __instance, Collider other)
        {
            if (Traverse.Create(__instance).Field("isAI").GetValue<bool>()) return true;

            SwagTeleporter customTel = other.GetComponent<SwagTeleporter>();
            if (customTel == null) customTel = other.GetComponentInParent<SwagTeleporter>();

            if (customTel != null && customTel.targetTeleporter != null)
            {
                if (!customTel.isCooldown)
                {
                    customTel.StartTeleport(__instance);
                }

                return false;
            }

            return true;
        }
    }
}