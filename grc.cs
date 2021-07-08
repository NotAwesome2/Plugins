using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using MCGalaxy;
using MCGalaxy.Commands;
using MCGalaxy.Events.ServerEvents;
using MCGalaxy.Events.LevelEvents;
using MCGalaxy.Events.PlayerEvents;

namespace MCGalaxy {
	
	public sealed class GRC : Plugin {
		public override string name { get { return "grc"; } }
		public override string MCGalaxy_Version { get { return "1.9.0.5"; } }
		public override string creator { get { return "Goodly"; } }

		//"player" list is really a "map" list
		public static PlayerList coolMaps;
		public static Random rnd;
		public override void Load(bool startup) {
			coolMaps = PlayerList.Load("text/coolMaps.txt");
			rnd = new Random();
			
			Command.Register(new CmdGotoRandomCool());
			OnLevelDeletedEvent.Register(OnLevelDeleted, Priority.Low);
			OnLevelRenamedEvent.Register(OnLevelRenamed, Priority.Low);
		}
		
		public override void Unload(bool shutdown) {
			coolMaps.Save();
			Command.Unregister(Command.Find("GotoRandomCool"));
			OnLevelDeletedEvent.Unregister(OnLevelDeleted);
			OnLevelRenamedEvent.Unregister(OnLevelRenamed);
		}
		
		static void OnLevelDeleted(string map) {
		    coolMaps.Remove(map);
		}
		static void OnLevelRenamed(string srcMap, string dstMap) {
		    if (coolMaps.Remove(srcMap)) {
				coolMaps.Add(dstMap);
			}
			
		}
	}
	
	public class CmdGotoRandomCool : Command2
	{
		public override string name { get { return "GotoRandomCool"; } }
		public override string shortcut { get { return "grc"; } }
		public override string type { get { return "other"; } }
		public override bool museumUsable { get { return true; } }
		public override LevelPermission defaultRank { get { return LevelPermission.Guest; } }
        public override CommandPerm[] ExtraPerms {
            get { return new[] { new CommandPerm(LevelPermission.Operator, "can modify the list of maps that are deemed cool") }; }
        }
		public override void Use(Player p, string message, CommandData data)
		{	
			if (message.Length == 0) {
				List<string> actualNames = GRC.coolMaps.All();
				if (actualNames.Count == 0) { p.Message("There are currently no cool maps..."); return; }
				
				string mapName = actualNames[GRC.rnd.Next(actualNames.Count)];
				
				while (mapName == p.level.name) {
					if (actualNames.Count == 1) { p.Message("You are already in the only cool map."); return; }
					mapName = actualNames[GRC.rnd.Next(actualNames.Count)];
				}
				
				Command.Find("goto").Use(p, mapName);
				return;
			}
			
			if (message.CaselessEq("add")) {
				if (!CheckExtraPerm(p, data, 1)) { return; }
				if (GRC.coolMaps.Add(p.level.name)) {
					Chat.MessageFrom(p, p.ColoredName+"%S added %b"+p.level.name+"%S to the list of cool maps.", null);
					//p.Message("Added %b{0}%S to the list of cool maps!", p.level.name);
				} else {
					p.Message("%b{0}%S is already in the list of cool maps.", p.level.name);
				}
				return;
			}
			if (message.CaselessEq("remove")) {
				if (!CheckExtraPerm(p, data, 1)) { return; }
				if (GRC.coolMaps.Remove(p.level.name)) {
					Chat.MessageFrom(p, p.ColoredName+"%S removed %b"+p.level.name+"%S from the list of cool maps.", null);
					//p.Message("Removed %b{0}%S from the list of cool maps.", p.level.name);
				} else {
					p.Message("%b{0}%S is not in the list of cool maps. Therefore, it cannot be removed.", p.level.name);
				}
				return;
			}
			
			string[] words = message.SplitSpaces(2);
			if (words[0].CaselessEq("list")) {
				string modifier = words.Length > 1 ? words[1] : "";
				GRC.coolMaps.OutputPlain(p, "cool maps", "grc list", modifier);
				return;
			}
			
			p.Message("Use %T/grc %Swith no arguments to take yourself to a random map!");
		}
		public override void Help(Player p)
		{
            p.Message( "%T/GotoRandomCool");
            p.Message( "%H Takes you to a random map that has been deemed \"cool\".");
            p.Message( "%T/GotoRandomCool list");
            p.Message( "%H Displays a list of all the cool maps.");
            p.Message( "%T/GotoRandomCool [add/remove]");
            p.Message( "%H Updates the list of cool maps by adding or removing the map you're in.");
		}
	}
}
