using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class MainWindow
{
	private void ReorderBoardItem(string draggedId, int insertBeforeIndex)
	{
		var draggedIndex = _boardOrderIds.IndexOf(draggedId);
		if (draggedIndex < 0)
			return;

		_boardOrderIds.RemoveAt(draggedIndex);

		if (draggedIndex < insertBeforeIndex)
			insertBeforeIndex--;

		insertBeforeIndex = Math.Clamp(insertBeforeIndex, 0, _boardOrderIds.Count);
		_boardOrderIds.Insert(insertBeforeIndex, draggedId);

		PersistBoardOrder();
	}

	private void ConsolidateGroup(string groupId)
	{
		var group = _groups.FirstOrDefault(candidate => candidate.Id == groupId);
		if (group == null)
			return;

		var groupIndex = _boardOrderIds.IndexOf(groupId);
		if (groupIndex < 0)
			return;

		var memberIds = _boardOrderIds.Where(id => group.MemberPresetIds.Contains(id)).ToList();
		if (memberIds.Count == 0)
			return;

		_boardOrderIds.RemoveAll(id => group.MemberPresetIds.Contains(id));

		groupIndex = _boardOrderIds.IndexOf(groupId);
		_boardOrderIds.InsertRange(groupIndex + 1, memberIds);

		PersistBoardOrder();
		ToastService.Show(RootGrid, Loc.Format(LocGroupToastConsolidated, group.Name));
	}

	private void PersistBoardOrder()
	{
		BoardOrderStore.Save(_boardOrderIds);
		ReloadGallery();
	}

	private void BeginDragGhost(Preset preset, string? previewPath)
	{
		var size = CellSize * _cellScale;
		DragGhost.Width = size;
		DragGhost.Height = size;

		DragGhostImage.Width = CellPreviewSize * _cellScale;
		DragGhostImage.Height = CellPreviewSize * _cellScale;
		RenderOptions.SetBitmapScalingMode(DragGhostImage, BitmapScalingMode.NearestNeighbor);
		CursorPreviewService.ApplyPreview(DragGhostImage, previewPath);

		DragGhostText.Text = preset.Name;
		DragGhostText.FontSize = CellNameFontSize * CellFontScale;

		DragGhost.Visibility = Visibility.Visible;
		_draggedPresetId = preset.Id;
	}

	private void BeginGroupDragGhost(PresetGroup group)
	{
		var size = CellSize * _cellScale;
		DragGhost.Width = size;
		DragGhost.Height = size;

		DragGhostImage.Visibility = Visibility.Collapsed;
		DragGhost.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(GroupColors.ResolveHex(group.ColorKey))!);

		DragGhostText.Text = group.Name;
		DragGhostText.FontSize = CellNameFontSize * CellFontScale;
		DragGhostText.Foreground = System.Windows.Media.Brushes.White;

		DragGhost.Visibility = Visibility.Visible;
		_draggedGroupId = group.Id;
	}

	private void EndDragGhost()
	{
		DragGhost.Visibility = Visibility.Collapsed;
		DragGhost.Background = Brush(BrushSurface);
		DragGhostImage.Visibility = Visibility.Visible;
		DragGhostText.ClearValue(TextBlock.ForegroundProperty);
		ReorderInsertionLine.Visibility = Visibility.Collapsed;
		GroupAttachIndicator.Visibility = Visibility.Collapsed;
		_pendingInsertIndex = null;
		_pendingGroupAttachId = null;
		_draggedPresetId = null;
		_draggedGroupId = null;
	}

	private void UpdateDragGhostPosition(Point positionInRoot)
	{
		DragGhostTransform.X = positionInRoot.X - DragGhost.Width / 2;
		DragGhostTransform.Y = positionInRoot.Y - DragGhost.Height / 2;
	}

	private Rect GetClippedItemBoundsInGallery(FrameworkElement container)
	{
		var topLeftInGallery = container.TransformToAncestor(Gallery).Transform(new Point(0, 0));

		return new Rect(topLeftInGallery.X, topLeftInGallery.Y,
			container.RenderSize.Width, container.RenderSize.Height);
	}

	private void UpdateReorderIndicator(Point positionInRoot, Point positionInGallery) =>
		UpdateBoardReorderIndicator(positionInRoot, positionInGallery, _draggedPresetId, allowGroupAttach: true);

	private void UpdateGroupReorderIndicator(Point positionInRoot, Point positionInGallery) =>
		UpdateBoardReorderIndicator(positionInRoot, positionInGallery, _draggedGroupId, allowGroupAttach: false);

	private void UpdateBoardReorderIndicator(Point positionInRoot, Point positionInGallery, string? draggedId, bool allowGroupAttach)
	{
		var galleryOffsetInRoot = positionInRoot - positionInGallery;
		var cells = new List<(BoardEntry entry, Rect bounds)>();

		for (var itemIndex = 0; itemIndex < _boardOrder.Count; itemIndex++)
		{
			if (Gallery.ItemContainerGenerator.ContainerFromIndex(itemIndex + 1) is not FrameworkElement container)
				continue;

			cells.Add((_boardOrder[itemIndex], GetClippedItemBoundsInGallery(container)));
		}

		if (allowGroupAttach)
		{
			var groupHover = cells.FirstOrDefault(cell => cell.entry.Group != null && IsInCenterZone(cell.bounds, positionInGallery));
			if (groupHover.entry?.Group != null)
			{
				SetGroupAttachIndicator(groupHover.entry.Group.Id, groupHover.entry.Group.Collapsed, groupHover.bounds, galleryOffsetInRoot);
				return;
			}
		}

		ClearGroupAttachIndicator();

		if (cells.Count == 0)
		{
			_pendingInsertIndex = 0;
			ReorderInsertionLine.Visibility = Visibility.Collapsed;
			return;
		}

		var rows = cells
			.GroupBy(cell => Math.Round(cell.bounds.Top / ReorderRowGroupingTolerance) * ReorderRowGroupingTolerance)
			.Select(group =>
			{
				var ordered = group.OrderBy(cell => cell.bounds.Left).ToList();
				var top = ordered.Min(c => c.bounds.Top);
				var bottom = ordered.Max(c => c.bounds.Bottom);
				return (top, bottom, cells: ordered);
			})
			.OrderBy(r => r.top)
			.ToList();

		if (rows.Count == 0)
		{
			_pendingInsertIndex = 0;
			ReorderInsertionLine.Visibility = Visibility.Collapsed;
			return;
		}

		var selectedRow = rows.FirstOrDefault(row => positionInGallery.Y >= row.top && positionInGallery.Y <= row.bottom);
		if (selectedRow.cells.Count == 0)
		{
			if (positionInGallery.Y < rows[0].top)
				selectedRow = rows[0];
			else
				selectedRow = rows[^1];
		}

		var rowTop = selectedRow.top;
		var rowHeight = selectedRow.bottom - selectedRow.top;
		var rowCells = selectedRow.cells;

		foreach (var (entry, bounds) in rowCells)
		{
			if (positionInGallery.X >= bounds.Left + bounds.Width / 2)
				continue;

			SetInsertionIndicator(entry.BoardIndex, bounds.Left - CellMargin, rowTop, rowHeight);
			return;
		}

		var lastCell = rowCells[^1];
		SetInsertionIndicator(lastCell.entry.BoardIndex + 1, lastCell.bounds.Right + CellMargin, rowTop, rowHeight);
		return;

		void SetInsertionIndicator(int insertBeforeIndex, double centerXInGallery, double topInGallery, double height)
		{
			var draggedIndex = draggedId != null ? _boardOrderIds.IndexOf(draggedId) : -1;

			if (draggedIndex >= 0 && (insertBeforeIndex == draggedIndex || insertBeforeIndex == draggedIndex + 1))
			{
				_pendingInsertIndex = null;
				ReorderInsertionLine.Visibility = Visibility.Collapsed;
				return;
			}

			_pendingInsertIndex = insertBeforeIndex;
			ReorderInsertionLineTransform.X = centerXInGallery + galleryOffsetInRoot.X - ReorderIndicatorWidth / 2;
			ReorderInsertionLineTransform.Y = topInGallery + galleryOffsetInRoot.Y;
			ReorderInsertionLine.Height = height;
			ReorderInsertionLine.Visibility = Visibility.Visible;
		}
	}

	private static bool IsInCenterZone(Rect bounds, Point point)
	{
		var marginX = bounds.Width * GroupAttachZoneMargin;
		var marginY = bounds.Height * GroupAttachZoneMargin;
		var zone = new Rect(bounds.Left + marginX, bounds.Top + marginY,
			Math.Max(0, bounds.Width - 2 * marginX), Math.Max(0, bounds.Height - 2 * marginY));

		return zone.Contains(point);
	}

	private void SetGroupAttachIndicator(string groupId, bool collapsed, Rect boundsInGallery, Vector galleryOffsetInRoot)
	{
		_pendingGroupAttachId = groupId;
		_pendingInsertIndex = null;
		ReorderInsertionLine.Visibility = Visibility.Collapsed;

		var colorKey = _groups.FirstOrDefault(candidate => candidate.Id == groupId)?.ColorKey;
		GroupAttachIndicator.Stroke = colorKey != null
			? new SolidColorBrush((Color)ColorConverter.ConvertFromString(GroupColors.ResolveHex(colorKey))!)
			: Brush(BrushAccent);

		var cellSize = CellSize * _cellScale;
		var tileTop = collapsed
			? boundsInGallery.Top
			: boundsInGallery.Top + (boundsInGallery.Height - cellSize) / 2;

		GroupAttachIndicatorTransform.X = boundsInGallery.Left + galleryOffsetInRoot.X - GroupOutlinePadding;
		GroupAttachIndicatorTransform.Y = tileTop + galleryOffsetInRoot.Y - GroupOutlinePadding;
		GroupAttachIndicator.Width = cellSize + GroupOutlinePadding * 2;
		GroupAttachIndicator.Height = cellSize + GroupOutlinePadding * 2;
		GroupAttachIndicator.Visibility = Visibility.Visible;
	}

	private void ClearGroupAttachIndicator()
	{
		_pendingGroupAttachId = null;
		GroupAttachIndicator.Visibility = Visibility.Collapsed;
	}

	private void AttachPresetToGroup(string draggedPresetId, string groupId)
	{
		if (!_groups.Any(candidate => candidate.Id == groupId))
			return;

		if (_presetToGroup.TryGetValue(draggedPresetId, out var oldGroup) && oldGroup.Id != groupId)
			GroupStore.RemoveMember(oldGroup.Id, draggedPresetId);

		GroupStore.AddMember(groupId, draggedPresetId);

		var groupIndex = _boardOrderIds.IndexOf(groupId);
		if (groupIndex < 0)
		{
			ReloadGallery();
			return;
		}

		ReorderBoardItem(draggedPresetId, groupIndex + 1);
	}

	private IDisposable? _windowDragLeaveWatchdog;

	private void OnWindowDragOver(object sender, DragEventArgs e)
	{
		if (e.Data.GetDataPresent(PresetDragFormatName))
		{
			var positionInRoot = e.GetPosition(RootGrid);
			var positionInGallery = e.GetPosition(Gallery);
			UpdateDragGhostPosition(positionInRoot);
			UpdateReorderIndicator(positionInRoot, positionInGallery);

			e.Effects = DragDropEffects.Move;
			e.Handled = true;
			return;
		}

		if (e.Data.GetDataPresent(GroupDragFormatName))
		{
			var positionInRoot = e.GetPosition(RootGrid);
			var positionInGallery = e.GetPosition(Gallery);
			UpdateDragGhostPosition(positionInRoot);
			UpdateGroupReorderIndicator(positionInRoot, positionInGallery);

			e.Effects = DragDropEffects.Move;
			e.Handled = true;
			return;
		}

		e.Effects = HasDroppableCursorSource(e) ? DragDropEffects.Copy : DragDropEffects.None;
		e.Handled = true;
	}

	private void OnWindowDragEnter(object sender, DragEventArgs e)
	{
		_windowDragLeaveWatchdog ??= DropZoneService.StartLeaveWatchdog(this, HideWindowDragIndicators);

		if (e.Data.GetDataPresent(PresetDragFormatName) || e.Data.GetDataPresent(GroupDragFormatName))
		{
			DragGhost.Visibility = Visibility.Visible;
			return;
		}

		if (HasDroppableCursorSource(e))
			WindowDropIndicator.Visibility = Visibility.Visible;
	}

	private void OnWindowDragLeave(object sender, DragEventArgs e) =>
		DropZoneService.HandleWindowDragLeave(this, HideWindowDragIndicators);

	private void HideWindowDragIndicators()
	{
		WindowDropIndicator.Visibility = Visibility.Collapsed;
		DragGhost.Visibility = Visibility.Collapsed;
		ReorderInsertionLine.Visibility = Visibility.Collapsed;
		GroupAttachIndicator.Visibility = Visibility.Collapsed;

		_windowDragLeaveWatchdog?.Dispose();
		_windowDragLeaveWatchdog = null;
	}

	private void OnWindowDrop(object sender, DragEventArgs e)
	{
		WindowDropIndicator.Visibility = Visibility.Collapsed;
		_windowDragLeaveWatchdog?.Dispose();
		_windowDragLeaveWatchdog = null;

		if (e.Data.GetData(PresetDragFormatName) is string draggedPresetId)
		{
			e.Handled = true;

			if (_pendingGroupAttachId is { } groupId)
				AttachPresetToGroup(draggedPresetId, groupId);
			else if (_pendingInsertIndex is { } insertIndex)
				ReorderBoardItem(draggedPresetId, insertIndex);

			return;
		}

		if (e.Data.GetData(GroupDragFormatName) is string draggedGroupId)
		{
			e.Handled = true;

			if (_pendingInsertIndex is { } groupInsertIndex)
				ReorderBoardItem(draggedGroupId, groupInsertIndex);

			return;
		}

		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return;

		e.Handled = true;

		Dispatcher.BeginInvoke(new Action(() => HandleDroppedPaths(paths)), DispatcherPriority.Input);
	}

	private void HandleDroppedPaths(string[] paths)
	{
		var packagePath = paths.FirstOrDefault(path => File.Exists(path) && PresetPackageService.IsSupportedPackageFile(path));
		if (packagePath != null)
		{
			DetectedPackage? detected;
			try
			{
				detected = PresetPackageService.TryDetectPackage(packagePath);
			}
			catch (PackageVersionUnsupportedException exception)
			{
				ToastService.Show(RootGrid, Loc.Format(LocErrorImportVersionUnsupported, exception.FoundVersion, exception.MaxSupportedVersion));
				return;
			}

			if (detected != null)
			{
				ImportPackage(detected);
				return;
			}

			// Not a recognized package (bundle/manifest/etc.). If it's a plain archive, fall through
			// and treat it like a dropped folder: look for cur/ani files inside it directly.
			if (!ArchiveImportService.IsArchiveFile(packagePath))
			{
				ToastService.Show(RootGrid, Loc.Get(LocErrorImportUnrecognized));
				return;
			}
		}

		foreach (var folderPath in paths.Where(Directory.Exists))
		{
			var detectedFolder = PresetPackageService.TryDetectPackageFromFolder(folderPath);

			if (detectedFolder == null)
				continue;

			ImportPackage(detectedFolder);
			return;
		}

		List<string> files;
		try
		{
			files = ResolveCursorFiles(paths);
		}
		catch (Exception exception)
		{
			MessageBox.Show(Loc.Format(LocErrorArchiveExtractFailed, exception.Message),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
			return;
		}

		if (files.Count == 0)
			return;

		OpenEditor(null, files, GetSuggestedPresetName(paths));
	}

	private static string? GetSuggestedPresetName(IEnumerable<string> paths)
	{
		var folder = paths.FirstOrDefault(Directory.Exists);

		if (folder != null)
			return System.IO.Path.GetFileName(folder);

		var archive = paths.FirstOrDefault(ArchiveImportService.IsArchiveFile);

		return archive != null ? System.IO.Path.GetFileNameWithoutExtension(archive) : null;
	}

	private static bool HasDroppableCursorSource(DragEventArgs e)
	{
		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return false;

		return paths.Any(path =>
			Directory.Exists(path) || ArchiveImportService.IsArchiveFile(path) ||
			PresetPackageService.IsSupportedPackageFile(path) || ImageToCursorService.IsConvertibleFile(path));
	}

	private static List<string> ResolveCursorFiles(IEnumerable<string> paths)
	{
		var result = new List<string>();

		foreach (var path in paths)
		{
			if (Directory.Exists(path))
				result.AddRange(Directory.EnumerateFiles(path, FileSearchPattern, SearchOption.TopDirectoryOnly)
					.Where(ImageToCursorService.IsConvertibleFile));
			else if (ArchiveImportService.IsArchiveFile(path))
				result.AddRange(Directory.EnumerateFiles(
						ArchiveImportService.ExtractToTempFolder(path), FileSearchPattern, SearchOption.AllDirectories)
					.Where(ImageToCursorService.IsConvertibleFile));
			else if (ImageToCursorService.IsConvertibleFile(path))
				result.Add(path);
		}

		return result;
	}
}
