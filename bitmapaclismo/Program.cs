﻿using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace bitmapaclismo
{
    static class bitmapaclismo
    {
        static int readInt(byte[] bytes, int address) {
            return bytes[address] + (bytes[address + 1] << 8) + (bytes[address + 2] << 16) + (bytes[address + 3] << 24);
        }
        static void writeInt(byte[] bytes, int address, int value) {
            bytes[address] = (byte)value;
            bytes[address+1] = (byte)(value>>8);
            bytes[address+2] = (byte)(value>>16);
            bytes[address+3] = (byte)(value>>24);
        }
        [STAThread]
        static void Main() {
            
            try {
                FolderBrowserDialog fbd = new FolderBrowserDialog();
                fbd.Description = "Open level folder";
                fbd.UseDescriptionForTitle = true;
                fbd.ShowDialog();

                if (!Directory.Exists(fbd.SelectedPath)) {
                    MessageBox.Show("Could not find folder specified");
                    return;
                }
                if (!Path.Exists($@"{fbd.SelectedPath}\terrain")) {
                    MessageBox.Show("Could not find terrain file in folder specified");
                    return;
                }

                string fileName = Path.GetFileName(fbd.SelectedPath);
                byte[] terrainBytes = File.ReadAllBytes($@"{fbd.SelectedPath}\terrain");

                int sizex = readInt(terrainBytes, 0x1E -5 + fileName.Length);
                int sizey = readInt(terrainBytes, 0x22 -5 + fileName.Length);
                int sizez = readInt(terrainBytes, 0x26 -5 + fileName.Length);
                int sizeL = readInt(terrainBytes, 0x2A -5 + fileName.Length);
                int totalSize = readInt(terrainBytes, 0x2E -5 + fileName.Length);
                int heightwidth = (int)Math.Sqrt(totalSize); //use sqrt for only square map because idk braindamage

                int zRange = 100; //Fixed
                if (heightwidth != sizex || heightwidth != sizey || zRange != sizez) {
                    MessageBox.Show("Unexpected size parameters in terrain file - was this already modified?");
                }

                if (MessageBox.Show("Choose your action:\n\nYes - Convert terrain file to a bitmap image.\nNo - Convert a bitmap image to the terrain file.", "Select Action", MessageBoxButtons.YesNo) == DialogResult.Yes) {
                    ConvertTerrainToBitmap(terrainBytes, fbd.SelectedPath, heightwidth);
                } else {
                    ConvertBitmapToTerrain(terrainBytes, fileName, fbd.SelectedPath, heightwidth, zRange, fbd);
                }
            } catch (Exception e)
            {
                MessageBox.Show("Unknown error: " + e.Message + "\n\n" + e.StackTrace);
            }
        }

        static void ConvertBitmapToTerrain(byte[] terrainBytes, string fileName, string folderPath, int heightwidth, int zRange, FolderBrowserDialog fbd) {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = $@"Open {heightwidth}x{heightwidth}px 24bit bitmap";
            ofd.ShowDialog();
            if (!Path.Exists(ofd.FileName)) {
                MessageBox.Show("Could not find bmp file specified");
                return;
            }
            byte[] heightMapBytes = File.ReadAllBytes(ofd.FileName);

            if (MessageBox.Show("Extend height to 255 because you want to break the game?", "", MessageBoxButtons.YesNo) == DialogResult.Yes) {
                zRange = 255;
            }

            int hMapStart = heightMapBytes.Length -1 - heightwidth*heightwidth*3;
            int terrainStart = 0x2d + fileName.Length;

            int hMapCurrent = hMapStart;
            int terrainCurrent = terrainStart;

            for (int x=0; x<heightwidth; x++) {
                for (int y=0; y<heightwidth; y++) {
                    int pixelIndex = x*heightwidth+y;
                    if (hMapCurrent != hMapStart + pixelIndex * 3) throw new Exception("the math didn't math");
                    if (terrainCurrent != terrainStart + pixelIndex * 4) throw new Exception("the math didn't math");

                    byte hMapValue = heightMapBytes[hMapCurrent];
                    float adjustedValue = hMapValue / 255f;

                    float filteredValue = adjustedValue;//(float)Math.Sqrt(adjustedValue) / 2; //I just wanted something nice when testing

                    int result = (int)(filteredValue * zRange);
                    writeInt(terrainBytes, terrainCurrent, result);

                    hMapCurrent += 3;
                    terrainCurrent += 4;
                }
            }
            File.WriteAllBytes($@"{fbd.SelectedPath}\terrain", terrainBytes);
            MessageBox.Show("Finished converting bitmap to terrain.");
        }
        
        static void ConvertTerrainToBitmap(byte[] terrainBytes, string folderPath, int heightwidth) {
            Bitmap bitmap = new Bitmap(heightwidth, heightwidth, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            int terrainStart = 0x2d + Path.GetFileName(folderPath).Length;
            int terrainCurrent = terrainStart;

            int zRange = 100;

            for (int x = 0; x < heightwidth; x++) {
                for (int y = 0; y < heightwidth; y++) {
                    int heightValue = readInt(terrainBytes, terrainCurrent);
                    float normalizedValue = heightValue / (float)zRange;
                    byte pixelValue = (byte)(normalizedValue * 255);

                    Color color = Color.FromArgb(pixelValue, pixelValue, pixelValue);
                    bitmap.SetPixel(x, y, color);

                    terrainCurrent += 4;
                }
            }

            string outputPath = Path.Combine(folderPath, "terrain.bmp");
            bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Bmp);
            MessageBox.Show($"Terrain converted to bitmap and saved as {outputPath}");
        }
    }
}