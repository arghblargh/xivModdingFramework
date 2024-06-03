﻿using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using xivModdingFramework.Materials;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.FileTypes;

namespace xivModdingFramework.Textures
{
    public class TextureHelpers
    {


        /// <summary>
        /// Paralellizes a pixel a modify action into a series of smaller task chunks,
        /// calling the given Action with the byte offset for the given pixel.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        internal static async Task ModifyPixels(Action<int> action, int width, int height)
        {
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < height; i++)
            {
                var h = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int x = 0; x < width; x++)
                    {
                        var offset = ((width * h) + x) * 4;
                        action(offset);
                    }
                }));
            }
            await Task.WhenAll(tasks);
        }



        internal static async Task FillChannel(byte[] data, int width, int height, int channel, byte value)
        {
            Action<int> act = (i) =>
            {
                data[i + channel] = value;
            };
            await ModifyPixels(act, width, height);
        }

        internal static async Task CreateIndexTexture(byte[] normalPixelData, byte[] indexPixelData, int width, int height)
        {
            await ModifyPixels((int offset) =>
            {
                var originalCset = normalPixelData[offset + 3];

                if (originalCset > 15 && originalCset < 25)
                {
                    var a = "A";
                }

                // We could try to run a blend on this to add more degrees of gradient potentially?
                var blendRem = originalCset % 34;
                var originalRow = originalCset / 17;

                if (blendRem > 17)
                {
                    if (blendRem < 26)
                    {
                        // Stays in this row, clamped to the closer row.
                        blendRem = 17;
                    }
                    else
                    {
                        // Goes to next row, clamped to the closer row.
                        blendRem = 0;
                        originalRow++;
                    }
                }

                var newBlend = (byte)(255 - Math.Round((blendRem / 17.0f) * 255.0f));
                var newRow = (byte) (((originalRow / 2) * 17) + 8);


                // BRGRA format output.
                indexPixelData[offset + 0] = 0;
                indexPixelData[offset + 1] = newBlend;
                indexPixelData[offset + 2] = newRow;
                indexPixelData[offset + 3] = 255;
            }, width, height);
        }
        internal static async Task CreateHairMaps(byte[] normalPixelData, byte[] maskPixelData, int width, int height)
        {
            await ModifyPixels((int offset) =>
            {
                var newGreen = (byte)(255 - maskPixelData[offset]);

                // Output is BGRA

                // Normal Red (Swizzle)
                normalPixelData[offset + 2] = maskPixelData[offset + 0];
                // Normal Blue - Highlight Color
                normalPixelData[offset + 0] = maskPixelData[offset + 3];


                // Mask Red - Specular Power
                maskPixelData[offset + 2] = maskPixelData[offset + 1];
                // Mask Green - Roughness
                maskPixelData[offset + 1] = newGreen;
                // Mask Alpha - Albedo
                maskPixelData[offset + 3] = maskPixelData[offset];
                // Mask Blue - SSS Thickness Map
                maskPixelData[offset + 0] = 49;

            }, width, height);
        }

        /// <summary>
        /// Resizes two textures to the sum largest size between them, using ImageSharp to do the processing.
        /// </summary>
        /// <param name="texA"></param>
        /// <param name="texB"></param>
        /// <returns></returns>
        internal static async Task<(byte[] TexA, byte[] TexB, int Width, int Height)> ResizeImages(XivTex texA, XivTex texB)
        {
            var maxW = Math.Max(texA.Width, texB.Width);
            var maxH = Math.Max(texA.Height, texB.Height);

            var timgA = ResizeImage(texA, maxW, maxH);
            var timgB = ResizeImage(texB, maxW, maxH);

            await Task.WhenAll(timgA, timgB);

            return (timgA.Result, timgB.Result, maxW, maxH);
        }

        /// <summary>
        /// Resize a texture to the given size, returning the raw pixel data.
        /// </summary>
        /// <param name="tex"></param>
        /// <param name="newWidth"></param>
        /// <param name="newHeight"></param>
        /// <returns></returns>
        internal static async Task<byte[]> ResizeImage(XivTex tex, int newWidth, int newHeight)
        {
            var pixels = await tex.GetRawPixels();
            if (newWidth == tex.Width && newHeight == tex.Height)
            {
                return pixels;
            }
            return await ResizeImage(pixels, tex.Width, tex.Height, newWidth, newHeight);
        }


        /// <summary>
        /// Resize an image to the given size, returning the raw pixel data.
        /// Assumes RGBA 8.8.8.8 data as the incoming byte array.
        /// </summary>
        /// <param name="tex"></param>
        /// <param name="newWidth"></param>
        /// <param name="newHeight"></param>
        /// <returns></returns>
        internal static async Task<byte[]> ResizeImage(byte[] pixelData, int width, int height, int newWidth, int newHeight)
        {
            return await Task.Run(() =>
            {
                using var img = Image.LoadPixelData<Rgba32>(pixelData, width, height);
                img.Mutate(x => x.Resize(
                    new ResizeOptions
                    {
                        Size = new Size(newWidth, newHeight),
                        PremultiplyAlpha = false,
                    })
                );
                var data = new byte[newWidth * newHeight * 4];
                img.CopyPixelDataTo(data.AsSpan());
                return data;
            });
        }
    }
}
