#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenOP2.SpriteLoaders
{
	public class Op2SpriteLoader : ISpriteLoader
	{
		class BitmapSpriteFrame : ISpriteFrame
		{
			public SpriteFrameType Type { get; set; }
			public Size Size { get; set; }
			public Size FrameSize { get; set; }
			public float2 Offset { get; set; }
			public byte[] Data { get; set; }
			public bool DisableExportPadding { get { return false; } }
		}

		List<uint[]> framePalettes;

		void LoadPalettes(Stream s)
		{
			var cpal = s.ReadASCII(4);
			if (cpal != "CPAL")
				throw new InvalidDataException();

			framePalettes = new List<uint[]>();
			var paletteCount = s.ReadUInt32();
			for (var p = 0; p < paletteCount; p++)
			{
				var ppal = s.ReadASCII(4);
				if (ppal != "PPAL")
					throw new InvalidDataException();

				var offset = s.ReadUInt32();

				var head = s.ReadASCII(4);
				if (head != "head")
					throw new InvalidDataException();

				var bytesPerEntry = s.ReadUInt32();
				var unknown = s.ReadUInt32();

				var data = s.ReadASCII(4);
				if (data != "data")
					throw new InvalidDataException();

				var paletteSize = s.ReadUInt32();
				var colors = paletteSize / bytesPerEntry;
				var paletteData = new Color[colors];
				for (var c = 0; c < colors; c++)
				{
					var blue = s.ReadByte();
					var green = s.ReadByte();
					var red = s.ReadByte();
					paletteData[c] = Color.FromArgb(red, green, blue);
					var reserved = s.ReadByte();
				}

				framePalettes.Add(paletteData.Select(d => (uint)d.ToArgb()).ToArray());
			}
		}

		public bool TryParseSprite(Stream s, out ISpriteFrame[] frames, out TypeDictionary metadata)
		{
			var start = s.Position;

			var signature = s.ReadASCII(2);
			if (signature != "BM")
			{
				s.Position = start;
				metadata = null;
				frames = null;
				return false;
			}

			var size = s.ReadUInt32();
			var reserved1 = s.ReadUInt16();
			var reserved2 = s.ReadUInt16();
			var dataStart = s.ReadUInt32();

			var prt = Game.ModData.DefaultFileSystem.Open("op2_art.prt");
			LoadPalettes(prt);
			var palettes = new Dictionary<int, uint[]>();

			var spriteCount = prt.ReadUInt32();
			frames = new ISpriteFrame[spriteCount];
			for (var f = 0; f < frames.Length; f++)
			{
				var paddedWidth = prt.ReadUInt32();
				var dataOffset = prt.ReadUInt32();
				var height = prt.ReadUInt32();
				var width = prt.ReadUInt32();
				var type = prt.ReadUInt16();
				var palette = prt.ReadUInt16();
				palettes.Add(f, framePalettes[palette]);

				var dataSize = new Size((int)paddedWidth, (int)height);
				var frameSize = new Size((int)width, (int)height);

				s.Seek(dataStart + dataOffset, SeekOrigin.Begin);
				var data = s.ReadBytes((int)(paddedWidth * height));

				frames[f] = new BitmapSpriteFrame()
				{
					Size = dataSize,
					FrameSize = frameSize,
					Data = data,
					Type = SpriteFrameType.Indexed
				};
			}

			metadata = new TypeDictionary { new EmbeddedSpritePalette(framePalettes: palettes) };

			return true;
		}
	}
}
