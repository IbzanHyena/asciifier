// Asciifier, for converting images to ASCII art.
// Copyright (C) 2023 Ibzan
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Immutable;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace Asciifier;

internal static class Program
{
    private static readonly ImmutableDictionary<CharacterSet, char[]> Characters =
        ImmutableDictionary<CharacterSet, char[]>.Empty
            .Add(CharacterSet.Ascii,
                @"0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!""#$%&'()*+,-./:;<=>?@[\]^_`{|}~ "
                    .ToCharArray())
            .Add(CharacterSet.Blocks, @"▀▁▂▃▄▅▆▇█▉▊▋▌▍▎▏▐░▒▓▔▕▖▗▘▙▚▛▜▝▞▟ ".ToCharArray())
            .Add(CharacterSet.AsciiAndBlocks,
                @"0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!""#$%&'()*+,-./:;<=>?@[\]^_`{|}~ ▀▁▂▃▄▅▆▇█▉▊▋▌▍▎▏▐░▒▓▔▕▖▗▘▙▚▛▜▝▞▟"
                    .ToCharArray())
            .Add(CharacterSet.Symbols,
                @"!@#$%^&*()-=_+[]{}\|;:'"",<.>/? ".ToCharArray());

    /// <summary>
    ///     Converts an image from a file to ASCII art, using the specified font.
    /// </summary>
    /// <param name="input">The input filepath for the image.</param>
    /// <param name="font">The input filepath for the font.</param>
    /// <param name="output">The output filepath for the image.</param>
    /// <param name="characterSet">Which set of characters to use.</param>
    /// <param name="fontSize">The font size to draw at.</param>
    /// <param name="colour">Whether to use color.</param>
    // ReSharper disable once UnusedMember.Local
    private static void Main(FileInfo input, FileInfo font, FileInfo output,
        CharacterSet characterSet = CharacterSet.AsciiAndBlocks, int fontSize = 12, bool colour = false)
    {
        FontCollection fonts = new();
        FontFamily family = fonts.Add(font.FullName);
        // ReSharper disable once InconsistentNaming
        Font font_ = family.CreateFont(fontSize, FontStyle.Regular);

        IEnumerable<Image<L16>> InvertGlyph(Image<L16> image)
        {
            var invertedImage = image.Clone();
            invertedImage.Mutate(x => x.Invert());
            return new[] { image, invertedImage };
        }

        var characters = Characters[characterSet].Select(c => CreateGlyphImage(font_, c)).SelectMany(InvertGlyph).ToImmutableArray();
        using var source = Image.Load(input.FullName).CloneAs<Rgba32>();
        using var result = AsciifyImage(source, colour, characters);

        result.SaveAsPng(output.FullName);
    }

    private static Image<Rgba32> AsciifyImage(Image<Rgba32> rawSource, bool colour,
        ImmutableArray<Image<L16>> characters)
    {
        // Get the smallest character size
        int width = characters.Select(i => i.Width).Min();
        int height = characters.Select(i => i.Height).Min();
        // Convert the image to greyscale
        using var rawSourceGreyscale = rawSource.Clone();
        rawSourceGreyscale.Mutate(x => x.Grayscale());
        using var source = rawSourceGreyscale.CloneAs<L16>();
        // Create the target image, and fill it with black
        var target = new Image<Rgba32>(source.Width, source.Height);
        target.Mutate(x => x.Clear(Color.Black));

        var drawingOptions = new DrawingOptions
        {
            ShapeOptions = new ShapeOptions
            {
                IntersectionRule = IntersectionRule.Nonzero
            },
            GraphicsOptions = new GraphicsOptions
            {
                Antialias = false,
                AlphaCompositionMode = PixelAlphaCompositionMode.SrcOver,
                ColorBlendingMode = PixelColorBlendingMode.Multiply
            }
        };

        // Get the cells of the image

        Parallel.For(0, source.Width / width, xIndex =>
        {
            // ReSharper disable once AccessToDisposedClosure
            Parallel.For(0, source.Height / height, yIndex =>
            {
                int i = xIndex * width;
                int j = yIndex * height;
                // Get the character that best matches the cell
                // ReSharper disable once AccessToDisposedClosure
                using var characterImage = characters.AsParallel().MinBy(c => GetMeanDifference(c, source, i, j))!.CloneAs<Rgba32>();

                if (colour)
                {
                    // Get the mean colour, and fill the character with it
                    Rgba32 meanColour = GetMeanColour(rawSource, i, j, characterImage.Width, characterImage.Height);
                    characterImage.Mutate(x => x.Fill(drawingOptions, meanColour));
                }

                // Draw the character
                var point = new Point(i, j);
                // ReSharper disable once AccessToDisposedClosure
                target.Mutate(x => x.DrawImage(characterImage, point, 1.0f));
            });
        });

        return target;
    }

    private static Rgba32 GetMeanColour(Image<Rgba32> image, int xOffset, int yOffset, int width, int height)
    {
        double totalR = 0.0, totalG = 0.0, totalB = 0.0;
        var count = 0;
        for (var i = 0; i < width; i++)
        {
            if (i + xOffset >= image.Width) break;
            for (var j = 0; j < height; j++)
            {
                if (j + yOffset >= image.Height) break;
                Rgba32 pixel = image[i + xOffset, j + yOffset];
                totalR += pixel.R;
                totalG += pixel.G;
                totalB += pixel.B;
                count++;
            }
        }

        return new Rgba32((byte)(totalR / count), (byte)(totalG / count), (byte)(totalB / count));
    }

    private static double GetMeanDifference(Image<L16> character, Image<L16> source, int xOffset, int yOffset)
    {
        // Create a 2d array of differences
        double total = 0;
        var count = 0;
        for (var i = 0; i < character.Width; i++)
        {
            if (i + xOffset >= source.Width) break;
            for (var j = 0; j < character.Height; j++)
            {
                if (j + yOffset >= source.Height) break;
                total += Math.Abs(character[i, j].PackedValue - source[i + xOffset, j + yOffset].PackedValue);
                count++;
            }
        }

        return total / count;
    }

    private static Image<L16> CreateGlyphImage(Font font, char c)
    {
        var s = c.ToString();
        var options = new TextOptions(font)
        {
            TextAlignment = TextAlignment.Start,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            KerningMode = KerningMode.Standard
        };
        FontRectangle rect = TextMeasurer.Measure(s, options);
        var image = new Image<L16>((int)Math.Ceiling(rect.Width), (int)Math.Ceiling(rect.Height));
        image.Mutate(x => x.DrawText(s, font, Color.White, new PointF(0, 0)));
        return image;
    }

    private enum CharacterSet
    {
        Ascii,
        Blocks,
        AsciiAndBlocks,
        Symbols
    }
}