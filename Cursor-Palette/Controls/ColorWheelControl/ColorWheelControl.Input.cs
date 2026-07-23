using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CursorPalette.Services;

namespace CursorPalette.Controls;

public partial class ColorWheelControl : UserControl
{
	private void OnEyedropperButtonClick(object sender, MouseButtonEventArgs e) =>
		EyedropperRequested?.Invoke(this, EventArgs.Empty);

	private void OnWheelModeClick(object sender, MouseButtonEventArgs e) => SetMode(PickerMode.Wheel);

	private void OnSquareModeClick(object sender, MouseButtonEventArgs e) => SetMode(PickerMode.Square);

	private void SetMode(PickerMode mode)
	{
		if (_mode == mode)
			return;

		_mode = mode;

		var isSquare = mode == PickerMode.Square;

		WheelImage.Visibility = isSquare ? Visibility.Collapsed : Visibility.Visible;
		BrightnessOverlay.Visibility = isSquare ? Visibility.Collapsed : Visibility.Visible;
		Indicator.Visibility = isSquare ? Visibility.Collapsed : Visibility.Visible;
		SquareImage.Visibility = isSquare ? Visibility.Visible : Visibility.Collapsed;
		SquareIndicatorLayer.Visibility = isSquare ? Visibility.Visible : Visibility.Collapsed;
		HueRow.Visibility = isSquare ? Visibility.Visible : Visibility.Collapsed;
		BrightnessRow.Visibility = isSquare ? Visibility.Collapsed : Visibility.Visible;

		WheelModeButton.Background = isSquare
			? (Brush)FindResource(BrushSurface)
			: (Brush)FindResource(BrushAccent);
		SquareModeButton.Background = isSquare
			? (Brush)FindResource(BrushAccent)
			: (Brush)FindResource(BrushSurface);

		WheelModeIcon.Stroke = Brushes.White;
		SquareModeIcon.Stroke = Brushes.White;

		if (isSquare)
		{
			SquareImage.Source = GenerateSquareBitmap(_hue);
			UpdateSquareIndicator();
		}
	}

	private void OnWheelMouseDown(object sender, MouseButtonEventArgs e)
	{
		_isDraggingWheel = true;
		WheelImage.CaptureMouse();
		UpdateFromMouse(e.GetPosition(WheelImage));
		e.Handled = true;
	}

	private void OnWheelMouseMove(object sender, MouseEventArgs e)
	{
		if (!_isDraggingWheel)
			return;

		UpdateFromMouse(e.GetPosition(WheelImage));

		e.Handled = true;
	}

	private void OnWheelMouseUp(object sender, MouseButtonEventArgs e)
	{
		_isDraggingWheel = false;
		WheelImage.ReleaseMouseCapture();
		e.Handled = true;
	}

	private void UpdateFromMouse(Point position)
	{
		var center = WheelSize / 2.0;
		var deltaX = position.X - center;
		var deltaY = position.Y - center;
		var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

		_hue = (Math.Atan2(deltaY, deltaX) * 180.0 / Math.PI + FullCircleDegrees) % FullCircleDegrees;
		_saturation = Math.Min(1, distance / center);

		UpdateIndicator();
		UpdatePreview();

		ColorChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnSquareMouseDown(object sender, MouseButtonEventArgs e)
	{
		_isDraggingSquare = true;
		SquareImage.CaptureMouse();
		UpdateFromSquareMouse(e.GetPosition(SquareImage));
		e.Handled = true;
	}

	private void OnSquareMouseMove(object sender, MouseEventArgs e)
	{
		if (!_isDraggingSquare)
			return;

		UpdateFromSquareMouse(e.GetPosition(SquareImage));

		e.Handled = true;
	}

	private void OnSquareMouseUp(object sender, MouseButtonEventArgs e)
	{
		_isDraggingSquare = false;
		SquareImage.ReleaseMouseCapture();
		e.Handled = true;
	}

	private void UpdateFromSquareMouse(Point position)
	{
		_saturation = Math.Min(1, Math.Max(0, position.X / WheelSize));
		_value = Math.Min(1, Math.Max(0, 1 - position.Y / WheelSize));

		UpdateSquareIndicator();
		UpdatePreview();

		ColorChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnHueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		_hue = e.NewValue;

		if (_mode == PickerMode.Square)
			SquareImage.Source = GenerateSquareBitmap(_hue);

		if (!_initialized)
			return;

		UpdatePreview();

		ColorChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnBrightnessChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		_value = e.NewValue;

		BrightnessOverlay.Opacity = 1 - _value;

		if (!_initialized)
			return;

		UpdatePreview();

		ColorChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnAlphaChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		_alphaPercent = e.NewValue;

		if (!_initialized)
			return;

		UpdateAlphaPercent();
		UpdatePreview();

		ColorChanged?.Invoke(this, EventArgs.Empty);
	}
}
