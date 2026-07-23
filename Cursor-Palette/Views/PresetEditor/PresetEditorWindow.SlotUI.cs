using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ellipse = System.Windows.Shapes.Ellipse;
using Rectangle = System.Windows.Shapes.Rectangle;
using CursorPalette.Models;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PresetEditorWindow
{
	private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

	private Slot CreateSlot(CursorRoleInfo role, string? defaultPath)
	{
		var preview = new Image { Width = SlotPreviewSize, Height = SlotPreviewSize, SnapsToDevicePixels = true };
		RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.NearestNeighbor);

		var hotspotDot = new Ellipse
		{
			Width = HotspotDotSize,
			Height = HotspotDotSize,
			Fill = Brush(BrushAccent),
			Stroke = System.Windows.Media.Brushes.White,
			StrokeThickness = 1.5,
			IsHitTestVisible = false,
			Visibility = Visibility.Collapsed,
		};

		var previewHost = new Canvas
		{
			Width = SlotPreviewSize,
			Height = SlotPreviewSize,
			Margin = new Thickness(0, PreviewTopMargin, 0, 0),
		};

		Canvas.SetLeft(preview, 0);
		Canvas.SetTop(preview, 0);
		previewHost.Children.Add(preview);
		previewHost.Children.Add(hotspotDot);

		var placeholderBadge = new Border
		{
			Background = Brush(BrushSurfaceHover),
			BorderBrush = Brush(BrushBorder),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(4),
			Padding = new Thickness(4, 1, 4, 1),
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0, 0, 0, 3),
			Child = new TextBlock
			{
				Text = Loc.Get(LocEditorPlaceholderBadge),
				FontSize = PlaceholderBadgeFontSize,
				Foreground = Brush(BrushTextDim),
			},
		};

		var roleName = new TextBlock
		{
			Text = Loc.Get("S." + role.DisplayKey),
			FontWeight = FontWeights.SemiBold,
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(4, 6, 4, 0),
			FontSize = RoleNameFontSize,
		};

		var fileText = new TextBlock
		{
			Text = Loc.Get(LocEditorEmptySlot),
			Foreground = Brush(BrushTextDim),
			FontSize = FileTextFontSize,
			TextAlignment = TextAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis,
			Margin = new Thickness(4, 2, 4, 0),
		};

		var browseButton = new Button
		{
			Content = Loc.Get(LocEditorBrowse),
			Style = (Style)Application.Current.Resources[StyleButton],
			FontSize = ButtonFontSize,
			Padding = new Thickness(6, 3, 6, 3),
			Margin = new Thickness(0, 6, 0, 0),
			HorizontalAlignment = HorizontalAlignment.Center,
		};

		var pickExistingButton = new Button
		{
			Content = PickExistingButtonContent,
			Style = (Style)Application.Current.Resources[StyleButton],
			FontSize = ButtonFontSize,
			Width = IconButtonSize,
			Height = IconButtonSize,
			Padding = new Thickness(0),
			ToolTip = Loc.Get(LocEditorPickExisting),
		};
		var pivotButton = new Button
		{
			Content = PivotButtonContent,
			Style = (Style)Application.Current.Resources[StyleButton],
			FontSize = ButtonFontSize,
			Width = IconButtonSize,
			Height = IconButtonSize,
			Padding = new Thickness(0),
			Margin = new Thickness(6, 0, 0, 0),
			ToolTip = Loc.Get(LocEditorPivotTooltip),
			Visibility = Visibility.Collapsed,
		};

		var positionButton = new Button
		{
			Content = PositionButtonContent,
			Style = (Style)Application.Current.Resources[StyleButton],
			FontSize = ButtonFontSize,
			Width = IconButtonSize,
			Height = IconButtonSize,
			Padding = new Thickness(0),
			Margin = new Thickness(6, 0, 0, 0),
			ToolTip = Loc.Get(LocEditorPositionTooltip),
			Visibility = Visibility.Collapsed,
		};

		var iconButtonsRow = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0, 6, 0, 0),
		};
		iconButtonsRow.Children.Add(pickExistingButton);
		iconButtonsRow.Children.Add(pivotButton);
		iconButtonsRow.Children.Add(positionButton);

		var primaryButtons = new StackPanel
		{
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		primaryButtons.Children.Add(browseButton);
		primaryButtons.Children.Add(iconButtonsRow);

		var clearButton = new Button
		{
			Content = ClearButtonContent,
			Style = (Style)Application.Current.Resources[StyleDangerButton],
			FontSize = ButtonFontSize,
			Width = CornerBadgeSize,
			Height = CornerBadgeSize,
			Padding = new Thickness(0),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0, 6, 6, 0),
			ToolTip = Loc.Get(LocEditorClearSlot),
			Visibility = Visibility.Collapsed,
		};

		var linkBadge = new Rectangle
		{
			Width = LinkBadgeIconSize,
			Height = LinkBadgeIconSize,
			Fill = Brush(BrushAccent),
			OpacityMask = new ImageBrush(new BitmapImage(new Uri(LinkIconUri))),
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0, 0, 0, 4),
			Visibility = Visibility.Collapsed,
		};

		var lockIcon = new Rectangle
		{
			Width = LockIconSize,
			Height = LockIconSize,
			Fill = Brush(BrushText),
			OpacityMask = new ImageBrush(new BitmapImage(new Uri(LockIconUri))),
			IsHitTestVisible = false,
		};

		var lockButton = new Button
		{
			Content = lockIcon,
			Style = (Style)Application.Current.Resources[StyleButton],
			Width = CornerBadgeSize,
			Height = CornerBadgeSize,
			Padding = new Thickness(0),
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(6, 6, 0, 0),
			ToolTip = Loc.Get(LocEditorLockTooltip),
		};

		var downloadIcon = new Rectangle
		{
			Width = DownloadIconSize,
			Height = DownloadIconSize,
			Fill = Brush(BrushTextDim),
			OpacityMask = new ImageBrush(new BitmapImage(new Uri(DownloadIconUri))),
			IsHitTestVisible = false,
		};

		var downloadButton = new Border
		{
			Width = CornerBadgeSize,
			Height = CornerBadgeSize,
			Background = Brushes.Transparent,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom,
			Margin = new Thickness(0, 0, 6, 6),
			Cursor = Cursors.Hand,
			ToolTip = Loc.Get(LocEditorDownloadTooltip),
			Visibility = Visibility.Collapsed,
			Child = downloadIcon,
		};
		downloadButton.MouseEnter += (_, _) => downloadIcon.Fill = Brush(BrushAccent);
		downloadButton.MouseLeave += (_, _) => downloadIcon.Fill = Brush(BrushTextDim);

		var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		panel.Children.Add(placeholderBadge);
		panel.Children.Add(linkBadge);
		panel.Children.Add(previewHost);
		panel.Children.Add(roleName);
		panel.Children.Add(fileText);
		panel.Children.Add(primaryButtons);

		var dropIndicator = new Rectangle
		{
			Stroke = Brush(BrushAccent),
			StrokeThickness = 3,
			StrokeDashArray = new DoubleCollection { 4, 2 },
			RadiusX = SlotCornerRadius,
			RadiusY = SlotCornerRadius,
			Margin = new Thickness(2),
			IsHitTestVisible = false,
			Visibility = Visibility.Collapsed,
		};

		var slotContent = new Grid();
		slotContent.Children.Add(panel);
		slotContent.Children.Add(clearButton);
		slotContent.Children.Add(lockButton);
		slotContent.Children.Add(downloadButton);
		slotContent.Children.Add(dropIndicator);

		var border = new Border
		{
			Width = SlotWidth,
			Height = SlotHeight,
			Margin = new Thickness(SlotMargin),
			CornerRadius = new CornerRadius(SlotCornerRadius),
			Background = Brush(BrushSurface),
			BorderThickness = new Thickness(SlotBorderThickness),
			BorderBrush = Brush(BrushBorder),
			Child = slotContent,
			AllowDrop = true,
		};

		var slot = new Slot
		{
			Role = role,
			DefaultPath = defaultPath,
			PreviewImage = preview,
			FileText = fileText,
			ClearButton = clearButton,
			PivotButton = pivotButton,
			PositionButton = positionButton,
			LockButton = lockButton,
			LockIcon = lockIcon,
			DownloadButton = downloadButton,
			PrimaryButtons = primaryButtons,
			DropIndicator = dropIndicator,
			PlaceholderBadge = placeholderBadge,
			HotspotDot = hotspotDot,
			LinkBadge = linkBadge,
		};

		browseButton.Click += (_, _) => BrowseForSlot(slot);
		pickExistingButton.Click += (_, _) => PickExistingForSlot(slot);
		pivotButton.Click += (_, _) => OpenHotspotEditor(slot);
		positionButton.Click += (_, _) => OpenPaintEditor(slot);
		clearButton.Click += (_, _) => ClearSlot(slot);
		lockButton.Click += (_, _) => SetSlotLocked(slot, !slot.IsLocked);
		downloadButton.MouseLeftButtonUp += (_, e) =>
		{
			DownloadSlot(slot);
			e.Handled = true;
		};

		DropZoneService.AttachManaged(
			border,
			e => !slot.IsLocked && GetSingleCursorFile(e) != null,
			_ => { },
			HideAllDropIndicators);

		border.Drop += (_, e) =>
		{
			if (slot.IsLocked)
			{
				e.Handled = true;

				return;
			}

			var file = GetSingleCursorFile(e);

			if (file != null)
			{
				if (IsCursorFullyTransparent(file))
				{
					MessageBox.Show(Loc.Get(LocEditorEmptyCursorWarning), Title,
						MessageBoxButton.OK, MessageBoxImage.Warning);
					e.Handled = true;

					return;
				}

				SetSlotSource(slot, file);
				e.Handled = true;
			}
		};

		Slots.Items.Add(border);

		SetSlotPlaceholder(slot);

		return slot;
	}
}
