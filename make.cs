//reference System.Core.dll

using System;
using System.Linq;
using MCGalaxy;
using MCGalaxy.Commands;
using MCGalaxy.DB;
using MCGalaxy.Blocks;
using System.Collections.Generic;
using System.IO;

using BlockID = System.UInt16;

namespace NA2 {
    public sealed class Make : Plugin {
        public override string name { get { return "make"; } }
        public override string creator { get { return "Goodly"; } }
        public override string MCGalaxy_Version { get { return "1.9.4.7"; } }
        
        static Command makeCommand = new CmdMake();
        static Command makeGBCommand = new CmdMakeGB();
        public override void Load(bool startup) {
            CmdMakeBase.Load();
            
            Command.Register(makeCommand);
            Command.Register(makeGBCommand);
        }
        public override void Unload(bool shutdown) {
            Command.Unregister(makeCommand);
            Command.Unregister(makeGBCommand);
        }
    }
    
    public abstract class CmdMakeBase : Command2 {
        
        protected delegate void MakeType(CmdMakeBase maker, Player p, int origin, string name, BlockID requestedSlot);
        protected static string TYPES = "";
        protected static Dictionary<string, MakeType> makeActions = new Dictionary<string, MakeType>();
        
        protected static readonly object locker = new object();
        
        public static void Load() {
            makeActions["slabs"] = MakeSlabs;
            makeActions["walls"] = MakeWalls;
            makeActions["stairs"] = MakeStairs;
            makeActions["flatstairs"] = MakeFlatStairs;
            makeActions["corners"] = MakeCorners;
            makeActions["eighths"] = MakeEighths;
            makeActions["panes"] = MakePanes;
            makeActions["poles"] = MakePoles;
            
            TYPES = string.Join(", ", makeActions.Keys);
        }
        
        static string GetBlockName(Player p, BlockID blockID) {
            blockID = Block.FromRaw(blockID); //convert from clientside to server side ID
            
            if (Block.IsPhysicsType(blockID)) return "Physics block";
            
            BlockDefinition def = null;
            if (!p.IsSuper) {
                def = p.level.GetBlockDef(blockID);
            } else {
                def = BlockDefinition.GlobalDefs[blockID];
            }
            if (def == null && blockID > 0 && blockID < Block.CPE_COUNT) {
                p.Message("Making default");
                def = DefaultSet.MakeCustomBlock(blockID);
                p.Message("def.Name is {0}", def.Name);
            }
            
            if (def != null) { return def.Name; }
            
            return "Unknown";
        }
        
        protected abstract bool CanUse(Player p);
        protected abstract Command BlockDefCommand { get; }
        protected abstract int Dir { get; }
        protected abstract int DefaultRequestedSlot { get; }
        protected abstract BlockID IterateTo { get; }
        protected abstract BlockDefinition[] GetBlockDefs(Player p);
        protected abstract string BlockPropsScope { get; }
        
        Command _defCmd;
        public Command defCmd { get { return _defCmd; } private set { _defCmd = value; } }
        public override void Use(Player p, string message, CommandData data) {
            if (!CanUse(p)) {
                return;
            }
            
            if (message == "") { Help(p); return; }
            string[] args = message.SplitSpaces(3);
            if (args.Length < 2) { Help(p); return; }
            
            string type = args[0].ToLower();
            BlockID sourceID;
            if (!CommandParser.GetBlock(p, args[1], out sourceID)) { return; }
            sourceID = Block.Convert(sourceID); // convert physics blocks to their visual form
            sourceID = Block.ToRaw(sourceID);   // convert server block IDs to client block IDs
            
            string name = GetBlockName(p, (BlockID)sourceID);
            int requestedSlot = DefaultRequestedSlot;
            if (requestedSlot == -1) {
                p.Message("&WThere are no unused custom block ids left.");
                return;
            }
            if (args.Length > 2) {
                if (!CommandParser.GetInt(p, args[2], "starting ID", ref requestedSlot, Block.CPE_COUNT, Block.MaxRaw)) { return; } 
            }
            
            //p.Message("requested slot is {0}", requestedSlot);
            //p.Message("IterateTo: {0}", IterateTo);
            
            if (makeActions.ContainsKey(type)) {
                lock (locker) {
                    defCmd = BlockDefCommand; //assign to temporary variable to avoid Command.Find spam
                    makeActions[type](this, p, sourceID, name, (BlockID)requestedSlot);
                }
            } else {
                p.Message("%WInvalid type \"{0}\".", type);
                p.Message("Types can be: %b{0}", TYPES);
            }
        }
        
        bool Iterator(ref BlockID b) {
            if (b > IterateTo) { b--; return true; }
            else if (b < IterateTo) { b++; return true; }
            //reached goal stop iterating
            return false;
        }
        protected bool AreEnoughBlockIDsFree(Player p, int amountRequested, out int startID, BlockID requestedSlot) {
            startID = 0;
            int amountFound = 0;
            
            BlockDefinition[] defs = GetBlockDefs(p);
            
            BlockID b = requestedSlot;
            do {
                BlockID cur = Block.FromRaw(b);
                if (defs[cur] == null) {
                    if (startID == 0) {
                        startID = b;
                    }
                    amountFound++;
                    if (amountFound == amountRequested) {
                        return true;
                    }
                    continue;
                }
                
                if (startID != 0) {
                    //if a starting point was already found and we run into a used slot before finding the amount requested, stop
                    break;
                }
            } while (Iterator(ref b));
            
            p.Message("%WThere are not enough free sequential block IDs to perform this command.");
            string start = (startID != 0) ? " starting at " + startID : "";
            p.Message("(found {0} free IDs{1}, but needed {2})", amountFound, start, amountRequested);
            return false;
        }
        
        static void MakePanes(CmdMakeBase maker, Player p, int origin, string name, BlockID requestedSlot) {
            int dest;
            if (!maker.AreEnoughBlockIDsFree(p, 2, out dest, requestedSlot)) { return; }
            
            //WE  (0, 0, 6) to (16, 16, 10)
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-WE");
            maker.defCmd.Use(p, "edit " + dest + " min 0 0 6");
            maker.defCmd.Use(p, "edit " + dest + " max 16 16 10");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //NS (6, 0, 0) to (10, 16, 16)
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-NS");
            maker.defCmd.Use(p, "edit " + dest + " min 6 0 0");
            maker.defCmd.Use(p, "edit " + dest + " max 10 16 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
        }
        static void MakeSlabs(CmdMakeBase maker, Player p, int origin, string name, BlockID requestedSlot) {
            int dest;
            if (!maker.AreEnoughBlockIDsFree(p, 2, out dest, requestedSlot)) { return; }
            
            //down
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-D");
            maker.defCmd.Use(p, "edit " + dest + " min 0 0 0");
            maker.defCmd.Use(p, "edit " + dest + " max 16 8 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 1");
            Command.Find("blockproperties").Use(p, maker.BlockPropsScope+" "+(dest)+" stackblock "+origin);
            dest += maker.Dir;
            //up
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-U");
            maker.defCmd.Use(p, "edit " + dest + " min 0 8 0");
            maker.defCmd.Use(p, "edit " + dest + " max 16 16 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 1");
            
        }
        static void MakeWalls(CmdMakeBase maker, Player p, int origin, string name, BlockID requestedSlot) {
            int dest;
            if (!maker.AreEnoughBlockIDsFree(p, 4, out dest, requestedSlot)) { return; }
            
            int height = 16;
            //north
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-N");
            maker.defCmd.Use(p, "edit " + dest + " min 0 0 0");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 8");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //south
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-S");
            maker.defCmd.Use(p, "edit " + dest + " min 0 0 8");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //west
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-W");
            maker.defCmd.Use(p, "edit " + dest + " min 0 0 0");
            maker.defCmd.Use(p, "edit " + dest + " max 8 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //east
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-E");
            maker.defCmd.Use(p, "edit " + dest + " min 8 0 0");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
        }
        static void MakeStairs(CmdMakeBase maker, Player p, int origin, string name, BlockID requestedSlot) {
            int dest;
            if (!maker.AreEnoughBlockIDsFree(p, 8, out dest, requestedSlot)) { return; }
            
            
            int height = 8;
            //north
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-D-N");
            maker.defCmd.Use(p, "edit " + dest + " min 0 0 0");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 8");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //south
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-D-S");
            maker.defCmd.Use(p, "edit " + dest + " min 0 0 8");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //west
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-D-W");
            maker.defCmd.Use(p, "edit " + dest + " min 0 0 0");
            maker.defCmd.Use(p, "edit " + dest + " max 8 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //east
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-D-E");
            maker.defCmd.Use(p, "edit " + dest + " min 8 0 0");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            
            //--------------------------------------------------------------------------------upper
            height = 16;
            
            //north
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-U-N");
            maker.defCmd.Use(p, "edit " + dest + " min 0 8 0");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 8");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //south
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-U-S");
            maker.defCmd.Use(p, "edit " + dest + " min 0 8 8");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //west
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-U-W");
            maker.defCmd.Use(p, "edit " + dest + " min 0 8 0");
            maker.defCmd.Use(p, "edit " + dest + " max 8 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //east
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-U-E");
            maker.defCmd.Use(p, "edit " + dest + " min 8 8 0");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
        }
        static void MakeFlatStairs(CmdMakeBase maker, Player p, int origin, string name, BlockID requestedSlot) {
            int dest;
            if (!maker.AreEnoughBlockIDsFree(p, 8, out dest, requestedSlot)) { return; }
            
            
            int height = 8;
            //north
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-D-N");
            maker.defCmd.Use(p, "edit " + dest + " min 0 0 0");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 1");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //south
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-D-S");
            maker.defCmd.Use(p, "edit " + dest + " min 0 0 15");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //west
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-D-W");
            maker.defCmd.Use(p, "edit " + dest + " min 0 0 0");
            maker.defCmd.Use(p, "edit " + dest + " max 1 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //east
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-D-E");
            maker.defCmd.Use(p, "edit " + dest + " min 15 0 0");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            
            //--------------------------------------------------------------------------------upper
            height = 16;
            
            //north
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-U-N");
            maker.defCmd.Use(p, "edit " + dest + " min 0 8 0");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 1");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //south
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-U-S");
            maker.defCmd.Use(p, "edit " + dest + " min 0 8 15");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //west
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-U-W");
            maker.defCmd.Use(p, "edit " + dest + " min 0 8 0");
            maker.defCmd.Use(p, "edit " + dest + " max 1 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //east
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-U-E");
            maker.defCmd.Use(p, "edit " + dest + " min 15 8 0");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
        }
        static void MakeCorners(CmdMakeBase maker, Player p, int origin, string name, BlockID requestedSlot) {
            int dest;
            if (!maker.AreEnoughBlockIDsFree(p, 4, out dest, requestedSlot)) { return; }
            
            int height = 16;
            //north
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-NW");
            maker.defCmd.Use(p, "edit " + dest + " min 0 0 0");
            maker.defCmd.Use(p, "edit " + dest + " max 8 "+height+" 8");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //south
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-SE");
            maker.defCmd.Use(p, "edit " + dest + " min 8 0 8");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //west
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-SW");
            maker.defCmd.Use(p, "edit " + dest + " min 0 0 8");
            maker.defCmd.Use(p, "edit " + dest + " max 8 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //east
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-NE");
            maker.defCmd.Use(p, "edit " + dest + " min 8 0 0");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 8");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
        }
        static void MakeEighths(CmdMakeBase maker, Player p, int origin, string name, BlockID requestedSlot) {
            int dest;
            if (!maker.AreEnoughBlockIDsFree(p, 8, out dest, requestedSlot)) { return; }
            
            int height = 8;
            //north
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-D-NW");
            maker.defCmd.Use(p, "edit " + dest + " min 0 0 0");
            maker.defCmd.Use(p, "edit " + dest + " max 8 "+height+" 8");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //south
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-D-SE");
            maker.defCmd.Use(p, "edit " + dest + " min 8 0 8");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //west
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-D-SW");
            maker.defCmd.Use(p, "edit " + dest + " min 0 0 8");
            maker.defCmd.Use(p, "edit " + dest + " max 8 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //east
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-D-NE");
            maker.defCmd.Use(p, "edit " + dest + " min 8 0 0");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 8");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            
            
            height = 16;
            //north
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-U-NW");
            maker.defCmd.Use(p, "edit " + dest + " min 0 8 0");
            maker.defCmd.Use(p, "edit " + dest + " max 8 "+height+" 8");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //south
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-U-SE");
            maker.defCmd.Use(p, "edit " + dest + " min 8 8 8");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //west
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-U-SW");
            maker.defCmd.Use(p, "edit " + dest + " min 0 8 8");
            maker.defCmd.Use(p, "edit " + dest + " max 8 "+height+" 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //east
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-U-NE");
            maker.defCmd.Use(p, "edit " + dest + " min 8 8 0");
            maker.defCmd.Use(p, "edit " + dest + " max 16 "+height+" 8");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
        }
        static void MakePoles(CmdMakeBase maker, Player p, int origin, string name, BlockID requestedSlot) {
            int dest;
            if (!maker.AreEnoughBlockIDsFree(p, 3, out dest, requestedSlot)) { return; }
            
            //UD
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-UD");
            maker.defCmd.Use(p, "edit " + dest + " min 4 0 4");
            maker.defCmd.Use(p, "edit " + dest + " max 12 16 12");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //NS
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-NS");
            maker.defCmd.Use(p, "edit " + dest + " min 4 4 0");
            maker.defCmd.Use(p, "edit " + dest + " max 12 12 16");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
            dest += maker.Dir;
            //WE
            maker.defCmd.Use(p, "copy " + origin + " " + dest);
            maker.defCmd.Use(p, "edit " + dest + " name " + name + "-WE");
            maker.defCmd.Use(p, "edit " + dest + " min 0 4 4");
            maker.defCmd.Use(p, "edit " + dest + " max 16 12 12");
            maker.defCmd.Use(p, "edit " + dest + " blockslight 0");
        }
        
        public override void Help(Player p) {
            p.Message("%T/{0} [type] [block] <optional starting ID>", name);
            p.Message("%HCreates block variants out of [block] based on [type].");
            p.Message("%H[type] can be: %b{0}", TYPES);
            p.Message("%HFor example, %T/make eighths stone %Hwould give you 8 new stone eighth piece blocks.");
        }
    }
    
    public sealed class CmdMake : CmdMakeBase {
        public override string name { get { return "Make"; } }
        public override string shortcut { get { return ""; } }
        public override string type { get { return "other"; } }
        public override bool museumUsable { get { return false; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Guest; } }
        
        
        protected override bool CanUse(Player p) {
            bool canUse = false;
            if (LevelInfo.IsRealmOwner(p.name, p.level.name)) canUse = true;
            if (p.group.Permission >= LevelPermission.Operator && p.group.Permission >= p.level.BuildAccess.Min) { canUse = true; }
            if (!canUse) { p.Message("&cYou can only use this command on your own maps."); }
            return canUse;
        }
        protected override Command BlockDefCommand { get { return Command.Find("levelblock"); } }
        protected override int Dir { get { return -1; } }
        protected override int DefaultRequestedSlot { get { return Block.MaxRaw; } }
        protected override BlockID IterateTo { get { return Block.CPE_COUNT; } }
        protected override BlockDefinition[] GetBlockDefs(Player p) {
            return p.level.CustomBlockDefs;
        }
        protected override string BlockPropsScope { get { return "level"; } }
    }
    
    public sealed class CmdMakeGB : CmdMakeBase {
        public override string name { get { return "MakeGB"; } }
        public override string shortcut { get { return ""; } }
        public override string type { get { return "other"; } }
        public override bool museumUsable { get { return false; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Admin; } }
        
        
        protected override bool CanUse(Player p) { return true; }
        protected override Command BlockDefCommand { get { return Command.Find("globalblock"); } }
        protected override int Dir { get { return 1; } }
        protected override int DefaultRequestedSlot {
            get {
                BlockDefinition[] defs = BlockDefinition.GlobalDefs;
                for (BlockID b = Block.CPE_COUNT; b <= Block.MaxRaw; b++) {
                    BlockID block = Block.FromRaw(b);
                    if (defs[block] == null) return b;
                }
                return -1;
            }
        }
        protected override BlockID IterateTo { get { return Block.MaxRaw; } }
        protected override BlockDefinition[] GetBlockDefs(Player p) { return BlockDefinition.GlobalDefs; }
        protected override string BlockPropsScope { get { return "global"; } }
    }
}
