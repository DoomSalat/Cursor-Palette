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

		PushHistory();

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

		var position = e.GetPosition(ViewportContent);
		var deltaX = (int)Math.Round(position.X - _spriteDragStart.X);
		var deltaY = (int)Math.Round(position.Y - _spriteDragStart.Y);

		var (minX, maxX) = HorizontalRange();
		var (minY, maxY) = VerticalRange();
		_offsetX = Math.Clamp(_dragStartOffsetX + deltaX, minX, maxX);
		_offsetY = Math.Clamp(_dragStartOffsetY + deltaY, minY, maxY);

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

		if (_offsetX <= min)
			return;

		PushHistory();

		_offsetX = Math.Max(min, _offsetX - 1);
		RenderAll();
	}

	private void OnMoveRightClick(object sender, RoutedEventArgs e)
	{
		var (_, max) = HorizontalRange();

		if (_offsetX >= max)
			return;

		PushHistory();

		_offsetX = Math.Min(max, _offsetX + 1);
		RenderAll();
	}

	private void OnMoveUpClick(object sender, RoutedEventArgs e)
	{
		var (min, _) = VerticalRange();

		if (_offsetY <= min)
			return;

		PushHistory();

		_offsetY = Math.Max(min, _offsetY - 1);
		RenderAll();
	}

	private void OnMoveDownClick(object sender, RoutedEventArgs e)
	{
		var (_, max) = VerticalRange();

		if (_offsetY >= max)
			return;

		PushHistory();

		_offsetY = Math.Min(max, _offsetY + 1);
		RenderAll();
	}

	private void OnSnapClick(object sender, RoutedEventArgs e)
	{
		var (fractionX, fractionY) = ParseFraction((Button)sender);
		var (minX, maxX) = HorizontalRange();
		var (minY, maxY) = VerticalRange();
		var newOffsetX = SnapOffset(fractionX, minX, maxX);
		var newOffsetY = SnapOffset(fractionY, minY, maxY);

		if (newOffsetX == _offsetX && newOffsetY == _offsetY)
			return;

		PushHistory();

		_offsetX = newOffsetX;
		_offsetY = newOffsetY;

		RenderAll();
	}
}
