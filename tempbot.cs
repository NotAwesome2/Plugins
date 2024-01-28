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

using Vec3I = MCGalaxy.Maths.Vec3S32;
using KeyFrameList = System.Collections.Generic.List<MCGalaxy.PluginTempBot.KeyFrame>;

//unknownshadow200: well player ids go from 0 up to 255. normal bots go from 127 down to 64, then 254 down to 127, then finally 63 down to 0.

namespace MCGalaxy {
    
    public sealed class PluginTempBot : Plugin {
        public override string name { get { return "tempbot"; } }
        public override string MCGalaxy_Version { get { return "1.9.4.7"; } }
        public override string creator { get { return "goodly"; } }
        
        public delegate void DoAI(Player p, PlayerBot bot);
        static Dictionary<string, DoAI> AIDict = new Dictionary<string, DoAI>();
        
        public const byte botIDstartValue = 128; //-128 signed
        public static Dictionary<string, Tinfo> tinfoFor = new Dictionary<string, Tinfo>();
        
        public static byte NextFreeID(Player p) {
            for (int i = 0; i < tinfoFor[p.name].usedIDs.Length; i++) {
                if (!tinfoFor[p.name].usedIDs[i]) {
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
            
            AIDict["dice"] = DoDiceFall;
            AIDict["stare"] = DoStare;
            AIDict["wait"] = DoWait;
            AIDict["move"] = DoMove;
            AIDict["anim"] = DoAnim;
            AIDict["model"] = DoModel;
            AIDict["remove"] = CmdTempBot.Remove;
            AIDict["summon"] = DoTP;
            AIDict["message"] = DoMessage;
            AIDict["msg"] = DoMessage;
            AIDict["runscript"] = DoRunScript;
            AIDict["rot"] = DoRot;
            
            Activate();
        }
        
        public override void Unload(bool shutdown) {
            Command.Unregister(tempbotCmd);
            Command.Unregister(flipcoinCmd);
            Command.Unregister(moveBotsCmd);
            
            OnPlayerDisconnectEvent.Unregister(HandleDisconnect);
            OnSentMapEvent.Unregister(HandleSentMap);
            
            tinfoFor.Clear();
            
            instance.Cancel(tickBots);
        }
        
        static void HandleDisconnect(Player p, string reason) {
            tinfoFor.Remove(p.name);
            
        }
        static void HandleSentMap(Player p, Level prevLevel, Level level) {
            tinfoFor.Remove(p.name);
        }
        
        static Scheduler instance;
        static SchedulerTask tickBots;
        public static void Activate() {
            if (instance == null) instance = new Scheduler("TempBotsScheduler");
            
            tickBots = instance.QueueRepeat(BotsTick, null, TimeSpan.FromMilliseconds(100));
        }
        
        static void BotsTick(SchedulerTask task) {
            
            foreach (Player p in PlayerInfo.Online.Items) {
                if (!tinfoFor.ContainsKey(p.name)) { continue; }
                
                Tinfo tinfo = tinfoFor[p.name];
                tinfo.DoRecord();
                
                List<PlayerBot> bots = tinfo.botList;
                if (bots.Count == 0) { continue; }
                for (int j = 0; j < bots.Count; j++) { BotTick(p, bots[j]); }
            }
        }

        static void BotTick(Player p, PlayerBot bot) {
            //p.Message("Oh lawd %1{0}%S is ticking!", bot.name);
            
            if (string.IsNullOrEmpty(bot.AIName)) { return; }
            
            string lowercaseAIName = bot.AIName.SplitSpaces(2)[0].ToLower();
            if (AIDict.ContainsKey(lowercaseAIName)) {
                AIDict[lowercaseAIName](p, bot);
                return;
            }
            
            string instruction;
            string trailingInstructions;
            ParseInstructions(bot.AIName, out instruction, out trailingInstructions);
            
            if (bot.AIName.StartsWith(" ")) {
                p.Message("%cUnrecognized bot ai \"%S{0}%c\". Did you accidentally put a space after a comma separator?", instruction);
            } else {
                p.Message("%cUnrecognized bot ai \"%S{0}%c\".", instruction);
            }
            bot.AIName = "";
        }
        
        static void ParseInstructions(string full, out string instruction, out string trailingInstructions) {
            trailingInstructions = "";
            int charIndex = full.IndexOf(',');
            
            if (charIndex == -1) {
                instruction = full;
            } else {
                instruction = full.Substring(0, charIndex);
                trailingInstructions = full.Substring(charIndex+1);
            }
        }
        
        static void DoRunScript(Player p, PlayerBot bot) {
            string instruction;
            string trailingInstructions;
            ParseInstructions(bot.AIName, out instruction, out trailingInstructions);
            
            string[] bits = instruction.SplitSpaces(2);
            string runscriptArgs = "DEFAULT ARGS";
            if (bits.Length > 1) {
                runscriptArgs = bits[1];
            } else {
                p.Message("%cNo arguments were provided for runscript.");
            }
            
            bot.AIName = trailingInstructions;
            
            
            try {
                CommandData data = default(CommandData);
                data.Context = CommandContext.MessageBlock;
                Command.Find("runscript").Use(p, runscriptArgs, data);
            } catch (Exception e) {
                p.Message("An error occured when running tempbot runscript command! {0}", e.Message);
                p.Message("Please tell Goodly.");
            }
        }
        
        static void DoMessage(Player p, PlayerBot bot) {
            string instruction;
            string trailingInstructions;
            ParseInstructions(bot.AIName, out instruction, out trailingInstructions);
            
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

        static void DoRot(Player p, PlayerBot bot) {
            string instruction;
            string trailingInstructions;
            
            ParseInstructions(bot.AIName, out instruction, out trailingInstructions);
            bot.AIName = trailingInstructions;

            string[] bits = instruction.SplitSpaces(2);
            if (bits.Length > 1) {
                string[] args = bits[1].Split(' ');
                if (args.Length >= 3) {
                    int rotx = 0, roty = 0, rotz = 0;
                    if (
                        Int32.TryParse(args[0], out rotx) &&
                        Int32.TryParse(args[1], out roty) &&
                        Int32.TryParse(args[2], out rotz)
                       ) {
                        CmdTempBot.RotBot(p, bot, rotx, roty, rotz);
                    } else {
                        p.Message("%cCould not parse one or more arguments of rot.");
                    }
                } else if (args.Length >= 2) {
                    bool prop_error = false;
                    
                    EntityProp prop = EntityProp.RotX;
                    if (args[0].CaselessEq("x")) {
                        prop = EntityProp.RotX;
                    } else if (args[0].CaselessEq("y")) {
                        prop = EntityProp.RotY;
                    } else if (args[0].CaselessEq("z")) {
                        prop = EntityProp.RotZ;
                    } else {
                        prop_error = true;
                    }
                    
                    int angle = 0;
                    if (!prop_error && Int32.TryParse(args[1], out angle)) {
                        CmdTempBot.RotAxisBot(p, bot, prop, angle);
                    } else {
                        p.Message("%cCould not parse one or more arguments of rot.");
                    }
                } else {
                    p.Message("%cNot enough arguments provided for rot.");
                }
            } else {
                p.Message("%cNo arguments were provided for rot.");
            }
        }
        
        static void DoTP(Player p, PlayerBot bot) {
            string instruction;
            string trailingInstructions;
            ParseInstructions(bot.AIName, out instruction, out trailingInstructions);
            
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
        
        static void DoAnim(Player p, PlayerBot bot) {
            string instruction;
            string trailingInstructions;
            ParseInstructions(bot.AIName, out instruction, out trailingInstructions);
            
            string[] bits = instruction.SplitSpaces(2);
            string instructionName = bits[0];
            if (bits.Length < 2) { p.Message("&cNot enough arguments for tempbot anim"); bot.AIName = ""; return; }
            
            string baseCode = bits[1];
            byte[] data = null;
            try {
                data = Convert.FromBase64String(baseCode);
            } catch (Exception e) {
                p.Message("There was an exception when decoding the animation data (&f{0}&S)", baseCode);
                p.Message("&c{0}", e.Message);
                bot.AIName = ""; return;
            }
            
            int pointer = 0;
            KeyFrame frame;
            try {
                frame = KeyFrame.FromBytes(data, ref pointer);
            } catch (Exception e) {
                p.Message("There was an exception when decoding an animation frame");
                p.Message("&c{0}", e.Message);
                bot.AIName = ""; return;
            }
            
            
            //frame.Print(p);
            if (frame.useTranslation) {
                bot.Pos = new Position(frame.translation.X, frame.translation.Y, frame.translation.Z);
            } else if (frame.useRelative) {
                bot.Pos = new Position(
                    bot.Pos.X + frame.relTranslation.X,
                    bot.Pos.Y + frame.relTranslation.Y,
                    bot.Pos.Z + frame.relTranslation.Z);
            }
            if (frame.useRotation) {
                bot.Rot = new Orientation(frame.yaw, frame.pitch);
            }
            if (frame.useModel) {
                CmdTempBot.SetModel(p, bot, frame.model);
            }
            
            p.Send(Packet.Teleport(bot.id, bot.Pos, bot.Rot, p.Supports(CpeExt.ExtEntityPositions)));
            
            
            if (pointer == data.Length) {
                //end of stream, move on to next botai
                bot.AIName = trailingInstructions; return;
            }
            byte[] newData = new byte[data.Length - pointer];
            
            //Copy(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length);
            Array.Copy(data, pointer, newData, 0, newData.Length);
            
            bot.AIName = instructionName+" "+Convert.ToBase64String(newData)+","+trailingInstructions;
        }
        
        static void DoDiceFall(Player p, PlayerBot bot) {
            string instruction;
            string trailingInstructions;
            ParseInstructions(bot.AIName, out instruction, out trailingInstructions);
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
            string trailingInstructions;
            ParseInstructions(bot.AIName, out instruction, out trailingInstructions);
            
            
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
            string trailingInstructions;
            ParseInstructions(bot.AIName, out instruction, out trailingInstructions);
            
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
            string trailingInstructions;
            ParseInstructions(bot.AIName, out instruction, out trailingInstructions);
            
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
            string trailingInstructions;
            ParseInstructions(bot.AIName, out instruction, out trailingInstructions);
            
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
        
        public sealed class CmdTempBot : Command2 {   
            public override string name { get { return "tempbot"; } }
            public override string shortcut { get { return "tbot"; } }
            public override string type { get { return CommandTypes.Other; } }
            public override bool museumUsable { get { return false; } }
            public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
            
            public override void Use(Player p, string message, CommandData data) {
                if (p.name.EndsWith("-")) { return; } //Betacraft not allowed to use this
                if (message.CaselessEq("where")) { DoWhere(p); return; }
                if (!CanUse(p, data)) { return; }
                
                
                
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
                
                if (args[0].CaselessEq("skin")) {
                    if (args.Length < 2) { p.Message("%cYou need args for botName and skinName."); return; }
                    TrySetSkin(p, args[1]);
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
                
                if (args[0].CaselessEq("record")) {
                    ToggleRecord(p);
                    return;
                }
                
                if (args[0].CaselessEq("rot")) {
                  if (args.Length < 2) { p.Message("%cYou need args for botName, axis and angle OR botName, rotX, rotY and rotZ."); return; }
                  TryRot(p, args[1]);
                  return;
                }
                
                Help(p);
            }
            
            static void ToggleRecord(Player p) {
                if (!tinfoFor.ContainsKey(p.name)) {
                    tinfoFor[p.name] = new Tinfo(p);
                }
                Tinfo tinfo = tinfoFor[p.name];
                if (!tinfo.recording) {
                    tinfo.StartRecording();
                } else {
                    tinfo.StopRecording();
                }
            }
            
            static void DoWhere(Player p) {
                //-16 so it's centered on the block instead of min corner
                Vec3F32 pos = new Vec3F32(p.Pos.X-16, p.Pos.Y - Entities.CharacterHeight, p.Pos.Z-16);
                pos.X /= 32;
                pos.Y /= 32;
                pos.Z /= 32;
                
                p.Send(Packet.Message(
                    pos.X + " " + pos.Y + " " + pos.Z+" "+Orientation.PackedToDegrees(p.Rot.RotY)+" "+Orientation.PackedToDegrees(p.Rot.HeadX),
                                      CpeMessageType.Normal, p.hasCP437));
            }
            
            static bool CanUse(Player p, CommandData data) {
                if (!(p.group.Permission >= LevelPermission.Operator)) {
                    if (!Hacks.CanUseHacks(p)) {
                        if (data.Context != CommandContext.MessageBlock) {
                            p.Message("%cYou cannot use this command manually when hacks are disabled.");
                            return false;
                        }
                    }
                }
                return true;
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
                if (!tinfoFor.ContainsKey(p.name)) {
                    tinfoFor[p.name] = new Tinfo(p);
                }
                
                
                foreach (PlayerBot b in tinfoFor[p.name].botList) {
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
                tinfoFor[p.name].usedIDs[ID - PluginTempBot.botIDstartValue] = true;
                
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
                tinfoFor[p.name].botList.Add(bot);
                
                Entities.Spawn(p, bot);
            }
            
            void TryRemove(Player p, string message) {
                string[] args = message.Split(' ');
                Remove(p, args[0]);
            }
            
            public static void Remove(Player p, string botName) {
                PlayerBot bot = GetBotAtName(p, botName);
                Remove(p, bot);
            }
            public static void Remove(Player p, PlayerBot bot) {
                if (bot != null) {
                    Entities.Despawn(p, bot);
                    //p.Message("Successfully removed {0} with ID of {1}", bot.name, bot.id);
                    //p.Message("ID {0} is now freed from index {1}", bot.id, bot.id - botIDstartValue);
                    
                    tinfoFor[p.name].usedIDs[bot.id - botIDstartValue] = false;
                    tinfoFor[p.name].botList.Remove(bot);
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
            
            void TrySetSkin(Player p, string message) {
                string[] args = message.SplitSpaces(2);
                if (args.Length < 2) { p.Message("%cYou need args for botName and skinName."); return; }
                
                PlayerBot bot = GetBotAtName(p, args[0]);
                if (bot != null) {
                    SetSkin(p, bot, args[1]);
                }
            }
            
            public static void SetSkin(Player p, PlayerBot bot, string skinName) {
                bot.SkinName = skinName;
                
                //       SendSpawnEntity(byte id, string name, string skin, Position pos, Orientation rot);
                p.Session.SendSpawnEntity(bot.id, bot.DisplayName, bot.SkinName, bot.Pos, bot.Rot);
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
            
            void TryRot(Player p, string message) {
                string[] args = message.Split(' ');
                if (args.Length < 3) { p.Message("%cYou need args for botName, axis and angle OR botName, rotX, rotY and rotZ."); return; }
                
                PlayerBot bot = GetBotAtName(p, args[0]);
                if (bot != null) {
                    if (args.Length >= 4) {
                        int rotx = 0, roty = 0, rotz = 0;
                        if (!CommandParser.GetInt(p, args[1], "X rotation", ref rotx)) { return; }
                        if (!CommandParser.GetInt(p, args[2], "Y rotation", ref roty)) { return; }
                        if (!CommandParser.GetInt(p, args[3], "Z rotation", ref rotz)) { return; }
                        RotBot(p, bot, rotx, roty, rotz);
                    } else {
                        int angle = 0;
                        if (!CommandParser.GetInt(p, args[2], "Angle", ref angle)) { return; }

                        if (args[1].CaselessEq("x")) {
                          RotAxisBot(p, bot, EntityProp.RotX, angle);
                        } else if (args[1].CaselessEq("y")) {
                          RotAxisBot(p, bot, EntityProp.RotY, angle);
                        } else if (args[1].CaselessEq("z")) {
                          RotAxisBot(p, bot, EntityProp.RotZ, angle);
                        } else {
                          p.Message("%cAxis must be X, Y or Z.");
                        }
                    }
                }
            }

            public static void RotAxisBot(Player p, PlayerBot bot, EntityProp prop, int value) {
                byte angle = Orientation.DegreesToPacked(value);
                
                Orientation rot = bot.Rot;
                if (prop == EntityProp.RotX) rot.RotX = angle;
                if (prop == EntityProp.RotY) rot.RotY = angle;
                if (prop == EntityProp.RotZ) rot.RotZ = angle;
                bot.Rot = rot;
                
                if (p.Supports(CpeExt.EntityProperty)) {
                    p.Send(Packet.EntityProperty(bot.id, prop, value));
                }
            }
            
            public static void RotBot(Player p, PlayerBot bot, int rotx, int roty, int rotz) {
                byte angle_x = Orientation.DegreesToPacked(rotx);
                byte angle_y = Orientation.DegreesToPacked(roty);
                byte angle_z = Orientation.DegreesToPacked(rotz);
                
                Orientation rot = bot.Rot;
                rot.RotX = angle_x;
                rot.RotY = angle_y;
                rot.RotZ = angle_z;
                bot.Rot = rot;
                
                if (p.Supports(CpeExt.EntityProperty)) {
                    p.Send(Packet.EntityProperty(bot.id, EntityProp.RotX, rotx));
                    p.Send(Packet.EntityProperty(bot.id, EntityProp.RotY, roty));
                    p.Send(Packet.EntityProperty(bot.id, EntityProp.RotZ, rotz));
                }
            }
            
            static PlayerBot GetBotAtName(Player p, string botName) {
                Tinfo list;
                if (!tinfoFor.TryGetValue(p.name, out list) || list.botList.Count == 0)
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
                p.Message("%T/TempBot skin [botName] [skin name]");
                p.Message("%H Sets skin of a client-side bot.");
                p.Message("%T/TempBot ai [botName] [ai arguments]");
                p.Message("%H Sets ai. Use %T/help tempbot ai %Hfor more info.");
                p.Message("%T/TempBot where");
                p.Message("%HPuts your current X, Y, Z, yaw and pitch into chat for copy pasting.");
                p.Message("%T/TempBot rot [botName] x/y/z [angle]");
                p.Message("%T/TempBot rot [botName] [rotX rotY rotZ]");
                p.Message("%HSets the XYZ rotation of a client-side bot.");
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
                    p.Message("%Tanim [anim data]");
                    p.Message("%H Get anim data from toggling &b/tbot record");
                    p.Message("%Tsummon [X Y Z] [yaw pitch]");
                    p.Message("%H Instantly teleports this tempbot.");
                    p.Message("%Tmodel [modelname]");
                    p.Message("%H Sets model.");
                    p.Message("%Tmsg [message]");
                    p.Message("%H You can't use commas in the message!");
                    p.Message("%H Use ; instead and it will become ,");
                    p.Message("%Tremove");
                    p.Message("%H Removes this tempbot.");
                    p.Message("%Trot x/y/z [angle]");
                    p.Message("%Trot [rotX rotY rotZ]");
                    p.Message("%H Sets rotation of tempbot.");
                    p.Message("%HYou can chain AI together with commas. e.g:");
                    p.Message("%Hstare 10,wait 20,move 32 64 30");
                    p.Message("%HPlease note the (lack of) spaces.");
                    return;
                }
                Help(p);
            }
            
        }
        
        
        public struct KeyFrame {
            //RotY = yaw; HeadX = pitch;
            
            public static KeyFrame Make(KeyFrameList list, Position pos, Orientation rot, string model) {
                KeyFrame the = new KeyFrame();
                
                
                the.translation = new Vec3I(pos.X, pos.Y, pos.Z);
                the.yaw = rot.RotY;
                the.pitch = rot.HeadX;
                the.model = model;
                
                if (list.Count == 0) {
                    the.useTranslation = true;
                    the.useRotation = true;
                    list.Add(the);
                    return the;
                }
                var prev = list[list.Count-1]; //maybe this should be a stack
                
                the.relTranslation = the.translation - prev.translation;
                
                if (the.yaw == prev.yaw && the.pitch == prev.pitch) { the.useRotation = false; } else { the.useRotation = true; }
                
                if (prev.model == the.model) { the.useModel = false; } else { the.useModel = true; }
                
                if (the.relTranslation.X == 0 && the.relTranslation.Y == 0 && the.relTranslation.Z == 0) {
                    //no translation from previous frame
                    list.Add(the);
                    return the;
                }
                
                
                if (the.relTranslation.X >= sbyte.MinValue && the.relTranslation.X <= sbyte.MaxValue &&
                    the.relTranslation.Y >= sbyte.MinValue && the.relTranslation.Y <= sbyte.MaxValue &&
                    the.relTranslation.Z >= sbyte.MinValue && the.relTranslation.Z <= sbyte.MaxValue) {
                        
                    the.useRelative = true;
                    list.Add(the);
                    return the;
                }
                
                the.useTranslation = true;
                
                list.Add(the);
                return the;
            }
            
            
            const byte TRANS_FLAG = (byte)(1 << 0);
            const byte REL_FLAG   = (byte)(1 << 1);
            const byte ROT_FLAG   = (byte)(1 << 2);
            const byte MOD_FLAG   = (byte)(1 << 3);

            
            public bool useTranslation;
            public bool useRelative;
            public bool useRotation;
            public bool useModel;
            
            public Vec3I translation;
            public Vec3I relTranslation;
            public byte yaw, pitch;
            public string model;
            
            public void Print(Player p) {
                //string base64 = Convert.ToBase64String(ToBytes());
                //p.Message(base64);
                if (useTranslation) {
                    p.Message("&xTranslation: {0} {1} {2}", translation.X, translation.Y, translation.Z);
                }
                if (useRelative) {
                    p.Message("&xRelative: {0} {1} {2}", relTranslation.X, relTranslation.Y, relTranslation.Z);
                }
                if (useRotation) {
                    p.Message("&xYaw pitch: {0} {1}", yaw, pitch);
                }
                if (useModel) {
                    p.Message("&xModel: {0}", model);
                }
                if (!useTranslation && !useRelative && !useRotation && !useModel) {
                    p.Message("(no change)");
                }
            }
            public void PrintBytes(Player p) {
                byte[] bytes = new byte[2 + NetUtils.StringSize];
                bytes[0] = Network.Opcode.Message;
                bytes[1] = (byte)CpeMessageType.Normal;
                
                byte[] frameBytes = ToBytes();
                if (frameBytes.Length > NetUtils.StringSize) { p.Message("&cToo big!!"); return; }
                
                for (int i = 0; i < NetUtils.StringSize; i++) {
                    if (i >= frameBytes.Length) {
                        bytes[i+2] = (byte)' '; //pad with spaces
                        continue;
                    }
                    bytes[i+2] = frameBytes[i];
                }
                
                p.Send(bytes);
                //p.Message(frameBytes[0].ToString());
            }
            
            public byte[] ToBytes() {
                List<byte> bytes = new List<byte>();
                
                byte transFlag = useTranslation ? TRANS_FLAG : (byte)0;
                byte relFlag   = useRelative    ? REL_FLAG   : (byte)0;
                byte rotFlag   = useRotation    ? ROT_FLAG   : (byte)0;
                byte modFlag   = useModel       ? MOD_FLAG   : (byte)0;
                
                byte flags = (byte)(transFlag | relFlag | rotFlag | modFlag);
                bytes.Add(flags);
                
                if (useTranslation) {
                    bytes.AddRange(BitConverter.GetBytes(translation.X));
                    bytes.AddRange(BitConverter.GetBytes(translation.Y));
                    bytes.AddRange(BitConverter.GetBytes(translation.Z));
                }
                if (useRelative) {
                    sbyte x = (sbyte)relTranslation.X;
                    sbyte y = (sbyte)relTranslation.Y;
                    sbyte z = (sbyte)relTranslation.Z;
                    
                    bytes.Add((byte)x);
                    bytes.Add((byte)y);
                    bytes.Add((byte)z);
                }
                if (useRotation) {
                    bytes.Add(yaw);
                    bytes.Add(pitch);
                }
                if (useModel) {
                    byte[] stringBytes = new byte[NetUtils.StringSize];
                    //NetUtils.Write(string str, byte[] array, int offset, bool hasCP437);
                    NetUtils.Write(model, stringBytes, 0, false);
                    bytes.AddRange(stringBytes);
                }
                
                return bytes.ToArray();
            }
            
            public static KeyFrame FromBytes(byte[] stream, ref int pointer) {
                KeyFrame frame = new KeyFrame();
                
                byte flags = stream[pointer];
                pointer++;
                
                frame.useTranslation = ((flags & TRANS_FLAG) > 0) ? true : false;
                frame.useRelative    = ((flags & REL_FLAG  ) > 0) ? true : false;
                frame.useRotation    = ((flags & ROT_FLAG  ) > 0) ? true : false;
                frame.useModel       = ((flags & MOD_FLAG  ) > 0) ? true : false;
                
                if (frame.useTranslation) {
                    frame.translation.X = BitConverter.ToInt16(stream, pointer); pointer += sizeof(int);
                    frame.translation.Y = BitConverter.ToInt16(stream, pointer); pointer += sizeof(int);
                    frame.translation.Z = BitConverter.ToInt16(stream, pointer); pointer += sizeof(int);
                }
                else if (frame.useRelative) {
                    frame.relTranslation.X = (sbyte)stream[pointer]; pointer++;
                    frame.relTranslation.Y = (sbyte)stream[pointer]; pointer++;
                    frame.relTranslation.Z = (sbyte)stream[pointer]; pointer++;
                }
                if (frame.useRotation) {
                    frame.yaw   = stream[pointer]; pointer++;
                    frame.pitch = stream[pointer]; pointer++;
                }
                if (frame.useModel) {
                    //NetUtils.ReadString(byte[] data, int offset);
                    frame.model = NetUtils.ReadString(stream, pointer); pointer += NetUtils.StringSize;
                }
                
                return frame;
            }
        }
        
        public class Tinfo {
            const CpeMessageType STOP_LINE = CpeMessageType.Status1;
            const CpeMessageType REC_LINE = CpeMessageType.Status3;
            
            public Tinfo(Player p) {
                this.p = p;
                //This is required for CustomModels plugin to work correctly with tempbots. Do not remove.
                p.Extras["TempBot_BotList"] = botList;
            }
            public Player p;
            public List<PlayerBot> botList = new List<PlayerBot>();
            public bool[] usedIDs = new bool[64];
            
            
            static string Battery(float percent) {
                int maxHealth = 6;
                int health = (int)(maxHealth * percent);
                if (health < maxHealth) { health++; }
                
                string healthColor = "&a";
                if (percent <= 0.25f) { healthColor = "&c"; }
                else if (percent <= 0.5f) { healthColor = "&e"; }
                
                var builder = new System.Text.StringBuilder(healthColor, maxHealth + 4);
                
                string final;
                for (int i = 1; i < health + 1; ++i) {
                    builder.Append("|");
                }
                if (health >= maxHealth) {
                    final = builder.ToString();
                    return final;
                }

                int lostHealth = maxHealth - health;

                builder.Append("&8");
                for (int i = 1; i < lostHealth + 1; ++i) {
                    builder.Append("|");
                }
                final = builder.ToString();
                return final;
            }
            
            public KeyFrameList keyFrames = new KeyFrameList();
            public bool recording { get; private set; }
            int maxFrames = 60 * 10;
            public void StartRecording() {
                p.Message("&aStarted recording tempbot movement.");
                p.SendCpeMessage(STOP_LINE, "&7(stop with /tbot record)");
                keyFrames.Clear();
                recording = true;
            }
            public void DoRecord() {
                if (!recording) { return; }
                
                var frame = KeyFrame.Make(keyFrames, p.Pos, p.Rot, p.Model);
                
                float percent = 1 - (keyFrames.Count / (float)maxFrames);
                if (percent <= 0) { StopRecording(); return; }
                
                string battery = "["+Battery(percent)+"&f]:";
                if (keyFrames.Count % 10 < 5) {
                    p.SendCpeMessage(REC_LINE, "&c• &fREC   "+battery);
                } else {
                    p.SendCpeMessage(REC_LINE, "&fREC   "+battery);
                }
                
                
                //p.Message("ORIGINAL:");
                //frame.Print(p);
                
                
                //int pointer = 0;
                //var converted = KeyFrame.FromBytes(frame.ToBytes(), ref pointer);
                
                //p.Message("CONVERTED {0}:", pointer);
                //converted.Print(p);
                //p.Message("-");
            }
            public void StopRecording() {
                p.Message("&eStopped recording tempbot movement.");
                p.SendCpeMessage(STOP_LINE, "");
                p.SendCpeMessage(REC_LINE, "");
                recording = false;
                DisplayCode();
            }
            void DisplayCode() {
                List<byte> allBytes = new List<byte>();
                foreach (var frame in keyFrames) {
                    allBytes.AddRange(frame.ToBytes());
                }
                string base64 = Convert.ToBase64String(allBytes.ToArray());
                
                if (!Directory.Exists(directory)) { DisplayCodeToChat(base64); return; } //non na2 case
                
                File.WriteAllText(fullPath, base64);
                p.Message("&eYou may find your anim data here:");
                p.Message("&b"+url);
                
            }
            void DisplayCodeToChat(string base64) {
                string[] sections = new string[(int)Math.Ceiling(base64.Length / (float)NetUtils.StringSize)];
                p.Message("Total length: &b{0}&S, sections: &b{1}", base64.Length, sections.Length);
                for (int i = 0; i < sections.Length; i++) {
                    
                    int start = i * NetUtils.StringSize;
                    int left = base64.Length - start;
                    int end = Math.Min(start + (NetUtils.StringSize - 1), start + (left - 1));
                    int length = (end - start) +1;
                    
                    sections[i] = base64.Substring(start, length);
                    p.Send(Packet.Message(sections[i], CpeMessageType.Normal, p.hasCP437));
                }
            }
            
            
            string folder    { get { return "tbotanims/"; } }
            string directory { get { return "/home/na2/Website-Files/" + folder; } }
            string fileName  { get { return p.name+".txt"; } }
            string fullPath  { get { return directory + fileName; } }
            string url       { get { return "https://notawesome.cc/" + folder + fileName; } }
        }
        
        public class CmdFlipCoin : Command2 {
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
        
        public class CmdMoveBots : Command2 {
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
}
