namespace CustomClothingBase;

public class JsonFileWatcher
{
    private FileSystemWatcher _contentWatcher;

    public JsonFileWatcher(string directoryToWatch)
    {
        Directory.CreateDirectory(directoryToWatch);
        _contentWatcher = new FileSystemWatcher
        {
            Path = directoryToWatch,
            Filter = "*.json", // Only watch JSON files
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };

        _contentWatcher.Changed += OnFileChanged;
        _contentWatcher.EnableRaisingEvents = false;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
        => PatchClass.ClearClothingCache();

    public void Dispose() => _contentWatcher.Dispose();
}
