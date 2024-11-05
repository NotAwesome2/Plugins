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
using System.Text;
using System.Linq;

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

        const string Folder = "plugins/higherlight/";
        const string HighlightBlocksFile = Folder + "highlightblocks.properties";
        const string PlacedProp = "HighlightPlaced";
        const string DeletedProp = "HighlightDeleted";


        /// <summary>
        /// Lime and TNT. Cannot change.
        /// </summary>
        static BlockID[] defaultBlocks = new BlockID[] { Block.Lime, Block.TNT };
        public static BlockID GetDefault(int i) {
            return defaultBlocks[i];
        }
        /// <summary>
        /// Initialized with LoadHighlightBlocks
        /// </summary>
        BlockID[] preferredBlocks = null;

        public BlockID this[int i] {
            get { return preferredBlocks[i]; }
        }
        public int Length { get { return preferredBlocks.Length; } }

        public void Load() {
            GenerateFile();

            string[] lines = File.ReadAllLines(HighlightBlocksFile);

            preferredBlocks = (BlockID[])defaultBlocks.Clone();

            foreach (string l in lines) {
                if (l.StartsWith('#')) continue;

                string line = RemoveWhitespace(l);
                string[] words = line.Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length != 2) continue;

                string prop = words[0];
                string value = words[1];

                if (prop.CaselessEq(PlacedProp)) { ParseProp(prop, value, 0); continue; }
                if (prop.CaselessEq(DeletedProp)) { ParseProp(prop, value, 1); continue; }

                Logger.Log(LogType.Warning, "higherlight.cs: There is no config property \"{0}\".", prop);
            }
        }
        static void GenerateFile() {
            Directory.CreateDirectory(Folder);
            if (!File.Exists(HighlightBlocksFile)) {
                File.WriteAllLines(HighlightBlocksFile,
                    new string[] {
                        "# This file controls which block IDs should be used for /highlight blocks.",
                        "# Lines starting with # are ignored",
                        "# If these are set to blank or 0, the server will ignore them and automatically generate highlight blocks to use.",
                        PlacedProp + " = ",
                        DeletedProp + " = ",
                    }
                );
            }
        }
        void ParseProp(string prop, string value, int index) {
            BlockID parsed;
            if (!BlockID.TryParse(value,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out parsed)) {
                Logger.Log(LogType.Warning, "higherlight.cs: Could not parse {0} = {1} as a block ID.", prop, value);
                return;
            }
            if (parsed == Block.Obsidian) { Logger.Log(LogType.Warning, "higherlight.cs: Obsidian is not a valid highlight block."); return; }

            if (parsed > 0 && parsed <= Block.MaxRaw) {
                preferredBlocks[index] = Block.FromRaw(parsed);
            }
        }

        static string RemoveWhitespace(string input) {
            StringBuilder sb = new StringBuilder(input.Length);
            foreach (char c in input) {
                if (Char.IsWhiteSpace(c)) continue;
                sb.Append(c);
            }
            return sb.ToString();
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
        public static BlockID[] actualHighlightBlocks {
            get {
                return new BlockID[] { HighlightDrawOp.DefaultPlaceHighlight, HighlightDrawOp.DefaultDeleteHighlight };
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
        public static void OnJoiningLevel(Player p, Level lvl, ref bool canJoin) {
            if (!canJoin) return; //Not really foolproof if another plugin runs after this, but good enough...
            CleanupHighlight(p);
        }
        public static void OnPlayerCommand(Player p, string cmd, string args, CommandData data) {
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
                HighlightDrawOp.DefaultPlaceHighlight = config[0];
                HighlightDrawOp.DefaultDeleteHighlight = config[1];
                return false;
            }

            BlockDefinition[] defs = p.level.CustomBlockDefs;

            for (int i = 0; i < config.Length; i++) {
                BlockID prefBlock = config[i];

                BlockDefinition def;
                if (defs[prefBlock] == null) {
                    if (prefBlock <= Block.CPE_MAX_BLOCK) {
                        //The user might choose a block from the default cc set that the server hasn't made a blockdef for yet
                        def = DefaultSet.MakeCustomBlock(prefBlock);
                    } else {
                        //The user chose a block that isn't in the default set and hasn't been defined at all. Fallback to default.
                        def = DefaultSet.MakeCustomBlock(HighlightConfig.GetDefault(i));
                    }
                } else {
                    //The user chose a block that the server has a blockdef for. Use its blockdef.
                    def = defs[prefBlock].Copy();
                }

                string oldName = def.Name;
                def.Name = i == 0 ? "Placed highlight" : "Deleted highlight";
                if (oldName.EndsWith('#')) {
                    def.Name += '#';
                }
                if (oldName.StartsWith('#')) {
                    def.Name = '#' + def.Name;
                }

                def.Brightness = 8;
                def.UseLampBrightness = true;
                def.SetBlock(freeIDs[i]);

                p.Session.SendDefineBlock(def);
            }
            p.Extras[TempBlockdefsKey] = freeIDs;

            HighlightDrawOp.DefaultPlaceHighlight = freeIDs[0];
            HighlightDrawOp.DefaultDeleteHighlight = freeIDs[1];
            return true;
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
        /// 
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
                p.Message("Removing highlight blockdef {0}", def.RawID);
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

        const string xrayFlag = " xray";
        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { Help(p); return; }

            bool xray = false;
            if (message.CaselessEnds(xrayFlag)) {
                xray = true;
                message = message.Substring(0, message.Length - xrayFlag.Length);
            }

            if (!ValidateHighlightInput(p, message)) return;


            
            if (!PluginHigherlight.SetupHighlightBlocks(p)) {
                ReplaceExistingHighlightBlocks(p, 0, 0, 0, p.level.MaxX, p.level.MaxY, p.level.MaxZ, 0, 0, 0);
            }
            DarkenBlocks(p, xray);
            if (xray) AddZone(p);

            PluginHigherlight.highlight.Use(p, message, data);
            CommandData data2 = data;
            data2.Context = CommandContext.MessageBlock;
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

            List<ushort> highlights = PluginHigherlight.actualHighlightBlocks.ToList();

            for (int i = 0; i < defs.Length; i++) {
                if (defs[i] == null) continue;

                if (!xray && defs[i].FullBright == false) continue;
                if (highlights.Contains(defs[i].GetBlock())) continue;

                BlockDefinition copy = defs[i].Copy();

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

            BlockID[] highlightBlocks = PluginHigherlight.actualHighlightBlocks;

            BufferedBlockSender buffer = new BufferedBlockSender(p);
            int index = 0;
            for (int yi = y3 + yLen - 1; yi >= y3; --yi) {
                for (int zi = z3; zi < z3 + zLen; ++zi) {
                    for (int xi = x3; xi < x3 + xLen; ++xi) {

                        bool place = false;
                        foreach (BlockID b in highlightBlocks) {
                            if (p.level.GetBlock((ushort)xi, (ushort)yi, (ushort)zi) == b) { place = true; break; }
                        }
                        if (!place) { continue; }
                        
                        int pos = p.level.PosToInt((ushort)xi, (ushort)yi, (ushort)zi);
                        if (pos >= 0) buffer.Add(pos, Block.Obsidian);
                        index++;
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
            p.Message("&HAdd \"{0}\" to make all blocks except highlight invisible.", xrayFlag);
        }
    }
}
