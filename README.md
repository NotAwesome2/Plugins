# Plugins
Plugins written for Not Awesome 2

### How do add them to my server?
- Place the .cs file in *plugins* folder.
- Use `/pcompile [plugin name]`
- Use `/pload [plugin name]`

You only need to do this once. It will load automatically in the future.

If you want to stop using the plugin, use `/punload [plugin name]`, then delete the plugin.dll file.

## writefont.cs
This plugin was originally (and graciously) written by UnknownShadow200 for Not Awesome 2.
In order for it to function, you must create the folder "extra/fonts" and place a font .png file inside. These font files are arranged like the ones that the client texture pack uses.
An example font (the font used in the webclient) has been provided in the writefont folder. See /help writefont for in-game usage.

## cefcontainment.cs
This plugin makes it so that only players who have permission to build in the map may use [cef](https://github.com/SpiralP/classicube-cef-loader-plugin) commands.
Additional features:
- Adds /chat command, allows you to send to local chat without being in a local chat map
- Prefixing message with $$ sends it to local chat
- Cef click is disabled for security reasons
- Messages containing bad words aren't sent at all. OPs are informed what word they tried to use.

## nocefdm.cs
Disables cef from sending automatic DMs. This prevents users from automatically syncing cef screens when they join a map, which could be used to bypass cefcontainment.

## grc.cs
This plugin adds /GotoRandomCool, which is like /gotorandom, but based on a curated list. The list can be managed with the /grc command.

## make.cs
This plugins adds /make and /makeGB, which allows you to quickly create shapes such as slabs, walls, and stairs, for the level you're in or the entire server.

Use `/help make` or `/help makegb` for more information.

## orderblocks.cs
This plugin makes ordering the block menu much easier. See `/help orderblocks` for usage. The top left of the inventory matches with 0, 0, 0 in the blockorder world. All blocks should have 1 block of air between them.

## tempbot.cs
This plugin is complimentary to [Not-Awesome-Script](https://github.com/NotAwesome2/Not-Awesome-Script). It allows you to create clientside instances of bots to manipulate with scripted movements. See /help tempbot for more details.

This plugin also comes with bonus /flipcoin and /movebots commands. See their /help for more info.

## ipnick.cs
This plugin introduces an "IP nickname" to players, which allows anyone to identify accounts by IP without seeing the actual IP address.

It is displayed in /whois and when the player logs in.

## nasgen.cs
This plugin adds [Not-Awesome-Survival](https://github.com/NotAwesome2/Nas) style gen as a /newlvl gen option.

## _extralevelprops.cs
This plugin adds the command `/mapext` which allows adding extra properties that are displayed in `/map`. These properties do not do anything on their own, but they can be read by other plugins to add functionality.

[Full documentation](documentation/_extralevelprops.md).

## kickoutdated.cs
This plugin will prevent players from connecting if they are not using enhanced mode or are outdated. A relevant kick message will be displayed to the player. Players who are VIP will not be kicked (see /help vip). 