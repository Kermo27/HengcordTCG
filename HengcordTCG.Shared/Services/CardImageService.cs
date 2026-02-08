using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Fonts;
using HengcordTCG.Shared.Models;

namespace HengcordTCG.Shared.Services;

public class CardImageService
{
    private readonly string _templatePath;
    private readonly string _artPath;
    private readonly string _fontPath;

    public CardImageService(string baseAssetPath)
    {
        _templatePath = Path.Combine(baseAssetPath, "Templates");
        _artPath = Path.Combine(baseAssetPath, "Arts");
        _fontPath = Path.Combine(baseAssetPath, "Fonts");
    }

    private string GetArtFilePath(string cardName)
    {
        if (string.IsNullOrEmpty(cardName)) return Path.Combine(_artPath, "placeholder.png");
        
        string[] extensions = { ".png", ".jpg", ".jpeg", ".webp" };
        foreach (var ext in extensions)
        {
            string path = Path.Combine(_artPath, $"{cardName}{ext}");
            if (File.Exists(path)) return path;
        }
        return Path.Combine(_artPath, "placeholder.png");
    }

    public Task<byte[]> GenerateCardImageAsync(Card card) 
        => GenerateCardImageAsync(card, null, new CardLayout());

    public async Task<byte[]> GenerateCardImageAsync(Card card, string? customArtPath, CardLayout layout)
    {
        // 1. Load Assets
        string templateFile = Path.Combine(_templatePath, $"{card.Rarity}.png");
        if (!File.Exists(templateFile)) templateFile = Path.Combine(_templatePath, "Common.png");
        
        using var template = await Image.LoadAsync(templateFile);
        
        // Create canvas with template size
        using var canvas = new Image<Rgba32>(template.Width, template.Height);

        // 2. Load and Draw Art First (Background)
        string artFile = !string.IsNullOrEmpty(customArtPath) && File.Exists(customArtPath) 
            ? customArtPath 
            : GetArtFilePath(card.Name);

        if (File.Exists(artFile))
        {
            using var art = await Image.LoadAsync(artFile);
            
            art.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(layout.ArtWidth, layout.ArtHeight),
                Mode = ResizeMode.Crop
            }));

            canvas.Mutate(x => x.DrawImage(art, new Point(layout.ArtX, layout.ArtY), 1f));
        }

        // 3. Draw Template Over Art
        canvas.Mutate(x => x.DrawImage(template, new Point(0, 0), 1f));

        // 4. Draw Text Over Everything
        string fontFile = Path.Combine(_fontPath, "Ebrima Regular.ttf");
        if (File.Exists(fontFile))
        {
            var fontCollection = new FontCollection();
            var family = fontCollection.Add(fontFile);
            var fontName = family.CreateFont(layout.NameFontSize, FontStyle.Bold);
            var fontAtk = family.CreateFont(layout.AttackFontSize, FontStyle.Bold);
            var fontDef = family.CreateFont(layout.DefenseFontSize, FontStyle.Bold);

            canvas.Mutate(x => 
            {
                // Name
                var nameOptions = new RichTextOptions(fontName)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Origin = new PointF(layout.NameX, layout.NameY)
                };
                x.DrawText(nameOptions, card.Name, Color.White);

                // Attack
                x.DrawText(card.Attack.ToString(), fontAtk, Color.Red, new PointF(layout.AttackX, layout.AttackY));
                
                // Defense
                x.DrawText(card.Defense.ToString(), fontDef, Color.Blue, new PointF(layout.DefenseX, layout.DefenseY));
            });
        }

        // 5. Save to MemoryStream
        using var ms = new MemoryStream();
        await canvas.SaveAsPngAsync(ms);
        return ms.ToArray();
    }
}
