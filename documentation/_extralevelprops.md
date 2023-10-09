# _extralevelprops.cs
This plugin adds the command `/mapext` which allows adding extra properties that are displayed in `/map`. These properties do not do anything on their own, but they can be read by other plugins to add functionality. After you load this plugin, edit `plugins/extralevelprops/_extralevelprops.txt` to define extra properties. This must be done in order to set them.

## Usage in plugins
At the top of your plugin source code, add:

`//pluginref _extralevelprops.dll`

Note that it is a comment. This is correct syntax. This will allow you to reference classes, namespaces, etc, from that plugin's dll.

Then use ExtraLevelProps namespace with a `using` statement:

`using ExtraLevelProps;`

This will enable all of the Level extension methods which you can call on an instance of a Level.

## Methods

"key" is the name of the property you are trying to get the value from. It is not case sensitive.

```CS
public static bool HasExtraProp(this Level level, string key)

//Example usage: does this level have the given property at all?
bool hasScriptProp = p.level.HasExtraProp("script");
```

```CS
public static string GetExtraPropString(this Level level, string key, string defaultValue = "")

//Example usage: which script this level should run by default if no script is provided.
//Default to level name if no "script" property has been set.
string currentLevelScriptName = p.level.GetExtraPropString("script", p.level.name);
```

```CS
public static bool GetExtraPropBool(this Level level, string key)

//Example usage: is this a minigame level or not?
//Defaults to false if no "minigame" property has been set or the property could not be parsed as a bool.
bool minigameLevel = p.level.GetExtraPropBool("minigame");
```

```CS
public static int GetExtraPropInt(this Level level, string key)

//Example usage: how many lives does the player start with in this map?
//Defaults to 0 if no "max_lives" property has been set or the property could not be parsed as an int.
int maxLives = p.level.GetExtraPropInt("max_lives");
```

```CS
public static bool SetExtraProp(this Level level, string key, string value)
//Set level properties through plugin code if you do not want to use or allow the use of /mapext
//Returns false if the property was removed (value is null, empty, or 0), otherwise true.
//This method will throw a System.ArgumentException if you attempt to set a property that has not been defined yet.
//This method will throw a System.ArgumentException if you pass a key or value with characters that are not included in
//the allowed character set: ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890._+,-

//Example usage: set the script this level should run by default if no script is provided.
p.level.SetExtraProp("script", "common_actions");
```

```CS
public static List<KeyValuePair<string, string>> AllExtraProps(this Level level)
//Get all of the extra properties of this level.
//Returns a list of KeyValuePairs from the level where .Key is the property name and .Value is the property value.
//If this level has never had any extra properties set, this method will return null.
//If this level has had extra properties set since it was loaded that have since been removed, this method will return an empty List.
//This collection is cached. It will not update if the level properties change after you retrieve it and
//editing values in this collection will not result in the level properties being edited.

//Example usage: display all of the current extra level props.
var kvps = p.level.AllExtraProps();
bool none = (kvps == null || kvps.Count == 0);
if (none) { p.Message("  no extra settings have been specified."); return; }

p.Message("&TExtra map settings:");
//sort alphabetically for aesthetics (you need using System.Linq; for this):
kvps.Sort((name1, name2) => string.Compare(name1.Key, name2.Key));

foreach (var kvp in kvps) {
    p.Message("  &6{0}&S: {1}", kvp.Key, kvp.Value);
}

```