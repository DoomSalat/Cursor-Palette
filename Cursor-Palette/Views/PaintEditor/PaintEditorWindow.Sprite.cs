using System.Windows;
using System.Windows.Controls;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PaintEditorWindow
{
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
