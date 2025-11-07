//reference System.Core.dll

using System;
using System.Collections.Generic;
using MCGalaxy;
using MCGalaxy.Commands;
using MCGalaxy.Commands.World;
using MCGalaxy.Generator;
using MCGalaxy.Drawing.Brushes;
using MCGalaxy.Drawing.Ops;
using MCGalaxy.Maths;
using MCGalaxy.Events.PlayerEvents;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Threading;

using BlockID = System.UInt16;

//using Vector3 = MCGalaxy.Maths.Vec3F32;
//using Vector3I = MCGalaxy.Maths.Vec3S32;

namespace NA2 {
    

    public sealed class RV : Plugin {
        

        public override string name { get { return "rv"; } }
        public override string MCGalaxy_Version { get { return "1.9.4.9"; } }
        public override string creator { get { return "Goodly"; } }
        
        Command[] cmds = new Command[] { new CmdReplaceVars(), new CmdPaintVars(), };
        
        public override void Load(bool startup) {
            foreach (Command cmd in cmds) { Command.Register(cmd); }
            
            OnBlockChangingEvent.Register(OnBlockChanging, Priority.Low);
            OnBlockChangedEvent.Register(OnBlockChanged, Priority.Low);
            OnPlayerCommandEvent.Register(OnPlayerCommand, Priority.Low);
        }
        public override void Unload(bool shutdown) {
            foreach (Command cmd in cmds) { Command.Unregister(cmd); }
            
            OnBlockChangingEvent.Unregister(OnBlockChanging);
            OnBlockChangedEvent.Unregister(OnBlockChanged);
            OnPlayerCommandEvent.Unregister(OnPlayerCommand);
        }
        
        const string BKEY = "pv_paintedBlock";
        static void OnBlockChanging(Player p, ushort x, ushort y, ushort z, BlockID block, bool placing, ref bool cancel) {
            if (!CmdPaintVars.Painting(p)) { return; }
            
            p.Extras[BKEY] = p.level.GetBlock(x, y, z);
        }
        static void OnBlockChanged(Player p, ushort x, ushort y, ushort z, ChangeResult result) {
            if (!CmdPaintVars.Painting(p)) { return; }
            
            BlockID here = (BlockID)p.Extras[BKEY]; //p.level.GetBlock(x, y, z);
            BlockID held = p.GetHeldBlock();
            
            string hereName = Block.GetName(p, here);
            Variant v = Variant.Get(p, hereName, held);
            
            BlockID output;
            if (v == null) {
                output = Block.Invalid;
            } else {
                output = v[here];
            }
            
            if (output == Block.Invalid) { p.Message("No matching {0} shape found.", Block.GetName(p, held)); output = here; }
            
            p.level.UpdateBlock(p, x, y, z, output);
            
        }
        static void OnPlayerCommand(Player p, string cmd, string message, CommandData data) {
            if (cmd.CaselessEq("abort")) { CmdPaintVars.SetPainting(p, false); }
        }
    }
    
    
    public class CmdPaintVars : Command2 {
        public override string name { get { return "PaintVars"; } }
        public override string shortcut { get { return "pv"; } }
        public override bool MessageBlockRestricted { get { return false; } }
        public override string type { get { return CommandTypes.Building; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
        
        const string KEY = "RV_PAINTING";
        public static bool Painting(Player p) { return p.Extras.GetBoolean(KEY); }
        public static void SetPainting(Player p, bool value) { if (value == false) { p.Extras.Remove(KEY); return; } p.Extras[KEY] = value; }
        
        public override void Use(Player p, string message, CommandData data) {
            if (message.Length > 0) { Help(p); return; }
            SetPainting(p, !Painting(p));
            
            string type = Painting(p) ? "&aON" : "&cOFF";
            p.Message("Painting variant mode: " + type + "&S.");
        }
        
        public override void Help(Player p) {
            p.Message("&T/PaintVars");
            p.Message("&HLike /paint, but attempts to match the block shape you are painting into");
            p.Message("&HMake sure you're holding the full-sized version of the block you want to paint with.");
            p.Message("&HSee also: &T/ReplaceVars");
        }
    }
    
    public class CmdReplaceVars : Command2 {
        public override string name { get { return "ReplaceVars"; } }
        public override string shortcut { get { return "rv"; } }
        public override bool MessageBlockRestricted { get { return false; } }
        public override string type { get { return CommandTypes.Building; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
        
        public override void Use(Player p, string message, CommandData data)
        {
            if (message.Length == 0) { Help(p); return; }
            string[] args = message.SplitSpaces();
            if (args.Length < 1) { p.Message("You must give a block to be replaced and a optionally block to replace with."); return; }
            
            BlockID target;
            BlockID output = Block.Invalid;
            
            if (!CommandParser.GetBlockIfAllowed(p, args[0], "draw with", out target)) { return; }
            if (args.Length > 1 && !CommandParser.GetBlockIfAllowed(p, args[1], "draw with", out output)) { return; }
            
            var rvArgs = new RvArgs(target, output);
            
            p.MakeSelection(2, "&fSelection region for &SReplaceVar", rvArgs, Callback);
            p.Message("Place or break two blocks to determine the edges.");
        }
        class RvArgs {
            public BlockID target, output;
            public RvArgs(BlockID target, BlockID output) {
                this.target = target; this.output = output;
            }
        }
        static bool Callback(Player p, Vec3S32[] marks, object state, BlockID block) {
            RvArgs rvArgs = (RvArgs)state;
            BlockID target = rvArgs.target;
            BlockID output = rvArgs.output == Block.Invalid ? block : rvArgs.output;
            
            Variant variant = new Variant(p, target, output);
            ReplaceVarOp op = ReplaceVarOp.Create(p, variant);
            
            if (op == null) { return false; }
            DrawOpPerformer.Do(op, null, p, marks);
            return true;
        }
        
        public override void Help(Player p) {
            p.Message("&T/ReplaceVars [block] <other>");
            p.Message("&HReplaces [block] and its stairs, slabs, etc, with <other> and its stairs, slabs, etc.");
            p.Message("&HIf <other> is not provided, uses your currently held block");
            p.Message("&HSee also: &T/PaintVars");
        }
    }
    
    public class Variant {
        Dictionary<BlockID, BlockID> outputBlock = new Dictionary<BlockID, BlockID>();
        
        public Variant(Player p, BlockID target, BlockID output) {
            outputBlock[target] = output;
            
            //no spaces are in this
            
            string targetName = StripVarNames(GetPrefix(Block.GetName(p, target)));
            string outputName = StripVarNames(GetPrefix(Block.GetName(p, output)));
            
            var targets = new List<BlockDefinition>();
            var outputs = new List<BlockDefinition>();
            
            foreach (var def in p.level.CustomBlockDefs) {
                if (def == null) { continue; }
                PopulateDict(p, targetName, def, targets);
                PopulateDict(p, outputName, def, outputs);
            }
            
            
            foreach (var targetDef in targets) {
                
                string targetSuffix = GetSuffix(targetDef);
                
                if (targetSuffix == null) { continue; }
                
                foreach (var outputDef in outputs) {
                    string outputSuffix = GetSuffix(outputDef);
                    
                    if (outputSuffix.CaselessEq(targetSuffix)) {
                        outputBlock[targetDef.GetBlock()] = outputDef.GetBlock();
                    }
                }
            }
        }
        public static Variant Get(Player p, string targetName, BlockID output) {
            BlockID targetID = FindBaseBlock(p, targetName);
            if (targetID == Block.Invalid) { return null; }
            return new Variant(p, targetID, output);
        }
        static BlockID FindBaseBlock(Player p, string varBlockName) {
            
            BlockDefinition varDef = null;
            foreach (var def in p.level.CustomBlockDefs) {
                if (def == null) { continue; }
                //varBlockName has no spaces, so strip from def when matching
                if (def.Name.Replace(" ", "").CaselessEq(varBlockName)) {
                    varDef = def;
                    break;
                }
            }
            if (varDef == null) { return Block.Invalid; }
            
            
            string baseBlockName = StripVarNames(GetPrefix(varDef.Name));
            //p.Message("Looking for {0}...", baseBlockName);
            
            foreach (var def in p.level.CustomBlockDefs) {
                if (def == null) { continue; }
                //Both have spaces
                if (def.Name.CaselessEq(baseBlockName)) {
                    return def.GetBlock();
                }
            }
            //p.Message("Failed.");
            return Block.Invalid;
        }
        
        //If a var contains another var, it must be placed earlier in the list than the one it contains.
        //e.g. walls must come before wall otherwise "walls" will become "s"
        static string[] varNames = new string[] { " slab", " walls", " wall", " stair", " corner" };
        
        static void PopulateDict(Player p, string groupName, BlockDefinition def, List<BlockDefinition> list) {
            if (def.Name.IndexOf('-') == -1) { return; }
            
            string defName = GetPrefix(def.Name);
            
            defName = StripVarNames(defName);
            
            if (defName.Replace(" ", "").CaselessEq(groupName)) { list.Add(def); }
        }
        static string StripVarNames(string input) {
            foreach (string varName in varNames) {
                input = input.ToLower().Replace(varName, "");
            }
            return input;
        }
        static string GetSuffix(BlockDefinition def) {
            int dashIndex = def.Name.IndexOf('-'); if (dashIndex == -1) { return null; }
            return def.Name.Substring(dashIndex+1);
        }
        static string GetPrefix(string input) {
            int dashIndex = input.IndexOf('-'); if (dashIndex == -1) { return input; }
            return input.Substring(0, dashIndex);
        }
        
        public BlockID this[int index]
        {
            get { return outputBlock.ContainsKey((BlockID)index) ? outputBlock[(BlockID)index] : Block.Invalid; }
        }
    }
    
    public class ReplaceVarOp : DrawOp {
        
        public static ReplaceVarOp Create(Player p, Variant variant) {
            return new ReplaceVarOp(p, variant);
        }
        public override string Name { get { return "ReplaceVariants"; } }
        public override long BlocksAffected(Level lvl, Vec3S32[] marks) { return SizeX * SizeY * SizeZ; }
        
        Player p;
        Level level;
        Variant variant;
        
        private ReplaceVarOp(Player p, Variant variant) {
            this.Player = p;
            this.p = p;
            level = p.level;
            Level = p.level;
            this.variant = variant;
        }
        DrawOpOutput output;
        
        public override void Perform(Vec3S32[] marks, Brush brush, DrawOpOutput output) {
            this.output = output;
            
            Vec3U16 p1 = Clamp(Min), p2 = Clamp(Max);
            for (ushort y = p1.Y; y <= p2.Y; y++)
                for (ushort z = p1.Z; z <= p2.Z; z++)
                    for (ushort x = p1.X; x <= p2.X; x++)
            {
                BlockID toPlace = variant[level.GetBlock(x, y, z)];
                output(Place(x, y, z, toPlace));
            }
        }
        
    }

}
