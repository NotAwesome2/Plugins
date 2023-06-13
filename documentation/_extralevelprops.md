# _extralevelprops.cs
This plugin adds the command `/mapext` which allows adding extra properties that are displayed in `/map`. These properties do not do anything on their own, but they can be read by other plugins to add functionality. After you load this plugin, you can use *plugins/extralevelprops/_verifiedprops.txt* to define which extra properties can be set by players.

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
public static void SetExtraProp(this Level level, string key, string value)
//Set level properties through plugin code if you do not want to use or allow the use of /mapext

//Example usage: set the script this level should run by default if no script is provided.
p.level.SetExtraProp("script", "common_actions");

//This method will throw a System.ArgumentException if you pass a key or value with characters that are not included in
//the allowed character set: ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890._
```