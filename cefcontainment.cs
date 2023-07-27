//reference System.dll

using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MCGalaxy;
using MCGalaxy.Commands;
using MCGalaxy.Events.ServerEvents;
using MCGalaxy.Events.LevelEvents;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Modules.Relay.Discord;

namespace MCGalaxy {
    
    public sealed class PluginCefContainment : Plugin {
        
        static string[] allowedWebsites = new string[] { "https://www.youtube.com", "https://youtube.com", "https://youtu.be", "https://i.imgur.com" };
        
        public override string name { get { return "CefContainment"; } }
        public override string MCGalaxy_Version { get { return "1.9.4.9"; } }
        public override string creator { get { return "Goodly"; } }
        
        public override void Load(bool startup) {
            Command.Register(new CmdChat());
            OnPlayerChatEvent.Register(OnPlayerChat, Priority.High);
            OnPlayerCommandEvent.Register(OnPlayerCommand, Priority.Low);
        }
        
        public override void Unload(bool shutdown) {
            Command.Unregister(Command.Find("Chat"));
            OnPlayerChatEvent.Unregister(OnPlayerChat);
            OnPlayerCommandEvent.Unregister(OnPlayerCommand);
        }
        
        const string LOCAL_PREFIX = "$$";
        
        static void OnPlayerChat(Player p, string message) {
            bool local = message.StartsWith(LOCAL_PREFIX) || CmdChat.IsLocal(p);
            
            if (message.StartsWith(LOCAL_PREFIX)) {
                message = message.Substring(LOCAL_PREFIX.Length);
            }
            
            string stripped = Colors.Strip(message);
            if (stripped.StartsWith("cef ")) {
                if (!LevelInfo.Check(p, p.Rank, p.level, "use cef commands in this level")) { p.cancelchat = true; return; }
                
                string[] split = stripped.SplitSpaces(2);
                if (split.Length < 2) { return; }
                if (!AllowedCefCommand(p, split[1])) { p.cancelchat = true; return; }
                
                local = true;
            }
            
            
            string filteredMessage = message;
            bool hasSlur = ProfanityFilter.Parse(filteredMessage) != message;
            if (hasSlur) {
                p.cancelchat = true;
                if (message.CaselessContains("$skin") || message.CaselessContains("sskin")) {
                    p.Message("&cThe &b$&bskin &ctoken is blocked from chat to avoid confusing messages.");
                } else if (!message.CaselessEq("phag") && message.CaselessContains("phag") || message.CaselessContains("roofage")) {
                    p.Message("&cOops! &SYour message has triggered a false positive in our slur filter. Try rewording it.");
                } else {
                    p.Message("&cThere's better ways to get attention kiddo. Read &b/rules");
                }
                
                Chat.MessageOps("&STo Ops: "+p.name+" was blocked from saying \""+message+"\"");
                DiscordPlugin.Bot.SendStaffMessage("To Ops: "+p.name+" was blocked from saying \""+message+"\"");
                Logger.Log(LogType.SuspiciousActivity, "CEFcontainment: {0} was blocked from saying \"{1}\"", p.name, message);
            }
            
            
            if (local) { SendAsLocal(p, message); }
        }
        static void SendAsLocal(Player p, string message) {
            p.cancelchat = true;
            Chat.MessageChat(ChatScope.Level, p, "<Local>λFULL: &f" + message, p.level, null);
        }
        
        static bool HasCef(Player p) {
            return p.Session.appName != null && p.Session.appName.Contains("+ cef");
        }
        static void OnPlayerCommand(Player p, string cmd, string args, CommandData data) {
            if (!HasCef(p)) { return; }
            if (cmd.CaselessEq("pclients")) {
                //why would I do such a thing? to remove its usage from /last
                Command.Find("pclients").Use(p, args);
                p.cancelcommand = true;
            }
        }
        
        
        
        static string[] urlCommands = new string[] { "create", "play", "queue" };
        static bool AllowedCefCommand(Player p, string message) {
            
            string[] cefCommandArgs = message.SplitSpaces(2);
            string cefCommand = cefCommandArgs[0];
            if (cefCommand.CaselessEq("click")) { p.Message("&WSorry, cef click is disabled for security reasons."); return false; }
            
            if (cefCommandArgs.Length < 2) { return true; } //only one arg, okay to send since no url
            string cefArgs = cefCommandArgs[1];
            
            bool isUrlCommand = false;
            foreach (string command in urlCommands) {
                if (command.CaselessEq(cefCommand)) { isUrlCommand = true; break; }
            }
            if (!isUrlCommand) { return true; } //no URL commands, okay to send
            
            
            string[] args = cefArgs.SplitSpaces();
            
            //You can paste multiple urls in one command, check every single one just in case
            List<string> urls = new List<string>();
            foreach (string arg in args) {
                if (arg[0] == '-') { continue; } //flag, ignore
                urls.Add(arg);
            }
            if (urls.Count == 0) { return true; } //no urls, okay to send

            bool allowed = true;
            foreach (string url in urls) {
                if (url.CaselessContains("redirect")) { allowed = false; break; }
                
                bool whitelistMatch = false;
                foreach (string site in allowedWebsites) {
                    if (url.StartsWith(site) || IsYoutubeVideoID(url)) { whitelistMatch = true; break; }
                }
                if (!whitelistMatch) { allowed = false; }
            }
            
            if (!allowed) { p.Message("&WThe url {0} is not allowed in cef.", urls[0]); return false; }
            
            return true;
        }
        //cef can play videos from pasting only a video ID alone (thanks icanttellyou)
        static bool IsYoutubeVideoID(string s) {
            //(regex is from https://webapps.stackexchange.com/a/101153)
            return Regex.IsMatch(s, @"[0-9A-Za-z_-]{10}[048AEIMQUYcgkosw]");
        }
    }
    
    public class CmdChat : Command2
    {
        public override string name { get { return "Chat"; } }
        public override string shortcut { get { return ""; } }
        public override bool MessageBlockRestricted { get { return true; } }
        public override string type { get { return "other"; } }
        public override bool museumUsable { get { return false; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Admin; } }
        
        const string KEY = "CmdChat_IsLocal";
        
        const string LOCAL_FLAG = "local", GLOBAL_FLAG = "global";
        public override CommandAlias[] Aliases {
            get { return new[] {
                    new CommandAlias("LocalChat", LOCAL_FLAG), new CommandAlias("GlobalChat", GLOBAL_FLAG),
                    new CommandAlias("lc", LOCAL_FLAG), new CommandAlias("gc", GLOBAL_FLAG)
                }; }
        }
        
        public override void Use(Player p, string message, CommandData data)
        {
            if (message.CaselessEq(LOCAL_FLAG)) {
                if (IsLocal(p)) { p.Message("You are already in <Local> chat mode. Use &T/gc&S to turn it off."); return; }
                p.Extras[KEY] = true; p.Message("<Local> chat mode (for you) is now &aON&S.");
                return;
            }
            if (message.CaselessEq(GLOBAL_FLAG)) {
                if (!IsLocal(p)) { p.Message("You are already in normal chat mode. Use &T/lc&S to turn <Local> chat mode on."); return; }
                p.Extras.Remove(KEY); p.Message("<Local> chat mode (for you) is now &cOFF&S.");
                return;
            }
            p.Message("&cUse either &T/lc&c or &T/gc&c to turn on local or global chat mode.");
        }
        public static bool IsLocal(Player p) {
            if (!p.Extras.Contains(KEY)) { return false; }
            return (bool)p.Extras[KEY];
        }
        public override void Help(Player p)
        {
            p.Message("%T/LocalChat");
            p.Message("%HMakes &byour&H messages send to &S<Local>&H chat.");
            p.Message("To change the whole map to local chat, use /os map chat");
            p.Message("%T/GlobalChat");
            p.Message("%HUndoes the effects of %T/LocalChat");
        }
    }
}
