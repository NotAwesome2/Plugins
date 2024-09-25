# _extralevelprops.cs
This plugin adds the command `/mapext` which allows adding extra properties that are displayed in `/map`. These properties do not do anything on their own, but they can be read by other plugins to add functionality.

After you load this plugin and another plugin that uses it, you may edit `plugins/extralevelprops/_extralevelprops.txt` to change the the rank permission needed to set properties.

## 1. How to use _extralevelprops class members in your plugin

At the top of your plugin source code, add:

`//pluginref _extralevelprops.dll`

Note that it is a comment. This is correct syntax. This will allow you to reference classes, namespaces, etc, from that plugin's dll.

Then use ExtraLevelProps namespace with a `using` statement:

`using ExtraLevelProps;`

This will enable all of the Level extension methods which you can call on an instance of a Level.

## 2. How to define a property in your plugin

In your plugin's Load method:
- call `ExtraLevelProps.ExtraLevelProps.Register`

Example:
```CS
const string NO_BOTS_PROP = "nobots";
static string[] noBotsDesc = new string[] {
    "[true/false]",
    "Disallows the creation of bots.", };

public override void Load(bool startup) {
    //"name" is the name of the plugin.
    ExtraLevelProps.ExtraLevelProps.Register(name, NO_BOTS_PROP, LevelPermission.Guest, noBotsDesc, ExtraLevelProps.ExtraLevelProps.OnPropChangingBool);
}
```

In your plugin's Unload method:
- call `ExtraLevelProps.ExtraLevelProps.Unregister`

Example:
```CS
public override void Unload(bool shutdown) {
    ExtraLevelProps.ExtraLevelProps.Unregister(NO_BOTS_PROP);
}
```

The Register method may throw an ArgumentException if another plugin is already using the prop you're trying to define.

Make sure to handle that gracefully by adding a try-catch to exit the plugin Load or make sure that it's the first line of code you call to ensure nothing else is left dangling.

### Methods

```CS
public static void Register(string pluginName, string propName, LevelPermission defaultPermission, string[] propDesc, OnPropChanging onPropChanging, DisplayValue displayValue = null);
```
- `pluginName` -
The name of the plugin you're calling this from. You can just pass `name`. This helps provide feedback to the user if the plugin fails to load.
- `propName` - The name of the property you're adding. This may not contain spaces and is limited to the allowed character set for props. See `SetExtraProp` comments for details.
- `defaultPermission` - The default lowest rank that is allowed to set this permission using `/mapext`. LevelPermission.Guest is recommended.
- `propDesc` - An array of lines that will be displayed to the player using `/help mapext [prop]`. The first line should describe the input format like `[true/false]` and the following lines should describe what the property does.
- `onPropChanging` - The method that will be called when a player changes this property. You may pass null if you do not need to do anything special when the property is changed. See below for the delegate that describes the required function signature (arguments).
- `displayValue` - The optional method that will be called when this property is displayed using `/map`. See below for the delegate that describes the required function signature (arguments).

```CS
public static void Unregister(string propName);
```
- `propName` -
The name of the prop you're unregistering. This should be the same as the one from Register. Make sure not to forget to unregister this when you unload the plugin, otherwise bad things can happen.

### Delegates
```CS
public delegate void OnPropChanging(Player p, Level level, ref string value, ref bool cancel);
```
- `p` - The player who is changing this property.
- `level` - The level this property is being changed in
- `value` - The value that the property is about to be changed to. Because this is passed by ref, your plugin can modify this value before it is changed.
- `cancel` - Set this boolean to true if you want to prevent the prop from being changed. Tip: provide feedback for why the prop change was cancelled using `p.Message`

```CS
public delegate string DisplayValue(Player p, Level level, string value);
```
- `p` - The player this value is being displayed to.
- `level` - The level this property is set in.
- `value` - The value that is about to be displayed. To change this, your function should return a string based on this passed in value.

## 3. How to get a property in your plugin

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

## 4. Misc methods

```CS
public static bool SetExtraProp(this Level level, string key, string value)
//Set level properties through plugin code if you do not want to use or allow the use of /mapext
//Returns false if the property was removed (value is null, empty, or 0), otherwise true.
//This method will throw a System.ArgumentException if you attempt to set a property that has not been defined yet.
//This method will throw a System.ArgumentException if you pass a key or value with characters that are not included in
//the allowed character set: ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890._+,-/

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

```CS
public static void OnPropChangingBool(Player p, Level level, ref string value, ref bool cancel);
//This is a convenience method that you can pass to
//ExtraLevelProps.ExtraLevelProps.Register onPropChanging parameter
//to ensure that the property is treated like a true/false boolean.
//Remember that you need to include the double prefix:
//ExtraLevelProps.ExtraLevelProps.OnPropChangingBool
```