using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PresetEditorWindow
{
	private void OnFileNameEditButtonClick(Slot slot)
	{
		if (slot.SourcePath == null)
			return;

		slot.FileNameEditBox.Text = Path.GetFileNameWithoutExtension(slot.SourcePath);
		slot.FileNameRow.Visibility = Visibility.Collapsed;
		slot.FileNameEditContainer.Visibility = Visibility.Visible;
		UpdateFileNamePlaceholder(slot);
		slot.FileNameEditBox.Focus();
		slot.FileNameEditBox.SelectAll();
	}

	private void OnFileNameEditBoxKeyDown(Slot slot, KeyEventArgs eventArgs)
	{
		if (eventArgs.Key == Key.Enter)
		{
			CommitFileNameEdit(slot);
			eventArgs.Handled = true;
		}
		else if (eventArgs.Key == Key.Escape)
		{
			CancelFileNameEdit(slot);
			eventArgs.Handled = true;
		}
	}

	private void OnFileNameEditBoxLostFocus(Slot slot, RoutedEventArgs eventArgs)
	{
		if (slot.FileNameEditContainer.Visibility == Visibility.Visible)
			CommitFileNameEdit(slot);
	}

	private void UpdateFileNamePlaceholder(Slot slot)
	{
		slot.FileNamePlaceholder.Visibility = string.IsNullOrEmpty(slot.FileNameEditBox.Text)
			? Visibility.Visible
			: Visibility.Collapsed;
	}

	private void CancelFileNameEdit(Slot slot)
	{
		slot.FileNameEditContainer.Visibility = Visibility.Collapsed;
		slot.FileNameRow.Visibility = Visibility.Visible;
	}

	private void CommitFileNameEdit(Slot slot)
	{
		slot.FileNameEditContainer.Visibility = Visibility.Collapsed;
		slot.FileNameRow.Visibility = Visibility.Visible;

		var sourcePath = slot.SourcePath;

		if (sourcePath == null)
			return;

		var extension = Path.GetExtension(sourcePath);
		var rawName = slot.FileNameEditBox.Text;
		var invalid = Path.GetInvalidFileNameChars();
		var newName = new string(rawName.Where(character => !invalid.Contains(character)).ToArray()).Trim();

		if (string.IsNullOrWhiteSpace(newName))
			newName = slot.Role.RegistryName;

		var newFileName = newName + extension;
		var currentFileName = Path.GetFileName(sourcePath);

		if (string.Equals(newFileName, currentFileName, StringComparison.OrdinalIgnoreCase))
			return;

		var directory = Path.GetDirectoryName(sourcePath);

		if (directory == null)
			return;

		var directPath = Path.Combine(directory, newFileName);

		if (File.Exists(directPath))
			return;

		var tempDirectory = Path.Combine(Path.GetTempPath(), TempRenameDirPrefix + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempDirectory);

		var tempPath = Path.Combine(tempDirectory, newFileName);

		File.Copy(sourcePath, tempPath, overwrite: true);

		slot.SourcePath = tempPath;
		slot.FileText.Text = newFileName;

		CursorPreviewService.Invalidate(tempPath);
		CursorPreviewService.ApplyPreview(slot.PreviewImage, tempPath);
		UpdateHotspotDot(slot);
	}
}
