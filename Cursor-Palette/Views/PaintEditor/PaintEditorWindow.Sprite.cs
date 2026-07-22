using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PaintEditorWindow
{
	private void OnSpriteMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (_currentTool != AppState.PaintEditorToolMove)
			return;

		_isDraggingSprite = true;
		_spriteDragStart = e.GetPosition(ViewportContent);
		_dragStartOffsetX = _offsetX;
		_dragStartOffsetY = _offsetY;
		PreviewImage.CaptureMouse();
		e.Handled = true;
	}

	private void OnSpriteMouseMove(object sender, MouseEventArgs e)
	{
		if (!_isDraggingSprite)
			return;

		var pos = e.GetPosition(ViewportContent);
		var dx = (int)Math.Round(pos.X - _spriteDragStart.X);
		var dy = (int)Math.Round(pos.Y - _spriteDragStart.Y);

		var (minX, maxX) = HorizontalRange();
		var (minY, maxY) = VerticalRange();
		_offsetX = Math.Clamp(_dragStartOffsetX + dx, minX, maxX);
		_offsetY = Math.Clamp(_dragStartOffsetY + dy, minY, maxY);

		RenderAll();
	}

	private void OnSpriteMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		_isDraggingSprite = false;
		PreviewImage.ReleaseMouseCapture();
	}

	private void OnMoveLeftClick(object sender, RoutedEventArgs e)
	{
		var (min, _) = HorizontalRange();
		_offsetX = Math.Max(min, _offsetX - 1);
		RenderAll();
	}

	private void OnMoveRightClick(object sender, RoutedEventArgs e)
	{
		var (_, max) = HorizontalRange();
		_offsetX = Math.Min(max, _offsetX + 1);
		RenderAll();
	}

	private void OnMoveUpClick(object sender, RoutedEventArgs e)
	{
		var (min, _) = VerticalRange();
		_offsetY = Math.Max(min, _offsetY - 1);
		RenderAll();
	}

	private void OnMoveDownClick(object sender, RoutedEventArgs e)
	{
		var (_, max) = VerticalRange();
		_offsetY = Math.Min(max, _offsetY + 1);
		RenderAll();
	}

	private void OnSnapClick(object sender, RoutedEventArgs e)
	{
		var (fractionX, fractionY) = ParseFraction((Button)sender);
		var (minX, maxX) = HorizontalRange();
		var (minY, maxY) = VerticalRange();

		_offsetX = SnapOffset(fractionX, minX, maxX);
		_offsetY = SnapOffset(fractionY, minY, maxY);

		RenderAll();
	}
}
