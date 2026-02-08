using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.Services;

using Avalonia.Markup.Xaml;

namespace HengcordTCG.Editor
{
    public partial class MainWindow : Window
    {
        private readonly CardImageService _imageService;
        private byte[]? _lastGenerated;

        public MainWindow()
        {
            AvaloniaXamlLoader.Load(this);
            
            // Asset path logic:
            // 1. Check if Assets folder exists right next to the executable (for distribution)
            // 2. Fallback to developer paths (up several levels)
            string appDir = AppContext.BaseDirectory;
            string assetPath = Path.Combine(appDir, "Assets");

            if (!Directory.Exists(assetPath))
            {
                // Dev fallback: go up from bin/Debug/net9.0/
                assetPath = Path.Combine(appDir, "..", "..", "..", "..", "Assets");
            }

            _imageService = new CardImageService(assetPath);

            var generateButton = this.FindControl<Button>("GenerateButton");
            if (generateButton != null)
            {
                generateButton.Click += OnGenerateClicked;
            }
        }

        private string? _selectedArtPath;

        public async void OnSelectArtClicked(object? sender, RoutedEventArgs e)
        {
            var storage = this.StorageProvider;
            var files = await storage.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Wybierz grafikę karty",
                FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp" } } },
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                _selectedArtPath = files[0].Path.LocalPath;
                var label = this.FindControl<TextBlock>("SelectedArtLabel");
                if (label != null) label.Text = $"Wybrano: {Path.GetFileName(_selectedArtPath)}";
                UpdateStatus($"Wybrano art: {Path.GetFileName(_selectedArtPath)}");
            }
        }

        public async void OnSaveClicked(object? sender, RoutedEventArgs e)
        {
            if (_lastGenerated == null) return;

            var storage = this.StorageProvider;
            var file = await storage.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Zapisz kartę",
                DefaultExtension = "png",
                SuggestedFileName = "karta.png",
                FileTypeChoices = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Images") { Patterns = new[] { "*.png" } } }
            });

            if (file != null)
            {
                using var stream = await file.OpenWriteAsync();
                await stream.WriteAsync(_lastGenerated);
                UpdateStatus("Zapisano kartę pomyślnie.");
            }
        }

        public async void OnGenerateClicked(object? sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Generowanie...");
                
                var card = new Card
                {
                    Name = this.FindControl<TextBox>("CardNameInput")?.Text ?? "Unknown",
                    Attack = (int)(this.FindControl<NumericUpDown>("AttackInput")?.Value ?? 0),
                    Defense = (int)(this.FindControl<NumericUpDown>("DefenseInput")?.Value ?? 0),
                    Rarity = Enum.Parse<Rarity>(this.FindControl<ComboBox>("RarityInput")?.SelectedItem is ComboBoxItem item ? item.Content?.ToString() ?? "Common" : "Common")
                };

                var layout = new CardLayout
                {
                    ArtX = (int)(this.FindControl<NumericUpDown>("ArtXInput")?.Value ?? 50),
                    ArtY = (int)(this.FindControl<NumericUpDown>("ArtYInput")?.Value ?? 100),
                    ArtWidth = (int)(this.FindControl<NumericUpDown>("ArtWInput")?.Value ?? 400),
                    ArtHeight = (int)(this.FindControl<NumericUpDown>("ArtHInput")?.Value ?? 300),
                    
                    NameX = (int)(this.FindControl<NumericUpDown>("NameXInput")?.Value ?? 250),
                    NameY = (int)(this.FindControl<NumericUpDown>("NameYInput")?.Value ?? 50),
                    NameFontSize = (int)(this.FindControl<NumericUpDown>("NameSizeInput")?.Value ?? 24),
                    
                    AttackX = (int)(this.FindControl<NumericUpDown>("AtkXInput")?.Value ?? 100),
                    AttackY = (int)(this.FindControl<NumericUpDown>("AtkYInput")?.Value ?? 420),
                    AttackFontSize = (int)(this.FindControl<NumericUpDown>("AtkSizeInput")?.Value ?? 32),
                    
                    DefenseX = (int)(this.FindControl<NumericUpDown>("DefXInput")?.Value ?? 350),
                    DefenseY = (int)(this.FindControl<NumericUpDown>("DefYInput")?.Value ?? 420),
                    DefenseFontSize = (int)(this.FindControl<NumericUpDown>("DefSizeInput")?.Value ?? 32)
                };

                _lastGenerated = await _imageService.GenerateCardImageAsync(card, _selectedArtPath, layout);
                
                var previewImage = this.FindControl<Image>("PreviewImage");
                if (previewImage != null)
                {
                    using var ms = new MemoryStream(_lastGenerated);
                    previewImage.Source = new Bitmap(ms);
                }

                var saveButton = this.FindControl<Button>("SaveButton");
                if (saveButton != null) saveButton.IsEnabled = true;
                
                UpdateStatus("Wygenerowano podgląd.");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Błąd: {ex.Message}");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private void UpdateStatus(string message)
        {
            var statusLabel = this.FindControl<TextBlock>("StatusLabel");
            if (statusLabel != null) statusLabel.Text = message;
        }
    }
}
