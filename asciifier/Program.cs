using System.Collections.Immutable;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing.Processors;

namespace Asciifier;

internal static class Program
{
    private static readonly char[] Characters =
        (@"0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!""#$%&'()*+,-./:;<=>?@[\]^_`{|}~ "
         + @"▀▁▂▃▄▅▆▇█▉▊▋▌▍▎▏▐░▒▓▔▕▖▗▘▙▚▛▜▝▞▟ ")
            .ToCharArray();

    private static void Main(string[] argv)
    {
        if (argv.Length != 2)
        {
            Console.WriteLine("Usage: asciifier <font> <image>");
            throw new Exception("Invalid arguments");
        }

        string fontPath = argv[0];
        FontCollection fonts = new();
        FontFamily family = fonts.Add(fontPath);
        Font font = family.CreateFont(24, FontStyle.Regular);

        // Draw each character
        var characters = Characters.Select(c => CreateGlyphImage(font, c)).ToImmutableArray();
        int width = characters.Select(i => i.Width).Min();
        int height = characters.Select(i => i.Height).Min();
        string imagePath = argv[1];

        using var rawSource = Image.Load(imagePath).CloneAs<Rgba32>();
        using var rawSourceGreyscale = rawSource.Clone();
        rawSourceGreyscale.Mutate(x => x.Grayscale());
        rawSourceGreyscale.Mutate(x => x.BinaryThreshold(0.1f));
        using var source = rawSourceGreyscale.CloneAs<L16>();
        using var target = new Image<Rgba32>(source.Width, source.Height);
        var point = new Point(0, 0);
        target.Mutate(x => x.Clear(Color.Black));
        // Get the cells of the image
        for (var i = 0; i < source.Width; i += width)
        for (var j = 0; j < source.Height; j += height)
        {
            // Get the character that best matches the cell
            using var characterImage = characters.MinBy(c => GetMeanDifference(c, source, i, j)).CloneAs<Rgba32>();
            // Get the mean colour
            Rgba32 meanColour = GetMeanColour(rawSource, i, j, characterImage.Width, characterImage.Height);
            var options = new DrawingOptions
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
            
            // Draw the character
            point.X = i;
            point.Y = j;
            characterImage.Mutate(x => x.Fill(options, meanColour));
            target.Mutate(x => x.DrawImage(characterImage, point, 1.0f));

        }

        target.SaveAsPng("out.png");
    }

    private static Rgba32 GetMeanColour(Image<Rgba32> image, int xOffset, int yOffset, int width, int height)
    {
        double totalR = 0.0, totalG = 0.0, totalB = 0.0;
        var count = 0;
        for (var i = 0; i < width; i++)
        {
            if (i + xOffset >= image.Width) { break; }
            for (var j = 0; j < height; j++)
            {
                if (j + yOffset >= image.Height) { break; }
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
}
