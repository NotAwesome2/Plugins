using System;
using MCGalaxy.Events;
using MCGalaxy.Events.PlayerEvents;

namespace MCGalaxy {

	public class PluginKickOutdated : Plugin {
		public override string creator { get { return "Not Goodly"; } }
		public override string MCGalaxy_Version { get { return "1.9.4.9"; } }
		public override string name { get { return "KickOutdated"; } }

		public override void Load(bool startup) {
			OnPlayerFinishConnectingEvent.Register(DoKickOutdated, Priority.High);
		}

		public override void Unload(bool shutdown) {
			OnPlayerFinishConnectingEvent.Unregister(DoKickOutdated);
		}

		void Kick(Player p, string reason) {
			p.Leave(reason, true);
			p.cancelconnecting = true;
		}

		void DoKickOutdated(Player p) {
			if (Server.vip.Contains(p.name)) { return; }
			string app = p.Session.appName;
			const string updateMsg = "Please update with Launcher options -> Updates";
			if (app == null) { Kick(p, "Please use 'Enhanced' mode chosen from the launcher."); return; }

			if (app.StartsWith("ClassiCube Client")) { Kick(p, "Please download the new client from classicube.net."); return; }
			if (app.CaselessContains("ClassicalSharp")) { Kick(p, "Please download the new client from classicube.net."); }
			if (app.CaselessContains("viafabric")) { Kick(p, "This server is only compatible with ClassiCube client."); return; }

			if (!p.Supports(CpeExt.BlockDefinitions)) { Kick(p, "Please do not turn custom blocks off."); }

			if (!p.Supports(CpeExt.FastMap)) { Kick(p, "No Fastmap: " + updateMsg); }
			if (!p.Supports(CpeExt.ExtTextures)) { Kick(p, "No ExtTex: " + updateMsg); }
			if (!p.Supports(CpeExt.CustomModels, 2)) { Kick(p, "No CustomModel: " + updateMsg); }
			if (!p.Supports(CpeExt.ExtEntityTeleport)) { Kick(p, "No ExtTP: " + updateMsg); }
		}
	}
}
