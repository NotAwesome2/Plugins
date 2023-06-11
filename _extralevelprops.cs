//reference System.Core.dll
//reference System.dll

using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MCGalaxy;
using MCGalaxy.Maths;
using MCGalaxy.Network;
using MCGalaxy.Config;
using MCGalaxy.Commands;
using MCGalaxy.Tasks;
using MCGalaxy.Util;
using MCGalaxy.Events.ServerEvents;
using MCGalaxy.Events.LevelEvents;
using MCGalaxy.Events.PlayerEvents;

namespace ExtraLevelProps
{
    
    
    //Based on MCGalaxy.ExtrasCollection
    public sealed class ExtrasCollection {
        readonly Dictionary<string, string> dict = new Dictionary<string, string>();
        readonly object locker = new object();
        
        public List<KeyValuePair<string, string>> All() {
            lock (locker) {
                var all = new List<KeyValuePair<string, string>>();
                foreach (var pair in dict) {
                    all.Add(new KeyValuePair<string, string>(pair.Key, pair.Value));
                }
                return all;
            }
        }
        
        public int Count { get { lock (locker) { return dict.Count; } } }
        public string this[string key] {
            get { lock (locker) { return dict[key]; } }
            set { lock (locker) { dict[key] = value; } }
        }
        
        public void Clear() { lock (locker) { dict.Clear(); } }
        public bool ContainsKey(string key) { lock (locker) { return dict.ContainsKey(key); } }
        public bool Remove(string key) { lock (locker) { return dict.Remove(key); } }
        
        public bool TryGet(string key, out string value) {
            lock (locker) { return dict.TryGetValue(key, out value); }
        }

        public bool GetBoolean(string key) { return GetBoolean(key, false); }
        public bool GetBoolean(string key, bool defaultValue) {
            string value;
            if (TryGet(key, out value)) {
                try { return Convert.ToBoolean(value); }
                catch (Exception) { }
            }
            return defaultValue;
        }

        public int GetInt(string key) { return GetInt(key, 0); }
        public int GetInt(string key, int defaultValue) {
            string value;
            if (TryGet(key, out value)) {
                try { return Convert.ToInt32(value); }
                catch (Exception) { }
            }
            return defaultValue;
        }

        public string GetString(string key) { return GetString(key, null); }
        public string GetString(string key, string defaultValue) {
            string value;
            if (TryGet(key, out value)) {
                try { return Convert.ToString(value); }
                catch (Exception) { }
            }
            return defaultValue;
        }
    }
    
    public static class ExtraLevelProps {
        public static string GetExtraPropString(this Level level, string key, string defaultValue = "") {
            lock (Core.extrasLocker) {
                if (!Core.levelCollections.ContainsKey(level.name)) { return defaultValue; }
                return Core.levelCollections[level.name].GetString(key.ToLower(), defaultValue);
            }
        }
        public static bool GetExtraPropBool(this Level level, string key) {
            lock (Core.extrasLocker) {
                if (!Core.levelCollections.ContainsKey(level.name)) { return false; }
                return Core.levelCollections[level.name].GetBoolean(key.ToLower());
            }
        }
        public static int GetExtraPropInt(this Level level, string key) {
            lock (Core.extrasLocker) {
                if (!Core.levelCollections.ContainsKey(level.name)) { return 0; }
                return Core.levelCollections[level.name].GetInt(key.ToLower());
            }
        }
        public static void SetExtraProp(this Level level, string key, string value) {
            if (!Core.IsValidProp(key)) { throw new System.ArgumentException("Illegal characters in property name \""+key+"\"."); }
            if (!Core.IsEmptyValue(value) && !Core.IsValidProp(value)) { throw new System.ArgumentException("Illegal characters in property value \""+value+"\"."); }
            
            lock (Core.extrasLocker) {
                if (!Core.levelCollections.ContainsKey(level.name)) { Core.levelCollections[level.name] = new ExtrasCollection(); }
                key = key.ToLower();
                if (Core.IsEmptyValue(value)) { Core.levelCollections[level.name].Remove(key); }
                else { Core.levelCollections[level.name][key] = value; }
            }
        }
        public static bool HasExtraProp(this Level level, string key) {
            lock (Core.extrasLocker) {
                if (!Core.levelCollections.ContainsKey(level.name)) { return false; }
                return Core.levelCollections[level.name].ContainsKey(key.ToLower());
            }
        }
        public static List<KeyValuePair<string, string>> AllExtraProps(this Level level) {
            lock (Core.extrasLocker) {
                if (!Core.levelCollections.ContainsKey(level.name)) { return null; }
                return Core.levelCollections[level.name].All();
            }
        }
    }
    
    public sealed class Core : Plugin {
        public override string name { get { return "_extralevelprops"; } }
        public override string MCGalaxy_Version { get { return "1.9.4.5"; } }
        public override string creator { get { return "Goodly"; } }
        
        const string propsAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890._";
        const string propsSplitter = " = ";
        static string[] propsSplitterSeperators = new string[] { propsSplitter };
        
    	internal static bool IsValidProp(string name) {
            if (name.Length > 0 && name.ContainsAllIn(propsAlphabet)) return true;
            return false;
        }
        internal static bool IsEmptyValue(string value) { return string.IsNullOrEmpty(value) || value.CaselessEq("false"); }
        
        internal static readonly object extrasLocker = new object();
        internal static Dictionary<string, ExtrasCollection> levelCollections = new Dictionary<string, ExtrasCollection>();
        
        static string propsDirectory = "plugins/extralevelprops/";
        static string PropsPath(string levelName) { return propsDirectory + levelName + ".properties"; }
        static string verifiedPath = propsDirectory + "_verifiedprops.txt";
        
        static readonly object verifiedLocker = new object();
        static Dictionary<string, string> verifiedProps = new Dictionary<string, string>();
        static void LoadVerifiedProps() {
            string[] verLines = File.ReadAllLines(verifiedPath);
            lock (verifiedLocker) {
                verifiedProps.Clear();
                foreach (var line in verLines) {
                    if (line.StartsWith("#")) { continue; }
                    string[] bits = line.SplitSpaces(2);
                    if (bits.Length != 2) { continue; }
                    string propName = bits[0];
                    string propDesc = bits[1];
                    verifiedProps[propName] = propDesc;
                }
            }
        }
        internal static bool IsVerified(string key) {
            lock (verifiedLocker) {
                return verifiedProps.ContainsKey(key.ToLower());
            }
        }
        internal static List<KeyValuePair<string, string>> AllVerifiedProps() {
            lock (verifiedLocker) {
                var all = new List<KeyValuePair<string, string>>();
                foreach (var pair in verifiedProps) {
                    all.Add(new KeyValuePair<string, string>(pair.Key, pair.Value));
                }
                return all;
            }
        }
        
        internal const string verifiedColor = "&6";
        internal const string unverifiedColor = "&7";
        internal static string FormatKey(string key) { return IsVerified(key) ? verifiedColor+key : unverifiedColor+key; }
        internal static string FormatValue(string value) {
            string color = "&7";
            if (value.CaselessEq("true")) { color = "&a"; }
            return color+value;
        }
        
        static void LoadCollection(string levelName) {
            lock (extrasLocker) {
                if (!File.Exists(PropsPath(levelName))) { return; }
                string[] lines = File.ReadAllLines(PropsPath(levelName));
                ExtrasCollection col = new ExtrasCollection();
                
                foreach (string line in lines) {
                    string[] bits = line.Split(propsSplitterSeperators, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (bits.Length != 2) { continue; } //malformed key value pair
                    if (!(IsValidProp(bits[0]) && IsValidProp(bits[1]))) { continue; } //key value pair contains illegal characters
                    col[bits[0]] = bits[1];
                }
                levelCollections[levelName] = col;
            }
        }
        static void UnloadCollection(string levelName) {
            lock (extrasLocker) {
                SaveCollectionToDisk(levelName);
                levelCollections.Remove(levelName);
            }
        }
        static void SaveCollectionToDisk(string levelName) {
            lock (extrasLocker) {
                if (!levelCollections.ContainsKey(levelName)) { return; }
                ExtrasCollection col = levelCollections[levelName];
                var kvps = col.All();
                List<string> lines = new List<string>();
                foreach (var kvp in kvps ) {
                    if (IsEmptyValue(kvp.Value)) { continue; }
                    lines.Add(kvp.Key + propsSplitter + kvp.Value);
                }
                if (lines.Count == 0) { File.Delete(PropsPath(levelName)); }
                else { File.WriteAllLines(PropsPath(levelName), lines.ToArray()); }
            }
        }
        static void EraseCollectionOnDisk(string levelName) {
            lock (extrasLocker) {
                File.Delete(PropsPath(levelName));
            }
        }
        static void CopyCollectionOnDisk(string sourceName, string targetName) {
            lock (extrasLocker) {
                SaveCollectionToDisk(sourceName); //Save source first to write any unsaved edits to disk before copying
                if (!File.Exists(PropsPath(sourceName))) { return; } //nothing to copy
                File.Copy(PropsPath(sourceName), PropsPath(targetName));
            }
        }
        
        static void PrepareDirectory() {
            if (!Directory.Exists(propsDirectory)) {
                Directory.CreateDirectory(propsDirectory);
            }
            if (!File.Exists(verifiedPath)) {
                string[] template = new string[] {
                    "# This file allows you specify which extra level props are verified.",
                    "# Users will be unable to set level properties that are not verified.",
                    "# Place names of verified properties each on their own line, followed by space then a description.",
                    "# You must reload the plugin for these to apply."
                    };
                File.WriteAllLines(verifiedPath, template);
            }
        }
        public override void Load(bool startup) {
            Command.Register(new CmdMapExtra());
            
            OnConfigUpdatedEvent.Register(OnConfigUpdated, Priority.Low);
            OnPlayerCommandEvent.Register(OnPlayerCommand, Priority.Low);
            
            OnLevelAddedEvent.Register(OnLevelAdded,     Priority.Critical);
            OnLevelRemovedEvent.Register(OnLevelRemoved, Priority.Low);
            OnLevelDeletedEvent.Register(OnLevelDeleted, Priority.Low);
            OnLevelCopiedEvent.Register(OnLevelCopied,   Priority.Critical);
            OnLevelRenamedEvent.Register(OnLevelRenamed, Priority.Critical);
            
            PrepareDirectory();
            LoadVerifiedProps();
            
            //initial load for any maps that are already loaded
            lock (extrasLocker) {
                Level[] levels = LevelInfo.Loaded.Items;
                foreach (Level level in levels) {
                    LoadCollection(level.name);
                }
            }
        }
        public override void Unload(bool shutdown) {
            Command.Unregister(Command.Find("mapextra"));
            
            OnConfigUpdatedEvent.Unregister(OnConfigUpdated);
            OnPlayerCommandEvent.Unregister(OnPlayerCommand);
            
            OnLevelAddedEvent.Unregister(OnLevelAdded);
            OnLevelRemovedEvent.Unregister(OnLevelRemoved);
            OnLevelDeletedEvent.Unregister(OnLevelDeleted);
            OnLevelCopiedEvent.Unregister(OnLevelCopied);
            OnLevelRenamedEvent.Unregister(OnLevelRenamed);
            
            //unload everything
            lock (extrasLocker) {
                Level[] levels = LevelInfo.Loaded.Items;
                foreach (Level level in levels) {
                    UnloadCollection(level.name);
                }
            }
        }
        
        static void OnConfigUpdated() {
            LoadVerifiedProps();
        }
        
        static void OnPlayerCommand(Player p, string cmd, string message, CommandData data) {
            if (message.Length == 0 && cmd.CaselessEq("map")) {
                Server.MainScheduler.QueueOnce(DisplayDelayed, p, TimeSpan.FromMilliseconds(50));
            }
        }
        static void DisplayDelayed(SchedulerTask task) {
            Player p = (Player)task.State;
            CmdMapExtra.Display(p, false);
        }
        
        static void OnLevelAdded(Level level) {
            LoadCollection(level.name);
            
            //MsgGoodly("OnLevelAdded {0}", level.name);
        }
        static void OnLevelRemoved(Level level) {
            UnloadCollection(level.name);
            
            //MsgGoodly("OnLevelRemoved {0}", level.name);
        }
        static void OnLevelDeleted(string levelName) { //occurs after OnLevelRemoved
            EraseCollectionOnDisk(levelName);
            
            //MsgGoodly("OnLevelDeleted {0}", levelName);
        }
        static void OnLevelCopied(string srcMap, string dstMap) {
            CopyCollectionOnDisk(srcMap, dstMap);
            
            //MsgGoodly("OnLevelCopied {0} {1}", srcMap, dstMap);
        }
        static void OnLevelRenamed(string srcMap, string dstMap) {
            //Event order: Removed, Removed, Renamed(YOU ARE HERE), Added
            CopyCollectionOnDisk(srcMap, dstMap);
            EraseCollectionOnDisk(srcMap);
            
            //MsgGoodly("OnLevelRenamed {0} {1}", srcMap, dstMap);
        }
        
        static void MsgGoodly(string message, params object[] args) {
            Player goodly = PlayerInfo.FindExact("goodlyay+"); if (goodly == null) { return; }
            goodly.Message("&b"+message, args);
        }
    }
    
    public class CmdMapExtra : Command2 {
        public override string name { get { return "MapExtra"; } }
        public override string shortcut { get { return "mapext"; } }
        public override bool MessageBlockRestricted { get { return true; } }
        public override string type { get { return "World"; } }
        public override bool museumUsable { get { return false; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Admin; } }
        
        public static void Display(Player p, bool showNone) {
            var kvps = p.level.AllExtraProps();
            bool none = (kvps == null || kvps.Count == 0);
            if (showNone || !none) { p.Message("&TExtra map settings:"); }
            if (none && showNone) { p.Message("  (no extra settings have been specified)"); }
            if (none) { return; }
            
            kvps.Sort((name1, name2) => string.Compare(name1.Key, name2.Key));
            
            foreach (var kvp in kvps) {
                p.Message("  {0}&S: {1}", Core.FormatKey(kvp.Key), Core.FormatValue(kvp.Value));
            }
        }
        public static void DisplayVerifiedProps(Player p) {
            p.Message("Extra map properties you can set:");
            var kvps = Core.AllVerifiedProps();
            foreach (var kvp in kvps) {
                p.Message("  {0}{1}&S: &H{2}", Core.verifiedColor, kvp.Key, kvp.Value);
            }
        }
        
        bool CanUse(Player p) {
            bool canUse = LevelInfo.IsRealmOwner(p.name, p.level.name) || p.group.Permission >= LevelPermission.Operator;
            if (!canUse) { p.Message("You can only use &T/{0} &Son levels you own.", name); return false; }
            return canUse;
        }
        
        public override void Use(Player p, string message, CommandData data) {
            if (message == "") {
                Help(p);
                return;
            }
            
            //if (!LevelInfo.Check(p, data.Rank, p.level, "modify extra map properties")) { return; }
            if (!CanUse(p)) { return; }
            
            string[] words = message.SplitSpaces();
            if (words.Length > 2) { p.Message("&WToo many arguments! You may only specify one property and one value at a time."); return; }
            string key = words[0].ToLower();
            string value = words.Length > 1 ? words[1] : "";
            try {
                bool alreadyRemoved = !p.level.HasExtraProp(key);
                
                //Do not allow adding of unverified keys, but allow removing
                if (!Core.IsEmptyValue(value) && !Core.IsVerified(key)) {
                    p.Message("&WThere is no property \"{0}&W\".", Core.FormatKey(key));
                    DisplayVerifiedProps(p);
                    return;
                }
                
                p.level.SetExtraProp(key, value);
                if (Core.IsEmptyValue(value)) {
                    if (alreadyRemoved) {
                        p.Message("There is no property \"{0}&S\" to remove.", Core.FormatKey(key));
                    } else {
                        p.Message("Removed extra map property \"{0}&S\".", Core.FormatKey(key));
                    }
                }
                else {
                    p.Message("Set {0}&S to: {1}", Core.FormatKey(key), Core.FormatValue(value));
                }
            }
            catch (System.ArgumentException e) { p.Message("&W{0}", e.Message); }
        }

        public override void Help(Player p) {
            p.Message("&T/MapExtra [property] <value>");
            p.Message("&HSets an extra map property of the current level.");
            DisplayVerifiedProps(p);
            p.Message("Use &T/Map &Sto see this map's current extra level properties.");
        }
    }
}