using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using MCGalaxy;
using MCGalaxy.Commands;
using MCGalaxy.Commands.Chatting;
using MCGalaxy.Blocks;
using MCGalaxy.Events.ServerEvents;
using MCGalaxy.Events.LevelEvents;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Events.EntityEvents;
using BlockID = System.UInt16;

using MCGalaxy.Network;
using MCGalaxy.Maths;
using MCGalaxy.Tasks;
using MCGalaxy.DB;

//unknownshadow200: well player ids go from 0 up to 255. normal bots go from 127 down to 64, then 254 down to 127, then finally 63 down to 0.

namespace MCGalaxy {
    
    public sealed class CmdTempBot : Command2 {   
        public override string name { get { return "tempbot"; } }
        public override string shortcut { get { return "tbot"; } }
        public override string type { get { return CommandTypes.Other; } }
        public override bool museumUsable { get { return false; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }

        public override void Use(Player p, string message, CommandData data) {		
            if (message.CaselessEq("where")) {
                //-16 so it's centered on the block instead of min corner
                Vec3F32 pos = new Vec3F32(p.Pos.X-16, p.Pos.Y - Entities.CharacterHeight, p.Pos.Z-16);
                pos.X /= 32;
                pos.Y /= 32;
                pos.Z /= 32;
    			
    			p.Send(Packet.Message(
    			    pos.X + " " + pos.Y + " " + pos.Z+" "+Orientation.PackedToDegrees(p.Rot.RotY)+" "+Orientation.PackedToDegrees(p.Rot.HeadX),
    			                      CpeMessageType.Normal, p.hasCP437));
    			return;
            }
            
			if (!(p.group.Permission >= LevelPermission.Operator)) {
				if (!Hacks.CanUseHacks(p)) {
					if (data.Context != CommandContext.MessageBlock) {
						p.Message("%cYou cannot use this command manually when hacks are disabled.");
						return;
					}
				}
			}
            if (message.Length == 0) { Help(p); return; }
            string[] args = message.SplitSpaces(2);
            
            
            if (args[0].CaselessEq("add")) {
                if (args.Length < 2) { p.Message("%cYou need args for botName, X, Y, Z, yaw, and pitch. Skin and botNick are optional."); return; }
                TryAdd(p, args[1]);
                return;
            }
            
            if (args[0].CaselessEq("remove")) {
                if (args.Length < 2) { p.Message("%cYou must provide a botName to remove."); return; }
                TryRemove(p, args[1]);
                return;
            }
            
            if (args[0].CaselessEq("model")) {
                if (args.Length < 2) { p.Message("%cYou need args for botName and modelName."); return; }
                TrySetModel(p, args[1]);
                return;
            }
            
            if (args[0].CaselessEq("ai")) {
                if (args.Length < 2) { p.Message("%cYou need args for botName and AIName."); return; }
                TrySetAIName(p, args[1]);
                return;
            }
            
            if (args[0].CaselessEq("summon")) {
                if (args.Length < 2) { p.Message("%cYou need args for botName, X, Y, Z, yaw, and pitch."); return; }
                TryTP(p, args[1]);
                return;
            }
            
            Help(p);
            
        }
        void TryAdd(Player p, string message) {
                string[] args = message.SplitSpaces(8);
                if (args.Length < 6) { p.Message("%cYou need args for botName, X, Y, Z, yaw, and pitch. Skin and botNick are optional."); return; }
                
                string botName    = args[0];
                if (!Formatter.ValidName(p, botName, "bot")) return;
                float x = 0, y = 0, z = 0;
                int yaw = 0, pitch = 0;
                string skin       = (args.Length < 7) ? botName : args[6];
                string botNick    = (args.Length < 8) ? botName : args[7];
                //p.Rot.
                if (!CommandParser.GetReal(p, args[1], "x position", ref x)) { return; }
                if (!CommandParser.GetReal(p, args[2], "y position", ref y)) { return; }
                if (!CommandParser.GetReal(p, args[3], "z position", ref z)) { return; }
                if (!CommandParser.GetInt(p,  args[4], "yaw", ref yaw)) { return; }
                if (!CommandParser.GetInt(p,  args[5], "pitch", ref pitch)) { return; }
                Add(p, botName, x, y, z, yaw, pitch, skin, botNick);
        }
        
        void Add(Player p, string botName, float x, float y, float z, int yaw, int pitch, string skin, string botNick = "empty") {
            if (!PluginTempBot.tempBotListAtPlayerName.ContainsKey(p.name)) {
				PluginTempBot.tempBotListAtPlayerName[p.name] = new TempBotList();
				p.Extras["TempBot_BotList"] = PluginTempBot.tempBotListAtPlayerName[p.name].botList;
			}
			
            
            foreach (PlayerBot b in PluginTempBot.tempBotListAtPlayerName[p.name].botList) {
                if (botName.CaselessEq(b.name)) {
                    //p.Message("%cThere is already a tempbot with that name.");
                    return;
                }
            }
            
            byte ID = PluginTempBot.NextFreeID(p);
            if (ID == 0) {
                p.Message("%cReached the limit of tempbots allowed.");
                return;
            }
            PluginTempBot.tempBotListAtPlayerName[p.name].usedIDs[ID - PluginTempBot.botIDstartValue] = true;
            
            PlayerBot bot = new PlayerBot(botName, p.level);
            bot.DisplayName = botNick;
            //+16 so that it's centered on the block instead of min corner
            Position pos = Position.FromFeet((int)(x*32) +16, (int)(y*32), (int)(z*32) +16);
            
            bot.SetInitialPos(pos);
            byte byteYaw = Orientation.DegreesToPacked(yaw);
            byte bytePitch = Orientation.DegreesToPacked(pitch);
            bot.SetYawPitch(byteYaw, bytePitch);
            bot.SkinName = skin;
            //bot.AIName = "stare";
            bot.id = ID;

            //p.Message("Picked {0} as ID for {1}", bot.id, bot.name);
            PluginTempBot.tempBotListAtPlayerName[p.name].botList.Add(bot);
            
            Entities.Spawn(p, bot);
        }
        
        void TryRemove(Player p, string message) {
            string[] args = message.Split(' ');
            Remove(p, args[0]);
        }
        
        public static void Remove(Player p, string botName) {
            PlayerBot bot = GetBotAtName(p, botName);
            if (bot != null) {
                Entities.Despawn(p, bot);
                //p.Message("Successfully removed {0} with ID of {1}", bot.name, bot.id);
                //p.Message("ID {0} is now freed from index {1}", bot.id, bot.id - PluginTempBot.botIDstartValue);
                
                PluginTempBot.tempBotListAtPlayerName[p.name].usedIDs[bot.id - PluginTempBot.botIDstartValue] = false;
                PluginTempBot.tempBotListAtPlayerName[p.name].botList.Remove(bot);
            }
        }
        
        void TrySetModel(Player p, string message) {
            string[] args = message.SplitSpaces(2);
            if (args.Length < 2) { p.Message("%cYou need args for botName and modelName."); return; }
            
            PlayerBot bot = GetBotAtName(p, args[0]);
            if (bot != null) {
                SetModel(p, bot, args[1]);
            }

        }
        public static void SetModel(Player p, PlayerBot bot, string modelName) {
            bot.Model = modelName;
            if (p.Supports(CpeExt.ChangeModel)) {
				OnSendingModelEvent.Call(bot, ref modelName, p);
                p.Send(Packet.ChangeModel(bot.id, modelName, p.hasCP437));
            }
        }
        
        void TryTP(Player p, string message) {
            string[] args = message.Split(' ');
            if (args.Length < 6) { p.Message("%cYou need args for botName, X, Y, Z, yaw, and pitch."); return; }
            PlayerBot bot = GetBotAtName(p, args[0]);
            if (bot != null) {
                float x = 0, y = 0, z = 0;
                int yaw = 0, pitch = 0;
                if (!CommandParser.GetReal(p, args[1], "x position", ref x)) { return; }
                if (!CommandParser.GetReal(p, args[2], "y position", ref y)) { return; }
                if (!CommandParser.GetReal(p, args[3], "z position", ref z)) { return; }
                if (!CommandParser.GetInt(p,  args[4], "yaw", ref yaw)) { return; }
                if (!CommandParser.GetInt(p,  args[5], "pitch", ref pitch)) { return; }
                TPBot(p, bot, x, y, z, yaw, pitch);
            }
        }
        public static void TPBot(Player p, PlayerBot bot, float x, float y, float z, int yaw, int pitch) {
            Position pos = Position.FromFeet((int)(x*32) +16, (int)(y*32), (int)(z*32) +16);
            bot.Pos = pos;
            
            //looks weird if their head snaps then instantly looks at you again...
            if (!bot.AIName.CaselessStarts("stare")) {
                byte byteYaw = Orientation.DegreesToPacked(yaw);
                byte bytePitch = Orientation.DegreesToPacked(pitch);
                Orientation rot = bot.Rot;
                rot.HeadX = bytePitch;
                rot.RotY = byteYaw;
                bot.Rot = rot;
            }
            
            p.Send(Packet.Teleport(bot.id, bot.Pos, bot.Rot, p.Supports(CpeExt.ExtEntityPositions)));
        }
        
        void TrySetAIName(Player p, string message) {
            string[] args = message.SplitSpaces(2);
            if (args.Length < 2) { p.Message("%cYou need args for botName and AIName"); return; }
            PlayerBot bot = GetBotAtName(p, args[0]);
            if (bot != null) {
                SetAIName(p, bot, args[1]);
            }
        }
        void SetAIName(Player p, PlayerBot bot, string AIName) {
            bot.AIName = AIName;
        }
        
        static PlayerBot GetBotAtName(Player p, string botName) {
            TempBotList list;
            if (!PluginTempBot.tempBotListAtPlayerName.TryGetValue(p.name, out list) || list.botList.Count == 0)
            {
                //p.Message("No tempbots currently exist.");
                return null;
            }
            foreach (PlayerBot bot in list.botList) {
                if (bot.name.CaselessEq(botName)) {
                    return bot;
                }
            }
            //p.Message("No tempbots match \"{0}\".", botName);
            return null;
        }
		
        
        public override void Help(Player p) {
            p.Message("%T/TempBot add [botName] [x y z] [yaw pitch] <skin> <botNick>");
            p.Message("%H Places a client-side bot.");
            p.Message("%T/TempBot summon [botName] [x y z] [yaw pitch]");
            p.Message("%H Summons a client-side bot.");
            p.Message("%T/TempBot remove [botName]");
            p.Message("%H Removes a client-side bot.");
            p.Message("%T/TempBot model [botName] [model name]");
            p.Message("%H Sets model of a client-side bot.");
            p.Message("%T/TempBot ai [botName] [ai arguments]");
            p.Message("%H Sets ai. Use %T/help tempbot ai %Hfor more info.");
            p.Message("%T/TempBot where %H- puts your current X, Y, Z, yaw and pitch into chat for copy pasting.");
        }
        
        public override void Help(Player p, string message) {
            if (message.CaselessEq("ai")) {
                p.Message("%HValid AI actions for tempbots:");
                p.Message("%Tstare <time in tenths of a second>");
                p.Message("%H No time provided means stare forever.");
                p.Message("%Twait [time]");
                p.Message("%H Waits for [time in tenths of a second]");
                p.Message("%Tmove [X Y Z] <speed>");
                p.Message("%H <speed> is optional, default is 14.");
                p.Message("%Tsummon [X Y Z] [yaw pitch]");
                p.Message("%H Instantly teleports this tempbot.");
                p.Message("%Tmodel [modelname]");
                p.Message("%H Sets model.");
                p.Message("%Tmsg [message]");
                p.Message("%H You can't use commas in the message!");
                p.Message("%H Use ; instead and it will become ,");
                p.Message("%Tremove");
                p.Message("%H Removes this tempbot.");
                p.Message("%HYou can chain AI together with commas. e.g:");
                p.Message("%Hstare 10,wait 20,move 32 64 30");
                p.Message("%HPlease note the (lack of) spaces.");
                return;
            }
            Help(p);
        }
		
    }
    
    public class TempBotList {
        public List<PlayerBot> botList = new List<PlayerBot>();
        public bool[] usedIDs = new bool[64];
    }
	
	public sealed class PluginTempBot : Plugin {
		public override string name { get { return "tempbot"; } }
		public override string MCGalaxy_Version { get { return "1.9.0.5"; } }
		public override string creator { get { return "goodly"; } }
		
		public const byte botIDstartValue = 128;
		public static Dictionary<string, TempBotList> tempBotListAtPlayerName = new Dictionary<string, TempBotList>();
		
		public static byte NextFreeID(Player p) {
		    for (int i = 0; i < tempBotListAtPlayerName[p.name].usedIDs.Length; i++) {
		        if (!tempBotListAtPlayerName[p.name].usedIDs[i]) {
		            return (byte)(i + botIDstartValue);
		        }
		    }
		    return 0;
		}
		
		Command tempbotCmd;
		Command flipcoinCmd;
		Command moveBotsCmd;
		public override void Load(bool startup) {
			
			tempbotCmd = new CmdTempBot();
			flipcoinCmd = new CmdFlipCoin();
			moveBotsCmd = new CmdMoveBots();
			Command.Register(tempbotCmd);
			Command.Register(flipcoinCmd);
			Command.Register(moveBotsCmd);
			
			OnPlayerDisconnectEvent.Register(HandleDisconnect, Priority.High);
			OnSentMapEvent.Register(HandleSentMap, Priority.High);
			
			Activate();
		}
		
		public override void Unload(bool shutdown) {
		    Command.Unregister(tempbotCmd);
		    Command.Unregister(flipcoinCmd);
		    Command.Unregister(moveBotsCmd);
			
			OnPlayerDisconnectEvent.Unregister(HandleDisconnect);
			OnSentMapEvent.Unregister(HandleSentMap);
			
			tempBotListAtPlayerName.Clear();
			
			instance.Cancel(tickBots);
		}
		
		
		static void HandleDisconnect(Player p, string reason) {
			tempBotListAtPlayerName.Remove(p.name);
			
		}
		static void HandleSentMap(Player p, Level prevLevel, Level level) {
			tempBotListAtPlayerName.Remove(p.name);

		}
		
		
        static Scheduler instance;
        static SchedulerTask tickBots;
        public static void Activate() {
            if (instance == null) instance = new Scheduler("TempBotsScheduler");
            
            tickBots = instance.QueueRepeat(BotsTick, null, TimeSpan.FromMilliseconds(100));
        }
        
        static void BotsTick(SchedulerTask task) {
            
            foreach (Player p in PlayerInfo.Online.Items) {
                if (!tempBotListAtPlayerName.ContainsKey(p.name)) { continue; }
                
                List<PlayerBot> bots = tempBotListAtPlayerName[p.name].botList;
                if (bots.Count == 0) { continue; }
                for (int j = 0; j < bots.Count; j++) { BotTick(p, bots[j]); }
            }
        }

        static void BotTick(Player p, PlayerBot bot) {
            //p.Message("Oh lawd %1{0}%S is ticking!", bot.name);
            if (bot.AIName.CaselessStarts("dice")) {
                DoDiceFall(p, bot);
                return;
            }
            if (bot.AIName.CaselessStarts("stare")) {
                DoStare(p, bot);
                return;
            }
            if (bot.AIName.CaselessStarts("wait")) {
                DoWait(p, bot);
                return;
            }
            if (bot.AIName.CaselessStarts("move")) {
                DoMove(p, bot);
                return;
            }
            if (bot.AIName.CaselessStarts("model")) {
                DoModel(p, bot);
                return;
            }
            if (bot.AIName.CaselessStarts("remove")) {
                CmdTempBot.Remove(p, bot.name);
                return;
            }
            if (bot.AIName.CaselessStarts("summon")) {
                DoTP(p, bot);
                return;
            }
            if (bot.AIName.CaselessStarts("message") || bot.AIName.CaselessStarts("msg")) {
                DoMesssage(p, bot);
                return;
            }
            if (bot.AIName.CaselessStarts("runscript")) {
                DoRunScript(p, bot);
                return;
            }
            if (string.IsNullOrEmpty(bot.AIName)) { return; }
            
            string instruction;
            int charIndex = bot.AIName.IndexOf(',');
            if (charIndex == -1) {
                instruction = bot.AIName;
            } else {
                instruction = bot.AIName.Substring(0, charIndex);
            }
            
            if (bot.AIName.StartsWith(" ")) {
                p.Message("%cUnrecognized bot ai \"%S{0}%c\". Did you accidentally put a space after a comma separator?", instruction);
            } else {
                p.Message("%cUnrecognized bot ai \"%S{0}%c\".", instruction);
            }
            bot.AIName = "";
        }
        
        static void DoRunScript(Player p, PlayerBot bot) {
            string instruction;
            string trailingInstructions = "";
            int charIndex = bot.AIName.IndexOf(',');
            
            if (charIndex == -1) {
                instruction = bot.AIName;
            } else {
                instruction = bot.AIName.Substring(0, charIndex);
                trailingInstructions = bot.AIName.Substring(charIndex+1);
            }
            
            
            string[] bits = instruction.SplitSpaces(2);
            string runscriptArgs = "DEFAULT ARGS";
            if (bits.Length > 1) {
                runscriptArgs = bits[1];
            } else {
                p.Message("%cNo arguments were provided for runscript.");
            }
            
            bot.AIName = trailingInstructions;
            Command.Find("runscript").Use(p, runscriptArgs);
        }
        
        static void DoMesssage(Player p, PlayerBot bot) {
            string instruction;
            string trailingInstructions = "";
            int charIndex = bot.AIName.IndexOf(',');
            
            if (charIndex == -1) {
                instruction = bot.AIName;
            } else {
                instruction = bot.AIName.Substring(0, charIndex);
                trailingInstructions = bot.AIName.Substring(charIndex+1);
            }
            
            
            string[] bits = instruction.SplitSpaces(2);
            string message = "DEFAULT TEMPBOT MESSAGE";
            if (bits.Length > 1) {
                message = bits[1];
            } else {
                p.Message("%cNo arguments were provided for message.");
            }
            
            bot.AIName = trailingInstructions;
            p.Message(message.Replace(';', ','));
        }
        
        static void DoTP(Player p, PlayerBot bot) {
            string instruction;
            string trailingInstructions = "";
            int charIndex = bot.AIName.IndexOf(',');
            
            if (charIndex == -1) {
                instruction = bot.AIName;
            } else {
                instruction = bot.AIName.Substring(0, charIndex);
                trailingInstructions = bot.AIName.Substring(charIndex+1);
            }
            
            float x = 0, y = 0, z = 0;
            int yaw = 0, pitch = 0;
            bool actuallySummon = true;
            string[] bits = instruction.SplitSpaces(2);
            if (bits.Length > 1) {
                string[] args = bits[1].Split(' ');
                if (args.Length >= 5) {
                    if (!(
                        float.TryParse(args[0], out x) &&
                        float.TryParse(args[1], out y) &&
                        float.TryParse(args[2], out z) &&
                        Int32.TryParse(args[3], out yaw) &&
                        Int32.TryParse(args[4], out pitch)
                       )) {
                        actuallySummon = false;
                        p.Message("%cCould not parse one or more arguments of summon.");
                    }
                    
                } else {
                    actuallySummon = false;
                    p.Message("%cNot enough arguments provided for summon.");
                }
                
                
            } else {
                actuallySummon = false;
                p.Message("%cNo arguments were provided for summon.");
            }
            
            bot.AIName = trailingInstructions;
            if (actuallySummon) { CmdTempBot.TPBot(p, bot, x, y, z, yaw, pitch); }
        }
        
        static void DoDiceFall(Player p, PlayerBot bot) {
            string instruction;
            string trailingInstructions = "";
            int charIndex = bot.AIName.IndexOf(',');
            
            if (charIndex == -1) {
                instruction = bot.AIName;
            } else {
                instruction = bot.AIName.Substring(0, charIndex);
                trailingInstructions = bot.AIName.Substring(charIndex+1);
            }
            //dice velocity finalModel altModel1 altModel2 [...]
            
            //[dice] [velocity] [bounceTimes] [finalModel altModel1 altModel2]
            string[] bits = instruction.SplitSpaces(4);
            const float gravity = 12;
            const float terminal = gravity * 8;
            
            float yVel = 0;
            int bounceThisManyTimes = 1;
            int newYpos = bot.Pos.Y;
            bool done = false;
            //at least "dice velocity model"
            if (bits.Length >= 4) {
                float.TryParse(bits[1], out yVel);
                Int32.TryParse(bits[2], out bounceThisManyTimes);
                yVel -= gravity;
                if (yVel < -terminal) { yVel = -terminal; }
                int groundHeight = GetPosBelowFeet(p, bot);
                
                newYpos = bot.Pos.Y + (int)yVel;
                if (newYpos - Entities.CharacterHeight <= groundHeight) {
                    if (bounceThisManyTimes >= 0) {
                        //p.Message("Bounced.");
                        bounceThisManyTimes--;
                        yVel *= 0.7f;
                        yVel = -yVel;
                    } else {
                        yVel = 0;
                    }
                    newYpos = groundHeight + Entities.CharacterHeight;

                }
                //p.Message("yVel is {0}", yVel);
                
                string[] models = bits[3].Split(' ');
                Random rnd = new Random();
                
                if (newYpos <= groundHeight + Entities.CharacterHeight && bounceThisManyTimes <= -1) {
                    //always land on the first model. rigged
                    CmdTempBot.SetModel(p, bot, models[0]);
                    //p.Message("pig...");
                    //CmdTempBot.SetModel(p, bot, "pig");
                    done = true;
                } else if (bounceThisManyTimes == 0) {
                    CmdTempBot.SetModel(p, bot, models[rnd.Next(0, models.Length)]);
                }
                
                bot.AIName = done ? "" : "dice "+yVel+" "+bounceThisManyTimes+" "+bits[3];
            }

            Position pos = new Position(bot.Pos.X, newYpos, bot.Pos.Z);
            bot.Pos = pos;
            
            //bot.AIName = trailingInstructions;
            p.Send(Packet.Teleport(bot.id, bot.Pos, bot.Rot, p.Supports(CpeExt.ExtEntityPositions)));
        }
        
        static int GetPosBelowFeet(Player p, PlayerBot bot) {
            Vec3S32 coords = bot.Pos.FeetBlockCoords;
            for (int y = coords.Y; y >= 0; y--) {
                if (!p.Level.IsAirAt((ushort)coords.X, (ushort)y, (ushort)coords.Z)) {
                    return (y * 32)+32;
                }
            }
            return 0;
        }
        
        static void DoModel(Player p, PlayerBot bot) {
            string instruction;
            string trailingInstructions = "";
            int charIndex = bot.AIName.IndexOf(',');
            
            if (charIndex == -1) {
                instruction = bot.AIName;
            } else {
                instruction = bot.AIName.Substring(0, charIndex);
                trailingInstructions = bot.AIName.Substring(charIndex+1);
            }
            
            
            string[] bits = instruction.Split(' ');
            string modelName = "humanoid";
            if (bits.Length > 1) {
                //model[0] humanoid[1]
                modelName = bits[1];
            } else {
                p.Message("%cNo arguments were provided for model, defaulting to humanoid.");
            }
            
            bot.AIName = trailingInstructions;
            CmdTempBot.SetModel(p, bot, modelName);
        }
        
        static void DoStare(Player p, PlayerBot bot) {
            string instruction;
            string trailingInstructions = "";
            int charIndex = bot.AIName.IndexOf(',');
            
            if (charIndex == -1) {
                instruction = bot.AIName;
            } else {
                instruction = bot.AIName.Substring(0, charIndex);
                trailingInstructions = bot.AIName.Substring(charIndex+1);
            }
            
            string[] bits = instruction.Split(' ');
            if (bits.Length > 1) {
                int ticker = 1;
                if (!Int32.TryParse(bits[1], out ticker)) { p.Message("%cCould not parse stare wait time of \"{0}\", defaulting to 1.", bits[1]); }
                
                ticker--;
                if (ticker > 0) {
                    instruction = bits[0] + " " + ticker;
                    bot.AIName = instruction+","+trailingInstructions;
                    //p.Message("Ticked! bot.AIName is {0} now.", bot.AIName);
                }
                else {
                    bot.AIName = trailingInstructions;
                    //p.Message("Removed! bot.AIName is {0} now.", bot.AIName);
                }
            }
            
            LookAtPlayer(p, bot);
        }
        
        static void DoWait(Player p, PlayerBot bot) {
            string instruction;
            string trailingInstructions = "";
            int charIndex = bot.AIName.IndexOf(',');
            
            if (charIndex == -1) {
                instruction = bot.AIName;
            } else {
                instruction = bot.AIName.Substring(0, charIndex);
                trailingInstructions = bot.AIName.Substring(charIndex+1);
            }
            
            string[] bits = instruction.Split(' ');
            if (bits.Length > 1) {
                int ticker = 1;
                if (!Int32.TryParse(bits[1], out ticker)) { p.Message("%cCould not parse wait time of \"{0}\", defaulting to 1.", bits[1]); }
                
                ticker--;
                if (ticker > 0) {
                    instruction = bits[0] + " " + ticker;
                    bot.AIName = instruction+","+trailingInstructions;
                    //p.Message("Ticked! bot.AIName is {0} now.", bot.AIName);
                }
                else {
                    bot.AIName = trailingInstructions;
                    //p.Message("Removed! bot.AIName is {0} now.", bot.AIName);
                }
            } else {
                p.Message("%cNo arguments were provided for wait, defaulting to 1.");
            }
        }
        
        static void DoMove(Player p, PlayerBot bot) {
            string instruction;
            string trailingInstructions = "";
            int charIndex = bot.AIName.IndexOf(',');
            
            if (charIndex == -1) {
                instruction = bot.AIName;
            } else {
                instruction = bot.AIName.Substring(0, charIndex);
                trailingInstructions = bot.AIName.Substring(charIndex+1);
            }
            
            Position goal = bot.Pos;
            float speed = 14;
            
            string[] bits = instruction.SplitSpaces(2);
            if (bits.Length > 1) {
                string argsJoined = bits[1];
                //argsJoined can be "x y z" or "x y z speed".
                goal = FloatPosArgsToPosition(p, bot.Pos, argsJoined);
                string[] args = argsJoined.Split(' ');
                if (args.Length >= 4) {
                    if (!float.TryParse(args[3], out speed)) {
                        p.Message("%cCould not parse speed to move with.");
                    }
                }
            } else {
                p.Message("%cNo arguments were provided for move.");
            }
            
            DestInfo destInfo = new DestInfo();
            MoveTowardPos(p, bot, goal, speed, out destInfo);
            if (destInfo.reachedDest) {
                bot.AIName = trailingInstructions;
                //p.Message("Removed! bot.AIName is {0} now.", bot.AIName);
            }
        }
        
        public static Position FloatPosArgsToPosition(Player p, Position fallback, string coordsJoined) {
            string[] coords = coordsJoined.Split(' ');
            if (coords.Length < 3) { p.Message("%cCould not parse coordinates to move to."); return fallback; }
            
            float x = 0, y = 0, z = 0;
            if (!(
                float.TryParse(coords[0], out x) &&
                float.TryParse(coords[1], out y) &&
                float.TryParse(coords[2], out z)
               ))
            { p.Message("%cCould not parse coordinates to move to."); return fallback; }
            
            return Position.FromFeet((int)(x*32) +16, (int)(y*32), (int)(z*32) +16);
        }
        
        public static void LookAtPlayer(Player p, PlayerBot bot) {
            //copy pasted from HunterInstructions.FaceTowards --
            int srcHeight = ModelInfo.CalcEyeHeight(p);
            int dstHeight = ModelInfo.CalcEyeHeight(bot);
            
            int dx = p.Pos.X - bot.Pos.X, dy = (p.Pos.Y + srcHeight) - (bot.Pos.Y + dstHeight), dz = p.Pos.Z - bot.Pos.Z;
            Vec3F32 dir = new Vec3F32(dx, dy, dz);
            dir = Vec3F32.Normalise(dir);
            
            Orientation rot = bot.Rot;
            DirUtils.GetYawPitch(dir, out rot.RotY, out rot.HeadX);
            bot.Rot = rot;
            // --
            
            p.Send(Packet.Teleport(bot.id, bot.Pos, bot.Rot, p.Supports(CpeExt.ExtEntityPositions)));
        }
        
        public static void MoveTowardPos(Player p, PlayerBot bot, Position there, float speed, out DestInfo destInfo) {
            destInfo = new DestInfo();
            Position newPos = GetPositionAdvancingToward(bot.Pos, there, speed, out destInfo);
            
            //Only get a new head rotation if it should turn its head
            if (destInfo.shouldTurnHead) { 
                Orientation newRot = GetRotLookingAt(bot.Pos, there);
                 bot.Rot = newRot;
            }
            
            bot.Pos = newPos;
            
            p.Send(Packet.Teleport(bot.id, bot.Pos, bot.Rot, p.Supports(CpeExt.ExtEntityPositions)));
        }
        
        static Orientation GetRotLookingAt(Position here, Position there) {
            Vec3F32 dir = GetDirToward(here, there);
            
            Orientation rot = new Orientation();
            DirUtils.GetYawPitch(dir, out rot.RotY, out rot.HeadX);
            return rot;
        }
        
        static Position GetPositionAdvancingToward(Position here, Position there, float speed, out DestInfo destInfo) {
            destInfo = new DestInfo();
            if (here == there) { destInfo.reachedDest = true; destInfo.shouldTurnHead = false; return there; }
            Vec3F32 dir = GetDirToward(here, there);
            Vec3F32 hereVec = new Vec3F32((float)here.X, (float)here.Y, (float)here.Z);
            Vec3F32 thereVec = new Vec3F32((float)there.X, (float)there.Y, (float)there.Z);
            
            
            dir = new Vec3F32(dir.X*speed, dir.Y*speed, dir.Z*speed);
            
            if (dir.Length > (hereVec - thereVec).Length ) { destInfo.reachedDest = true; destInfo.shouldTurnHead = true; return there; }
            
            Vec3F32 advanced = hereVec + dir;
            return new Position((int)advanced.X, (int)advanced.Y, (int)advanced.Z);
        }
        
        static Vec3F32 GetDirToward(Position here, Position there) {
            int dx = there.X - here.X, dy = there.Y - here.Y, dz = there.Z - here.Z;
            Vec3F32 dir = new Vec3F32(dx, dy, dz);
            return Vec3F32.Normalise(dir);
        }
        
        public class DestInfo {
            public bool reachedDest = false;
            public bool shouldTurnHead = true;
        }
        
		
    }
    
	public class CmdFlipCoin : Command2
	{
		public override string name { get { return "FlipCoin"; } }
		
		public override string shortcut { get { return ""; } }
		
		public override bool MessageBlockRestricted { get { return false; } }

		public override string type { get { return "other"; } }
		
		public override bool museumUsable { get { return false; } }
		
		public override LevelPermission defaultRank { get { return LevelPermission.Guest; } }
		public CommandData globalData;
		public override void Use(Player p, string message, CommandData data)
		{
		    if (data.Context != CommandContext.MessageBlock) { p.Message("You can only use this command through message blocks."); return; }
		    if (message.Length == 0) { Help(p); return; }
		    string[] bits = message.SplitSpaces(5);
		    if (bits.Length < 5) { Help(p); return; }
		    //x y z 3 stone dirt cobblestone
		    
		    int x = -1, y = -1, z = -1;
		    int amount = 1;
		    string[] models = bits[4].Split(' ');
		    
		    if (!CommandParser.GetInt(p, bits[0], "X coordinate", ref x, 0, (int)p.level.Width)) { return; }
		    if (!CommandParser.GetInt(p, bits[1], "Y coordinate", ref y, 0, (int)p.level.Height)) { return; }
		    if (!CommandParser.GetInt(p, bits[2], "Z coordinate", ref z, 0, (int)p.level.Length)) { return; }
		    int maxDice = 8;
		    if (!CommandParser.GetInt(p, bits[3], "amount", ref amount, 1, maxDice)) {
		        return;
		    }
		    globalData.Context = CommandContext.MessageBlock;
		    globalData.Rank = LevelPermission.Nobody;
		    
		    if (MessageCmd.CanSpeak(p, name)) {
		        Chat.MessageFromLevel(p, "<Local>λNICK %Srolls the block dice...");
		        p.CheckForMessageSpam();
		    } else {
		        return;
		    }
		    
		    for (int i = 0; i < maxDice; i++) {
		        Command.Find("tempbot").Use(p, "remove flipCoinbot"+i, globalData);
		        //p.Message("removed {0}", i);
		    }
		    //Thread.Sleep(250);
		    
		    Random seedRand = new Random();
		    int seed = seedRand.Next();
		    
		    Vec3S32 pivot = new Vec3S32(x, y, z);
		    
		    Player[] players = PlayerInfo.Online.Items;
			foreach (Player pl in players) {
		        if (pl.level != p.level) { continue; }
		        Random rand = new Random(seed);
		        //reset?
		        models = bits[4].Split(' ');
		        
    			SpawnBots(pl, amount);
    			//Thread.Sleep(250);
    			ArrangeBots(pl, pivot, amount, models, rand);
    			//Thread.Sleep(250);
    			ModelBots(pl, amount, models, rand);
    			//Thread.Sleep(500);
    			ActivateBots(pl, amount, models, rand);
		    }
		}
		
		public override void Help(Player p)
		{
            p.Message( "%T/FlipCoin [x y z] [amount] [model1 model2 etc...]");
            p.Message( "%HDrops [amount] of temporary bots around [x y z] which then have a random roll based on the possible list of models given.");
		}
		
		void RandomlySwapFirstElement(Random rnd, ref string[] models) {
		    if (models.Length < 1) { return; }
		    int indexForNewFront = rnd.Next(0, models.Length);
		    string oldFront = models[0];
		    
		    //swap them around with the randomly chosen index
		    models[0] = models[indexForNewFront];
		    models[indexForNewFront] = oldFront;
		}
		
		void SpawnBots(Player p, int amount) {
			for (int i = 0; i < amount; i++) {
			    Command.Find("tempbot").Use(p, "add flipCoinbot"+i+" 0 -128 0 0 0 Goodly empty", globalData);
			    Command.Find("tempbot").Use(p, "model flipCoinbot"+i+" 0", globalData);
			}
		}
		
		void ArrangeBots(Player p, Vec3S32 pivot, int amount, string[] models, Random rnd) {
		    Vec3S32[] botPositions = new Vec3S32[amount];
		    
			for (int i = 0; i < amount; i++) {
		        
		        botPositions[i] = GetRandomPosAround(pivot, i, botPositions, rnd);
		        string x = botPositions[i].X.ToString(),
		        y = ( (float)(botPositions[i].Y) + (rnd.NextDouble()*4) ).ToString(),
		        z = botPositions[i].Z.ToString();
		        
			    Command.Find("tempbot").Use(p, "summon flipCoinbot"+i+" "+x+" "+y+" "+z+" 0 0", globalData);
			}
		}
		
		void ModelBots(Player p, int amount, string[] models, Random rnd) {
		    for (int i = 0; i < amount; i++) {
			    //RandomlySwapFirstElement(seed, ref models);
			    Command.Find("tempbot").Use(p, "model flipCoinbot"+i+" "+models[rnd.Next(0, models.Length)], globalData);
		    }
		}
		
		void ActivateBots(Player p, int amount, string[] models, Random rnd) {
			for (int i = 0; i < amount; i++) {
			    RandomlySwapFirstElement(rnd, ref models);
			    string joined = models.Join(" ");
			    //[dice] [velocity] [bounceTimes] [finalModel altModel1 altModel2]
			    Command.Find("tempbot").Use(p, "ai flipCoinbot"+i+" dice 0 1 "+joined, globalData);
			}
		}
		
		Vec3S32 GetRandomPosAround(Vec3S32 pivot, int index, Vec3S32[] botPositions, Random rnd) {
		    const int extent = 2; //means a square of 5x5
		    const int height = 8;
		    //first one, any location is fine
		    if (index == 0) {
		        return new Vec3S32(pivot.X+rnd.Next(-extent, extent+1), pivot.Y+height, pivot.Z+rnd.Next(-extent, extent+1));
		    }
		    
		    Vec3S32 pos = new Vec3S32(pivot.X+rnd.Next(-extent, extent+1), pivot.Y+height, pivot.Z+rnd.Next(-extent, extent+1));
		    while (PosMatchesBotPositionsAtIndex(pos, botPositions, index)) {
		        pos = new Vec3S32(pivot.X+rnd.Next(-extent, extent+1), pivot.Y+height, pivot.Z+rnd.Next(-extent, extent+1));
		    }
		    return pos;
		}
		
		public bool PosMatchesBotPositionsAtIndex(Vec3S32 pos, Vec3S32[] botPositions, int index) {
		    for (int i = 0; i < index; i++) {
		        if (pos == botPositions[i]) { return true; }
		    }
		    return false;
		}

	}
	
	public class CmdMoveBots : Command2
	{
		public override string name { get { return "MoveBots"; } }
		
		public override string shortcut { get { return ""; } }
		
		public override bool MessageBlockRestricted { get { return true; } }

		public override string type { get { return "other"; } }
		
		public override bool museumUsable { get { return false; } }
		
		public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
		
		static bool OwnsMap(Player p, Level lvl) {
			if (lvl.name.CaselessStarts(p.name)) return true;
			string[] owners = lvl.Config.RealmOwner.Replace(" ", "").Split(',');
			
			foreach (string owner in owners) {
				if (owner.CaselessEq(p.name)) return true;
			}
			return false;
		}
		
		public override void Use(Player p, string message, CommandData data)
		{
		    if (message.Length == 0) { Help(p); return; }
			bool canUse = false;// = p.group.Permission >= p.level.BuildAccess.Min;
			
			if (OwnsMap(p, p.level) || p.group.Permission >= LevelPermission.Operator) canUse = true;
			if (!canUse) {
				p.Message("&cYou can only use this command on your own maps."); return;
			}
			
		    string[] bits = message.SplitSpaces(5);
		    if (bits.Length < 3) { Help(p); return; }
		    //x y z 3		    
		    int x = -1, y = -1, z = -1;
		    
		    if (!CommandParser.GetInt(p, bits[0], "X delta", ref x)) { return; }
		    if (!CommandParser.GetInt(p, bits[1], "Y delta", ref y)) { return; }
		    if (!CommandParser.GetInt(p, bits[2], "Z delta", ref z)) { return; }
		    x *= 32;
		    y *= 32;
		    z *= 32;
		    
		    Position pos;
		    byte yaw, pitch;
            PlayerBot[] bots = p.level.Bots.Items;
            for (int i = 0; i < bots.Length; i++) {
                
                pos.X = bots[i].Pos.X + x;
                pos.Y = bots[i].Pos.Y + y;
                pos.Z = bots[i].Pos.Z + z;
                yaw = bots[i].Rot.RotY; pitch = bots[i].Rot.HeadX;
                bots[i].Pos = pos;
                bots[i].SetYawPitch(yaw, pitch);
            }
            
            MCGalaxy.Bots.BotsFile.Save(p.level);
		    
		}
		
		public override void Help(Player p)
		{
            p.Message( "%T/MoveBots [x y z]");
            p.Message( "%HMoves all the bots in the map you're in by [x y z].");
            p.Message( "%HFor example, 0 1 0 would move all the bots up by 1 block.");
		}

	}
	
    
}