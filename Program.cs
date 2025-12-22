using System.CommandLine;
using System.CommandLine.Help;
using System.Drawing;
using System.Text;

var imageFileArgument = new Argument<FileInfo>("imagefile")
{
    Description = "Path to the BMP image file"
};

var widthOption = new Option<int>("-w", "--width")
{
    Description = "Width of each glyph in pixels",
    Required = true
};

var heightOption = new Option<int>("-h", "--height")
{
    Description = "Height of each glyph in pixels",
    Required = true
};

var startCodePointOption = new Option<int?>("-s", "--startcodepoint")
{
    Description = "Starting code point (e.g., 32 for space, 65 for 'A'; defaults to 0)",
    DefaultValueFactory = _ => null
};

var numGlyphsOption = new Option<int?>("-n", "--numglyphs")
{
    Description = "Number of glyphs to read from the image (optional, defaults to all)"
};

var xPadOption = new Option<int>("--xpad")
{
    Description = "Left padding, in pixels, before the glyph grid starts",
    DefaultValueFactory = _ => 0
};

var yPadOption = new Option<int>("--ypad")
{
    Description = "Top padding, in pixels, before the glyph grid starts",
    DefaultValueFactory = _ => 0
};

var xCellPadOption = new Option<int>("--xcellpad")
{
    Description = "Horizontal spacing, in pixels, between glyph cells",
    DefaultValueFactory = _ => 0
};

var yCellPadOption = new Option<int>("--ycellpad")
{
    Description = "Vertical spacing, in pixels, between glyph cells",
    DefaultValueFactory = _ => 0
};

var outputFileOption = new Option<string>("-o", "--output")
{
    Description = "Output file path (optional, defaults to input filename with .txt extension)"
};

var charsFileOption = new Option<string>("-c", "--charsfile")
{
    Description = "Path to a text file containing characters to map to glyphs (optional)"
};

var rootCommand = new RootCommand("Converts a BMP image to convfont format")
{
    imageFileArgument,
    widthOption,
    heightOption,
    startCodePointOption,
    numGlyphsOption,
    xPadOption,
    yPadOption,
    xCellPadOption,
    yCellPadOption,
    outputFileOption,
    charsFileOption
};

var helpOption = rootCommand.Options
    .OfType<HelpOption>()
    .Single();

helpOption.Aliases.Clear();
helpOption.Aliases.Add("--help");
helpOption.Aliases.Add("-help");
helpOption.Aliases.Add("/?");

rootCommand.SetAction((parseResult) =>
{
    var imageFile = parseResult.GetValue(imageFileArgument)!;
    var glyphWidth = parseResult.GetValue(widthOption);
    var glyphHeight = parseResult.GetValue(heightOption);
    var startCodePoint = parseResult.GetValue(startCodePointOption);
    var maxGlyphs = parseResult.GetValue(numGlyphsOption);
    var xPad = parseResult.GetValue(xPadOption);
    var yPad = parseResult.GetValue(yPadOption);
    var xCellPad = parseResult.GetValue(xCellPadOption);
    var yCellPad = parseResult.GetValue(yCellPadOption);
    var outputFile = parseResult.GetValue(outputFileOption);
    var charsFile = parseResult.GetValue(charsFileOption);

    return ProcessImage(
        imageFile,
        glyphWidth,
        glyphHeight,
        startCodePoint,
        maxGlyphs,
        xPad,
        yPad,
        xCellPad,
        yCellPad,
        outputFile,
        charsFile);
});

return await rootCommand.Parse(args).InvokeAsync();

static int ProcessImage(
    FileInfo imageFile,
    int glyphWidth,
    int glyphHeight,
    int? startCodePoint,
    int? maxGlyphs,
    int xPad,
    int yPad,
    int xCellPad,
    int yCellPad,
    string? outputFile,
    string? charsFile)
{
    if (!imageFile.Exists)
    {
        Console.WriteLine($"Error: Image file '{imageFile.FullName}' not found.");
        return 1;
    }

    if (glyphWidth <= 0)
    {
        Console.WriteLine("Error: Width must be a positive integer.");
        return 1;
    }

    if (glyphHeight <= 0)
    {
        Console.WriteLine("Error: Height must be a positive integer.");
        return 1;
    }

    if (xPad < 0 || yPad < 0)
    {
        Console.WriteLine("Error: Padding values must be non-negative integers.");
        return 1;
    }

    if (xCellPad < 0 || yCellPad < 0)
    {
        Console.WriteLine("Error: Cell padding values must be non-negative integers.");
        return 1;
    }

    var usingCharsFile = !string.IsNullOrWhiteSpace(charsFile);

    if (usingCharsFile && startCodePoint.HasValue)
    {
        Console.WriteLine("Error: --startcodepoint cannot be used with --charsfile.");
        return 1;
    }

    if (usingCharsFile && maxGlyphs.HasValue)
    {
        Console.WriteLine("Error: --numglyphs cannot be used with --charsfile.");
        return 1;
    }

    var resolvedStartCodePoint = startCodePoint ?? 0;

    if (!usingCharsFile && resolvedStartCodePoint < 0)
    {
        Console.WriteLine("Error: Start code point must be a non-negative integer.");
        return 1;
    }

    if (!usingCharsFile && maxGlyphs.HasValue && maxGlyphs.Value <= 0)
    {
        Console.WriteLine("Error: Number of glyphs must be a positive integer.");
        return 1;
    }

    List<string>? charRows = null;
    var definedGlyphs = 0;

    if (usingCharsFile)
    {
        if (!File.Exists(charsFile))
        {
            Console.WriteLine($"Error: Chars file '{charsFile}' not found.");
            return 1;
        }

        charRows = [.. File.ReadLines(charsFile)];
        definedGlyphs = charRows.Sum(line => line.Length);

        if (definedGlyphs == 0)
        {
            Console.WriteLine("Error: Chars file does not define any glyphs.");
            return 1;
        }
    }

    try
    {
        using var bitmap = new Bitmap(imageFile.FullName);

        var usableWidth = bitmap.Width - xPad;
        var usableHeight = bitmap.Height - yPad;

        if (usableWidth < glyphWidth || usableHeight < glyphHeight)
        {
            Console.WriteLine($"Error: Image dimensions ({bitmap.Width}x{bitmap.Height}) minus padding ({xPad}x{yPad}) are smaller than glyph dimensions ({glyphWidth}x{glyphHeight}).");
            return 1;
        }

        var columns = 1 + (usableWidth - glyphWidth) / (glyphWidth + xCellPad);
        var rows = 1 + (usableHeight - glyphHeight) / (glyphHeight + yCellPad);
        var totalGlyphs = columns * rows;

        if (columns == 0 || rows == 0)
        {
            Console.WriteLine($"Error: Image dimensions ({bitmap.Width}x{bitmap.Height}) are smaller than glyph dimensions ({glyphWidth}x{glyphHeight}) once padding is applied.");
            return 1;
        }

        var glyphLimit = totalGlyphs;

        if (usingCharsFile)
        {
            glyphLimit = Math.Min(totalGlyphs, definedGlyphs);
            Console.WriteLine($"Processing {bitmap.Width}x{bitmap.Height} image using up to {glyphLimit} glyphs defined in '{charsFile}'.");
            if (definedGlyphs > glyphLimit)
            {
                Console.WriteLine($"Note: Character file defines {definedGlyphs} glyphs but only {glyphLimit} fit within the image grid.");
            }
        }
        else
        {
            var glyphsToProcess = maxGlyphs.HasValue ? Math.Min(maxGlyphs.Value, totalGlyphs) : totalGlyphs;
            glyphLimit = glyphsToProcess;

            Console.WriteLine($"Processing {bitmap.Width}x{bitmap.Height} image into {glyphsToProcess} glyphs ({columns} columns x {rows} rows)");
            if (maxGlyphs.HasValue && maxGlyphs.Value < totalGlyphs)
            {
                Console.WriteLine($"Note: Limiting to {glyphsToProcess} glyphs (image contains {totalGlyphs} total)");
            }
        }

        outputFile = string.IsNullOrWhiteSpace(outputFile)
                        ? Path.ChangeExtension(imageFile.FullName, ".txt")
                        : outputFile;

        using var writer = new StreamWriter(outputFile, false, Encoding.UTF8);

        writer.WriteLine("convfont");
        writer.WriteLine($"Height: {glyphHeight}");
        writer.WriteLine($"Fixed width: {glyphWidth}");
        writer.WriteLine("Font data:");
        writer.WriteLine();

        var codePoint = resolvedStartCodePoint;
        var lastCodePointWritten = resolvedStartCodePoint - 1;
        var firstGlyph = true;
        var glyphsProcessed = 0;

        for (var row = 0; row < rows && glyphsProcessed < glyphLimit; row++)
        {
            if (usingCharsFile && (charRows == null || row >= charRows.Count))
                break;

            var columnsThisRow = usingCharsFile && charRows != null
                ? Math.Min(columns, charRows[row].Length)
                : columns;

            for (var col = 0; col < columnsThisRow && glyphsProcessed < glyphLimit; col++)
            {
                if (!firstGlyph)
                    writer.WriteLine();

                firstGlyph = false;

                if (usingCharsFile && charRows != null)
                {
                    var glyphChar = charRows[row][col];
                    if (glyphChar >= 32 && glyphChar <= 126)
                        writer.WriteLine($"Code point: '{glyphChar}'");
                    else
                        writer.WriteLine($"Code point: {(int)glyphChar}");
                }
                else
                {
                    if (codePoint >= 32 && codePoint <= 126)
                        writer.WriteLine($"Code point: '{(char)codePoint}'");
                    else
                        writer.WriteLine($"Code point: {codePoint}");

                    lastCodePointWritten = codePoint;
                    codePoint++;
                }

                writer.WriteLine("Data:");

                var startX = xPad + col * (glyphWidth + xCellPad);
                var startY = yPad + row * (glyphHeight + yCellPad);

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

                glyphsProcessed++;
            }
        }

        if (usingCharsFile)
        {
            Console.WriteLine($"Successfully created '{outputFile}' with {glyphsProcessed} glyphs defined by '{charsFile}'.");
        }
        else
        {
            var endCodePoint = glyphsProcessed > 0 ? lastCodePointWritten : resolvedStartCodePoint - 1;
            Console.WriteLine($"Successfully created '{outputFile}' with {glyphsProcessed} glyphs (code points {resolvedStartCodePoint}-{endCodePoint})");
        }

        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing image: {ex.Message}");
        return 1;
    }
}
