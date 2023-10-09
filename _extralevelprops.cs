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
    internal sealed class ExtrasCollection {
        readonly Dictionary<string, string> dict = new Dictionary<string, string>();
        readonly object locker = new object();
        
        internal List<KeyValuePair<string, string>> All() {
            lock (locker) {
                var all = new List<KeyValuePair<string, string>>();
                foreach (var pair in dict) {
                    all.Add(new KeyValuePair<string, string>(pair.Key, pair.Value));
                }
                return all;
            }
        }
        
        internal int Count { get { lock (locker) { return dict.Count; } } }
        internal string this[string key] {
            get { lock (locker) { return dict[key]; } }
            set { lock (locker) { dict[key] = value; } }
        }
        
        internal void Clear() { lock (locker) { dict.Clear(); } }
        internal bool ContainsKey(string key) { lock (locker) { return dict.ContainsKey(key); } }
        internal bool Remove(string key) { lock (locker) { return dict.Remove(key); } }
        
        internal bool TryGet(string key, out string value) {
            lock (locker) { return dict.TryGetValue(key, out value); }
        }

        internal bool GetBoolean(string key) { return GetBoolean(key, false); }
        internal bool GetBoolean(string key, bool defaultValue) {
            string value;
            if (TryGet(key, out value)) {
                try { return Convert.ToBoolean(value); }
                catch (Exception) { }
            }
            return defaultValue;
        }

        internal int GetInt(string key) { return GetInt(key, 0); }
        internal int GetInt(string key, int defaultValue) {
            string value;
            if (TryGet(key, out value)) {
                try { return Convert.ToInt32(value); }
                catch (Exception) { }
            }
            return defaultValue;
        }

        internal string GetString(string key) { return GetString(key, null); }
        internal string GetString(string key, string defaultValue) {
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
        // Returns false if the property was removed (value is null, empty, or zero), otherwise true
        public static bool SetExtraProp(this Level level, string key, string value) {
            if (!LevelProp.Exists(key)) { throw new System.ArgumentException("No property named \""+key+"\" has been defined, therefore it cannot be set."); }
            if (!LevelProp.ValidCharacters(key)) { throw new System.ArgumentException("Illegal characters in property name \""+key+"\"."); }
            if (!LevelProp.ValidCharacters(value) && !LevelProp.IsEmptyValue(value)) {
                throw new System.ArgumentException("Illegal characters in property value \""+value+"\".");
            }
            
            lock (Core.extrasLocker) {
                if (!Core.levelCollections.ContainsKey(level.name)) { Core.levelCollections[level.name] = new ExtrasCollection(); }
                key = key.ToLower();
                
                if (LevelProp.IsEmptyValue(value)) {
                    Core.levelCollections[level.name].Remove(key);
                    return false;
                }
                Core.levelCollections[level.name][key] = value;
                return true;
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
    
    internal class LevelProp {
        
        public const string propColor = "&6";
        const string propsAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890._+,-";
        static string verifiedPath = Core.propsDirectory + "_extralevelprops.txt";
        static readonly object verifiedLocker = new object();
        static Dictionary<string, LevelProp> definedProps = new Dictionary<string, LevelProp>();
        static string[] constructionInstruction = new string[] {
            "# This file allows you to define extra level properties. Lines starting with # are ignored.",
            "# Place names of verified properties each on their own line. Here is the format followed by an example:",
            "# property_name rank_permission_level description line 1|description line 2|etc",
            "# boardgame 30 When true: Move bots like board game pieces.|You must let the map unload then load and use /ad for this to apply.",
            "# After editing this file, you must use /server reload for the changes to apply."
            };
        
        public static void PrepareDirectory() {
            if (!File.Exists(verifiedPath)) {
                File.WriteAllLines(verifiedPath, constructionInstruction);
            }
        }
        public static void ReloadAll() {
            string[] verLines = File.ReadAllLines(verifiedPath);
            lock (verifiedLocker) {
                definedProps.Clear();
                foreach (var line in verLines) {
                    if (line.StartsWith("#")) { continue; }
                    var prop = Create(line);
                    if (prop == null) { continue; }
                    definedProps[prop.name] = prop;
                }
            }
        }
        
        public static List<LevelProp> AllDefined() {
            lock (verifiedLocker) {
                var all = new List<LevelProp>();
                foreach (var kvp in definedProps) {
                    all.Add(kvp.Value);
                }
                return all;
            }
        }
    	public static bool ValidCharacters(string name) {
            if (name.Length > 0 && name.ContainsAllIn(propsAlphabet)) return true;
            return false;
        }
        public static bool Exists(string key) {
            lock (verifiedLocker) {
                return definedProps.ContainsKey(key.ToLower());
            }
        }
        public static LevelProp Get(string key) {
            lock (verifiedLocker) {
                LevelProp prop;
                if (!definedProps.TryGetValue(key.ToLower(), out prop)) { return null; }
                return prop;
            }
        }
        public static string FormatValue(string value) {
            string color = "&7";
            if (value.CaselessEq("true")) { color = "&a"; }
            return color+value;
        }
        public static bool IsEmptyValue(string value) {
            if (string.IsNullOrEmpty(value) || value.CaselessEq("false")) { return true; }
            int intValue;
            if (int.TryParse(value, out intValue) && intValue == 0) { return true; }
            return false;
        }
        
        
        
        // non static members
        string name;
        public string coloredName { get { return propColor+name; } }
        //LevelPermission permission;
        public ItemPerms permission { get; private set; }
        string[] desc;
        
        static LevelProp Create(string line) {
            string[] bits = line.SplitSpaces(3);
            if (bits.Length < 3) {
                Logger.Log(LogType.Warning, "_extralevelprops: Not enough | separated sections to define a property in {0} ", verifiedPath);
                return null;
            }
            LevelProp prop = new LevelProp();
            
            prop.name = bits[0];
            if (!ValidCharacters(prop.name)) {
                Logger.Log(LogType.Warning, "_extralevelprops: Illegal characters in property name \"{0}\" in {1}.", prop.name, verifiedPath);
                return null;
            }
            int permAsInt;
            if (!int.TryParse(bits[1], out permAsInt)) {
                Logger.Log(LogType.Warning, "_extralevelprops: Could not parse {0} as a LevelPermission number in {1}", bits[1], verifiedPath);
                return null;
            }
            prop.permission = new ItemPerms((LevelPermission)permAsInt);
            prop.desc = bits[2].Split(new char[] { '|' });
            
            return prop;
        }
        public void Display(Player p) {
            string intro = "  "+coloredName;

            p.Message("  {0} &T{1}", coloredName, desc[0]);
            for (int i = 1; i < desc.Length; i++) {
                p.Message("    &H{0}", desc[i]);
            }
        }
    }
    
    internal sealed class Core : Plugin {
        public override string name { get { return "_extralevelprops"; } }
        public override string MCGalaxy_Version { get { return "1.9.4.5"; } }
        public override string creator { get { return "Goodly"; } }
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
            LevelProp.ReloadAll();
            
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
        
        const string propsSplitter = " = ";
        static string[] propsSplitterSeperators = new string[] { propsSplitter };
        
        internal static string propsDirectory = "plugins/extralevelprops/";
        static string PropsPath(string levelName) { return propsDirectory + levelName + ".properties"; }
        
        internal static readonly object extrasLocker = new object();
        internal static Dictionary<string, ExtrasCollection> levelCollections = new Dictionary<string, ExtrasCollection>();
        
        static void LoadCollection(string levelName) {
            lock (extrasLocker) {
                if (!File.Exists(PropsPath(levelName))) { return; }
                string[] lines = File.ReadAllLines(PropsPath(levelName));
                ExtrasCollection col = new ExtrasCollection();
                
                foreach (string line in lines) {
                    string[] bits = line.Split(propsSplitterSeperators, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (bits.Length != 2) { continue; } //malformed key value pair
                    if (!(LevelProp.ValidCharacters(bits[0]) && LevelProp.ValidCharacters(bits[1]))) { continue; } //key value pair contains illegal characters
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
                    if (LevelProp.IsEmptyValue(kvp.Value)) { continue; }
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
            LevelProp.PrepareDirectory();
        }
        
        static void OnConfigUpdated() {
            LevelProp.ReloadAll();
        }
        
        static void OnPlayerCommand(Player p, string cmd, string message, CommandData data) {
            if (message.Length == 0 && cmd.CaselessEq("map")) {
                Server.MainScheduler.QueueOnce(DisplayExtraLevelProps, p, TimeSpan.FromMilliseconds(50));
            }
        }
        static void DisplayExtraLevelProps(SchedulerTask task) {
            Player p = (Player)task.State;
            var kvps = p.level.AllExtraProps();
            if (kvps == null || kvps.Count == 0) { return; }
            kvps.Sort((name1, name2) => string.Compare(name1.Key, name2.Key));
            p.Message("&TExtra map settings:");
            foreach (var kvp in kvps) {
                p.Message("  {0}&S: {1}", LevelProp.propColor+kvp.Key, LevelProp.FormatValue(kvp.Value));
            }
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
        
        bool CanUse(Player p) {
            bool canUse = LevelInfo.IsRealmOwner(p.name, p.level.name) || p.group.Permission >= LevelPermission.Operator;
            if (!canUse) { p.Message("You can only use &T/{0} &Son levels you own.", name); return false; }
            return canUse;
        }
        
        public override void Use(Player p, string message, CommandData data) {
            if (message == "") { Help(p); return; }
            if (!CanUse(p)) { return; }
            
            string[] words = message.SplitSpaces();
            if (words.Length > 2) { p.Message("&WToo many arguments! You may only specify one property and one value at a time."); return; }
            string key = words[0].ToLower();
            string value = words.Length > 1 ? words[1] : "";
            
            LevelProp prop = LevelProp.Get(key);
            
            if (prop == null) {
                p.Message("&WThere is no property \"{0}&W\".", key);
                DisplayVerifiedProps(p);
                return;
            }
            if (!prop.permission.UsableBy(p)) {
                p.Message("Only {0} can edit the {1}&S property.", prop.permission.Describe(), prop.coloredName);
                return;
            }
            
            bool alreadyRemoved = !p.level.HasExtraProp(key);
            bool removed;
            try {
                removed = !p.level.SetExtraProp(key, value);
            } catch (System.ArgumentException e) { p.Message("&W{0}", e.Message); return; }
            
            if (removed) {
                if (alreadyRemoved) { p.Message("There is no property \"{0}&S\" to remove.", prop.coloredName); return; }
                p.Message("Removed extra map property \"{0}&S\".", prop.coloredName);
                return;
            }
            p.Message("Set {0}&S to: {1}", prop.coloredName, LevelProp.FormatValue(value));
        }

        public override void Help(Player p) {
            p.Message("&T/MapExtra [property] <value>");
            p.Message("&HSets an extra map property of the current level.");
            DisplayVerifiedProps(p);
            p.Message("Use &T/Map &Sto see this map's current extra level properties.");
        }
        static void DisplayVerifiedProps(Player p) {
            p.Message("Extra map properties you can set:");
            var props = LevelProp.AllDefined();
            foreach (var prop in props) {
                if (!prop.permission.UsableBy(p)) { continue; }
                prop.Display(p);
            }
        }
    }
}