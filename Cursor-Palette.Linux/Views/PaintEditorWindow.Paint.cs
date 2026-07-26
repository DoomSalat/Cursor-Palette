using Avalonia;
using Avalonia.Input;
using CursorPalette.Services;

namespace CursorPalette.Linux.Views;

public partial class PaintEditorWindow
{
	private bool _isPainting;
	private Point _paintLastPoint;
	private bool _hasLastStrokeEnd;
	private Point _lastStrokeEnd;
	private string? _previousToolForEyedropper;

	private bool IsPaintTool =>
		_currentTool == AppState.PaintEditorToolBrush ||
		_currentTool == AppState.PaintEditorToolEraser;

	private void PaintBegin(Point canvasPosition, KeyModifiers modifiers)
	{
		var pixelX = (int)Math.Floor(canvasPosition.X);
		var pixelY = (int)Math.Floor(canvasPosition.Y);

		if (pixelX < 0 || pixelX >= _canvasWidth || pixelY < 0 || pixelY >= _canvasHeight)
			return;

		PushHistory();

		if (modifiers.HasFlag(KeyModifiers.Shift) && _hasLastStrokeEnd)
		{
			PaintLine(_lastStrokeEnd, canvasPosition);
		}
		else
		{
			PaintPixel(pixelX, pixelY);
			_paintLastPoint = canvasPosition;
		}

		_isPainting = true;
		RenderAll();
	}

	private void PaintStrokeTo(Point canvasPosition, KeyModifiers modifiers)
	{
		if (!_isPainting)
			return;

		if (modifiers.HasFlag(KeyModifiers.Shift) && _hasLastStrokeEnd)
		{
			PaintLine(_lastStrokeEnd, canvasPosition);
			_paintLastPoint = canvasPosition;
		}
		else
		{
			PaintLine(_paintLastPoint, canvasPosition);
			_paintLastPoint = canvasPosition;
		}

		RenderAll();
	}

	private void PaintEnd()
	{
		if (!_isPainting)
			return;

		_isPainting = false;
		_hasLastStrokeEnd = true;
		_lastStrokeEnd = _paintLastPoint;
	}

	private void PaintLine(Point from, Point to)
	{
		var x0 = (int)Math.Round(from.X);
		var y0 = (int)Math.Round(from.Y);
		var x1 = (int)Math.Round(to.X);
		var y1 = (int)Math.Round(to.Y);

		var deltaX = Math.Abs(x1 - x0);
		var deltaY = Math.Abs(y1 - y0);
		var stepX = x0 < x1 ? 1 : -1;
		var stepY = y0 < y1 ? 1 : -1;
		var error = deltaX - deltaY;

		while (true)
		{
			PaintPixel(x0, y0);

			if (x0 == x1 && y0 == y1)
				break;

			var error2 = 2 * error;
			if (error2 > -deltaY) { error -= deltaY; x0 += stepX; }
			if (error2 < deltaX) { error += deltaX; y0 += stepY; }
		}
	}

	private void PaintPixel(int canvasX, int canvasY)
	{
		if (canvasX < 0 || canvasX >= _canvasWidth || canvasY < 0 || canvasY >= _canvasHeight)
			return;

		var spriteX = canvasX - _offsetX;
		var spriteY = canvasY - _offsetY;

		if (spriteX < 0 || spriteX >= _spriteWidth || spriteY < 0 || spriteY >= _spriteHeight)
		{
			if (_currentTool == AppState.PaintEditorToolEraser)
				return;

			var newSpriteWidth = Math.Max(_spriteWidth, spriteX + 1);
			var newSpriteHeight = Math.Max(_spriteHeight, spriteY + 1);

			if (spriteX < 0)
			{
				newSpriteWidth = _spriteWidth - spriteX;
				spriteX = 0;
			}
			if (spriteY < 0)
			{
				newSpriteHeight = _spriteHeight - spriteY;
				spriteY = 0;
			}

			newSpriteWidth = Math.Min(newSpriteWidth, MaxCanvasDimension);
			newSpriteHeight = Math.Min(newSpriteHeight, MaxCanvasDimension);

			var newSprite = new byte[newSpriteWidth * newSpriteHeight * BytesPerPixel];
			var blitOffsetX = spriteX - (canvasX - _offsetX);
			var blitOffsetY = spriteY - (canvasY - _offsetY);

			Blit(newSprite, newSpriteWidth, newSpriteHeight, _spriteBgra, _spriteWidth, _spriteHeight, blitOffsetX, blitOffsetY);

			_spriteBgra = newSprite;
			_spriteWidth = newSpriteWidth;
			_spriteHeight = newSpriteHeight;
			_offsetX = canvasX - spriteX;
			_offsetY = canvasY - spriteY;
			ClampOffset();
		}

		spriteX = canvasX - _offsetX;
		spriteY = canvasY - _offsetY;

		if (spriteX < 0 || spriteX >= _spriteWidth || spriteY < 0 || spriteY >= _spriteHeight)
			return;

		var pixelIndex = (spriteY * _spriteWidth + spriteX) * BytesPerPixel;

		if (_currentTool == AppState.PaintEditorToolEraser)
		{
			_spriteBgra[pixelIndex] = 0;
			_spriteBgra[pixelIndex + 1] = 0;
			_spriteBgra[pixelIndex + 2] = 0;
			_spriteBgra[pixelIndex + 3] = 0;
		}
		else
		{
			var color = _colorWheel.SelectedColor;
			_spriteBgra[pixelIndex] = color.B;
			_spriteBgra[pixelIndex + 1] = color.G;
			_spriteBgra[pixelIndex + 2] = color.R;
			_spriteBgra[pixelIndex + 3] = color.A;
		}
	}

	private void FloodFill(int startX, int startY)
	{
		if (startX < 0 || startX >= _canvasWidth || startY < 0 || startY >= _canvasHeight)
			return;

		var composed = Compose();
		var targetIndex = (startY * _canvasWidth + startX) * BytesPerPixel;
		var targetBlue = composed[targetIndex];
		var targetGreen = composed[targetIndex + 1];
		var targetRed = composed[targetIndex + 2];
		var targetAlpha = composed[targetIndex + 3];

		var color = _colorWheel.SelectedColor;
		var fillBlue = color.B;
		var fillGreen = color.G;
		var fillRed = color.R;
		var fillAlpha = color.A;

		if (targetBlue == fillBlue && targetGreen == fillGreen && targetRed == fillRed && targetAlpha == fillAlpha)
			return;

		var fillStack = new Stack<(int X, int Y)>();
		fillStack.Push((startX, startY));

		while (fillStack.Count > 0)
		{
			var (x, y) = fillStack.Pop();
			if (x < 0 || x >= _canvasWidth || y < 0 || y >= _canvasHeight)
				continue;

			var index = (y * _canvasWidth + x) * BytesPerPixel;
			if (composed[index] != targetBlue || composed[index + 1] != targetGreen ||
				composed[index + 2] != targetRed || composed[index + 3] != targetAlpha)
				continue;

			composed[index] = fillBlue;
			composed[index + 1] = fillGreen;
			composed[index + 2] = fillRed;
			composed[index + 3] = fillAlpha;

			fillStack.Push((x + 1, y));
			fillStack.Push((x - 1, y));
			fillStack.Push((x, y + 1));
			fillStack.Push((x, y - 1));
		}

		var bounds = FindOpaqueBounds(new CursorCanvasImage(_canvasWidth, _canvasHeight, 0, 0, composed));
		_spriteBgra = ExtractRegion(composed, _canvasWidth, bounds);
		_spriteWidth = bounds.Width;
		_spriteHeight = bounds.Height;
		_offsetX = bounds.X;
		_offsetY = bounds.Y;
		ClampOffset();
		_hasLastStrokeEnd = false;
	}
}
