using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PresetEditorWindow
{
	private IDisposable? _dragLeaveWatchdog;

	private void OnPresetWindowDragEnter(object sender, DragEventArgs e)
	{
		_dragLeaveWatchdog ??= DropZoneService.StartLeaveWatchdog(this, HideAllDropIndicators);

		if (HasDroppableFolderSource(e))
		{
			FolderDropIndicator.Visibility = Visibility.Visible;
			SetSlotIndicatorsVisibility(Visibility.Collapsed);
		}
		else if (GetSingleDroppableFile(e) != null)
		{
			FolderDropIndicator.Visibility = Visibility.Collapsed;
			SetSlotIndicatorsVisibility(Visibility.Visible);
		}
		else
		{
			HideAllDropIndicators();
		}
	}

	private void OnPresetWindowDragLeave(object sender, DragEventArgs e) =>
		DropZoneService.HandleWindowDragLeave(this, HideAllDropIndicators);

	private void OnPresetWindowDrop(object sender, DragEventArgs e)
	{
		HideAllDropIndicators();
	}

	private void HideAllDropIndicators()
	{
		FolderDropIndicator.Visibility = Visibility.Collapsed;
		SetSlotIndicatorsVisibility(Visibility.Collapsed);

		_dragLeaveWatchdog?.Dispose();
		_dragLeaveWatchdog = null;
	}

	private void SetSlotIndicatorsVisibility(Visibility visibility)
	{
		foreach (var slot in _slots)
			slot.DropIndicator.Visibility = visibility == Visibility.Visible && slot.IsLocked
				? Visibility.Collapsed
				: visibility;
	}

	private void OnFolderDragOver(object sender, DragEventArgs e)
	{
		e.Effects = HasDroppableFolderSource(e) ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = true;
	}

	private void OnFolderDrop(object sender, DragEventArgs e)
	{
		HideAllDropIndicators();

		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return;

		e.Handled = true;

		// Deferred: MessageBox.Show synchronously inside Drop confuses the OS
		// OLE drag-drop loop and leaves the cursor stuck in a "still dragging" state.
		Dispatcher.BeginInvoke(new Action(() => HandleDroppedFolderPaths(paths)), DispatcherPriority.Input);
	}

	private void HandleDroppedFolderPaths(string[] paths)
	{
		var folder = paths.FirstOrDefault(Directory.Exists);
		if (folder != null)
		{
			ImportFolder(folder);
			return;
		}

		var archive = paths.FirstOrDefault(ArchiveImportService.IsArchiveFile);
		if (archive == null)
			return;

		try
		{
			ImportFolder(ArchiveImportService.ExtractToTempFolder(archive), recursive: true,
				displayName: Path.GetFileNameWithoutExtension(archive));
		}
		catch (Exception ex)
		{
			MessageBox.Show(Loc.Format(LocErrorArchiveExtractFailed, ex.Message), Title,
				MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private static bool HasDroppableFolderSource(DragEventArgs e)
	{
		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return false;

		return paths.Any(path => Directory.Exists(path) || ArchiveImportService.IsArchiveFile(path));
	}

	private static string? GetSingleDroppableFile(DragEventArgs e)
	{
		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return null;

		return paths.FirstOrDefault(p => File.Exists(p) && ImageToCursorService.IsConvertibleFile(p));
	}
}
