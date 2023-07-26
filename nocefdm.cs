using System.Collections.Generic;
using MCGalaxy;
using MCGalaxy.Events.ServerEvents;

namespace MCGalaxy {

    public sealed class PluginNoCefDM : Plugin {
        public override string name { get { return "nocefdm"; } }
        public override string MCGalaxy_Version { get { return "1.9.4.5"; } }
        public override string creator { get { return "???"; } }

        public override void Load(bool startup) {

            OnChatEvent.Register(HandleChat, Priority.High);
        }

        public override void Unload(bool shutdown) {


            OnChatEvent.Unregister(HandleChat);
        }

        void HandleChat(ChatScope scope, Player source, string msg, object arg, ref ChatMessageFilter filter, bool irc) {
            if (!IsCefMessage(msg)) { return; }
            filter = (pl, xyz) => pl == source;
            
            //source.Message("&eMESSAGE NOT DELIVERED.");
            //source.Message("&WDirect messages are temporarily disabled on this server. Sorry!");
        }
        

        static bool IsCefMessage(string message) {
            return message.Contains("?CEF?") || message.Contains("!CEF!");
        }
        
        //static bool IsDM(string message) {
        //    if (message.Contains("[<]") || message.Contains("[>]")) { return true; }
        //    return false;
        //}
    }
}
