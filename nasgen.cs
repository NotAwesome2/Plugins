using System;
using System.Threading;
using System.Collections.Generic;
using MCGalaxy.Commands;
using MCGalaxy.Tasks;

using LibNoise;
using MCGalaxy;
using BlockID = System.UInt16;
using MCGalaxy.Generator;
using MCGalaxy.Generator.Foliage;

namespace MCGalaxy {
    
    public sealed class PluginNasGen : Plugin {
        public override string name { get { return "PluginNasGen"; } }
        public override string MCGalaxy_Version { get { return "1.9.2.0"; } }
        public override string creator { get { return "Goodly"; } }


        public override void Load(bool startup) {
            MapGen.Register("nasGen", GenType.Advanced, NasGen.Gen, "hello?");
        }
        public override void Unload(bool shutdown) {
            
        }
    }
    
    public static class NasGen {
        public const int mapWideness = 384;
        public const int mapTallness = 256;
        public const ushort oceanHeight = 60;
        public const ushort coalDepth = 4;
        public const ushort ironDepth = 16;
        public const ushort goldDepth = 50;
        public const ushort diamondDepth = 56;
        public const float coalChance = 1f;
        public const float ironChance = 1f/4f;
        public const float goldChance = 1f/8f;
        public const float diamondChance = goldChance * 0.25f;

        /// <summary>
        /// Returns true if seed and offsets were succesfully found
        /// </summary>
        public static bool GetSeedAndChunkOffset(string mapName, ref string seed, ref int chunkOffsetX, ref int chunkOffsetZ) {
            string[] bits = mapName.Split('_');
            if (bits.Length <= 1) { return false; }
            
            seed = bits[0];
            string[] chunks = bits[1].Split(',');
            if (chunks.Length <= 1) { return false; }
            
            if (!Int32.TryParse(chunks[0], out chunkOffsetX)) { return false; }
            if (!Int32.TryParse(chunks[1], out chunkOffsetZ)) { return false; }
            return true;
        }
        
        static int MakeInt(string seed) {
            if (seed.Length == 0) return new Random().Next();
            
            int value;
            if (!int.TryParse(seed, out value)) value = seed.GetHashCode();
            return value;
        }
        
        public static bool currentlyGenerating = false;
        public static bool Gen(Player p, Level lvl, MapGenArgs args) {
            if (lvl.Height < mapTallness) {
                p.Message("&cNasgen only works with world height &b{0}&c or greater.", mapTallness);
                p.Message("Try something like &b128 {0} 128 nasgen &Sfor generation arguments.", mapTallness);
                return false;
            }
            
            currentlyGenerating = true;
            int offsetX = 0, offsetZ = 0;
            int chunkOffsetX = 0, chunkOffsetZ = 0;
            
            string seed = args.Args;
            GetSeedAndChunkOffset(lvl.name, ref seed, ref chunkOffsetX, ref chunkOffsetZ);
            
            offsetX = chunkOffsetX * mapWideness;
            offsetZ = chunkOffsetZ * mapWideness;
            offsetX -= chunkOffsetX;
            offsetZ -= chunkOffsetZ;
            p.Message("offsetX offsetZ {0} {1}", offsetX, offsetZ);

            Perlin adjNoise = new Perlin();
            adjNoise.Seed = MakeInt(seed);
            Random r = new Random(adjNoise.Seed);
            DateTime dateStart = DateTime.UtcNow;

            GenInstance instance = new GenInstance();
            instance.p = p;
            instance.lvl = lvl;
            instance.adjNoise = adjNoise;
            instance.offsetX = offsetX;
            instance.offsetZ = offsetZ;
            instance.r = r;
            instance.seed = seed;
            instance.Do();

            //lvl.Config.Deletable = false;
            //lvl.Config.MOTD = "-hax +thirdperson";
            //lvl.Config.GrassGrow = false;
            TimeSpan timeTaken = DateTime.UtcNow.Subtract(dateStart);
            p.Message("Done in {0}", timeTaken.Shorten(true, true));

            //GotoInfo info = new GotoInfo();
            //info.p = p;
            //info.levelName = lvl.name;
            //SchedulerTask task = Server.MainScheduler.QueueOnce(Goto, info, TimeSpan.FromMilliseconds(1500));
            currentlyGenerating = false;
            return true;
        }
        public class GenInstance {
            public Player p;
            public Level lvl;
            //public NasLevel nl;
            public ushort[,] heightmap;
            public Perlin adjNoise;
            public float[,] temps;
            public int offsetX, offsetZ;
            public Random r;
            public string seed;
            BlockID topSoil;
            BlockID soil;

            public void Do() {
                CalcTemps();
                GenTerrain();
                CalcHeightmap();
                GenSoil();
                GenCaves();
                GenPlants();
                //GenOre();
                
            }

            void CalcTemps() {
                adjNoise.OctaveCount = 2;
                p.Message("Calculating temperatures");
                temps = new float[lvl.Width, lvl.Length];
                for (double z = 0; z < lvl.Length; ++z) {
                    for (double x = 0; x < lvl.Width; ++x) {
                        //divide by more for bigger scale
                        double scale = 150;
                        double xVal = (x + offsetX) / scale, zVal = (z + offsetZ) / scale;
                        const double adj = 1;
                        xVal += adj;
                        zVal += adj;
                        float val = (float)adjNoise.GetValue(xVal, 0, zVal);
                        val+= 0.1f;
                        val/= 2;
                        //if (z == 0) { Player.Console.Message("temp is {0}", val); }
                        temps[(int)x, (int)z] = val;
                    }
                }
            }
            void GenTerrain() {
                p.Message("Generating terrain");
                //more frequency = smaller map scale
                adjNoise.Frequency = 0.75;
                adjNoise.OctaveCount = 5;
                DateTime dateStartLayer;
                int counter = 0;
                double width = lvl.Width, height = lvl.Height, length = lvl.Length;

                counter = 0;
                dateStartLayer = DateTime.UtcNow;
                for (double y = 0; y < height; y++) {
                    //p.Message("Starting {0} layer.", ListicleNumber((int)(y+1)));
                    for (double z = 0; z < length; ++z)
                        for (double x = 0; x < width; ++x) {
                            //if (y < 128) {
                            //    lvl.SetTile((ushort)x, (ushort)(y), (ushort)z, Block.Stone);
                            //    continue;
                            //} else {
                            //    continue;
                            //}

                            if (y == 0) {
                                lvl.SetTile((ushort)x, (ushort)(y), (ushort)z, Block.Bedrock);
                                continue;
                            }
                            
                            
                            double threshDiv = temps[(int)x,(int)z];
                            threshDiv*= 1.5;
                            if (threshDiv <= 0) { threshDiv = 0; }
                            if (threshDiv > 1) { threshDiv = 1; }
                            //threshDiv = 1;
                            
                            
                            //double tallRandom = adjNoise.GetValue((x+offsetX)/500, 0, (z+offsetZ)/500);
                            //tallRandom*= 200;
                            //if (tallRandom <= 0.0) { tallRandom = 0.0; }
                            //else if (tallRandom > 1.0) { tallRandom = 1.0; }
                            
                            
                            double averageLandHeightAboveSeaLevel = 1;// - (6*tallRandom);
                            double minimumFlatness = 5;
                            double maxFlatnessAdded = 28;
                            
                            //multiply by more to more strictly follow halfway under = solid, above = air
                            double threshold =
                                (((y + (oceanHeight - averageLandHeightAboveSeaLevel)) / (height)) - 0.5)
                                * (minimumFlatness + (maxFlatnessAdded * threshDiv)); //4.5f
                            //threshold = 0;
                            
                            if (threshold < -1.5) {
                                lvl.SetTile((ushort)x, (ushort)(y), (ushort)z, Block.Stone);
                                continue;
                            }
                            if (threshold > 1.5) { continue; }

                            //divide y by less for more "layers"
                            
                            double xVal = (x + offsetX) / 200, yVal = y / (250 + (150 * threshDiv)), zVal = (z + offsetZ) / 200;
                            const double shrink = 2;
                            xVal *= shrink;
                            yVal *= shrink;
                            zVal *= shrink;
                            const double adj = 1;
                            xVal += adj;
                            yVal += adj;
                            zVal += adj;
                            double value = adjNoise.GetValue(xVal, yVal, zVal);
                            //if (counter % (256*256) == 0) {
                            //    Thread.Sleep(10);
                            //}
                            //counter++;

                            


                            if (value > threshold) {
                                lvl.SetTile((ushort)x, (ushort)(y), (ushort)z, Block.Stone);
                            } else if (y < oceanHeight) {
                                lvl.SetTile((ushort)x, (ushort)(y), (ushort)z, Block.Water);
                            }
                        }
                    TimeSpan span = DateTime.UtcNow.Subtract(dateStartLayer);
                    if (span > TimeSpan.FromSeconds(5)) {
                        p.Message("Initial gen {0}% complete.", (int)((y / height) * 100));
                        dateStartLayer = DateTime.UtcNow;
                    }
                }
                p.Message("Initial gen 100% complete.");



            }
            void CalcHeightmap() {
                p.Message("Calculating heightmap");
                heightmap = new ushort[lvl.Width, lvl.Length];
                for (ushort z = 0; z < lvl.Length; ++z)
                    for (ushort x = 0; x < lvl.Width; ++x) {
                        //         skip bedrock
                        for (ushort y = 1; y < lvl.Height; ++y) {
                            BlockID curBlock = lvl.FastGetBlock(x, y, z);
                            if (curBlock != Block.Stone) {
                                heightmap[x, z] = (ushort)(y - 1);
                                break;
                            }
                        }
                    }
                    //nl
            }
            void GenSoil() {
                int width = lvl.Width, height = lvl.Height, length = lvl.Length;
                p.Message("Now creating soil.");
                adjNoise.Seed = MakeInt(seed + "soil");
                adjNoise.Frequency = 1;
                adjNoise.OctaveCount = 6;

                for (int y = 0; y < height - 1; y++)
                    for (int z = 0; z < length; ++z)
                        for (int x = 0; x < width; ++x) {
                            soil = Block.Dirt;

                            if (lvl.FastGetBlock((ushort)x, (ushort)y, (ushort)z) == Block.Stone &&
                                lvl.FastGetBlock((ushort)x, (ushort)(y + 1), (ushort)z) != Block.Stone
                                && ShouldThereBeSoil(x, y, z)
                               ) {
                                
                                soil = GetSoilType(x, z);
                                if (y <= oceanHeight - 12) {
                                    soil = Block.Gravel;
                                } else if (y <= oceanHeight) {
                                    soil = Block.Sand;
                                }

                                int startY = y;
                                for (int yCol = startY; yCol > startY - 2 - r.Next(0, 2); yCol--) {
                                    if (yCol < 0) { break; }
                                    if (lvl.FastGetBlock((ushort)x, (ushort)(yCol), (ushort)z) == Block.Stone) {
                                        lvl.SetBlock((ushort)x, (ushort)(yCol), (ushort)z, soil);
                                    }
                                }
                            }
                        }
            }
            bool ShouldThereBeSoil(int x, int y, int z) {
                if (
                    IsNeighborLowEnough(x, y, z,-1, 0) ||
                    IsNeighborLowEnough(x, y, z, 1, 0) ||
                    IsNeighborLowEnough(x, y, z, 0,-1) ||
                    IsNeighborLowEnough(x, y, z, 0, 1))
                {
                    return false;
                }
                return true;
            }
            bool IsNeighborLowEnough(int x, int y, int z, int offX, int offZ) {
                int neighborX = x+offX;
                int neighborZ = z+offZ;
                if (neighborX >= lvl.Width  || neighborX < 0 ||
                    neighborZ >= lvl.Length || neighborZ < 0
                   ) { return false; }
                for (int i = 0; i < 4; i++) {
                    if (!lvl.IsAirAt((ushort)neighborX, (ushort)(y-i), (ushort)neighborZ)) {
                        return false;
                    }
                }
                return true;
            }
            void GenCaves() {
                int width = lvl.Width, height = lvl.Height, length = lvl.Length;

                p.Message("Now creating caves");
                adjNoise.Seed = MakeInt(seed + "cave");
                adjNoise.Frequency = 1; //more frequency = smaller map scale
                adjNoise.OctaveCount = 2;

                int counter = 0;
                DateTime dateStartLayer = DateTime.UtcNow;
                for (double y = 0; y < height; y++) {
                    //p.Message("Starting {0} layer.", ListicleNumber((int)(y+1)));
                    for (double z = 0; z < length; ++z)
                        for (double x = 0; x < width; ++x) {
                            double threshold = 0.55;
                            int caveHeight = heightmap[(int)x, (int)z] - 14;
                            if (y > caveHeight) {
                                threshold += 0.05 * (y - (caveHeight));
                            }
                            if (threshold > 1.5) { continue; }
                            bool tryCave = false;
                            BlockID thisBlock = lvl.FastGetBlock((ushort)x, (ushort)(y), (ushort)z);
                            if (thisBlock == Block.Stone || thisBlock == Block.Dirt) { tryCave = true; }
                            if (!tryCave) {
                                continue;
                            }

                            //divide y by less for more "layers"
                            double xVal = (x + offsetX) / 15, yVal = y / 7, zVal = (z + offsetZ) / 15;
                            const double adj = 1;
                            xVal += adj;
                            yVal += adj;
                            zVal += adj;
                            double value = adjNoise.GetValue(xVal, yVal, zVal);

                            //if (counter % (256*256) == 0) {
                            //    Thread.Sleep(10);
                            //}
                            counter++;

                            if (value > threshold) {
                                if (y <= 4) {
                                    lvl.SetTile((ushort)x, (ushort)(y), (ushort)z, Block.Lava);
                                } else {
                                    lvl.SetTile((ushort)x, (ushort)(y), (ushort)z, Block.Air);
                                }
                            }
                        }
                    TimeSpan span = DateTime.UtcNow.Subtract(dateStartLayer);
                    if (span > TimeSpan.FromSeconds(5)) {
                        p.Message("Cave gen {0}% complete.", (int)((y / height) * 100));
                        dateStartLayer = DateTime.UtcNow;
                    }
                }
                p.Message("Cave gen 100% complete.");
            }
            void GenPlants() {
                p.Message("Now creating grass and trees.");
                adjNoise.Seed = MakeInt(seed + "tree");
                adjNoise.Frequency = 1;
                adjNoise.OctaveCount = 1;

                for (int y = 0; y < (ushort)(lvl.Height - 1); y++)
                    for (int z = 0; z < lvl.Length; ++z)
                        for (int x = 0; x < lvl.Width; ++x) {
                            topSoil = Block.Extended|129; //Block.Grass;

                            if (lvl.FastGetBlock((ushort)x, (ushort)y, (ushort)z) == Block.Dirt &&
                                lvl.FastGetBlock((ushort)x, (ushort)(y + 1), (ushort)z) == Block.Air) {

                                if (r.Next(0, 50) == 0 && lvl.IsAirAt((ushort)x, (ushort)(y + 10), (ushort)z)) {
                                    
                                    double xVal = ((double)x + offsetX) / 200, yVal = (double)y / 130, zVal = ((double)z + offsetZ) / 200;
                                    const double adj = 1;
                                    xVal += adj;
                                    yVal += adj;
                                    zVal += adj;
                                    double value = adjNoise.GetValue(xVal, yVal, zVal);
                                    if (value > r.NextDouble()) {
                                        GenTree((ushort)x, (ushort)(y+1), (ushort)z);
                                    } else if (r.Next(0, 20) == 0) {
                                        GenTree((ushort)x, (ushort)(y+1), (ushort)z);
                                    }
                                } else if (r.Next(0, 2) == 0) {
                                    //tallgrass 40 wettallgrass Block.Extended|130
                                    lvl.SetBlock((ushort)x, (ushort)(y+1), (ushort)z, Block.Extended|130);
                                }

                                lvl.SetBlock((ushort)x, (ushort)(y), (ushort)z, topSoil);
                            }
                        }
            }
            void GenTree(ushort x, ushort y, ushort z) {
                topSoil = Block.Dirt;
                NasTree.GenOakTree(lvl, r, x, y, z);
                //nl
            }

            
            BlockID GetSoilType(int x, int z) {
                //if (temps[x,z] > 0.5f) {
                //    return Block.Sand;
                //}
                return Block.Dirt;
            }
            
            void GenOre() {
                for (int y = 0; y < (ushort)lvl.Height - 1; y++)
                    for (int z = 0; z < lvl.Length; ++z)
                        for (int x = 0; x < lvl.Width; ++x) {
                            BlockID curBlock = lvl.FastGetBlock((ushort)x, (ushort)(y), (ushort)z);
                            if (curBlock != Block.Stone) { continue; }
                            TryGenOre(x, y, z, coalDepth, coalChance, 627);
                            TryGenOre(x, y, z, ironDepth, ironChance, 628);
                            TryGenOre(x, y, z, goldDepth, goldChance, 629);
                            TryGenOre(x, y, z, diamondDepth, diamondChance, 630);
                        }
            }
            bool TryGenOre(int x, int y, int z, int oreDepth, float oreChance, BlockID oreID) {
                double chance = (double)(oreChance / 100);
                int height = heightmap[x, z];
                if (height < oceanHeight) { height = oceanHeight; }
                int howManyBlocksYouHaveToTravelDownFromTopToReachHeight = lvl.Height - height;
                howManyBlocksYouHaveToTravelDownFromTopToReachHeight += oreDepth;

                if (y <= lvl.Height - howManyBlocksYouHaveToTravelDownFromTopToReachHeight
                    && r.NextDouble() <= chance
                   ) {
                    //if (r.NextDouble() > 0.5) {
                    //    if (!BlockExposed(lvl, x, y, z)) { return false; }
                    //}
                    lvl.SetBlock((ushort)x, (ushort)y, (ushort)z, Block.FromRaw(oreID));
                    return true;
                }
                return false;
            }
            
            
            bool BlockExposed(int x, int y, int z) {
                if (lvl.IsAirAt((ushort)(x + 1), (ushort)y, (ushort)z)) { return true; }
                if (lvl.IsAirAt((ushort)(x - 1), (ushort)y, (ushort)z)) { return true; }
                if (lvl.IsAirAt((ushort)x, (ushort)(y + 1), (ushort)z)) { return true; }
                if (lvl.IsAirAt((ushort)x, (ushort)(y - 1), (ushort)z)) { return true; }
                if (lvl.IsAirAt((ushort)x, (ushort)y, (ushort)(z + 1))) { return true; }
                if (lvl.IsAirAt((ushort)x, (ushort)y, (ushort)(z - 1))) { return true; }
                return false;
            }
        }
        
    }
    
    public static class NasTree {
        public static void Setup() {
            
        }
        public static void GenOakTree(Level lvl, Random r, int x, int y, int z, bool broadcastChange = false) {
            Tree tree;
            tree = new OakTree();
            tree.SetData(r, r.Next(0, 8));
            PlaceBlocks(lvl, tree, x, y, z, broadcastChange);
        }
        
        private static void PlaceBlocks(Level lvl, Tree tree, int x, int y, int z, bool broadcastChange) {
            tree.Generate((ushort)x, (ushort)(y), (ushort)z, (X, Y, Z, raw) => {
                              BlockID here = lvl.GetBlock(X, Y, Z);
                              if (here == Block.Air || here == Block.Leaves) {
                      lvl.SetTile(X, Y, Z, (byte)raw);
                      if (broadcastChange) {
                          lvl.BroadcastChange(X, Y, Z, raw);
                      }
                  }
            });
        }
    }
    
}
