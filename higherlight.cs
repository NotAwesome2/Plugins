//reference System.Core.dll
//reference System.dll

using System;
using System.Collections.Generic;
using MCGalaxy;
using MCGalaxy.Blocks;
using MCGalaxy.Commands;
using MCGalaxy.DB;
using MCGalaxy.Drawing.Ops;
using MCGalaxy.Events.ServerEvents;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Network;
using MCGalaxy.Maths;
using BlockID = System.UInt16;
using System.IO;
using MCGalaxy.Config;

namespace NA2 {

    public static class EnvUtils {

        public const string EnvDarkenedKey = "Higherlight_zoneDarkened";

        public static void Darken(Player p) {
            //sky 0
            //cloud 1
            //fog 2
            //shadow 3
            //sun 4
            for (int i = 0; i < 5; i++) {
                string color = "555555";
                if (i <= 1) color = "9b9b9b";
                p.Session.SendSetEnvColor((byte)i, color);
            }
            p.Extras[EnvDarkenedKey] = true;
        }

        /// <summary>
        /// Re-darkens the env if it is still meant to be dark.
        /// </summary>
        public static void RefreshDarken(Player p) {
            if (!p.Extras.GetBoolean(EnvDarkenedKey)) return;
            Darken(p);
        }

    }

    public class HighlightConfig {

        public const BlockID PlacedFallback = Block.Lime;
        public const BlockID DeletedFallback = Block.TNT;

        [ConfigUShort("HighlightPlaced", "Highlight")]
        public BlockID highlightPlaced = PlacedFallback;
        [ConfigUShort("HighlightDeleted", "Highlight")]
        public BlockID highlightDeleted = DeletedFallback;
        
        const string Folder = "plugins/higherlight/";
        const string HighlightBlocksFile = Folder + "highlightblocks.properties";

        static ConfigElement[] cfg;
        public void Load() {
            if (!File.Exists(HighlightBlocksFile)) GenerateFile();
            
            if (cfg == null) cfg = ConfigElement.GetAll(typeof(HighlightConfig));

            ConfigElement.ParseFile(cfg, HighlightBlocksFile, this);
            ValidateHighlightBlock(ref highlightPlaced , PlacedFallback);
            ValidateHighlightBlock(ref highlightDeleted, DeletedFallback);
        }
        
        void GenerateFile() {
            Directory.CreateDirectory(Folder);
            if (cfg == null) cfg = ConfigElement.GetAll(typeof(HighlightConfig));
            using (StreamWriter w = new StreamWriter(HighlightBlocksFile)) {
                w.WriteLine("# This file controls which block IDs should be used for /highlight blocks.");
                w.WriteLine("# Lines starting with # are ignored");
                w.WriteLine("# 0 or an out-of-range value will fallback to the default of Lime and TNT respectively.");
                w.WriteLine();
                ConfigElement.Serialise(cfg, w, this);
            }
            
        }
        void ValidateHighlightBlock(ref BlockID block, BlockID fallback) {
            //When using highlight, there may be existing highlight blocks in the world. These get replaced by obsidian before highlight occurs, 
            //so don't allow using obsidian as a highlight block
            if (block < 1 || block > Block.MaxRaw || block == Block.Obsidian) { block = fallback; return; }
            block = Block.FromRaw(block); //Config file is raw value. Need to convert here
        }
    }

    public sealed class PluginHigherlight : Plugin {
        public override string name { get { return "Higherlight"; } }
        public override string MCGalaxy_Version { get { return "1.9.5.0"; } }
        public override string creator { get { return "Goodly"; } }

        public static Command highlight;
        public static Command2 higherlightCmd;

        public static HighlightConfig config;
        /// <summary>
        /// Read-only collection of currently set place and delete highlight blocks
        /// </summary>
        public static List<BlockID> HighlightBlocks {
            get {
                return new List<BlockID> { HighlightDrawOp.DefaultPlaceHighlight, HighlightDrawOp.DefaultDeleteHighlight };
            }
        }

        public override void Load(bool startup) {
            config = new HighlightConfig();
            config.Load();
            
            highlight = Command.Find("highlight");
            
            higherlightCmd = new CmdHigherlight();
            
            Command.Unregister(highlight);
            Command.Register(higherlightCmd);

            OnConfigUpdatedEvent.Register(config.Load, Priority.Low);
            OnJoiningLevelEvent.Register(OnJoiningLevel, Priority.Low);
            OnPlayerCommandEvent.Register(OnPlayerCommand, Priority.Low);
            OnChangedZoneEvent.Register(EnvUtils.RefreshDarken, Priority.Low);
        }
        public override void Unload(bool shutdown) {
            HighlightDrawOp.DefaultPlaceHighlight = Block.Green;
            HighlightDrawOp.DefaultDeleteHighlight = Block.Red;
            
            Command.Register(highlight);
            Command.Unregister(higherlightCmd);

            OnConfigUpdatedEvent.Unregister(config.Load);
            OnJoiningLevelEvent.Unregister(OnJoiningLevel);
            OnPlayerCommandEvent.Unregister(OnPlayerCommand);
            OnChangedZoneEvent.Unregister(EnvUtils.RefreshDarken);
        }
        
        /// <summary>
        /// Runs before the level switches, removing the blockdefs.
        /// </summary>
        static void OnJoiningLevel(Player p, Level lvl, ref bool canJoin) {
            if (!canJoin) return; //Not really foolproof if another plugin runs after this, but good enough...
            CleanupHighlight(p);
        }
        
        static void OnPlayerCommand(Player p, string cmd, string args, CommandData data) {
            if (cmd.CaselessEq("reload")) CleanupHighlight(p);
        }

        /// <summary>
        /// Generates highlight blockdefs and sets the HighlightDrawOp blocks to them.
        /// Returns true if enough spaces were free to generate new highlight blockdefs.
        /// </summary>
        public static bool SetupHighlightBlocks(Player p) {

            List<BlockID> freeIDs = GetFreeIDs(p, 2);
            if (freeIDs == null) {
                //Worst case scenario. Blockdef IDs aren't free so we need to use the actual preferredBlock ID as the highlight blocks.
                HighlightDrawOp.DefaultPlaceHighlight = config.highlightPlaced;
                HighlightDrawOp.DefaultDeleteHighlight = config.highlightDeleted;
                return false;
            }

            BlockDefinition[] defs = p.level.CustomBlockDefs;

            SetupHighlightBlock(p, freeIDs, defs, config.highlightPlaced, 0);
            SetupHighlightBlock(p, freeIDs, defs, config.highlightDeleted, 1);
            p.Extras[TempBlockdefsKey] = freeIDs;

            HighlightDrawOp.DefaultPlaceHighlight = freeIDs[0];
            HighlightDrawOp.DefaultDeleteHighlight = freeIDs[1];
            return true;
        }

        static void SetupHighlightBlock(Player p, List<BlockID> freeIDs, BlockDefinition[] defs, BlockID prefBlock, int i) {
            BlockDefinition def;
            if (defs[prefBlock] == null) {
                if (prefBlock <= Block.CPE_MAX_BLOCK) {
                    //The user might choose a block from the default cc set that the server hasn't made a blockdef for yet
                    def = DefaultSet.MakeCustomBlock(prefBlock);
                } else {
                    //The user chose a block that isn't in the default set and hasn't been defined at all. Fallback to default.
                    def = DefaultSet.MakeCustomBlock(i == 0 ? HighlightConfig.PlacedFallback : HighlightConfig.DeletedFallback);
                }
            } else {
                //The user chose a block that the server has a blockdef for. Use its blockdef.
                def = defs[prefBlock].Copy();
            }

            string oldName = def.Name;
            def.Name = i == 0 ? "Placed highlight" : "Deleted highlight";
            if (oldName.EndsWith("#")) {
                def.Name += '#';
            }
            if (oldName.StartsWith("#")) {
                def.Name = '#' + def.Name;
            }

            def.Brightness = 8;
            def.UseLampBrightness = true;
            def.SetBlock(freeIDs[i]);

            p.Session.SendDefineBlock(def);
        }
        
        /// <summary>
        /// Returns a list of MCGalaxy blockIDs or null if amount not found
        /// </summary>
        static List<BlockID> GetFreeIDs(Player p, int amount) {

            BlockDefinition[] defs = p.level.CustomBlockDefs;

            List<BlockID> blocks = new List<BlockID>();

            for (BlockID b = Block.CPE_COUNT; b <= Block.MaxRaw; b++) {
                BlockID block = Block.FromRaw(b);
                if (defs[block] == null) blocks.Add(block);
                if (blocks.Count == amount) { return blocks; }
            }

            return null;
        }

        const string TempBlockdefsKey = "Higherlight_blockdefs";
        /// <summary>
        /// Removes the blocks generated by highlight, if they haven't already been removed.
        /// Removes the flag that keeps env dark for player.
        /// </summary>
        public static void CleanupHighlight(Player p) {
            p.Extras.Remove(EnvUtils.EnvDarkenedKey);
            object o;
            if (!p.Extras.TryGet(TempBlockdefsKey, out o)) { return; }
            p.Extras.Remove(TempBlockdefsKey);

            List<BlockID> blocks = (List<BlockID>)o;
            foreach (BlockID id in blocks) {
                BlockDefinition def = new BlockDefinition();
                def.RawID = Block.ToRaw(id);
                p.Session.SendUndefineBlock(def);
            }
        }

    }

    public class CmdHigherlight : Command2 {
        public override string name { get { return "Highlight"; } }
        public override string shortcut { get { return "hl"; } }
        public override bool MessageBlockRestricted { get { return true; } }
        public override string type { get { return "Moderation"; } }
        public override bool museumUsable { get { return false; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Admin; } }

        const string XRAY_FLAG = " xray";
        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { Help(p); return; }

            bool xray = false;
            if (message.CaselessEnds(XRAY_FLAG)) {
                xray = true;
                message = message.Substring(0, message.Length - XRAY_FLAG.Length);
            }

            if (!ValidateHighlightInput(p, message)) return;


            
            if (!PluginHigherlight.SetupHighlightBlocks(p)) {
                ReplaceExistingHighlightBlocks(p, 0, 0, 0, p.level.MaxX, p.level.MaxY, p.level.MaxZ, 0, 0, 0);
            }
            DarkenBlocks(p, xray);
            if (xray) AddZone(p);

            PluginHigherlight.highlight.Use(p, message, data);
            EnvUtils.Darken(p);
            p.Message("Use %b/reload %Sto fix blocks and environment colors.");
        }
        /// <summary>
        /// Duplicates the cmd parsing checks from original hl command
        /// </summary>
        static bool ValidateHighlightInput(Player p, string message) {
            string[] words = message.SplitSpaces();
            if (words.Length >= 2) {
                TimeSpan unused = new TimeSpan();
                if (!CommandParser.GetTimespan(p, words[1], ref unused, "highlight the past", "s")) return false;
            }
            words[0] = PlayerDB.MatchNames(p, words[0]);
            if (words[0] == null) return false;
            return true;
        }

        static void DarkenBlocks(Player p, bool xray) {
            BlockDefinition[] defs = p.level.CustomBlockDefs;

            List<BlockID> highlights = PluginHigherlight.HighlightBlocks;

            for (int i = 0; i < defs.Length; i++) {
                BlockDefinition def = defs[i];

                if (defs[i] == null) {
                    if (i <= Block.CPE_MAX_BLOCK) {
                        //The server might not have defined the default blocks
                        def = DefaultSet.MakeCustomBlock((ushort)i);
                    } else {
                        //No default or custom block found, skip
                        continue;
                    }
                }

                if (!xray && !def.FullBright) continue;
                if (highlights.Contains(def.GetBlock())) continue;

                BlockDefinition copy = def.Copy();

                copy.FullBright = false;
                copy.UseLampBrightness = false;
                copy.Brightness = 0;
                if (xray) {
                    copy.BlockDraw = 4;
                    //Force into cube shape so blockdraw guaranteed applies
                    copy.Shape = 16;
                    copy.MinX = 0;
                    copy.MinY = 0;
                    copy.MinZ = 0;

                    copy.MaxX = 0;
                    copy.MaxY = 0;
                    copy.MaxZ = 0;
                    //preserve map edge collision
                    if (copy.RawID != Block.Bedrock) {
                        copy.CollideType = 0;
                    }
                }

                p.Session.SendDefineBlock(copy);
                //p.Send(Packet.DefineBlockExt(copy, true, true, true, true));
            }

        }
        static void AddZone(Player p) {
            var min = new Vec3U16(0, 0, 0);
            var max = new Vec3U16(p.level.Width, p.level.Height, p.level.Length);

            //Invert max and min so the line is inside the bedrock
            p.Send(Packet.MakeSelection(127, "map bounds", max, min, 0, 0, 0, 1, true));
        }
        
        static void ReplaceExistingHighlightBlocks(Player p, int x1, int y1, int z1, int x2, int y2, int z2, int x3, int y3, int z3) {

            int xLen = (x2 - x1) + 1;
            int yLen = (y2 - y1) + 1;
            int zLen = (z2 - z1) + 1;

            List<BlockID> highlightBlocks = PluginHigherlight.HighlightBlocks;

            BufferedBlockSender buffer = new BufferedBlockSender(p);
            for (int yi = y3 + yLen - 1; yi >= y3; --yi) {
                for (int zi = z3; zi < z3 + zLen; ++zi) {
                    for (int xi = x3; xi < x3 + xLen; ++xi) {
                        if (!highlightBlocks.Contains(p.level.GetBlock((ushort)xi, (ushort)yi, (ushort)zi))) continue;

                        int pos = p.level.PosToInt((ushort)xi, (ushort)yi, (ushort)zi);
                        if (pos >= 0) buffer.Add(pos, Block.Obsidian);
                    }
                }
            }
            buffer.Flush();
        }

        public override void Help(Player p) {
            p.Message("&T/Highlight [player] <timespan> <xray>");
            p.Message("&HHighlights blocks changed by [player] in the past <timespan>");
            p.Message("&H If <timespan> is not given, highlights for last 30 minutes");
            p.Message("&W/Highlight cannot be disabled, use /reload to un-highlight");
            p.Message("&HAdd \"{0}\" to make all blocks except highlight invisible.", XRAY_FLAG);
        }
    }
}
