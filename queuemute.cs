using System;
using System.Collections.Generic;
using System.IO;
using MCGalaxy;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Modules.Relay.Discord;
using MCGalaxy.Tasks;

namespace NA2 {
    public sealed class Core : Plugin {
        
        public override string name { get { return "queuemute"; } }
        public override string MCGalaxy_Version { get { return "1.9.5.0"; } }
        public override string creator { get { return "Goodly"; } }
        
        static Command[] cmds = new Command[] { new CmdQueueMute(), };
        
        public override void Load(bool startup) {
            OnPlayerFinishConnectingEvent.Register(QueuedMute.OnPlayerFinishConnecting, Priority.Normal);
            OnPlayerCommandEvent.Register(OnPlayerCommand, Priority.Normal);

            foreach (Command cmd in cmds) { Command.Register(cmd); }

            QueuedMute.LoadFromDisc();
        }
        public override void Unload(bool shutdown) {
            OnPlayerFinishConnectingEvent.Unregister(QueuedMute.OnPlayerFinishConnecting);
            OnPlayerCommandEvent.Unregister(OnPlayerCommand);

            foreach (Command cmd in cmds) { Command.Unregister(cmd); }

            QueuedMute.WriteToDisc();
        }

        static void OnPlayerCommand(Player p, string cmd, string message, CommandData data) {
            if (cmd.CaselessEq("mute")) {
                const string UnmuteFlag = "-unmute ";
                if (message.StartsWith(UnmuteFlag)) {
                    HandleUnmute(p, message.Substring(UnmuteFlag.Length), data);
                    return;
                }
                HandleMute(p, message, data);
                return;
            }
            if (cmd.CaselessEq("notes")) { HandleNotes(p, message, data); }
        }
        static void HandleNotes(Player p, string message, CommandData data) {
            string target = PlayerInfo.FindMatchesPreferOnline(Player.Console, message);
            if (target == null) return;

            QueuedMute mute = QueuedMute.GetMute(target);
            if (mute == null) return;

            p.cancelcommand = true;
            Command.Find("notes").Use(p, message, data);
            p.Message("{0}&S has a mute queued by {1} for &b{2}&S: {3}", p.FormatNick(target), mute.muter, mute.time, mute.reason);
        }
        static void HandleMute(Player p, string message, CommandData data) {
            if (message.Length == 0) return;

            string[] words = message.SplitSpaces(2);
            if (words.Length != 2) return;

            string muted = words[0];
            int matches;
            _ = PlayerInfo.FindMatches(Player.Console, muted, out matches);
            if (matches != 0) return;

            p.HandleCommand(cmds[0].name, message, data);
            p.cancelcommand = true; //After p.HandleCommand because it sets cancelcommand to false
        }
        static void HandleUnmute(Player p, string message, CommandData data) {
            if (message.Length == 0) return;

            string arg = message.SplitSpaces(2)[0];

            string unmuted = PlayerInfo.FindMatchesPreferOnline(Player.Console, arg);
            if (unmuted == null) return;
            
            if (QueuedMute.Unqueue(unmuted)) {
                CmdQueueMute.OpMessage("{0}&S removed {1}'s queued mute.", p.DisplayName, unmuted);
            }
        }
    }
    
    public class CmdQueueMute : Command2 {
        public override string name { get { return "QueueMute"; } }
        public override string shortcut { get { return "qm"; } }
        public override bool MessageBlockRestricted { get { return true; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override bool museumUsable { get { return false; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }

        public static void OpMessage(string message, params object[] args) {
            
            message = "To Ops: " + string.Format(message, args);

            //You can send these to your op channel if you change DiscordOpChannel ID and remove/edit the server name check.
            if (Server.Config.Name.StartsWith("Not Awesome 2")) {
                const string DiscordOpChannel = "935380096750088194";
                ChannelSendMessage discordMessage = new ChannelSendMessage(DiscordOpChannel, "**" + Colors.Strip(message) + "**");
                DiscordPlugin.Bot.Send(discordMessage);
            }

            Chat.MessageOps(message);
        }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { Help(p); return; }

            QueuedMute mute;
            try {
                mute = new QueuedMute(p, message);
            } catch (ArgumentException e) {
                p.Message("&W{0}", e.Message);
                return;
            }

            mute.Enqueue();
            Pronouns pros = Pronouns.GetFor(mute.muted)[0];
            OpMessage("{0}&S queued a mute for {1} for &b{2}&S the next time {3} log{4} in: {5}",
                p.DisplayName, mute.muted, mute.time, pros.Subject, pros.Plural ? "" : "s", mute.reason);
        }

        public override void Help(Player p) {
            p.Message("&T/{0} [player] [timespan] [reason]", name);
            p.Message("&HQueues a mute to be applied the next time [player] logs in.");
            p.Message("&HTo remove a queued mute, use &T/unmute [player]");
            p.Message("&HTo modify a queued mute, repeat this command.");
            p.Message("&HUse &T/notes [player] &Hto see their queued mute.");
        }
    }

    public class QueuedMute {

        const string Folder = "plugins/queuemute/";
        const string MutesFile = Folder + "queuedmutes.txt";

        static readonly Dictionary<string, QueuedMute> mutes = new Dictionary<string, QueuedMute>();
        static readonly object locker = new Object();


        public static void LoadFromDisc() {
            lock (locker) {
                if (!Directory.Exists(Folder)) {
                    Directory.CreateDirectory(Folder);
                }
                if (!File.Exists(MutesFile)) {
                    return;
                }

                string[] lines = File.ReadAllLines(MutesFile);
                foreach (string line in lines) {
                    try {
                        QueuedMute mute = new QueuedMute(line);
                        mutes[mute.muted] = mute;
                    } catch (ArgumentException e) {
                        Logger.LogError(e);
                    }
                }

            }
        }
        public static void WriteToDisc() {
            lock (locker) {
                if (!Directory.Exists(Folder)) {
                    Directory.CreateDirectory(Folder);
                }

                List<string> lines = new List<string>(mutes.Count);
                foreach (KeyValuePair<string, QueuedMute> pair in mutes) {
                    lines.Add(pair.Value.ToString());
                }
                File.WriteAllLines(MutesFile, lines);
            }
        }
        public static QueuedMute GetMute(string playerName) {
            lock (locker) {
                QueuedMute mute;
                mutes.TryGetValue(playerName, out mute);
                return mute;
            }
        }
        public static bool Unqueue(string playerName) {
            lock (locker) {
                return mutes.Remove(playerName);
            }
        }
        public static void OnPlayerFinishConnecting(Player p) {
            Server.MainScheduler.QueueOnce(_OnPlayerFinishConnecting, p, TimeSpan.FromSeconds(5));
        }
        static void _OnPlayerFinishConnecting(SchedulerTask task) {
            Player p = (Player)task.State;

            QueuedMute mute;
            lock (locker) {
                if (!mutes.TryGetValue(p.name, out mute)) return;
                Command.Find("mute").Use(Player.Console, string.Format("{0} {1} Muted by {2}: {3}", mute.muted, mute.time, mute.muter, mute.reason));
                mutes.Remove(p.name);
            }
        }

        public readonly string muter;
        public readonly string muted;
        public readonly string time;
        public readonly string reason;
        public QueuedMute(Player p, string message) {
            muter = p.name;
            string[] args = message.SplitSpaces(3);
            if (args.Length != 3) throw new ArgumentException("You must provide name, mute time, and mute reason.");

            muted = PlayerInfo.FindMatchesPreferOnline(p, args[0]);
            if (muted == null) throw new ArgumentException("Could not figure out who to mute.");
            time = args[1];
            reason = args[2];
        }
        public QueuedMute(string line) {
            string[] bits = line.SplitSpaces(4);
            if (bits.Length != 4) throw new ArgumentException(string.Format("Not enough arguments to generate queued mute from \"{1}\"", line));

            muter = bits[0];
            muted = bits[1];
            time = bits[2];
            reason = bits[3];
        }
        public void Enqueue() {
            lock (locker) {
                mutes[this.muted] = this;
            }
        }
        public override string ToString() {
            return string.Format("{0} {1} {2} {3}", muter, muted, time, reason);
        }
    }
}
