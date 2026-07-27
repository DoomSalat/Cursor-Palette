using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PaintEditorWindow
{
	private readonly record struct PixelRect(int X, int Y, int Width, int Height);

	private static PixelRect FindOpaqueBounds(CursorCanvasImage source)
	{
		var minX = int.MaxValue;
		var minY = int.MaxValue;
		var maxX = int.MinValue;
		var maxY = int.MinValue;
		var stride = source.Width * BytesPerPixel;

		for (var y = 0; y < source.Height; y++)
		{
			for (var x = 0; x < source.Width; x++)
			{
				if (source.Bgra[y * stride + x * BytesPerPixel + 3] == 0)
					continue;

				if (x < minX) minX = x;
				if (x > maxX) maxX = x;
				if (y < minY) minY = y;
				if (y > maxY) maxY = y;
			}
		}

		return maxX < minX
			? new PixelRect(0, 0, source.Width, source.Height)
			: new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
	}

	private static byte[] ExtractRegion(byte[] source, int sourceWidth, PixelRect region)
	{
		var stride = sourceWidth * BytesPerPixel;
		var regionStride = region.Width * BytesPerPixel;
		var result = new byte[regionStride * region.Height];

		for (var y = 0; y < region.Height; y++)
			Array.Copy(source, (region.Y + y) * stride + region.X * BytesPerPixel, result, y * regionStride, regionStride);

		return result;
	}

	private static void Blit(byte[] destination, int destinationWidth, int destinationHeight, byte[] source, int sourceWidth, int sourceHeight, int offsetX, int offsetY)
	{
		for (var y = 0; y < sourceHeight; y++)
		{
			var destinationY = y + offsetY;

			if (destinationY < 0 || destinationY >= destinationHeight)
				continue;

			for (var x = 0; x < sourceWidth; x++)
			{
				var destinationX = x + offsetX;

				if (destinationX < 0 || destinationX >= destinationWidth)
					continue;

				var sourceIndex = (y * sourceWidth + x) * BytesPerPixel;
				var destinationIndex = (destinationY * destinationWidth + destinationX) * BytesPerPixel;

				destination[destinationIndex] = source[sourceIndex];
				destination[destinationIndex + 1] = source[sourceIndex + 1];
				destination[destinationIndex + 2] = source[sourceIndex + 2];
				destination[destinationIndex + 3] = source[sourceIndex + 3];
			}
		}
	}

	private byte[] Compose()
	{
		var buffer = new byte[_canvasWidth * _canvasHeight * BytesPerPixel];
		Blit(buffer, _canvasWidth, _canvasHeight, _spriteBgra, _spriteWidth, _spriteHeight, _offsetX, _offsetY);

		return buffer;
	}

	private static byte[] ComposeFrame(TimelineFrame frame, int canvasSize)
	{
		var buffer = new byte[canvasSize * canvasSize * BytesPerPixel];
		Blit(buffer, canvasSize, canvasSize, frame.SpriteBgra, frame.SpriteWidth, frame.SpriteHeight, frame.OffsetX, frame.OffsetY);

		return buffer;
	}

	private (int Min, int Max) HorizontalRange() =>
		(Math.Min(0, _canvasWidth - _spriteWidth), Math.Max(0, _canvasWidth - _spriteWidth));

	private (int Min, int Max) VerticalRange() =>
		(Math.Min(0, _canvasHeight - _spriteHeight), Math.Max(0, _canvasHeight - _spriteHeight));

	private void ClampOffset()
	{
		var (minX, maxX) = HorizontalRange();
		var (minY, maxY) = VerticalRange();
		_offsetX = Math.Clamp(_offsetX, minX, maxX);
		_offsetY = Math.Clamp(_offsetY, minY, maxY);
	}

	private static bool IsFullyTransparent(byte[] bgra)
	{
		for (var i = 3; i < bgra.Length; i += BytesPerPixel)
		{
			if (bgra[i] != 0)
				return false;
		}

		return true;
	}
}
