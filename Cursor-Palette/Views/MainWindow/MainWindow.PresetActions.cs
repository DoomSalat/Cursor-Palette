using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class MainWindow
{
	private void SetSliderSilently(int sizeInPixels)
	{
		SizeSlider.Value = (sizeInPixels - RegistryCursorService.SizeStep) / (double)RegistryCursorService.SizeStep;
		SizeValueText.Text = $"{sizeInPixels} {PixelSuffix}";
	}

	private void ShowLoadingOverlay()
	{
		LoadingOverlay.Visibility = Visibility.Visible;
		((Storyboard)Resources[SpinnerStoryboardKey]).Begin(this, true);
	}

	private void HideLoadingOverlay()
	{
		((Storyboard)Resources[SpinnerStoryboardKey]).Stop(this);
		LoadingOverlay.Visibility = Visibility.Collapsed;
	}

	private async void ApplyPreset(Preset preset, bool force = false)
	{
		if (!force && preset.Id == _activePresetId)
			return;

		try
		{
			ShowLoadingOverlay();
			await Dispatcher.Yield(DispatcherPriority.Render);

			var sourceValues = new Dictionary<string, string>();
			foreach (var role in CursorRoles.All)
			{
				var path = PresetStore.GetRoleFilePath(preset, role.RegistryName);
				sourceValues[role.RegistryName] = path != null && File.Exists(path) ? path : EmptyValue;
			}

			var baseSize = preset.BaseSize;
			var useScaling = AppState.GetScaleCursorsEnabled() && preset.UseScaling;

			await Task.Run(() =>
			{
				RegistryCursorService.SaveSnapshotToDisk(RegistryCursorService.TakeSnapshot());
				var scaledValues = useScaling
					? CursorScalerService.ScaleValues(sourceValues, baseSize, preset.ScaleMode)
					: sourceValues;
				RegistryCursorService.ApplyValues(scaledValues);
				RegistryCursorService.SetBaseSize(baseSize);
			});

			_activeSourceValues = sourceValues;
			_activeUseScaling = useScaling;
			_activeScaleMode = preset.ScaleMode;

			_baselineSizePx = preset.BaseSize;
			SetSliderSilently(preset.BaseSize);
			_activePresetId = preset.Id;
			AppState.SetActivePresetId(preset.Id);

			ReloadGallery();
			SetScaleCheckboxSilently(preset.UseScaling);
			UpdateScaleIcon(preset.ScaleMode);
			UpdateUndoButton();
		}
		catch (Exception exception)
		{
			MessageBox.Show(Loc.Format(LocErrorApplyFailed, exception.Message),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
		}
		finally
		{
			HideLoadingOverlay();
		}
	}

	private async void ApplyDefault()
	{
		if (_activePresetId == null)
			return;

		try
		{
			ShowLoadingOverlay();
			await Dispatcher.Yield(DispatcherPriority.Render);

			var defaultSize = AppState.GetDefaultBaseSize();
			var defaultValues = RegistryCursorService.GetWindowsDefaultValues();
			var defaultUseScaling = AppState.GetScaleCursorsEnabled();
			var defaultScaleMode = AppState.GetScaleMode();

			await Task.Run(() =>
			{
				RegistryCursorService.SaveSnapshotToDisk(RegistryCursorService.TakeSnapshot());
				var scaledValues = defaultUseScaling
					? CursorScalerService.ScaleValues(defaultValues, defaultSize, defaultScaleMode)
					: defaultValues;
				RegistryCursorService.ApplyValues(scaledValues);
				RegistryCursorService.SetBaseSize(defaultSize);
			});

			_activeSourceValues = defaultValues;
			_activeUseScaling = defaultUseScaling;
			_activeScaleMode = defaultScaleMode;

			_activePresetId = null;
			AppState.SetActivePresetId(null);

			_baselineSizePx = defaultSize;
			SetSliderSilently(defaultSize);

			ReloadGallery();
			SetScaleCheckboxSilently(defaultUseScaling);
			UpdateScaleIcon(defaultScaleMode);
			UpdateUndoButton();
		}
		catch (Exception exception)
		{
			MessageBox.Show(Loc.Format(LocErrorApplyFailed, exception.Message),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
		}
		finally
		{
			HideLoadingOverlay();
		}
	}

	private static readonly Random RandomPicker = new();

	private void ApplyRandomFromGroup(PresetGroup group)
	{
		var presetsById = _presets.ToDictionary(p => p.Id);
		var members = group.MemberPresetIds
			.Where(id => presetsById.ContainsKey(id))
			.Select(id => presetsById[id])
			.Where(p => p.Id != _activePresetId)
			.ToList();

		if (members.Count == 0)
			return;

		var picked = members[RandomPicker.Next(members.Count)];
		ApplyPreset(picked, force: true);
	}

	private void ApplyRandomFromBoard()
	{
		var candidates = _presets.Where(p => p.Id != _activePresetId).ToList();

		if (candidates.Count == 0)
			return;

		var picked = candidates[RandomPicker.Next(candidates.Count)];
		ApplyPreset(picked, force: true);
	}

	private async void OnUndoButtonClick(object sender, RoutedEventArgs e)
	{
		var snapshot = RegistryCursorService.LoadSnapshotFromDisk();

		if (snapshot == null)
			return;

		try
		{
			ShowLoadingOverlay();
			await Dispatcher.Yield(DispatcherPriority.Render);

			var undoUseScaling = AppState.GetScaleCursorsEnabled();
			var undoScaleMode = AppState.GetScaleMode();

			await Task.Run(() =>
			{
				RegistryCursorService.SaveSnapshotToDisk(RegistryCursorService.TakeSnapshot());
				var scaledValues = undoUseScaling
					? CursorScalerService.ScaleValues(snapshot.Values, snapshot.BaseSize, undoScaleMode)
					: snapshot.Values;
				RegistryCursorService.ApplyValues(scaledValues);
				RegistryCursorService.SetBaseSize(snapshot.BaseSize);
			});

			_activeSourceValues = new Dictionary<string, string>(snapshot.Values);

			_activePresetId = FindPresetIdByValues(snapshot.Values);
			AppState.SetActivePresetId(_activePresetId);

			_baselineSizePx = snapshot.BaseSize;
			SetSliderSilently(snapshot.BaseSize);

			ReloadGallery();

			var undoPreset = _activePresetId != null
				? _presets.FirstOrDefault(candidate => candidate.Id == _activePresetId)
				: null;
			var undoEffectiveUseScaling = undoPreset != null ? undoUseScaling && undoPreset.UseScaling : undoUseScaling;
			_activeUseScaling = undoEffectiveUseScaling;
			_activeScaleMode = undoPreset?.ScaleMode ?? undoScaleMode;
			SetScaleCheckboxSilently(undoPreset?.UseScaling ?? undoUseScaling);
			UpdateScaleIcon(_activeScaleMode);

			UpdateUndoButton();
		}
		catch (Exception exception)
		{
			MessageBox.Show(Loc.Format(LocErrorApplyFailed, exception.Message),
				Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
		}
		finally
		{
			HideLoadingOverlay();
		}
	}

	private string? FindPresetIdByValues(IReadOnlyDictionary<string, string> values)
	{
		if (!values.TryGetValue(CursorRoles.ArrowRoleName, out var arrow) || string.IsNullOrEmpty(arrow))
			return null;

		return _presets.FirstOrDefault(preset =>
			string.Equals(PresetStore.GetRoleFilePath(preset, CursorRoles.ArrowRoleName), arrow,
				StringComparison.OrdinalIgnoreCase))?.Id;
	}

	private void UpdateUndoButton() =>
		UndoButton.IsEnabled = RegistryCursorService.LoadSnapshotFromDisk() != null;

	private void DeletePreset(Preset preset)
	{
		var answer = MessageBox.Show(
			Loc.Format(LocConfirmDeleteText, preset.Name),
			Loc.Get(LocConfirmDeleteTitle),
			MessageBoxButton.YesNo, MessageBoxImage.Question);

		if (answer != MessageBoxResult.Yes)
			return;

		if (_presetToGroup.TryGetValue(preset.Id, out var owningGroup))
			GroupStore.RemoveMember(owningGroup.Id, preset.Id);

		PresetStore.Delete(preset.Id);

		if (_activePresetId == preset.Id)
		{
			_activePresetId = null;
			AppState.SetActivePresetId(null);
		}

		ReloadGallery();
	}

	private void MovePreset(Preset preset, int direction)
	{
		var visibleIndex = _visibleBoardIds.IndexOf(preset.Id);
		if (visibleIndex < 0)
			return;

		var targetVisibleIndex = visibleIndex + direction;
		if (targetVisibleIndex < 0 || targetVisibleIndex >= _visibleBoardIds.Count)
			return;

		var ownIndex = _boardOrderIds.IndexOf(preset.Id);
		var targetIndex = _boardOrderIds.IndexOf(_visibleBoardIds[targetVisibleIndex]);
		if (ownIndex < 0 || targetIndex < 0)
			return;

		(_boardOrderIds[ownIndex], _boardOrderIds[targetIndex]) = (_boardOrderIds[targetIndex], _boardOrderIds[ownIndex]);
		PersistBoardOrder();
	}

	private void DownloadPreset(Preset preset)
	{
		var roleFiles = new Dictionary<string, string>();

		foreach (var role in CursorRoles.All)
		{
			var resolvedPath = PresetStore.GetRoleFilePath(preset, role.RegistryName);

			if (resolvedPath != null && File.Exists(resolvedPath))
				roleFiles[role.RegistryName] = resolvedPath;
		}

		var path = PresetPackageService.DownloadPresetAsFolder(preset.Name, roleFiles, preset.BaseSize, preset.UseScaling, preset.ScaleMode, preset.LockedRoles);

		if (path == null)
			return;

		ToastService.Show(RootGrid, Loc.Format(LocToastPresetDownloaded, preset.Name, roleFiles.Count));

		if (AppState.GetOpenFolderAfterDownload())
			ExplorerService.RevealFile(path);
	}

	private void StartInlineRename(Preset preset, TextBlock nameText, StackPanel panel)
	{
		var index = panel.Children.IndexOf(nameText);
		if (index < 0)
			return;

		var done = false;

		var textBox = new TextBox
		{
			Text = preset.Name,
			FontSize = nameText.FontSize,
			FontWeight = FontWeights.SemiBold,
			TextAlignment = TextAlignment.Center,
			Margin = new Thickness(nameText.Margin.Left, nameText.Margin.Top - 2, nameText.Margin.Right, nameText.Margin.Bottom),
			Style = (Style)Application.Current.Resources[StyleTextBox],
			Background = Brush(BrushBg),
			BorderBrush = Brush(BrushAccent),
			BorderThickness = new Thickness(1.5),
			Padding = new Thickness(6, 4, 6, 4),
		};

		void Restore()
		{
			var currentIndex = panel.Children.IndexOf(textBox);
			if (currentIndex < 0)
				return;

			panel.Children.RemoveAt(currentIndex);
			panel.Children.Insert(currentIndex, nameText);
		}

		void Commit()
		{
			if (done)
				return;
			done = true;

			var newName = textBox.Text.Trim();
			Restore();

			if (!string.IsNullOrWhiteSpace(newName) && newName != preset.Name)
			{
				PresetStore.Rename(preset.Id, newName);
				ReloadGallery();
			}
		}

		void Cancel()
		{
			if (done)
				return;
			done = true;
			Restore();
		}

		textBox.PreviewMouseLeftButtonDown += (_, e) => e.Handled = true;
		textBox.PreviewMouseLeftButtonUp += (_, e) => e.Handled = true;
		textBox.KeyDown += (_, e) =>
		{
			if (e.Key == Key.Enter)
			{
				Commit();
				e.Handled = true;
			}
			else if (e.Key == Key.Escape)
			{
				Cancel();
				e.Handled = true;
			}
		};
		textBox.LostFocus += (_, _) => Commit();

		panel.Children.RemoveAt(index);
		panel.Children.Insert(index, textBox);
		textBox.Focus();
		textBox.SelectAll();
	}

	private void DownloadSystemCursors(bool asImages)
	{
		var defaults = RegistryCursorService.GetWindowsDefaultValues();
		var folderName = Loc.Get(LocWindowsDefault);
		var destDir = Path.Combine(AppPaths.DownloadsDir, folderName);
		var attempt = 1;

		while (Directory.Exists(destDir))
			destDir = Path.Combine(AppPaths.DownloadsDir, $"{folderName} ({attempt++})");

		Directory.CreateDirectory(destDir);
		var count = 0;

		foreach (var role in CursorRoles.All)
		{
			if (!defaults.TryGetValue(role.RegistryName, out var rawPath) || string.IsNullOrWhiteSpace(rawPath))
				rawPath = PlaceholderCursorDefaults.GetPath(role.RegistryName);

			if (string.IsNullOrWhiteSpace(rawPath))
				continue;

			var expanded = Environment.ExpandEnvironmentVariables(rawPath);
			if (!File.Exists(expanded))
				continue;

			string destPath;

			if (asImages)
			{
				var ext = Path.GetExtension(expanded).ToLowerInvariant();

				if (ext == AniExtension)
				{
					var frames = AniCursorReader.Read(expanded);
					if (frames == null || frames.Frames.Count == 0)
						continue;

					var bitmaps = new List<System.Windows.Media.Imaging.BitmapSource>(frames.Frames.Count);
					var delays = new List<int>(frames.Frames.Count);

					for (var i = 0; i < frames.StepFrameIndices.Count; i++)
					{
						bitmaps.Add(frames.Frames[frames.StepFrameIndices[i]]);
						delays.Add((int)frames.StepDurations[i].TotalMilliseconds);
					}

					destPath = Path.Combine(destDir, $"{role.RegistryName}.gif");
					AnimatedGifWriter.Save(destPath, bitmaps, delays);
				}
				else
				{
					var image = CursorCanvasService.TryRead(expanded);
					if (image == null)
						continue;

					var bitmap = new System.Windows.Media.Imaging.WriteableBitmap(
						image.Width, image.Height, 96, 96,
						System.Windows.Media.PixelFormats.Bgra32, null);
					bitmap.WritePixels(
						new System.Windows.Int32Rect(0, 0, image.Width, image.Height),
						image.Bgra, image.Width * 4, 0);
					bitmap.Freeze();

					destPath = Path.Combine(destDir, $"{role.RegistryName}.png");
					var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
					encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));

					using var stream = File.Create(destPath);
					encoder.Save(stream);
				}
			}
			else
			{
				var extension = Path.GetExtension(expanded);
				destPath = Path.Combine(destDir, $"{role.RegistryName}{extension}");
				File.Copy(expanded, destPath);
			}

			var now = DateTime.Now;
			File.SetCreationTime(destPath, now);
			File.SetLastWriteTime(destPath, now);
			count++;
		}

		if (count == 0)
		{
			Directory.Delete(destDir);
			return;
		}

		ToastService.Show(RootGrid, Loc.Format(LocToastSystemCursorsDownloaded, count));
	}

	private void EditPreset(Preset preset) => OpenEditor(preset, Array.Empty<string>());

	private void OpenEditor(Preset? preset, IReadOnlyList<string> droppedFiles, string? suggestedName = null)
	{
		var editor = new PresetEditorWindow(preset, droppedFiles, suggestedName) { Owner = this };

		if (editor.ShowDialog() == true && editor.Result != null)
		{
			Preset saved;
			try
			{
				saved = PresetStore.Save(editor.Result);
			}
			catch (Exception ex)
			{
				MessageBox.Show(Loc.Format(LocErrorSaveFailed, ex.Message),
					Loc.Get(LocErrorTitle), MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			foreach (var fileName in saved.Roles.Values)
				CursorPreviewService.Invalidate(
					System.IO.Path.Combine(PresetStore.GetFilesDir(saved.Id), fileName));

			if (saved.Id == _activePresetId)
				ApplyPreset(saved, force: true);
			else
				ReloadGallery();

			ToastService.Show(RootGrid, Loc.Get(LocToastSaved));
		}
	}
}
