using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using AnnoDesigner.Core.Helper;
using AnnoDesigner.Core.Layout.Models;
using AnnoDesigner.Core.Models;
using AnnoDesigner.Gamedata;
using FileDBSerializing;
using RDAExplorer;

namespace IslandOutlinesExtractor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "outlines.zip");
            string[] inputFiles = null;

            do
            {
                Console.Write("Please enter the path to the extracted .a7m files (files may be in sub-directories): ");
                string input = Console.ReadLine();
                if (input == "quit") return;

                if (Directory.Exists(input))
                {
                    inputFiles = Directory.GetFiles(input, "*.a7m", SearchOption.AllDirectories);

                    if (inputFiles.Length == 0)
                    {
                        Console.WriteLine("No .a7m files found, please try again or enter 'quit' to exit.");
                        Console.WriteLine();
                        inputFiles = null;
                    }
                }
                else
                {
                    Console.WriteLine("Path does not exists, please try again or enter 'quit' to exit.");
                    Console.WriteLine();
                }
            } while (inputFiles == null);

            using (var outputStream = new FileStream(outputPath, FileMode.Create))
            {
                using (var archive = new ZipArchive(outputStream, ZipArchiveMode.Create, true))
                {
                    foreach (string path in inputFiles)
                    {
                        string islandName = Path.GetFileNameWithoutExtension(path);
                        try
                        {
                            IFileDBDocument gamedata = LoadIslandGamedata(path);
                            ParseIsland(islandName, gamedata, archive);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"SKIPPED {islandName}: {ex.Message}");
                        }
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Saved {inputFiles.Length} outlines in {outputPath}");
        }

        private static IFileDBDocument LoadIslandGamedata(string path)
        {
            using (RDAReader reader = new RDAReader() { FileName = path })
            {
                return reader.File("gamedata.data").GetFileDBDocument();
            }
        }

        private static void ParseIsland(string name, IFileDBDocument gamedata, ZipArchive archive)
        {
            Tag gameSessionManager = gamedata.Tag("GameSessionManager");
            Tag irrigationManager = gameSessionManager.Tag("IrrigationManager");
            Tag worldManager = gameSessionManager.Tag("WorldManager");

            Grid2D<UInt16> areaGrid = ParseBlocks<UInt16>(gameSessionManager.Tag("AreaIDs"));
            Console.WriteLine($"ISLANDSIZE\t{name}\t{areaGrid.Width}\t{areaGrid.Height}");
            Grid2D<bool> irrigationGrid = ParseBlocks<byte>(irrigationManager.Tag("m_StaticTileGrid"), areaGrid.Width, areaGrid.Height).ToBoolean(value => value != 0);
            Grid2D<bool> water = ParseBits(worldManager.Tag("Water"), invert: true);
            Grid2D<bool> river = ParseBits(worldManager.Tag("RiverGrid"));

            Grid2D<bool> islandGrid = new Grid2D<bool>(areaGrid.Width, areaGrid.Height);   // buildable land only
            Grid2D<bool> harbourGrid = new Grid2D<bool>(areaGrid.Width, areaGrid.Height);  // buildable coastal water
            Grid2D<bool> mountainGrid = new Grid2D<bool>(areaGrid.Width, areaGrid.Height); // non-buildable terrain
            Grid2D<bool> landMass = new Grid2D<bool>(areaGrid.Width, areaGrid.Height);      // whole island: buildable + cliffs + rivers

            for (int y = 0; y < areaGrid.Height; y++)
            {
                for (int x = 0; x < areaGrid.Width; x++)
                {
                    UInt16 value = areaGrid[x, y];

                    if (value == 0)
                    {
                        continue; // open sea / off-map
                    }
                    else if (value == 1)
                    {
                        // non-buildable. dry tiles are cliffs (still island), wet is deep sea
                        if (!water[x, y])
                        {
                            mountainGrid[x, y] = true;
                            landMass[x, y] = true;
                        }
                    }
                    else if (value == 0x2001 || value == 0x4001)  // 0x4001 = continental-island buildable zone
                    {
                        if (water[x, y])
                        {
                            harbourGrid[x, y] = true; // coastal water, counts as sea
                        }
                        else
                        {
                            if (!river[x, y]) islandGrid[x, y] = true; // rivers fall through as unpainted interior
                            landMass[x, y] = true;
                        }
                    }
                    else
                    {
                        throw new NotImplementedException($"Area ID 0x{value:X4} not implemented!");
                    }
                }
            }

            // marsh sits on land, trim the bit that spills onto the outer ring
            Grid2D<bool> marshGrid = irrigationGrid.Subtract(islandGrid.ToOutline());

            int W = areaGrid.Width, H = areaGrid.Height;
            bool In(int x, int y) => x >= 0 && x < W && y >= 0 && y < H;
            bool LandMass(int x, int y) => In(x, y) && landMass[x, y];
            // outside the island and not harbour water. the silhouette runs against this so cliffs
            // stay part of the island instead of getting carved out
            bool OpenSea(int x, int y) => !LandMass(x, y) && !(In(x, y) && harbourGrid[x, y]);

            // a thin line won't bevel (a half-tile cut just shaves it into loose triangles), so the
            // outline is a 2 tile band: the outer row bevels against the sea, the row behind it is
            // solid backing, which keeps the coast a clean 45 line
            Grid2D<bool> landEdge = new Grid2D<bool>(W, H);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    landEdge[x, y] = landMass[x, y] && (OpenSea(x - 1, y) || OpenSea(x + 1, y) || OpenSea(x, y - 1) || OpenSea(x, y + 1));
            bool LandEdge(int x, int y) => In(x, y) && landEdge[x, y];
            Grid2D<bool> bandGrid = new Grid2D<bool>(W, H);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    bandGrid[x, y] = landMass[x, y]
                        && (LandEdge(x, y) || LandEdge(x - 1, y) || LandEdge(x + 1, y) || LandEdge(x, y - 1) || LandEdge(x, y + 1));

            SerializableColor dodger = new SerializableColor(255, 30, 144, 255);    // shore edge
            SerializableColor lightBlue = new SerializableColor(255, 200, 228, 250); // buildable water
            SerializableColor olive = new SerializableColor(255, 205, 205, 140);     // marsh
            SerializableColor black = new SerializableColor(255, 0, 0, 0);           // silhouette
            SerializableColor rock = new SerializableColor(255, 170, 162, 152);      // non-buildable terrain

            // colour each island tile. order matters, the silhouette band wins over the fills under it
            Grid2D<SerializableColor> colorGrid = new Grid2D<SerializableColor>(W, H);
            Grid2D<bool> paintGrid = new Grid2D<bool>(W, H);

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (harbourGrid[x, y])
                    {
                        // water touching the island (incl. diagonally) gets the brighter shore colour
                        bool shore = LandMass(x - 1, y) || LandMass(x + 1, y) || LandMass(x, y - 1) || LandMass(x, y + 1)
                                  || LandMass(x - 1, y - 1) || LandMass(x + 1, y - 1) || LandMass(x - 1, y + 1) || LandMass(x + 1, y + 1);
                        colorGrid[x, y] = shore ? dodger : lightBlue;
                        paintGrid[x, y] = true;
                    }
                    else if (bandGrid[x, y])
                    {
                        colorGrid[x, y] = black;
                        paintGrid[x, y] = true;
                    }
                    else if (mountainGrid[x, y])
                    {
                        colorGrid[x, y] = rock;
                        paintGrid[x, y] = true;
                    }
                    else if (marshGrid[x, y])
                    {
                        colorGrid[x, y] = olive;
                        paintGrid[x, y] = true;
                    }
                }
            }

            LayoutFile layout = new LayoutFile
            {
                FileVersion = 4,
                LayoutVersion = new Version("1.0.0.0"),
                Objects = new List<AnnoObject>(),
                Modified = DateTime.Now,
            };

            // only bevel against empty space (unpainted land / sea), the cut corner is left blank.
            // boundaries between two painted regions stay square on purpose: beveling them and filling
            // the cut with the neighbour colour scattered mismatched triangles (cliff shards in the marsh)
            bool Empty(int ex, int ey) => !In(ex, ey) || !paintGrid[ex, ey];

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (!paintGrid[x, y]) continue;

                    bool n = Empty(x, y - 1);
                    bool s = Empty(x, y + 1);
                    bool e = Empty(x + 1, y);
                    bool w = Empty(x - 1, y);

                    if ((n ^ s) && (e ^ w))
                    {
                        // bits are 1=W 2=S 4=E 8=N. keep the inner triangle, leave the cut corner empty
                        byte inner;
                        if (n && e) inner = 0b0011;      // cut NE
                        else if (n && w) inner = 0b0110; // cut NW
                        else if (s && e) inner = 0b1001; // cut SE
                        else inner = 0b1100;             // cut SW
                        layout.Objects.Add(CreateBlocker(x, y, colorGrid[x, y], inner));
                    }
                    else
                    {
                        layout.Objects.Add(CreateBlocker(x, y, colorGrid[x, y]));
                    }
                }
            }

            ZipArchiveEntry output = archive.CreateEntry(name + ".ad", CompressionLevel.SmallestSize);
            using (Stream outputStream = output.Open())
            {
                SerializationHelper.SaveToStream(layout, outputStream);
            }
        }

        private static Grid2D<bool> ParseBits(Tag grid, bool invert = false)
        {
            int width = grid.Attribute("x").ToNumber<int>();
            int height = grid.Attribute("y").ToNumber<int>();
            bool[] values = new bool[width * height];

            BitArray bits = new BitArray(grid.Attribute("bits").Content);
            if (invert) bits = bits.Not();
            bits.CopyTo(values, 0);

            return new Grid2D<bool>(values, width, height);
        }

        private static Grid2D<T> ParseBlocks<T>(Tag grid, int? fallbackWidth = null, int? fallbackHeight = null) where T : struct, INumber<T>
        {
            int gridWidth = grid.Attribute("x")?.ToNumber<int>() ?? fallbackWidth ?? throw new NullReferenceException();
            int gridHeight = grid.Attribute("y")?.ToNumber<int>() ?? fallbackHeight ?? throw new NullReferenceException();
            bool isSparseEnabled = grid.Attribute("SparseEnabled").ToBoolean();
            Grid2D<T> result = new Grid2D<T>(gridWidth, gridHeight);

            if (isSparseEnabled)
            {
                UInt16? blockWidth = null;
                UInt16? blockHeight = null;

                foreach (Tag block in grid.Tags("block"))
                {
                    byte? mode = block.Attribute("mode")?.ToNumber<byte>();

                    if (mode == 1) // start
                    {
                        blockWidth = block.Attribute("x").ToNumber<UInt16>();
                        blockHeight = block.Attribute("y").ToNumber<UInt16>();
                    }
                    else if (mode == 0) // end
                    {
                        blockWidth = null;
                        blockHeight = null;
                    }
                    else if (mode == 2) // default
                    {
                        Debug.Assert(blockWidth.HasValue && blockHeight.HasValue, "Block not initialized!");
                        Grid2D<T> section = new Grid2D<T>(blockWidth.Value, blockHeight.Value);
                        T value = block.Attribute("default").ToNumber<T>();
                        section.Fill(value);

                        UInt16 destinationX = block.Attribute("x")?.ToNumber<UInt16>() ?? 0;
                        UInt16 destinationY = block.Attribute("y")?.ToNumber<UInt16>() ?? 0;
                        result.Copy(section, destinationX, destinationY);
                    }
                    else if (mode == null) // values
                    {
                        Debug.Assert(blockWidth.HasValue && blockHeight.HasValue, "Block not initialized!");
                        T[] values = block.Attribute("values").ToNumbers<T>().ToArray();
                        Grid2D<T> section = new Grid2D<T>(values, blockWidth.Value, blockHeight.Value);

                        UInt16 destinationX = block.Attribute("x")?.ToNumber<UInt16>() ?? 0;
                        UInt16 destinationY = block.Attribute("y")?.ToNumber<UInt16>() ?? 0;
                        result.Copy(section, destinationX, destinationY);
                    }
                    else
                    {
                        throw new NotImplementedException($"Mode {mode} not implemented!");
                    }
                }
            }
            else
            {
                throw new NotImplementedException("Non-sparse mode not implemented!");
            }

            return result;
        }

        #region Private Helper Methods

        private static AnnoObject CreateBlocker(int x, int y, SerializableColor color, byte? quadrants = null)
        {
            return new AnnoObject
            {
                Template = "Blocker",
                Identifier = "BlockTile_1x1",
                Position = new System.Windows.Point(x, y),
                Size = new System.Windows.Size(1, 1),
                Borderless = true,
                Color = color,
                TileQuadrants = quadrants,
            };
        }

        #endregion
    }
}
