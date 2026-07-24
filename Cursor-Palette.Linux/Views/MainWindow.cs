using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using CursorPalette.Linux.ViewModels;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public partial class MainWindow : Window
{
	private const string PixelSuffix = "px";
	private const string FooterFormat = "{0}  ·  v{1}  ·  {2}";
	private const string EmptyValue = "";
	private const string CursorFileFilterName = "Cursors";
	private const string DeleteButtonText = "Delete";
	private const string CancelButtonText = "Cancel";
	private const string AddCellPlusText = "+";
	private const string ThemeIconDark = "🌙";
	private const string ThemeIconLight = "☀";
	private const string MixedBadgeText = "🧩";

	private const string LocApplySize = "S.ApplySize";
	private const string LocUndo = "S.Undo";
	private const string LocResetDefault = "S.ResetDefault";
	private const string LocConfirmDeleteTitle = "S.ConfirmDelete.Title";
	private const string LocConfirmDeleteText = "S.ConfirmDelete.Text";
	private const string LocEditorCancel = "S.Editor.Cancel";

	private const double DialogMargin = 16;
	private const double DialogSpacing = 12;
	private const double ButtonSpacing = 8;
	private const double DeleteDialogWidth = 360;
	private const double DeleteDialogHeight = 160;

	private static readonly string[] SupportedLanguages = { "en", "ru", "de", "es", "ja", "zh" };
	private static readonly string[] CursorFilePatterns = { "*.cur", "*.ani", "*.png", "*.jpg", "*.bmp", "*.gif" };

	private readonly MainWindowViewModel _viewModel = new();
	private Slider? _sizeSlider;
	private TextBlock? _sizeValueText;
	private Button? _applySizeButton;
	private Button? _languageButton;

	public MainWindow()
	{
		InitializeComponent();
		DataContext = _viewModel;
		_viewModel.Initialize();

		_sizeSlider = this.FindControl<Slider>("SizeSlider");
		_sizeValueText = this.FindControl<TextBlock>("SizeValueText");
		_applySizeButton = this.FindControl<Button>("ApplySizeButton");
		_languageButton = this.FindControl<Button>("LanguageButton");

		if (_sizeSlider != null)
		{
			_sizeSlider.Value = _viewModel.BaselineSizePx;
			_sizeSlider.PropertyChanged += OnSizeSliderChanged;
		}

		if (_applySizeButton != null)
			_applySizeButton.Click += OnApplySizeClick;

		UpdateSizeText(_viewModel.BaselineSizePx);
		ApplyLocalization();
		UpdateThemeToggleIcon();

		var currentLang = LocalizationManager.Current;
		_languageIndex = Math.Max(0, Array.IndexOf(SupportedLanguages, currentLang));
		if (_languageButton != null)
			_languageButton.Content = SupportedLanguages[_languageIndex].ToUpperInvariant();

		AddHandler(DragDrop.DropEvent, OnDrop);
		AddHandler(DragDrop.DragOverEvent, OnDragOver);
	}

	private void ApplyLocalization()
	{
		if (_applySizeButton != null)
			_applySizeButton.Content = Loc.Get(LocApplySize);

		var undoButton = this.FindControl<Button>("UndoButton");
		if (undoButton != null)
			undoButton.Content = Loc.Get(LocUndo);

		var defaultLabel = this.FindControl<TextBlock>("DefaultLabel");
		if (defaultLabel != null)
			defaultLabel.Text = Loc.Get(LocResetDefault);
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private void OnSizeSliderChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
	{
		if (_sizeSlider == null)
			return;

		var size = (int)Math.Round(_sizeSlider.Value);
		UpdateSizeText(size);
	}

	private void UpdateSizeText(int size)
	{
		if (_sizeValueText != null)
			_sizeValueText.Text = $"{size} {PixelSuffix}";
	}

	private async void OnApplySizeClick(object? sender, RoutedEventArgs e)
	{
		if (_sizeSlider == null)
			return;

		var size = (int)Math.Round(_sizeSlider.Value);
		await _viewModel.ApplySizeAsync(size);
	}

	public async void OnPresetClick(object? sender, PointerPressedEventArgs e)
	{
		if (sender is not Control control || control.DataContext is not BoardItem item)
			return;

		if (item.IsAddCell)
		{
			await OpenFilePickerForCursors();
			return;
		}

		if (item.IsDefaultCell)
		{
			ApplyDefault();
			return;
		}

		if (item.IsPreset && item.Preset != null)
			await _viewModel.ApplyPresetAsync(item.Preset);
	}

	private async void ApplyDefault()
	{
		await _viewModel.ApplyDefaultAsync();
	}

	private async Task OpenFilePickerForCursors()
	{
		var topLevel = TopLevel.GetTopLevel(this);
		if (topLevel == null)
			return;

		var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = Loc.Get(LocResetDefault),
			AllowMultiple = true,
			FileTypeFilter = new[]
			{
				new FilePickerFileType(CursorFileFilterName)
				{
					Patterns = CursorFilePatterns
				}
			}
		});

		if (files.Count == 0)
			return;

		var paths = files.Select(f => f.Path.LocalPath).ToArray();
		await _viewModel.ImportCursorsAsync(paths);
	}

	private void OnDragOver(object? sender, DragEventArgs e)
	{
		if (e.Data.Contains(DataFormats.Files))
			e.DragEffects = DragDropEffects.Copy;
		else
			e.DragEffects = DragDropEffects.None;
	}

	private async void OnDrop(object? sender, DragEventArgs e)
	{
		if (e.Data.Contains(DataFormats.Files))
		{
			var files = e.Data.GetFiles();
			if (files == null)
				return;

			var paths = files.Select(f => f.Path.LocalPath).ToArray();
			await _viewModel.HandleDroppedPathsAsync(paths);
		}
	}

	private BoardItem? GetContextMenuItem(object? sender)
	{
		if (sender is MenuItem menuItem && menuItem.DataContext is BoardItem item)
			return item;

		if (sender is Control control && control.DataContext is BoardItem ctrlItem)
			return ctrlItem;

		return null;
	}

	public void OnMenuEdit(object? sender, RoutedEventArgs e)
	{
		// TODO: Open preset editor
	}

	public void OnMenuRename(object? sender, RoutedEventArgs e)
	{
		if (GetContextMenuItem(sender) is not { IsPreset: true, Preset: { } preset })
			return;

		// TODO: Implement proper rename dialog with TextBox
	}

	public void OnMenuMoveLeft(object? sender, RoutedEventArgs e)
	{
		if (GetContextMenuItem(sender) is not { IsPreset: true, Preset: { } preset })
			return;

		_viewModel.MovePreset(preset, -1);
	}

	public void OnMenuMoveRight(object? sender, RoutedEventArgs e)
	{
		if (GetContextMenuItem(sender) is not { IsPreset: true, Preset: { } preset })
			return;

		_viewModel.MovePreset(preset, 1);
	}

	public void OnMenuDownload(object? sender, RoutedEventArgs e)
	{
		// TODO: Implement download
	}

	public async void OnMenuDelete(object? sender, RoutedEventArgs e)
	{
		if (GetContextMenuItem(sender) is not { IsPreset: true, Preset: { } preset })
			return;

		var dialog = new Window
		{
			Title = Loc.Get(LocConfirmDeleteTitle),
			Width = DeleteDialogWidth,
			Height = DeleteDialogHeight,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
		};

		var panel = new StackPanel
		{
			Margin = new Avalonia.Thickness(DialogMargin),
			Spacing = DialogSpacing,
			VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
		};

		panel.Children.Add(new TextBlock
		{
			Text = Loc.Format(LocConfirmDeleteText, preset.Name),
			TextWrapping = Avalonia.Media.TextWrapping.Wrap,
		});

		var buttonPanel = new StackPanel
		{
			Orientation = Avalonia.Layout.Orientation.Horizontal,
			HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
			Spacing = ButtonSpacing,
		};

		var yesButton = new Button { Content = DeleteButtonText };
		var noButton = new Button { Content = Loc.Get(LocEditorCancel) };

		buttonPanel.Children.Add(noButton);
		buttonPanel.Children.Add(yesButton);
		panel.Children.Add(buttonPanel);
		dialog.Content = panel;

		noButton.Click += (_, _) => dialog.Close();
		yesButton.Click += (_, _) =>
		{
			_viewModel.DeletePreset(preset);
			dialog.Close();
		};

		await dialog.ShowDialog(this);
	}

	private async void OnUndoClick(object? sender, RoutedEventArgs e)
	{
		await _viewModel.UndoAsync();
	}

	private bool _isDarkTheme;

	private void OnThemeToggle(object? sender, RoutedEventArgs e)
	{
		_isDarkTheme = !_isDarkTheme;
		RequestedThemeVariant = _isDarkTheme ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
		UpdateThemeToggleIcon();
	}

	private void UpdateThemeToggleIcon()
	{
		var themeButton = this.FindControl<Button>("ThemeToggleButton");
		if (themeButton != null)
			themeButton.Content = _isDarkTheme ? ThemeIconDark : ThemeIconLight;
	}

	private int _languageIndex;

	private void OnLanguageClick(object? sender, RoutedEventArgs e)
	{
		_languageIndex = (_languageIndex + 1) % SupportedLanguages.Length;
		var lang = SupportedLanguages[_languageIndex];

		if (_languageButton != null)
			_languageButton.Content = lang.ToUpperInvariant();

		LocalizationManager.SetLanguage(lang);
		ApplyLocalization();
		_viewModel.ReloadGallery();
	}
}
