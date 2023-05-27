# asciifier

Converts images to ASCII art.

## Usage

Clone the repo and use `dotnet run` to run the program.

Using `dotnet run -- --help` will print the following usage information:

```
Description:
  Converts an image from a file to ASCII art, using the specified font.

Usage:
  asciifier [options]

Options:
  --input <input>                                        The input filepath for the image.
  --font <font>                                          The input filepath for the font.
  --output <output>                                      The output filepath for the image.
  --character-set <Ascii|AsciiAndBlocks|Blocks|Symbols>  Which set of characters to use. [default: AsciiAndBlocks]
  --font-size <font-size>                                The font size to draw at. [default: 12]
  --colour                                               Whether to use color. [default: False]
  --version                                              Show version information
  -?, -h, --help                                         Show help and usage information
```

## License

The code is licensed under the AGPLv3.
See [LICENSE](LICENSE) for full detail.
