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

namespace ExtraLevelProps {


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
                try { return Convert.ToBoolean(value); } catch (Exception) { }
            }
            return defaultValue;
        }

        internal int GetInt(string key) { return GetInt(key, 0); }
        internal int GetInt(string key, int defaultValue) {
            string value;
            if (TryGet(key, out value)) {
                try { return Convert.ToInt32(value); } catch (Exception) { }
            }
            return defaultValue;
        }

        internal string GetString(string key) { return GetString(key, null); }
        internal string GetString(string key, string defaultValue) {
            string value;
            if (TryGet(key, out value)) {
                try { return Convert.ToString(value); } catch (Exception) { }
            }
            return defaultValue;
        }
    }

    public static class ExtraLevelProps {

        /// <summary>
        /// Delegate to call when a player tries to change a prop with /mapext
        /// </summary>
        public delegate void OnPropChanging(Player p, Level level, ref string value, ref bool cancel);
        static readonly object propChangingEventLocker = new object();
        /// <summary>
        /// Simple preset function to use for OnPropChanging for boolean level props.
        /// </summary>
        public static void OnPropChangingBool(Player p, Level level, ref string value, ref bool cancel) {
            bool on = false;
            if (value == "") value = "false";
            if (!CommandParser.GetBool(p, value, ref on)) { cancel = true; return; }
            value = on.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Delegate to call when a level prop's value is displayed for the given player in the given level.
        /// </summary>
        public delegate string DisplayValue(Player p, Level level, string value);

        /// <summary>
        /// Called by other plugins to define an extra level prop so that it may be set.
        /// Throws: ArgumentException if the property name contains disallowed characters or it has already been registered.
        /// </summary>
        public static void Register(string pluginName, string propName, LevelPermission defaultPermission, string[] propDesc, OnPropChanging onPropChanging) {
            _Register(pluginName, propName, defaultPermission, propDesc, onPropChanging, null);
        }
        //Overload instead of optional parameter for binary compatibility (too lazy to reload more plugins...)
        public static void Register(string pluginName, string propName, LevelPermission defaultPermission, string[] propDesc,
            OnPropChanging onPropChanging, DisplayValue displayValue) {

            _Register(pluginName, propName, defaultPermission, propDesc, onPropChanging, displayValue);
        }
        static void _Register(string pluginName, string propName, LevelPermission defaultPermission, string[] propDesc,
            OnPropChanging onPropChanging, DisplayValue displayValue) {

            if (!LevelProp.ValidPropNameCharacters(propName)) {
                throw new ArgumentException(
                    String.Format("Plugin \"{0}\" cannot register property \"{1}\" because it contains illegal characters.", pluginName, propName));
            }

            lock (propChangingEventLocker) {

                propName = propName.ToLowerInvariant();
                LevelProp prop = LevelProp.Get(propName);

                if (prop != null && prop.desc != null) {
                    throw new ArgumentException(String.Format("The ExtraLevelProp \"{0}\" is already in use by the \"{1}\" plugin.", propName, pluginName));
                }
                if (prop == null) {
                    prop = new LevelProp(propName, defaultPermission, propDesc);
                } else {
                    prop.desc = propDesc;
                } //Just update description if it already exists so as to not override permission set from file

                prop.onPropChanging = onPropChanging;
                prop.displayValue = displayValue;
            }
        }

        /// <summary>
        /// Called by other plugins to undefine an extra level prop so that it may no longer be set.
        /// </summary>
        /// <param name="propName"></param>
        public static void Unregister(string propName) {
            propName = propName.ToLowerInvariant();

            lock (propChangingEventLocker) {
                LevelProp prop = LevelProp.Get(propName);
                if (prop == null) { return; }
                prop.desc = null;
                prop.onPropChanging = null;
                prop.displayValue = null;
                //Don't remove the prop from the defined list in LevelProp to preserve the permission set from file
            }
        }

        public static string GetExtraPropString(this Level level, string key, string defaultValue = "") {
            lock (Core.extrasLocker) {
                if (!Core.levelCollections.ContainsKey(level)) { return defaultValue; }
                return Core.levelCollections[level].GetString(key.ToLowerInvariant(), defaultValue);
            }
        }
        public static bool GetExtraPropBool(this Level level, string key) {
            lock (Core.extrasLocker) {
                if (!Core.levelCollections.ContainsKey(level)) { return false; }
                return Core.levelCollections[level].GetBoolean(key.ToLowerInvariant());
            }
        }
        public static int GetExtraPropInt(this Level level, string key) {
            lock (Core.extrasLocker) {
                if (!Core.levelCollections.ContainsKey(level)) { return 0; }
                return Core.levelCollections[level].GetInt(key.ToLowerInvariant());
            }
        }

        public enum SetExtraPropResult { Cancelled, Removed, Set }
        /// <summary>
        /// Called when another plugin tries to set an extra level prop
        /// </summary>
        public static SetExtraPropResult TrySetExtraProp(this Level level, string key, string value) {
            CheckCanSetProp(key);
            CheckCanSetValue(value);
            return SetExtraProp(level, key, value);
        }

        /// <summary>
        /// Called when a player tries to set an extra level prop using /mapext
        /// </summary>
        internal static SetExtraPropResult TrySetExtraProp(this Level level, Player p, string key, ref string value) {
            CheckCanSetProp(key);

            bool cancel = false;
            lock (propChangingEventLocker) {
                key = key.ToLowerInvariant();
                LevelProp prop = LevelProp.Get(key);
                if (prop != null && prop.onPropChanging != null) {
                    prop.onPropChanging.Invoke(p, level, ref value, ref cancel);
                }
            }
            if (cancel) { return SetExtraPropResult.Cancelled; }

            //Called after event invokation because it might change the value
            CheckCanSetValue(value);

            //Finally set value
            return SetExtraProp(level, key, value);
        }

        /// <summary>
        /// Called to actually set a level prop. Does not check if key or value use valid characters.
        /// </summary>
        static SetExtraPropResult SetExtraProp(Level level, string key, string value) {
            key = key.ToLowerInvariant();
            lock (Core.extrasLocker) {
                if (!Core.levelCollections.ContainsKey(level)) { Core.levelCollections[level] = new ExtrasCollection(); }

                if (LevelProp.IsEmptyValue(value)) {
                    Core.levelCollections[level].Remove(key);
                    return SetExtraPropResult.Removed;
                }
                Core.levelCollections[level][key] = value;
                return SetExtraPropResult.Set;
            }
        }
        static void CheckCanSetProp(string key) {
            if (!LevelProp.ValidPropNameCharacters(key)) { throw new System.ArgumentException("Illegal characters in property name \"" + key + "\"."); }
            if (!LevelProp.Exists(key)) { throw new System.ArgumentException("No property named \"" + key + "\" has been defined, therefore it cannot be set."); }
        }
        static void CheckCanSetValue(string value) {
            if (!LevelProp.ValidPropValueCharacters(value) && !LevelProp.IsEmptyValue(value)) {
                throw new System.ArgumentException("Illegal characters in property value \"" + value + "\".");
            }
        }

        public static bool HasExtraProp(this Level level, string key) {
            lock (Core.extrasLocker) {
                if (!Core.levelCollections.ContainsKey(level)) { return false; }
                return Core.levelCollections[level].ContainsKey(key.ToLowerInvariant());
            }
        }
        public static List<KeyValuePair<string, string>> AllExtraProps(this Level level) {
            lock (Core.extrasLocker) {
                if (!Core.levelCollections.ContainsKey(level)) { return null; }
                return Core.levelCollections[level].All();
            }
        }
    }

    internal class LevelProp {

        public const string propColor = "&6";
        const string propsAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890._+,-/";
        const string propsValuesAlphabet = propsAlphabet + " ";
        static string verifiedPath = Core.propsDirectory + "_extralevelprops.txt";
        static readonly object verifiedLocker = new object();
        static Dictionary<string, LevelProp> definedProps = new Dictionary<string, LevelProp>();
        static string[] constructionInstruction = new string[] {
            "# This file allows you to change the permissions of extra level properties.",
            "# Place names of properties each on their own line followed by the rank permission number.",
            "# property_name rank_permission_level",
            "# Example:",
            "# pushable_blocks 0",
            "# After editing this file, you must use /server reload for the changes to apply."
            };

        public static void PrepareDirectory() {
            if (!File.Exists(verifiedPath)) {
                File.WriteAllLines(verifiedPath, constructionInstruction);
            }
        }
        public static void LoadPermissionsFromFile() {
            string[] verLines = File.ReadAllLines(verifiedPath);
            lock (verifiedLocker) {
                //definedProps.Clear();

                foreach (var line in verLines) {
                    if (line.StartsWith("#")) { continue; }
                    string propName;
                    LevelPermission perms;

                    if (!GetPermission(line, out propName, out perms)) { continue; }

                    LevelProp prop = Get(propName);
                    if (prop == null) {
                        //If this prop hasn't been defined by any plugin or from the file, create a new one
                        prop = new LevelProp(propName, perms);
                    } else {
                        //If this prop already exists, just update the permission level from the file
                        prop.permission = new ItemPerms(perms);
                    }

                    definedProps[prop.name] = prop;
                }
            }
        }


        public static List<LevelProp> AllSettableBy(Player p) {
            lock (verifiedLocker) {

                var all = new List<LevelProp>();
                foreach (var kvp in definedProps) {
                    if (kvp.Value.desc == null || !kvp.Value.permission.UsableBy(p)) { continue; }
                    all.Add(kvp.Value);
                }

                all.Sort((a, b) => string.Compare(a.name, b.name));
                return all;
            }
        }
        public static bool ValidPropNameCharacters(string name) {
            if (name.Length > 0 && name.ContainsAllIn(propsAlphabet)) return true;
            return false;
        }
        public static bool ValidPropValueCharacters(string value) {
            if (value.Length > 0 && value.ContainsAllIn(propsValuesAlphabet)) return true;
            return false;
        }
        public static bool Exists(string key) {
            lock (verifiedLocker) {
                return definedProps.ContainsKey(key.ToLowerInvariant());
            }
        }
        /// <summary>
        /// Gets the LevelProp with the given name (key), case insensitive. Null if none found.
        /// </summary>
        public static LevelProp Get(string key) {
            lock (verifiedLocker) {
                LevelProp prop;
                if (!definedProps.TryGetValue(key.ToLowerInvariant(), out prop)) { return null; }
                return prop;
            }
        }
        public static string DefaultDisplayValue(string value) {
            string color = "&7";
            if (value.CaselessEq("true")) { color = "&a"; }
            return color + value;
        }
        public static bool IsEmptyValue(string value) {
            if (string.IsNullOrEmpty(value) || value.CaselessEq("false")) { return true; }
            int intValue;
            if (int.TryParse(value, out intValue) && intValue == 0) { return true; }
            return false;
        }

        static bool GetPermission(string line, out string propName, out LevelPermission perms) {
            string[] bits = line.SplitSpaces();
            propName = null;
            perms = 0;

            if (bits.Length < 2) {
                Logger.Log(LogType.Warning, "_extralevelprops: Each text line in {0} expects a property name and a rank permission number.", verifiedPath);
                return false;
            }

            propName = bits[0].ToLowerInvariant();
            if (!ValidPropNameCharacters(propName)) {
                Logger.Log(LogType.Warning,
                    "_extralevelprops: Cannot set permission for property name \"{0}\" in {1} because it contains illegal characters.",
                    propName, verifiedPath);

                return false;
            }

            int permAsInt;
            if (!int.TryParse(bits[1], out permAsInt)) {
                Logger.Log(LogType.Warning, "_extralevelprops: Could not parse {0} as a LevelPermission number in {1}", bits[1], verifiedPath);
                return false;
            }

            perms = (LevelPermission)permAsInt;
            return true;
        }


        // non static members
        public readonly string name;
        public string coloredName { get { return Group.GetColor(permission.MinRank) + name; } }
        public ItemPerms permission { get; private set; }
        internal string[] desc = null;

        public ExtraLevelProps.OnPropChanging onPropChanging = null;
        public ExtraLevelProps.DisplayValue displayValue = null;
        private LevelProp() { }
        /// <summary>
        /// Creates a prop with the given name and adds it to the list of defined props.
        /// </summary>
        public LevelProp(string name, LevelPermission defaultPermission, string[] desc = null) {
            this.name = name;
            this.permission = new ItemPerms(defaultPermission);
            this.desc = desc;
            lock (verifiedLocker) {
                definedProps[name] = this;
            }
        }

        /// <summary>
        /// Call to describe this prop with colored name and description.
        /// </summary>
        public void Describe(Player p) {
            p.Message("{0} &T{1}", coloredName, desc[0]);

            for (int i = 1; i < desc.Length; i++) {
                p.Message("  &H{0}", desc[i]);
            }
            p.Message("Usable by: " + permission.Describe());
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

            OnLevelAddedEvent.Register(OnLevelAdded, Priority.Critical);
            OnLevelRemovedEvent.Register(OnLevelRemoved, Priority.Low);
            OnLevelDeletedEvent.Register(OnLevelDeleted, Priority.Low);
            OnLevelCopiedEvent.Register(OnLevelCopied, Priority.Critical);
            OnLevelRenamedEvent.Register(OnLevelRenamed, Priority.Critical);

            PrepareDirectory();
            LevelProp.LoadPermissionsFromFile();

            //initial load for any maps that are already loaded
            lock (extrasLocker) {
                Level[] levels = LevelInfo.Loaded.Items;
                foreach (Level level in levels) {
                    LoadCollection(level);
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
                    UnloadCollection(level);
                }
            }
        }

        const string propsSplitter = " = ";
        static string[] propsSplitterSeperators = new string[] { propsSplitter };

        internal static string propsDirectory = "plugins/extralevelprops/";
        static string PropsPath(string levelName) { return propsDirectory + levelName + ".properties"; }

        internal static readonly object extrasLocker = new object();
        internal static Dictionary<Level, ExtrasCollection> levelCollections = new Dictionary<Level, ExtrasCollection>();

        static void LoadCollection(Level level) {
            lock (extrasLocker) {
                if (!File.Exists(PropsPath(level.name))) { return; }
                string[] lines = File.ReadAllLines(PropsPath(level.name));
                ExtrasCollection col = new ExtrasCollection();

                foreach (string line in lines) {
                    string[] bits = line.Split(propsSplitterSeperators, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (bits.Length != 2) { continue; } //malformed key value pair
                    if (!(LevelProp.ValidPropNameCharacters(bits[0]) && LevelProp.ValidPropValueCharacters(bits[1]))) { continue; } //key value pair contains illegal characters
                    col[bits[0]] = bits[1];
                }
                levelCollections[level] = col;
            }
        }
        static void UnloadCollection(Level level) {
            lock (extrasLocker) {
                SaveCollectionToDisk(level);
                levelCollections.Remove(level);
            }
        }
        static void SaveCollectionToDisk(string levelName) {
            lock (extrasLocker) {
                Level[] levels = LevelInfo.Loaded.Items;
                foreach (Level level in levels) {
                    if (level.name == levelName) { SaveCollectionToDisk(level); }
                }
            }
        }
        static void SaveCollectionToDisk(Level level) {
            lock (extrasLocker) {
                if (!levelCollections.ContainsKey(level)) { return; }
                ExtrasCollection col = levelCollections[level];
                var kvps = col.All();
                List<string> lines = new List<string>();
                foreach (var kvp in kvps) {
                    if (LevelProp.IsEmptyValue(kvp.Value)) { continue; }
                    lines.Add(kvp.Key + propsSplitter + kvp.Value);
                }
                if (lines.Count == 0) { File.Delete(PropsPath(level.name)); } else { File.WriteAllLines(PropsPath(level.name), lines.ToArray()); }
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
            LevelProp.LoadPermissionsFromFile();
        }

        static void OnPlayerCommand(Player p, string cmd, string message, CommandData data) {
            if (message.Length == 0 && cmd.CaselessEq("map")) {
                Server.MainScheduler.QueueOnce(DisplayExtraLevelProps, p, TimeSpan.FromMilliseconds(50));
            }
        }
        static void DisplayExtraLevelProps(SchedulerTask task) {
            Player p = (Player)task.State;
            List<KeyValuePair<string, string>> kvps = p.level.AllExtraProps();
            if (kvps == null || kvps.Count == 0) { return; }
            kvps.Sort((name1, name2) => string.Compare(name1.Key, name2.Key));
            p.Message("&TExtra map settings:");
            foreach (KeyValuePair<string, string> kvp in kvps) {

                LevelProp prop = LevelProp.Get(kvp.Key);
                ExtraLevelProps.DisplayValue display;

                if (prop != null && prop.displayValue != null) {
                    display = prop.displayValue;
                } else {
                    display = (pl, level, value) => LevelProp.DefaultDisplayValue(kvp.Value);
                }

                p.Message("  {0}&S: {1}", LevelProp.propColor + kvp.Key, display(p, p.level, kvp.Value));
            }
        }

        static void OnLevelAdded(Level level) {
            LoadCollection(level);

            //MsgGoodly("OnLevelAdded {0}", level.name);
        }
        static void OnLevelRemoved(Level level) {
            UnloadCollection(level);

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
            goodly.Message("&b" + message, args);
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

            string[] words = message.SplitSpaces(2);
            //if (words.Length > 2) { p.Message("&WToo many arguments! You may only specify one property and one value at a time."); return; }
            string key = words[0].ToLowerInvariant();
            string value = words.Length > 1 ? words[1] : "";

            LevelProp prop = LevelProp.Get(key);

            //A property could be non-null (hanging around to preserve permission through other plugin reloads) but have no desc, in which case it's not used
            if (prop == null || prop.desc == null) {
                p.Message("&WThere is no property \"{0}&W\".", key);
                DisplayDefinedProps(p);
                return;
            }
            if (!prop.permission.UsableBy(p)) {
                p.Message("Only {0} can edit the {1}&S property.", prop.permission.Describe(), prop.coloredName);
                return;
            }

            bool alreadyRemoved = !p.level.HasExtraProp(key);
            ExtraLevelProps.SetExtraPropResult setResult;
            try {
                setResult = p.level.TrySetExtraProp(p, key, ref value);
            } catch (System.ArgumentException e) { p.Message("&W{0}", e.Message); return; }

            //Silently quit. Feedback is handled by the plugin responsible for cancelling this prop
            if (setResult == ExtraLevelProps.SetExtraPropResult.Cancelled) { return; }

            if (setResult == ExtraLevelProps.SetExtraPropResult.Removed) {
                if (alreadyRemoved) { p.Message("There is no property \"{0}&S\" to remove.", prop.coloredName); return; }
                p.Message("Removed extra map property \"{0}&S\".", prop.coloredName);
                return;
            }

            p.Message("Set {0}&S to: {1}", prop.coloredName, LevelProp.DefaultDisplayValue(value));
        }

        public override void Help(Player p) {
            p.Message("&T/MapExtra [property] <value>");
            p.Message("&HSets an extra map property of the current level.");
            DisplayDefinedProps(p);
            p.Message("&HUse &T/help mapext [prop] &Hto learn about a property.");
            p.Message("Use &T/Map &Sto see this map's current extra level properties.");
        }
        public override void Help(Player p, string message) {
            if (message == "") { Help(p); return; }
            var prop = LevelProp.Get(message);
            if (prop == null || prop.desc == null) {
                p.Message("&WThere is no extra map prop \"{0}\"", message.ToLowerInvariant());
                DisplayDefinedProps(p);
                return;
            }
            prop.Describe(p);
        }
        static void DisplayDefinedProps(Player p) {
            p.Message("Extra map properties you can set:");
            List<LevelProp> props = LevelProp.AllSettableBy(p);
            p.Message("{0}", props.Join((pro) => pro.coloredName, "&S, "));
        }
    }
}