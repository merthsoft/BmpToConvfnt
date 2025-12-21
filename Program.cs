using System.Drawing;
using System.Text;

if (args.Length < 4 || args.Length > 5)
{
    Console.WriteLine("Usage: bmptoconvfnt imagefile width height startcodepoint [numglyphs]");
    Console.WriteLine("  imagefile       - Path to the BMP image file");
    Console.WriteLine("  width          - Width of each glyph in pixels");
    Console.WriteLine("  height         - Height of each glyph in pixels");
    Console.WriteLine("  startcodepoint - Starting code point (e.g., 32 for space, 65 for 'A')");
    Console.WriteLine("  numglyphs      - (Optional) Number of glyphs to read from the image");
    return 1;
}

var imageFile = args[0];
if (!File.Exists(imageFile))
{
    Console.WriteLine($"Error: Image file '{imageFile}' not found.");
    return 1;
}

if (!int.TryParse(args[1], out var glyphWidth) || glyphWidth <= 0)
{
    Console.WriteLine("Error: Width must be a positive integer.");
    return 1;
}

if (!int.TryParse(args[2], out var glyphHeight) || glyphHeight <= 0)
{
    Console.WriteLine("Error: Height must be a positive integer.");
    return 1;
}

if (!int.TryParse(args[3], out var startCodePoint) || startCodePoint < 0)
{
    Console.WriteLine("Error: Start code point must be a non-negative integer.");
    return 1;
}

int? maxGlyphs = null;
if (args.Length == 5)
{
    if (!int.TryParse(args[4], out var numGlyphs) || numGlyphs <= 0)
    {
        Console.WriteLine("Error: Number of glyphs must be a positive integer.");
        return 1;
    }
    maxGlyphs = numGlyphs;
}

try
{
    using var bitmap = new Bitmap(imageFile);
    
    var columns = bitmap.Width / glyphWidth;
    var rows = bitmap.Height / glyphHeight;
    var totalGlyphs = columns * rows;

    if (columns == 0 || rows == 0)
    {
        Console.WriteLine($"Error: Image dimensions ({bitmap.Width}x{bitmap.Height}) are smaller than glyph dimensions ({glyphWidth}x{glyphHeight}).");
        return 1;
    }

    var glyphsToProcess = maxGlyphs.HasValue ? Math.Min(maxGlyphs.Value, totalGlyphs) : totalGlyphs;

    Console.WriteLine($"Processing {bitmap.Width}x{bitmap.Height} image into {glyphsToProcess} glyphs ({columns} columns x {rows} rows)");
    if (maxGlyphs.HasValue && maxGlyphs.Value < totalGlyphs)
    {
        Console.WriteLine($"Note: Limiting to {glyphsToProcess} glyphs (image contains {totalGlyphs} total)");
    }

    var outputFile = Path.ChangeExtension(imageFile, ".txt");
    
    using var writer = new StreamWriter(outputFile, false, Encoding.UTF8);
    
    writer.WriteLine("convfont");
    writer.WriteLine($"Height: {glyphHeight}");
    writer.WriteLine($"Fixed width: {glyphWidth}");
    writer.WriteLine("Font data:");
    writer.WriteLine();

    var codePoint = startCodePoint;
    var firstGlyph = true;
    var glyphsProcessed = 0;

    for (var row = 0; row < rows && glyphsProcessed < glyphsToProcess; row++)
    {
        for (var col = 0; col < columns && glyphsProcessed < glyphsToProcess; col++)
        {
            if (!firstGlyph)
                writer.WriteLine();

            firstGlyph = false;

            if (codePoint == startCodePoint)
            {
                if (codePoint >= 32 && codePoint <= 126)
                    writer.WriteLine($"Code point: '{(char)codePoint}'");
                else
                    writer.WriteLine($"Code point: {codePoint}");
            }
            
            writer.WriteLine("Data:");

            var startX = col * glyphWidth;
            var startY = row * glyphHeight;

            for (var y = 0; y < glyphHeight; y++)
            {
                var line = new StringBuilder();
                
                for (var x = 0; x < glyphWidth; x++)
                {
                    var pixelX = startX + x;
                    var pixelY = startY + y;

                    if (pixelX < bitmap.Width && pixelY < bitmap.Height)
                    {
                        var pixel = bitmap.GetPixel(pixelX, pixelY);
                        var set = pixel.A > 128 && (pixel.R + pixel.G + pixel.B) / 3 < 128;
                        line.Append(set ? '#' : ' ');
                    }
                    else
                    {
                        line.Append(' ');
                    }
                }
                
                writer.WriteLine(line.ToString());
            }

            codePoint++;
            glyphsProcessed++;
        }
    }

    Console.WriteLine($"Successfully created '{outputFile}' with {glyphsProcessed} glyphs (code points {startCodePoint}-{codePoint - 1})");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"Error processing image: {ex.Message}");
    return 1;
}
