using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using CursorPalette.Services;

namespace CursorPalette.Views;

public partial class PaintEditorWindow
{
	private bool _isPainting;
	private Point _lastPaintPosition;
	private bool _hasLastPaintPosition;

	private bool IsPaintTool =>
		_currentTool == AppState.PaintEditorToolBrush ||
		_currentTool == AppState.PaintEditorToolEraser;

	private void PaintBegin(Point position)
	{
		_isPainting = true;
		_hasLastPaintPosition = false;

		PaintStrokeTo(position);
	}

	private void PaintStrokeTo(Point position)
	{
		if (_hasLastPaintPosition)
			PaintLine(_lastPaintPosition, position);
		else
			PaintPixel((int)Math.Floor(position.X), (int)Math.Floor(position.Y));

		_lastPaintPosition = position;
		_hasLastPaintPosition = true;
		RenderAll();
	}

	private void PaintEnd()
	{
		_isPainting = false;
		_hasLastPaintPosition = false;
	}

	private void PaintLine(Point from, Point to)
	{
		var startX = (int)Math.Floor(from.X);
		var startY = (int)Math.Floor(from.Y);
		var endX = (int)Math.Floor(to.X);
		var endY = (int)Math.Floor(to.Y);

		var deltaX = Math.Abs(endX - startX);
		var deltaY = Math.Abs(endY - startY);
		var stepX = startX < endX ? 1 : -1;
		var stepY = startY < endY ? 1 : -1;
		var error = deltaX - deltaY;

		while (true)
		{
			PaintPixel(startX, startY);

			if (startX == endX && startY == endY)
				break;

			var error2 = 2 * error;

			if (error2 > -deltaY) { error -= deltaY; startX += stepX; }
			if (error2 < deltaX) { error += deltaX; startY += stepY; }
		}
	}

	private void PaintPixel(int canvasX, int canvasY)
	{
		var spriteX = canvasX - _offsetX;
		var spriteY = canvasY - _offsetY;

		if (spriteX < 0 || spriteX >= _spriteWidth || spriteY < 0 || spriteY >= _spriteHeight)
			return;

		var pixelIndex = (spriteY * _spriteWidth + spriteX) * BytesPerPixel;

		if (_currentTool == AppState.PaintEditorToolEraser)
		{
			_spriteBgra[pixelIndex + 3] = 0;
		}
		else
		{
			var color = ColorWheel.SelectedColor;
			_spriteBgra[pixelIndex] = color.B;
			_spriteBgra[pixelIndex + 1] = color.G;
			_spriteBgra[pixelIndex + 2] = color.R;
			_spriteBgra[pixelIndex + 3] = color.A;
		}
	}

	private void FloodFill(int canvasX, int canvasY)
	{
		var spriteX = canvasX - _offsetX;
		var spriteY = canvasY - _offsetY;

		if (spriteX < 0 || spriteX >= _spriteWidth || spriteY < 0 || spriteY >= _spriteHeight)
			return;

		var startIndex = (spriteY * _spriteWidth + spriteX) * BytesPerPixel;
		var targetB = _spriteBgra[startIndex];
		var targetG = _spriteBgra[startIndex + 1];
		var targetR = _spriteBgra[startIndex + 2];
		var targetA = _spriteBgra[startIndex + 3];

		var color = ColorWheel.SelectedColor;

		if (targetB == color.B && targetG == color.G && targetR == color.R && targetA == color.A)
			return;

		var stack = new Stack<(int X, int Y)>();
		stack.Push((spriteX, spriteY));

		while (stack.Count > 0)
		{
			var (x, y) = stack.Pop();

			if (x < 0 || x >= _spriteWidth || y < 0 || y >= _spriteHeight)
				continue;

			var index = (y * _spriteWidth + x) * BytesPerPixel;

			if (_spriteBgra[index] != targetB ||
				_spriteBgra[index + 1] != targetG ||
				_spriteBgra[index + 2] != targetR ||
				_spriteBgra[index + 3] != targetA)
				continue;

			_spriteBgra[index] = color.B;
			_spriteBgra[index + 1] = color.G;
			_spriteBgra[index + 2] = color.R;
			_spriteBgra[index + 3] = color.A;

			stack.Push((x + 1, y));
			stack.Push((x - 1, y));
			stack.Push((x, y + 1));
			stack.Push((x, y - 1));
		}
	}
}
