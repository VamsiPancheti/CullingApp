// File: MainViewModel.cs
using ImageCulling.Core; 
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ImageCulling.Maui.Models;

public class MainViewModel : BindableObject
{
    private const string ThumbnailCacheDirectoryName = "gallery-thumbnails";
    private static readonly List<string> ThumbnailCachePaths = new();

    private string _folderPath = string.Empty;
    private string _statusMessage = "Ready.";
    private bool _isBusy;
    private VisualPhoto _selectedPhoto;

    public string FolderPath { get => _folderPath; set { _folderPath = value; OnPropertyChanged(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }
    public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }
    
    // Controls the Fullscreen Preview
    public VisualPhoto SelectedPhoto
    {
        get => _selectedPhoto;
        set
        {
            _selectedPhoto = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPreviewVisible));
        }
    }

    public bool IsPreviewVisible => SelectedPhoto != null;

    public ObservableCollection<VisualCategoryGroup> CategorizedPhotos { get; } = new();
    
    public ICommand AnalyzeFolderCommand { get; }
    public ICommand ClosePreviewCommand { get; }

    public MainViewModel()
    {
        AnalyzeFolderCommand = new Command(async () => await ProcessFolderAsync());
        ClosePreviewCommand = new Command(() => SelectedPhoto = null);
    }

    private async Task ProcessFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath)) return;

        CleanupThumbnailCache();
        SelectedPhoto = null;
        IsBusy = true;
        StatusMessage = "Analyzing & Grouping Photos...";
        CategorizedPhotos.Clear();

        try
        {
            var results = await Task.Run(() =>
            {
                var rawExtractor = new RawExtractor();
                var analyzer = new ImageAnalyzer();
                var grouper = new SceneGrouper();
                var culler = new AutoCuller();

                var extensions = new[] { ".arw", ".cr2", ".cr3", ".nef", ".dng" };
                var files = Directory.GetFiles(FolderPath).Where(f => extensions.Contains(Path.GetExtension(f).ToLower())).ToList();

                var tempMetadata = new List<PhotoMetadata>();
                var byteMap = new Dictionary<string, byte[]>();

                foreach (var file in files)
                {
                    try
                    {
                        byte[] jpegBytes = rawExtractor.ExtractEmbeddedJpeg(file);
                        if (jpegBytes == null || jpegBytes.Length == 0) continue;
                        byteMap[file] = jpegBytes;

                        var meta = analyzer.AnalyzePhoto(file, new float[512], jpegBytes);
                        tempMetadata.Add(meta);
                    }
                    catch { /* Skip bad files */ }
                }

                var scenes = grouper.GroupIntoScenes(tempMetadata);
                var allDecisions = new List<CullingDecision>();

                foreach (var scene in scenes)
                {
                    allDecisions.AddRange(culler.CullScene(scene));
                }

                return new { Decisions = allDecisions, RawByteMap = byteMap };
            });

            var groupedDecisions = results.Decisions.GroupBy(d => d.Status.Split(':')[0]).OrderBy(g => g.Key);

            foreach (var group in groupedDecisions)
            {
                var photosInGroup = new List<VisualPhoto>();
                foreach (var d in group)
                {
                    if (results.RawByteMap.TryGetValue(d.Photo.FilePath, out byte[] bytes) && bytes.Length > 0)
                    {
                        photosInGroup.Add(new VisualPhoto
                        {
                            FilePath = d.Photo.FilePath,
                            Status = d.Status,
                            SharpnessScore = d.Photo.SharpnessScore,
                            ThumbnailSource = CreateThumbnailSource(bytes)
                        });
                    }
                }

                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Dispatch(() =>
                    {
                        CategorizedPhotos.Add(new VisualCategoryGroup(group.Key, photosInGroup));
                    });
                }
            }

            StatusMessage = "Categorization Complete.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static ImageSource? CreateThumbnailSource(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }

        string cacheDirectory = Path.Combine(FileSystem.Current.CacheDirectory, ThumbnailCacheDirectoryName);
        Directory.CreateDirectory(cacheDirectory);

        string tempPath = Path.Combine(cacheDirectory, $"{Guid.NewGuid():N}.jpg");
        File.WriteAllBytes(tempPath, bytes);
        ThumbnailCachePaths.Add(tempPath);

        return ImageSource.FromFile(tempPath);
    }

    private static void CleanupThumbnailCache()
    {
        foreach (string path in ThumbnailCachePaths.Where(File.Exists))
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Ignore cleanup failures; the next run will retry.
            }
        }

        ThumbnailCachePaths.Clear();
    }
}