using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using MCGalaxy;
using MCGalaxy.Events.ServerEvents;
using MCGalaxy.Events.LevelEvents;
using MCGalaxy.Events.PlayerEvents;

namespace MCGalaxy {
    
    public sealed class PluginCefContainment : Plugin {
        public override string name { get { return "CefContainment"; } }
        public override string MCGalaxy_Version { get { return "1.9.0.5"; } }
        public override string creator { get { return "Goodly"; } }
        
        public override void Load(bool startup) {
            OnPlayerChatEvent.Register(RightBeforeChat, Priority.High);
        }
        
        public override void Unload(bool shutdown) {
            OnPlayerChatEvent.Unregister(RightBeforeChat);
        }
        
        static void RightBeforeChat(Player p, string message) {
            
            if (Colors.Strip(message).StartsWith("cef ")) {
                p.cancelchat = true;
                if (!LevelInfo.Check(p, p.Rank, p.level, "use cef commands in this level.")) return;
                Chat.MessageFromLevel(p, "<Local>λFULL: &f" +message);
                return;
            }

        }
    }
}
