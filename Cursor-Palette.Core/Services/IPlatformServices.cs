namespace CursorPalette.Services;

public interface IScreenColorPicker
{
	bool TryGetScreenPixelColor(out (byte R, byte G, byte B) color);
	(byte R, byte G, byte B) GetScreenPixelColor(int screenX, int screenY);
}

public interface IFileExplorer
{
	void RevealFile(string filePath);
}

public interface ISingleInstance
{
	bool TryAcquire();
	void NotifyExistingInstance();
}

public interface IAssetLoader
{
	Stream? TryOpenAsset(string relativePath);
}
