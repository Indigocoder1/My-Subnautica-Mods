using System.Reflection;
using HarmonyLib;
using QModManager.API.ModLoading;
using Logger = QModManager.Utility.Logger;

using SMLHelper.V2.Json;
using SMLHelper.V2.Options.Attributes;
using SMLHelper.V2.Handlers;
using UnityEngine;
using System.Collections.Generic;
using SMLHelper.V2.Json.Attributes;
using EquivalentExchange.Constructables;
using System;
using System.Collections.ObjectModel;

namespace EquivalentExchange
{
    [QModCore]
    public static class QMod
    {
        internal static Config config { get; } = OptionsPanelHandler.Main.RegisterModOptions<Config>();
        internal static SaveData SaveData { get; } = SaveDataHandler.Main.RegisterSaveDataCache<SaveData>();


        public static int EMCToFCSCreditRate => config.EMCToFCSCreditRate;
        public static int EMCConvertPerClick => config.EMCConvertPerClick;
        public const string FCSConvertName = "FCS Credit Convert";//name of the convert buttons
        public static readonly string FCSConvertDesc = $"Convert EMC into Alterra credits at a {EMCConvertPerClick} to {EMCConvertPerClick * EMCToFCSCreditRate} ratio";//description of convert button
        public static readonly string FCSConvertBackDesc = $"Convert Alterra credits into EMC at a {EMCConvertPerClick * EMCToFCSCreditRate} to {EMCConvertPerClick} ratio";//description of convert back button


        //the two tech types for converting emc to alterra credit and back
        internal static TechType FCSConvertType = TechTypeHandler.AddTechType("FCSConvert", FCSConvertName, FCSConvertDesc);
        internal static TechType FCSConvertBackType = TechTypeHandler.AddTechType("FCSConvertBack", FCSConvertName, FCSConvertBackDesc);
        public static Sprite FCSCreditIconSprite;

        [QModPatch]
        public static void Patch()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var stingers = ($"Nagorogan_{assembly.GetName().Name}");
            Logger.Log(Logger.Level.Info, $"Patching {stingers}");
            Harmony harmony = new Harmony(stingers);
            harmony.PatchAll(assembly);

            ConsoleCommandsHandler.Main.RegisterConsoleCommand("UnlockExchangeType", typeof(QMod), nameof(UnlockExchangeType));
            ConsoleCommandsHandler.Main.RegisterConsoleCommand("lockExchangeType", typeof(QMod), nameof(LockExchangeType));

            ConsoleCommandsHandler.Main.RegisterConsoleCommand("ExchangeUnlockAll", typeof (QMod), nameof(ExchangeUnlockAll));
            ConsoleCommandsHandler.Main.RegisterConsoleCommand("ExchangeLockAll", typeof(QMod), nameof(ExchangeLockAll));

            ConsoleCommandsHandler.Main.RegisterConsoleCommand("AddEMC", typeof(QMod), nameof(AddAmount));

            new ItemResearchStationConstructable().Patch();

            Logger.Log(Logger.Level.Info, "Patched successfully!");
        }
        public static bool TryUnlockTechType(TechType tt, out string reason)
        {
            reason = "Could not Find TechType";

            if (tt == TechType.None)
            {
                return false;
            }

            if (tt == TechType.TimeCapsule)//don't want people to be able to mass spawn time capsules, might have an issue with the time capsule server like before
            {
                return false;
            }
            reason = "";

            if (config.BlackListedTypes.Contains(tt))
            {
                reason = "Type was blacklisted";
                return false;
            }

            if (SaveData.learntTechTypes.Contains(tt))
            {
                reason = "Type was already unlocked";
                return false;
            }

            foreach (string str in config.AutoFilterStrings)
            {
                if (tt.ToString().ToLower().Contains(str.ToLower()))
                {
                    reason = "Type was found in AutoFilterStrings list";
                    return false;
                }
            }

            SaveData.learntTechTypes.Add(tt);
            return true;
        }
        public static bool TryUnlockTechType(TechType tt)
        {
            return TryUnlockTechType(tt, out _);
        }
        public static void ExchangeUnlockAll()
        {
            ErrorMessage.AddMessage("Unlocked all techtypes for exchange");
            foreach(string typeString in Enum.GetNames(typeof(TechType)))
            {
                TryUnlockTechType(GetTechType(typeString));
            }
        }
        public static void ExchangeLockAll()
        {
            ErrorMessage.AddMessage("Locked all techtypes for exchange");
            SaveData.learntTechTypes.Clear();
        }
        public static void UnlockExchangeType(string str)
        {
            var unlocked = TryUnlockTechType(GetTechType(str), out string reason);

            ErrorMessage.AddMessage(unlocked? $"Unlocked {str}" : $"Could not unlock {str} due to: {reason}");
        }
        public static void LockExchangeType(string str)
        {
            TechType type = GetTechType(str);
            if (type == TechType.None) return;
            if (SaveData.learntTechTypes.Contains(type))
                SaveData.learntTechTypes.Remove(type);
            ErrorMessage.AddMessage($"Locked {type}");
        }
        public static TechType GetTechType(string value)
        {
            return GetTechType(value, out _);
        }
        public static TechType GetTechType(string value, out bool isModded)
        {
            isModded = false;

            if (string.IsNullOrEmpty(value))
                return TechType.None;

            // Look for a known TechType
            if (TechTypeExtensions.FromString(value, out TechType tType, true))
                return tType;

            isModded = true;
            //  Not one of the known TechTypes - is it registered with SMLHelper?
            if (TechTypeHandler.TryGetModdedTechType(value, out TechType custom))
                return custom;

            return TechType.None;
        }

        public static void AddAmount(int amount) => SaveData.EMCAvailable += amount;
    }
    [Menu("Equivalent Exchange")]
    public class Config : ConfigFile
    {
        [Keybind("Menu Key 1", Tooltip = "Press both this key and Menu Key 2 at the same time to open the exchange menu")]
        public KeyCode menuKey = KeyCode.K; 
        [Keybind("Menu Key 2", Tooltip = "Press both this key and Menu Key 1 at the same time to open the exchange menu")]
        public KeyCode menuKey2 = KeyCode.J;

        [Toggle("Research Station Messages", Tooltip = "Whether or not the Item Research Station will display messages regarding unlocking/not unlocking items for exchange")]
        public bool researchStationMessages = false;

        public float inefficiencyMultiplier = 1f;

        public Dictionary<TechType, int> BaseMaterialCosts = new Dictionary<TechType, int>()//if you're a modder trying to change this value for your item, please use the ExternalModCompat class
        {
            { TechType.Titanium, 5 },
            { TechType.Copper, 7 },
            { TechType.Sulphur, 20 },
            { TechType.Diamond, 25 },
            { TechType.Gold, 20 },
            { TechType.Kyanite, 75 },
            { TechType.PrecursorIonCrystal, 100 },
            { TechType.Lead, 7 },
            { TechType.Lithium, 20 },
            { TechType.Magnetite, 20 },
            { TechType.ScrapMetal, 20 },
            { TechType.Nickel, 30 },
            { TechType.Quartz, 10 },
            { TechType.AluminumOxide, 30 },
            { TechType.Salt, 5 },
            { TechType.Silver, 10 },
            { TechType.UraniniteCrystal, 25 },
        };
        public Dictionary<TechType, int> OrganicMaterialsCosts = new Dictionary<TechType, int>()//if you're a modder trying to change this value for your item, please use the ExternalModCompat class
        {
            { TechType.CrashPowder, 1 },
            { TechType.AcidMushroom, 7 },
            { TechType.KooshChunk, 15 },
            { TechType.CoralChunk, 15 },
            { TechType.CreepvinePiece, 10 },
            { TechType.CreepvineSeedCluster, 15 },
            { TechType.WhiteMushroom, 25 },
            { TechType.EyesPlantSeed, 15 },
            { TechType.TreeMushroomPiece, 15 },
            { TechType.JellyPlant, 25 },
            { TechType.RedGreenTentacleSeed, 15 },
            { TechType.SeaCrownSeed, 15 },
            { TechType.StalkerTooth, 15 },
            { TechType.JeweledDiskPiece, 10 },
            { TechType.BloodOil, 15 },
        };
        public List<TechType> BlackListedTypes = new List<TechType>() 
        { 
            TechType.ThermalPlant, 
            TechType.TerraformerFragment,
            TechType.ThermalPlantFragment,
            TechType.Fragment,
            TechType.AquariumFragment
        };
        public List<string> AutoFilterStrings = new List<string>()
        {
            "fragment",
            "blueprint",
            "_kit",
        };
        public int EMCToFCSCreditRate = 500;//The ratio of EMC => Alterra Credit
        public int EMCConvertPerClick = 10;//The EMC amount converted per click
    }
    [FileName("EquivalentExchange")]
    public class SaveData : SaveDataCache
    {
        //public List<string> learntTechTypes = new List<string>();
        public EventList<TechType> learntTechTypes = new EventList<TechType>();
        public float EMCAvailable = 0;
    }
    public class EventList<T> : List<T>
    {
        public delegate void AddEvent(EventList<T> sender, T addedItem);
        public delegate void ClearEvent(EventList<T> sender, List<T> items);

        private AddEvent OnAdd;
        private AddEvent OnRemove;
        private ClearEvent OnClear;
        private List<IListListener> listeners = new List<IListListener>();
        public new void Add(T item)
        {
            if(OnAdd != null) OnAdd(this, item);

            foreach (var listener in listeners)
                listener?.OnAdd(this, item);

            base.Add(item);
        }
        public new void Remove(T item)
        {
            if (OnRemove != null) OnRemove(this, item);

            foreach(var listener in listeners)
                listener?.OnRemove(this, item);

            base.Remove(item);
        }
        public new void Clear()
        {
            if (OnClear != null) OnClear(this, this);

            foreach (var listener in listeners)
                listener.OnClear(this, this);
            base.Clear();
        }
        public void AddOnAddListener(AddEvent listener)
        {
            OnAdd += listener;
        }
        public void RemoveOnAddListener(AddEvent listener)
        {
            OnAdd -= listener;
        }
        public void AddOnRemoveListener(AddEvent listener)
        {
            OnRemove += listener;
        }
        public void RemoveOnRemoveListener(AddEvent listener)
        {
            OnRemove -= listener;
        }
        public void AddOnClearListener(ClearEvent listener)
        {
            OnClear += listener;
        }
        public void RemoveOnClearListener(ClearEvent listener)
        {
            OnClear -= listener;
        }
        public void AddListener(IListListener listener)
        {
            listeners.Add(listener);
        }
        public void RemoveListener(IListListener listener)
        {
            listeners.Remove(listener);
        }
        public interface IListListener
        {
            void OnAdd(EventList<T> sender, T item);
            void OnRemove(EventList<T> sender, T item);
            void OnClear(EventList<T> sender, List<T> items);
        }
    }
}