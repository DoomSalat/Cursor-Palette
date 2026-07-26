using CursorPalette.Services;
using System.Runtime.InteropServices;

namespace CursorPalette.Linux.Views;

public partial class PaintEditorWindow
{
	private (int Min, int Max) HorizontalRange()
	{
		var rangeMin = Math.Min(0, _canvasWidth - _spriteWidth);
		var rangeMax = Math.Max(0, _canvasWidth - _spriteWidth);
		return (Math.Min(rangeMin, rangeMax), Math.Max(rangeMin, rangeMax));
	}

	private (int Min, int Max) VerticalRange()
	{
		var rangeMin = Math.Min(0, _canvasHeight - _spriteHeight);
		var rangeMax = Math.Max(0, _canvasHeight - _spriteHeight);
		return (Math.Min(rangeMin, rangeMax), Math.Max(rangeMin, rangeMax));
	}

	private void ClampOffset()
	{
		var (minX, maxX) = HorizontalRange();
		var (minY, maxY) = VerticalRange();
		_offsetX = Math.Clamp(_offsetX, minX, maxX);
		_offsetY = Math.Clamp(_offsetY, minY, maxY);
	}

	private byte[] Compose()
	{
		var buffer = new byte[_canvasWidth * _canvasHeight * BytesPerPixel];
		Blit(buffer, _canvasWidth, _canvasHeight, _spriteBgra, _spriteWidth, _spriteHeight, _offsetX, _offsetY);
		return buffer;
	}

	private static void Blit(byte[] dest, int destWidth, int destHeight, byte[] src, int srcWidth, int srcHeight, int offsetX, int offsetY)
	{
		for (var srcY = 0; srcY < srcHeight; srcY++)
		{
			var destY = srcY + offsetY;
			if (destY < 0 || destY >= destHeight)
				continue;
			for (var srcX = 0; srcX < srcWidth; srcX++)
			{
				var destX = srcX + offsetX;
				if (destX < 0 || destX >= destWidth)
					continue;
				var srcIndex = (srcY * srcWidth + srcX) * BytesPerPixel;
				var destIndex = (destY * destWidth + destX) * BytesPerPixel;
				var alpha = src[srcIndex + 3];
				if (alpha == 0)
					continue;
				if (alpha == 255)
				{
					dest[destIndex] = src[srcIndex];
					dest[destIndex + 1] = src[srcIndex + 1];
					dest[destIndex + 2] = src[srcIndex + 2];
					dest[destIndex + 3] = 255;
				}
				else
				{
					var srcAlpha = alpha / 255.0;
					var destAlpha = dest[destIndex + 3] / 255.0;
					var outAlpha = srcAlpha + destAlpha * (1 - srcAlpha);
					if (outAlpha <= 0)
						continue;
					dest[destIndex] = (byte)((src[srcIndex] * srcAlpha + dest[destIndex] * destAlpha * (1 - srcAlpha)) / outAlpha);
					dest[destIndex + 1] = (byte)((src[srcIndex + 1] * srcAlpha + dest[destIndex + 1] * destAlpha * (1 - srcAlpha)) / outAlpha);
					dest[destIndex + 2] = (byte)((src[srcIndex + 2] * srcAlpha + dest[destIndex + 2] * destAlpha * (1 - srcAlpha)) / outAlpha);
					dest[destIndex + 3] = (byte)(outAlpha * 255);
				}
			}
		}
	}

	private static void AlphaComposite(byte[] dest, int destWidth, int destHeight, byte[] src, int srcWidth, int srcHeight, int offsetX, int offsetY)
	{
		for (var srcY = 0; srcY < srcHeight; srcY++)
		{
			var destY = srcY + offsetY;
			if (destY < 0 || destY >= destHeight)
				continue;
			for (var srcX = 0; srcX < srcWidth; srcX++)
			{
				var destX = srcX + offsetX;
				if (destX < 0 || destX >= destWidth)
					continue;
				var srcIndex = (srcY * srcWidth + srcX) * BytesPerPixel;
				var destIndex = (destY * destWidth + destX) * BytesPerPixel;
				var srcAlphaByte = src[srcIndex + 3];
				if (srcAlphaByte == 0)
					continue;
				if (srcAlphaByte == 255)
				{
					dest[destIndex] = src[srcIndex];
					dest[destIndex + 1] = src[srcIndex + 1];
					dest[destIndex + 2] = src[srcIndex + 2];
					dest[destIndex + 3] = 255;
				}
				else
				{
					var srcAlpha = srcAlphaByte / 255.0;
					var destAlpha = dest[destIndex + 3] / 255.0;
					var outAlpha = srcAlpha + destAlpha * (1 - srcAlpha);
					if (outAlpha <= 0)
						continue;
					dest[destIndex] = (byte)((src[srcIndex] * srcAlpha + dest[destIndex] * destAlpha * (1 - srcAlpha)) / outAlpha);
					dest[destIndex + 1] = (byte)((src[srcIndex + 1] * srcAlpha + dest[destIndex + 1] * destAlpha * (1 - srcAlpha)) / outAlpha);
					dest[destIndex + 2] = (byte)((src[srcIndex + 2] * srcAlpha + dest[destIndex + 2] * destAlpha * (1 - srcAlpha)) / outAlpha);
					dest[destIndex + 3] = (byte)(outAlpha * 255);
				}
			}
		}
	}

	private static (int X, int Y, int Width, int Height) FindOpaqueBounds(CursorCanvasImage image)
	{
		var imageWidth = image.Width;
		var imageHeight = image.Height;
		var bgra = image.Bgra;
		var minX = imageWidth;
		var minY = imageHeight;
		var maxX = -1;
		var maxY = -1;

		for (var y = 0; y < imageHeight; y++)
		{
			for (var x = 0; x < imageWidth; x++)
			{
				if (bgra[(y * imageWidth + x) * BytesPerPixel + 3] != 0)
				{
					if (x < minX) minX = x;
					if (x > maxX) maxX = x;
					if (y < minY) minY = y;
					if (y > maxY) maxY = y;
				}
			}
		}

		if (maxX < 0)
			return (0, 0, 1, 1);

		return (minX, minY, maxX - minX + 1, maxY - minY + 1);
	}

	private static byte[] ExtractRegion(byte[] src, int srcWidth, int regionX, int regionY, int width, int height)
	{
		var result = new byte[width * height * BytesPerPixel];
		for (var destY = 0; destY < height; destY++)
		{
			var srcY = regionY + destY;
			if (srcY < 0)
				continue;
			for (var destX = 0; destX < width; destX++)
			{
				var srcX = regionX + destX;
				if (srcX < 0)
					continue;
				var srcIndex = (srcY * srcWidth + srcX) * BytesPerPixel;
				var destIndex = (destY * width + destX) * BytesPerPixel;
				result[destIndex] = src[srcIndex];
				result[destIndex + 1] = src[srcIndex + 1];
				result[destIndex + 2] = src[srcIndex + 2];
				result[destIndex + 3] = src[srcIndex + 3];
			}
		}
		return result;
	}

	private static byte[] ExtractRegion(byte[] src, int srcWidth, (int X, int Y, int Width, int Height) bounds) =>
		ExtractRegion(src, srcWidth, bounds.X, bounds.Y, bounds.Width, bounds.Height);

	private static bool IsFullyTransparent(byte[] bgra)
	{
		for (var i = 3; i < bgra.Length; i += BytesPerPixel)
		{
			if (bgra[i] != 0)
				return false;
		}
		return true;
	}

	private static void ClearRect(byte[] buffer, int width, int height, int rectX, int rectY, int rectWidth, int rectHeight)
	{
		for (var y = Math.Max(rectY, 0); y < rectY + rectHeight && y < height; y++)
		{
			for (var x = Math.Max(rectX, 0); x < rectX + rectWidth && x < width; x++)
			{
				var index = (y * width + x) * BytesPerPixel;
				buffer[index] = 0;
				buffer[index + 1] = 0;
				buffer[index + 2] = 0;
				buffer[index + 3] = 0;
			}
		}
	}
}
