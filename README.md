# BmpToConvfnt

Creates a [Convfnt](https://github.com/drdnar/convfont/) ASCII file from an image file.

## Usage

```
bmptoconvfnt imagefile -w width -h height [options]
```

### Required arguments

- `imagefile`  
  Path to the BMP image file.

- `-w`, `--width`  
  Width of each glyph in pixels.

- `-h`, `--height`  
  Height of each glyph in pixels.

### Glyph selection

You can control which glyphs are generated in two ways:

1. By code point range (default mode)
2. By character file (explicit glyph mapping)

These modes are mutually exclusive.

#### By code point range (default)

```
bmptoconvfnt imagefile -w width -h height [--startcodepoint N] [--numglyphs M] [options]
```

- `-s N`, `--startcodepoint N`  
  Starting code point. Examples:  
  - `32` for space (`' '`)  
  - `65` for `'A'`  
  Defaults to `0` if omitted. Must be a non-negative integer.

- `-n M`, `--numglyphs M`  
  Maximum number of glyphs to read from the image.  
  If omitted, all glyph positions in the image grid are processed.  
  Must be a positive integer if specified.

The tool walks the glyph grid in row-major order (left to right, top to bottom), assigning consecutive code points starting from `startcodepoint`.

#### By character file (explicit mapping)

```
bmptoconvfnt imagefile -w width -h height --charsfile path/to/chars.txt [options]
```

- `-c PATH`, `--charsfile PATH`  
  Path to a UTF-8 text file whose characters define the glyphs and their order.

Each character in the file is mapped to a glyph cell in the image grid:

- Lines correspond to rows in the glyph grid.
- Characters in a line correspond to columns in that row.
- The first character of the first line maps to the first cell, the second character to the next cell, and so on.
- The tool stops once it either runs out of characters in the file or runs out of cells in the image grid.

The character file mode has these constraints:

- `--startcodepoint` cannot be used with `--charsfile`.
- `--numglyphs` cannot be used with `--charsfile`.

If the character file defines more glyphs than fit in the image grid, the extra characters are ignored.

### Layout options

These options define how glyphs are laid out in the source image:

- `--xpad N`
  Left padding (in pixels) before the glyph grid starts.  
  Default: `0`. Must be non-negative.

- `--ypad N`  
  Top padding (in pixels) before the glyph grid starts.  
  Default: `0`. Must be non-negative.

- `--xcellpad N`  
  Horizontal spacing (in pixels) between glyph cells.  
  Default: `0`. Must be non-negative.

- `--ycellpad N`  
  Vertical spacing (in pixels) between glyph cells.  
  Default: `0`. Must be non-negative.

The image is interpreted as a grid of fixed-size cells:

- Each cell is `width x height` pixels.
- The top-left cell starts at `(xpad, ypad)`.
- Each subsequent column is offset by `width + xcellpad` pixels horizontally.
- Each subsequent row is offset by `height + ycellpad` pixels vertically.

The tool validates that the padded image area is large enough to contain at least one full cell.

### Output options

- `-o`, `--output PATH`  
  Output file path for the generated Convfnt ASCII data.  
  If omitted, defaults to the input image filename with a `.txt` extension (same directory).

### Pixel interpretation

Each glyph is written in `convfont` format:

- Header:
  ```text
  convfont
  Height: <glyphHeight>
  Fixed width: <glyphWidth>
  Font data:
  ```
- For each glyph:
  - A `Code point: ...` line.
  - A `Data:` line.
  - `height` lines of `width` characters using:
    - `#` for a "set" pixel
    - Space (` `) for an "unset" pixel

A pixel is considered set if:

- Its alpha is greater than 128, and
- The average of its RGB components is less than 128 (i.e., sufficiently dark and opaque).

Pixels outside the image bounds (due to padding or cell spacing) are treated as unset (spaces).

### Errors

The tool prints descriptive error messages and exits with a non-zero code in cases such as:

- Image file not found.
- Invalid dimensions (non-positive width/height).
- Negative padding or cell spacing.
- Invalid `startcodepoint` or `numglyphs` values.
- `--charsfile` not found or defining zero glyphs.
- Image too small (after padding) to contain at least one `width x height` cell.

On success, it prints a summary that includes:

- The number of glyphs processed.
- Either the range of code points used, or the number of glyphs defined by the character file.
- The path to the generated output file.
