using System;
using System.IO;
using MCGalaxy.Drawing;
using MCGalaxy.Drawing.Brushes;
using MCGalaxy.Drawing.Ops;
using MCGalaxy.Maths;
using MCGalaxy.Util;
using MCGalaxy.Commands;
using BlockID = System.UInt16;

namespace MCGalaxy.Commands.Building {
	public sealed class WriteFontPlugin : Plugin {
		public override string name { get { return "WriteFontPlugin"; } }
		public override string creator { get { return ""; } }
		public override string welcome { get { return ""; } }
		public override string MCGalaxy_Version { get { return "1.9.4.2"; } }
		
		public override void Load(bool startup) {
			Command.Register(new CmdWriteFont());
			Command.Register(new CmdWriteChat());
		}
		public override void Unload(bool shutdown) {
			Command.Unregister(Command.Find("WriteFont"));
			Command.Unregister(Command.Find("WriteChat"));
		}
		
	}
	
	class CmdWriteFont : DrawCmd {
		public override string name { get { return "WriteFont"; } }
		public override LevelPermission defaultRank { get { return LevelPermission.Guest; } }
		
		protected override string SelectionType { get { return "direction"; } }
		protected override string PlaceMessage { get { return "Place or break two blocks to determine direction."; } }
		
		public override void Use(Player p, string message, CommandData data) {
			string[] args = message.SplitSpaces(2);
			if (args.Length < 2) { Help(p); return; }
			string font = args[0].ToLower();
			
			byte scale = 1, spacing = 1;
			bool validFont = false;
			foreach (string fontFile in GetFonts()) { if (fontFile == font) { validFont = true; break; } }
			if (!validFont) {
				p.Message("There is no font \"{0}\".", font);
				MessageFonts(p);
				return;
			}
			
			if (!Formatter.ValidName(p, font, "file")) return;
			string path = "extra/fonts/" + font + ".png";
			if (!File.Exists(path)) { p.Message("&WFont {0} doesn't exist", path); return; }
			
			FontWriteDrawOp op = new FontWriteDrawOp();
			op.Scale = scale; op.Spacing = spacing;
			op.Path  = path;  op.Text    = args[1];
			op.Text = Chat.Format(op.Text, p);
			op.Text = ProfanityFilter.Parse(op.Text);
			
			// TODO: filthy copy paste
			DrawArgs dArgs = new DrawArgs();
			dArgs.Message = message;
			dArgs.Player = p;
			dArgs.Op = op;
			
			// Validate the brush syntax is correct
			BrushFactory factory = MakeBrush(dArgs);
			BrushArgs bArgs = new BrushArgs(p, dArgs.BrushArgs, dArgs.Block);
			if (!factory.Validate(bArgs)) return;
			
			p.Message(PlaceMessage);
			p.MakeSelection(MarksCount, "Selecting " + SelectionType + " for %S" + dArgs.Op.Name, dArgs, DoDraw);
			// END filthy copy paste
		}
		
		protected override DrawOp GetDrawOp(DrawArgs dArgs) { return null; }
		
		protected override void GetMarks(DrawArgs dArgs, ref Vec3S32[] m) {
			if (m[0].X != m[1].X || m[0].Z != m[1].Z) return;
			dArgs.Player.Message("No direction was selected");
			m = null;
		}
		
		protected override void GetBrush(DrawArgs dArgs) { dArgs.BrushArgs = ""; }

		public override void Help(Player p) {
			p.Message("&T/WriteFont [font] [message]");
			p.Message("&HWrites [message] in blocks. Supports color codes.");
			MessageFonts(p);
			p.Message("&HUse &T/WriteChat &Hfor default font shortcut.");
		}
		static void MessageFonts(Player p) {
			p.Message("&HAvailable fonts: &b{0}", String.Join("&H, &b", GetFonts()));
		}
		static string[] GetFonts() {
			const string directory = "extra/fonts/";
			DirectoryInfo info = new DirectoryInfo(directory);
			FileInfo[] fontFiles = info.GetFiles();
			string[] allFonts = new string[fontFiles.Length];
			for (int i = 0; i < allFonts.Length; i++) {
				allFonts[i] = fontFiles[i].Name.Replace(".png", "");
			}
			return allFonts;
		}
	}
	
	sealed class CmdWriteChat : CmdWriteFont {
		public override string name { get { return "WriteChat"; } }

		public override void Use(Player p, string message, CommandData data) {
			if (message.Length == 0) { Help(p); return; }
			base.Use(p, "default " + message, data);
		}

		public override void Help(Player p) {
			p.Message("&T/WriteChat [message]");
			p.Message("&HWrites [message] with NA2 chat font. Supports color codes.");
			p.Message("&HSee &T/WriteFont &Hfor other fonts.");
		}
	}
	
	class FontWriteDrawOp : DrawOp {
		public override string Name { get { return "FontWrite"; } }
		public string Text, Path;
		public byte Scale, Spacing;
		public byte GlyphWidth, GlyphHeight;
		
		IPaletteMatcher selector = new RgbPaletteMatcher();
		ImagePalette palette     = ImagePalette.Find("Color");
		
		public override long BlocksAffected(Level lvl, Vec3S32[] marks) {
			int count = 0;
			byte[] data = File.ReadAllBytes(Path);

			using (IBitmap2D img = IBitmap2D.Create())
			{
				img.Decode(data);
				GlyphWidth = (byte)(img.Width >> 4);
				GlyphHeight = (byte)(img.Height >> 4);

				img.LockBits();

				for (int i = 0; i < Text.Length; i++) {
					char c = Text[i].UnicodeToCp437();

					if (c == '&' && i < Text.Length - 1) {
						char n = Text[i + 1].UnicodeToCp437();
						if (!Colors.List[n].Undefined) { i++; continue; }
					}
					count += CountBlocks(c, img);
				}
			}
			return count;
		}
		
		Vec3S32 dir, pos;
		public override void Perform(Vec3S32[] marks, Brush brush, DrawOpOutput output) {
			Vec3S32 p1 = marks[0], p2 = marks[1];
			if (Math.Abs(p2.X - p1.X) > Math.Abs(p2.Z - p1.Z)) {
				dir.X = p2.X > p1.X ? 1 : -1;
			} else {
				dir.Z = p2.Z > p1.Z ? 1 : -1;
			}
			
			pos = p1;
			selector.SetPalette(palette.Entries, palette.Entries);
			byte[] data = File.ReadAllBytes(Path);

			using (IBitmap2D img = IBitmap2D.Create())
			{
				img.Decode(data);
				GlyphWidth = (byte)(img.Width >> 4);
				GlyphHeight = (byte)(img.Height >> 4);

				img.LockBits();
				ColorDesc tint = Colors.List['f'];
				
				for (int i = 0; i < Text.Length; i++) {
					char c = Text[i].UnicodeToCp437();
					
					// tint text by colour code
					if (c == '&' && i < Text.Length - 1) {
						char n = Text[i+1].UnicodeToCp437();
						if (!Colors.List[n].Undefined) {
							tint = Colors.List[n]; i++;
							continue;
						}
					}
					DrawLetter(Player, c, img, tint, output); 
				}
			}
		}
		
		static int GetTileWidth(IBitmap2D src, int x, int y, int width, int height) {
			for (int xx = width - 1; xx >= 0; xx--) {
				// Is there a pixel in this column
				for (int yy = 0; yy < height; yy++) {
					if (src.Get(x + xx, y + yy).A > 20) return xx + 1;
				}
			}
			return 0;
		}

		int CountBlocks(char c, IBitmap2D src) {
			int X = (c & 0x0F) * GlyphWidth;
			int Y = (c >> 4)   * GlyphHeight;
			int width = GetTileWidth(src, X, Y, GlyphWidth, GlyphHeight);

			if (width == 0) {
				return 0;
			} else {
				int modified = 0;

				for (int xx = 0; xx < width; xx++) {
					for (int yy = 0; yy < GlyphHeight; yy++) {
						Pixel P = src.Get(X + xx, Y + (GlyphHeight - 1 - yy));
						if (P.A < 127) continue;

						modified += Scale * Scale;
					}
				}

				return modified;
			}
		}
		
		void DrawLetter(Player p, char c, IBitmap2D src, ColorDesc tint, DrawOpOutput output) {
			int X = (c & 0x0F) * GlyphWidth;
			int Y = (c >> 4)   * GlyphHeight;
			int width = GetTileWidth(src, X, Y, GlyphWidth, GlyphHeight);
			
			if (width == 0) {
				if (c != ' ') p.Message("\"{0}\" is not currently supported, replacing with space.", c);
				pos += dir * (GlyphWidth / 4 * Scale);
			} else {
				for (int xx = 0; xx < width; xx++) {
					for (int yy = 0; yy < GlyphHeight; yy++) {
						Pixel P = src.Get(X + xx, Y + (GlyphHeight-1-yy));
						if (P.A <= 127) continue;
						
						BlockID b;
						if (P.R == 128 && P.G == 128 && P.B == 128) {
							b = Block.FromRaw(383); //lower white slab
						}
						else if (P.R == 255 && P.G == 0 && P.B == 0) {
							b = Block.FromRaw(384); //upper white slab
						}
						else {
							P.R = (byte)(P.R * tint.R / 255); 
							P.G = (byte)(P.G * tint.G / 255);
							P.B = (byte)(P.B * tint.B / 255);
							b = selector.BestMatch(ref P);
						}
						
						for (int ver = 0; ver < Scale; ver++)
							for (int hor = 0; hor < Scale; hor++)
						{
							int x = pos.X + dir.X * hor, y = pos.Y + yy * Scale + ver, z = pos.Z + dir.Z * hor;
							output(Place((ushort)x, (ushort)y, (ushort)z, b));
						}
					}
					pos += dir * Scale;
				}
			}
			pos += dir * Spacing;
		}
	}
	
}


