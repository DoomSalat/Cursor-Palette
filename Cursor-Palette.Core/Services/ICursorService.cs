namespace CursorPalette.Services;

public sealed class CursorSnapshot
{
	public Dictionary<string, string> Values { get; init; } = new();
	public int BaseSize { get; init; } = CursorConstants.DefaultBaseSize;
}

public interface ICursorService
{
	void ApplyValues(IReadOnlyDictionary<string, string> values);
	void SetBaseSize(int sizeInPixels);
	int GetBaseSize();
	Dictionary<string, string> ReadCurrentValues();
	Dictionary<string, string> GetDefaultValues();
	void ResetToDefault();
	CursorSnapshot TakeSnapshot();
	void RestoreSnapshot(CursorSnapshot snapshot);
	void SaveSnapshotToDisk(CursorSnapshot snapshot);
	CursorSnapshot? LoadSnapshotFromDisk();
}
