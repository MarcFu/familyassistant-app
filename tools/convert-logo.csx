#!/usr/bin/env dotnet-script
#r "nuget: Svg.Skia, 2.0.0.4"
#r "nuget: SkiaSharp, 3.116.1"

using SkiaSharp;
using Svg.Skia;

var baseDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
var addonDir = Path.Combine(baseDir, "familyassistant");
var wwwrootDir = Path.Combine(baseDir, "src", "FamilyAssistant", "wwwroot");

void RenderSvgToPng(string svgPath, string pngPath, int width, int height)
{
    using var svg = new SKSvg();
    svg.Load(svgPath);
    
    if (svg.Picture == null)
    {
        Console.Error.WriteLine($"ERROR: Failed to load SVG: {svgPath}");
        return;
    }

    using var bitmap = new SKBitmap(width, height);
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.Transparent);

    // Scale SVG to fit target size
    var svgBounds = svg.Picture.CullRect;
    float scaleX = width / svgBounds.Width;
    float scaleY = height / svgBounds.Height;
    float scale = Math.Min(scaleX, scaleY);

    // Center in target
    float offsetX = (width - svgBounds.Width * scale) / 2f;
    float offsetY = (height - svgBounds.Height * scale) / 2f;

    canvas.Translate(offsetX, offsetY);
    canvas.Scale(scale);
    canvas.DrawPicture(svg.Picture);
    canvas.Flush();

    using var image = SKImage.FromBitmap(bitmap);
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    using var stream = File.OpenWrite(pngPath);
    data.SaveTo(stream);

    Console.WriteLine($"  {Path.GetFileName(pngPath)} ({width}x{height}) -> {pngPath}");
}

Console.WriteLine("Converting SVGs to PNGs...\n");

// icon.svg -> icon.png (128x128)
var iconSvg = Path.Combine(addonDir, "icon.svg");
var iconPng = Path.Combine(addonDir, "icon.png");
RenderSvgToPng(iconSvg, iconPng, 128, 128);

// logo.svg -> logo.png (250x100)
var logoSvg = Path.Combine(addonDir, "logo.svg");
var logoPng = Path.Combine(addonDir, "logo.png");
RenderSvgToPng(logoSvg, logoPng, 250, 100);

// icon.svg -> favicon.png (64x64)
var faviconPng = Path.Combine(wwwrootDir, "favicon.png");
RenderSvgToPng(iconSvg, faviconPng, 64, 64);

Console.WriteLine("\nDone!");
