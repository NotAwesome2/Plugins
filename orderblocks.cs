using System;
using System.Threading;
using MCGalaxy;
using MCGalaxy.Commands;
using MCGalaxy.Blocks;
using BlockID = System.UInt16;

namespace MCGalaxy {
	public sealed class PluginOrderBlocks : Plugin {
		public override string name { get { return "OrderBlocks"; } }
		public override string MCGalaxy_Version { get { return "1.9.0.5"; } }
		public override string creator { get { return "goodly"; } }

		Command orderBlocksCmd;
		public override void Load(bool startup) {
			orderBlocksCmd = new CmdOrderBlocks();
			Command.Register(orderBlocksCmd);
		}
		public override void Unload(bool shutdown) {
		    Command.Unregister(orderBlocksCmd);
		}
	}
	
	public class CmdOrderBlocks : Command2
	{
		public override string name { get { return "OrderBlocks"; } }
		public override string shortcut { get { return ""; } }
		public override bool MessageBlockRestricted { get { return true; } }
		public override string type { get { return "other"; } }
		public override bool museumUsable { get { return false; } }
		public override LevelPermission defaultRank { get { return LevelPermission.Admin; } }
		public override void Use(Player p, string message, CommandData data)
		{
		    if (!CanUse(p)) { p.Message("You can't use {0} in this map. /help OrderBlocks", name); return; }
		    
		    if (message.CaselessEq("place current order")) {
		        PlaceCurrentOrder(p);
		        return;
		    }
		    
			int loops = 767;
			for(int i = 1; i < loops+1; ++i)
			{
				Command.Find("globalblock").Use(p, "edit "+i+" order 0");
				
				//p.Message("i is " + i + ".");
			}
		    
		    int maxBlock = 767;
		    int orderIndex = 1;
		    for (ushort z = 0; z < 153; z = (ushort)(z+2)) {
		        //p.Message("z is {0}", z);
		        
    		    for (ushort x = 0; x < 19; x = (ushort)(x+2)) {
		            
    		        //p.Message("x is {0}", x);
    		        //p.level.UpdateBlock(p, x, 0, z, Block.Stone, BlockDBFlags.Drawn, true);
    		        BlockID curBlock = p.level.GetBlock(x, 0, z);
    		        
    		        if (Block.IsPhysicsType(curBlock)) {
    		            p.Message("skipping block with ToRaw ID of {0}", Block.ToRaw(curBlock));
    		            orderIndex++;
    		            continue;
    		        }
    		        BlockID clientID = Block.ToRaw(curBlock);
    		        
    		        if (curBlock != Block.Air) {
    		            //p.Message("The block's ID is {0}", curBlock);
    		            //p.Message("The block's clientside id is {0}", clientID);
    		            
    		            Command.Find("globalblock").Use(p, "edit "+clientID+" order "+orderIndex);
    		        }
    		        
    		        orderIndex++;
    		        if (orderIndex >= maxBlock) { break; }
    		    }
		        if (orderIndex >= maxBlock) { break; }
		    }
		    p.Message("we placed {0} blocks!", orderIndex);
		    
		}
		
		void PlaceCurrentOrder(Player p) {
		    p.Message("nothing");
		}
		
		bool CanUse(Player p) {
		    Level lvl = p.level;
		    if (lvl.name != "blockorder") { return false; }
		    if (lvl.Width != 19) { return false; }
		    //if (lvl.Height != 1) { return false; }
		    if (lvl.Length != 153) { return false; }
		    return true;
		}
		
		public override void Help(Player p)
		{
            p.Message("%T/OrderBlocks");
            p.Message("%HRe-orders the global inventory based on the layout of the blocks in the map you're in.");
            p.Message("%HThe map must be named \"blockorder\" and be sized 19 anyY 153.");
		}

	}
    
}