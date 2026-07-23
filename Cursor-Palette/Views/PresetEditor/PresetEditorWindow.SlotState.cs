using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PresetEditorWindow
{
	private void SetSlotSource(Slot slot, string path)
	{
		slot.SourcePath = path;
		slot.RefPresetId = null;
		slot.RefFileName = null;
		CursorPreviewService.ApplyPreview(slot.PreviewImage, path);
		slot.PreviewImage.Opacity = 1;
		slot.PlaceholderBadge.Visibility = Visibility.Collapsed;
		slot.LinkBadge.Visibility = Visibility.Collapsed;
		slot.FileText.Text = Path.GetFileName(path);
		slot.FileText.Foreground = Brush(BrushText);
		slot.ClearButton.Visibility = Visibility.Visible;
		slot.PivotButton.Visibility = Visibility.Visible;
		slot.PivotButton.IsEnabled = true;
		slot.PivotButton.ToolTip = Loc.Get(LocEditorPivotTooltip);
		slot.DownloadButton.Visibility = Visibility.Visible;

		slot.PositionButton.Visibility = CursorCanvasService.IsSupportedFile(path)
			? Visibility.Visible
			: Visibility.Collapsed;
		slot.PositionButton.IsEnabled = true;
		slot.PositionButton.ToolTip = Loc.Get(LocEditorPositionTooltip);

		UpdateHotspotDot(slot);
	}

	private void SetSlotReference(Slot slot, string presetId, string fileName)
	{
		slot.SourcePath = null;
		slot.RefPresetId = presetId;
		slot.RefFileName = fileName;

		var resolvedPath = Path.Combine(PresetStore.GetFilesDir(presetId), fileName);
		var label = BuildReferenceLabel(presetId, fileName);
		CursorPreviewService.ApplyPreview(slot.PreviewImage, resolvedPath);
		slot.PreviewImage.Opacity = 1;
		slot.PlaceholderBadge.Visibility = Visibility.Collapsed;
		slot.LinkBadge.Visibility = Visibility.Visible;
		slot.LinkBadge.ToolTip = Loc.Format(LocEditorLinkedRoleTooltip, label);
		slot.FileText.Text = label;
		slot.FileText.Foreground = Brush(BrushText);
		slot.ClearButton.Visibility = Visibility.Visible;
		slot.PivotButton.Visibility = Visibility.Visible;
		slot.PivotButton.IsEnabled = false;
		slot.PivotButton.ToolTip = Loc.Get(LocEditorPivotDisabledTooltip);
		slot.DownloadButton.Visibility = Visibility.Visible;

		slot.PositionButton.Visibility = CursorCanvasService.IsSupportedFile(resolvedPath)
			? Visibility.Visible
			: Visibility.Collapsed;
		slot.PositionButton.IsEnabled = false;
		slot.PositionButton.ToolTip = Loc.Get(LocEditorPositionDisabledTooltip);

		UpdateHotspotDot(slot);
	}

	private static string BuildReferenceLabel(string presetId, string fileName)
	{
		var sourceName = PresetStore.LoadAll().FirstOrDefault(p => p.Id == presetId)?.Name;

		return sourceName != null ? $"{sourceName} / {fileName}" : fileName;
	}

	private void SetSlotPlaceholder(Slot slot)
	{
		slot.SourcePath = null;
		slot.RefPresetId = null;
		slot.RefFileName = null;
		CursorPreviewService.ApplyPreview(slot.PreviewImage, slot.DefaultPath);
		slot.PreviewImage.Opacity = PlaceholderOpacity;
		slot.PlaceholderBadge.Visibility = string.IsNullOrWhiteSpace(slot.DefaultPath)
			? Visibility.Collapsed
			: Visibility.Visible;
		slot.FileText.Text = Loc.Get(LocEditorEmptySlot);
		slot.FileText.Foreground = Brush(BrushTextDim);
		slot.ClearButton.Visibility = Visibility.Collapsed;
		slot.PivotButton.Visibility = Visibility.Collapsed;
		slot.PositionButton.Visibility = Visibility.Visible;
		slot.DownloadButton.Visibility = Visibility.Collapsed;
		slot.HotspotDot.Visibility = Visibility.Collapsed;
		slot.LinkBadge.Visibility = Visibility.Collapsed;
	}

	private void ClearSlot(Slot slot) => SetSlotPlaceholder(slot);

	private void SetSlotLocked(Slot slot, bool locked)
	{
		slot.IsLocked = locked;

		var accent = Brush(BrushAccent);
		slot.LockButton.BorderBrush = locked ? accent : Brush(BrushBorder);
		slot.LockButton.BorderThickness = new Thickness(locked ? 2 : 1);
		slot.LockIcon.Fill = locked ? accent : Brush(BrushText);
		slot.LockButton.ToolTip = Loc.Get(locked ? LocEditorUnlockTooltip : LocEditorLockTooltip);

		slot.PrimaryButtons.IsEnabled = !locked;
		slot.ClearButton.IsEnabled = !locked;
	}

	private static string? GetSlotResolvedPath(Slot slot) =>
		slot.SourcePath ?? (slot.RefPresetId != null && slot.RefFileName != null
			? Path.Combine(PresetStore.GetFilesDir(slot.RefPresetId), slot.RefFileName)
			: null);

	private void UpdateHotspotDot(Slot slot)
	{
		var resolvedPath = GetSlotResolvedPath(slot);
		var hotspot = resolvedPath != null ? CursorHotspotService.Read(resolvedPath) : null;

		if (hotspot == null)
		{
			slot.HotspotDot.Visibility = Visibility.Collapsed;
			return;
		}

		var displayX = (hotspot.X + 0.5) / hotspot.Width * SlotPreviewSize;
		var displayY = (hotspot.Y + 0.5) / hotspot.Height * SlotPreviewSize;

		Canvas.SetLeft(slot.HotspotDot, displayX - HotspotDotSize / 2);
		Canvas.SetTop(slot.HotspotDot, displayY - HotspotDotSize / 2);
		slot.HotspotDot.Visibility = Visibility.Visible;
	}
}
