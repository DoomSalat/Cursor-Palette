using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CursorPalette.Services;

public static class DropZoneService
{
	private const double DefaultCornerRadius = 4;
	private const double IndicatorStrokeThickness = 3;
	private const double IndicatorMargin = 2;

	public static Rectangle CreateDropIndicator(double cornerRadius = DefaultCornerRadius)
	{
		return new Rectangle
		{
			Stroke = (Brush)Application.Current.FindResource("Brush.Accent"),
			StrokeThickness = IndicatorStrokeThickness,
			StrokeDashArray = new DoubleCollection { 4, 2 },
			RadiusX = cornerRadius,
			RadiusY = cornerRadius,
			Margin = new Thickness(IndicatorMargin),
			IsHitTestVisible = false,
			Visibility = Visibility.Collapsed
		};
	}

	public static void ShowIndicator(Rectangle indicator) =>
		indicator.Visibility = Visibility.Visible;

	public static void HideIndicator(Rectangle indicator) =>
		indicator.Visibility = Visibility.Collapsed;

	public static void Attach(
		FrameworkElement target,
		Rectangle indicator,
		Func<DragEventArgs, bool> canDrop,
		Action<string[]> onDrop)
	{
		target.DragEnter += (_, e) =>
		{
			if (canDrop(e))
			{
				e.Effects = DragDropEffects.Copy;
				ShowIndicator(indicator);
			}
			else
			{
				e.Effects = DragDropEffects.None;
			}

			e.Handled = true;
		};

		target.DragOver += (_, e) =>
		{
			e.Effects = canDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
			e.Handled = true;
		};

		target.DragLeave += (_, e) =>
		{
			HideIndicator(indicator);
			e.Handled = true;
		};

		target.Drop += (_, e) =>
		{
			HideIndicator(indicator);

			if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
				return;

			onDrop(paths);
			e.Handled = true;
		};
	}

	public static void AttachManaged(
		FrameworkElement target,
		Func<DragEventArgs, bool> canDrop,
		Action<string[]> onDrop,
		Action? onDropCleanup = null)
	{
		target.DragOver += (_, e) =>
		{
			e.Effects = canDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
			e.Handled = true;
		};

		target.Drop += (_, e) =>
		{
			onDropCleanup?.Invoke();

			if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
				return;

			onDrop(paths);
		};
	}

	public static string? GetFirstFile(DragEventArgs e, Func<string, bool>? filter = null)
	{
		if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
			return null;

		return filter != null
			? paths.FirstOrDefault(p => File.Exists(p) && filter(p))
			: paths.FirstOrDefault(File.Exists);
	}

	public static bool HasFileDrop(DragEventArgs e) =>
		e.Data.GetDataPresent(DataFormats.FileDrop);
}
