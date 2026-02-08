namespace HengcordTCG.Shared.Models;

public class CardLayout
{
    // Art Position and Size
    public int ArtX { get; set; } = 50;
    public int ArtY { get; set; } = 100;
    public int ArtWidth { get; set; } = 400;
    public int ArtHeight { get; set; } = 300;

    // Text Positions and Sizes
    public int NameX { get; set; } = 250; 
    public int NameY { get; set; } = 50;
    public int NameFontSize { get; set; } = 24;

    public int AttackX { get; set; } = 100;
    public int AttackY { get; set; } = 420; 
    public int AttackFontSize { get; set; } = 32;

    public int DefenseX { get; set; } = 350; 
    public int DefenseY { get; set; } = 420;
    public int DefenseFontSize { get; set; } = 32;
}
