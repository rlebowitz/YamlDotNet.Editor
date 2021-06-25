﻿//  This file is part of YamlDotNet - A .NET library for YAML.
//  Copyright (c) Antoine Aubry and contributors

//  Permission is hereby granted, free of charge, to any person obtaining a copy of
//  this software and associated documentation files (the "Software"), to deal in
//  the Software without restriction, including without limitation the rights to
//  use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
//  of the Software, and to permit persons to whom the Software is furnished to do
//  so, subject to the following conditions:

//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.

//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE.

using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.Core.Tokens;
using YamlDotNet.Editor.Lib;

namespace YamlDotNetEditor
{
	internal class TextBufferParser
	{
		private class TokenEndPositionComparer : IComparer<Token>
		{
			private TokenEndPositionComparer()
			{
			}

			public int Compare(Token x, Token y)
			{
				return x.End.Index.CompareTo(y.End.Index);
			}

			public static readonly TokenEndPositionComparer Instance = new TokenEndPositionComparer();
		}

		private readonly ITextBuffer _textBuffer;
		private Scanner _scanner;
		private readonly List<Token> _bufferedTokens = new List<Token>();
		private int _errorCount;
		private int _currentTokensVersionNumber;

		public TextBufferParser(ITextBuffer textBuffer)
		{
			_textBuffer = textBuffer;
			_textBuffer.Changed += BufferChanged;
			Rescan();
		}

		public event EventHandler<SnapshotSpanEventArgs> ParseTreeChanged;

		protected virtual void OnParseTreeChanged(SnapshotSpanEventArgs e)
		{
			if (ParseTreeChanged != null)
			{
				ParseTreeChanged(this, e);
			}
		}

		private void BufferChanged(object sender, TextContentChangedEventArgs e)
		{
			// If this isn't the most up-to-date version of the buffer, then ignore it for now (we'll eventually get another change event). 
			if (e.After != _textBuffer.CurrentSnapshot)
			{
				return;
			}

			if (Rescan())
			{
				var start = e.Changes.Min(c => Math.Min(c.NewPosition, c.OldPosition));

				OnParseTreeChanged(new SnapshotSpanEventArgs(new SnapshotSpan(_textBuffer.CurrentSnapshot, new Span(start, _textBuffer.CurrentSnapshot.Length - start))));
			}
		}

		private bool Rescan()
		{
			var textSnapshot = _textBuffer.CurrentSnapshot;
			if (_scanner == null || _currentTokensVersionNumber != textSnapshot.Version.VersionNumber)
			{
				_scanner = new Scanner(new StringReader(textSnapshot.GetText()), skipComments: false);
				_currentTokensVersionNumber = textSnapshot.Version.VersionNumber;
				_errorCount = 0;
				_bufferedTokens.Clear();

				return true;
			}

			return false;
		}

		public IEnumerable<Token> GetAllTokens()
		{
			var currentIndex = 0;
			ReadWhile((initial, current) => current.Line == initial.Line);
			while (currentIndex < _bufferedTokens.Count)
			{
				yield return _bufferedTokens[currentIndex++];

				if (currentIndex == _bufferedTokens.Count)
				{
					ReadWhile((initial, current) => current.Line == initial.Line);
				}
			}
		}

		public IEnumerable<Token> GetTokensBetween(int start, int end)
		{
			ReadWhile((initial, current) => current.Index <= end);

			// Dummy token used to perform the binary search
			var markerToken = new StreamStart(default(Mark), new Mark(start, 1, 1));

			var startIndex = _bufferedTokens.BinarySearch(markerToken, TokenEndPositionComparer.Instance);
			if (startIndex < 0)
			{
				startIndex = ~startIndex;
			}

			for (var i = startIndex; i < _bufferedTokens.Count; ++i)
			{
				var token = _bufferedTokens[i];
				if (token.Start.Index > end)
				{
					yield break;
				}

				yield return token;
			}
		}

		private void ReadWhile(Func<Mark, Mark, bool> predicate)
		{
			var initialPosition = _bufferedTokens.Count > 0
				? _bufferedTokens[_bufferedTokens.Count - 1].End
				: new Mark();

			// Give up after 100 syntax errors
			for (; _errorCount < 100; ++_errorCount)
			{
				try
				{
					var currentPosition = initialPosition;
					while (predicate(initialPosition, currentPosition) && _scanner.MoveNext())
					{
						_bufferedTokens.Add(_scanner.Current);
						currentPosition = _scanner.Current.End;
					}
					break;
				}
				catch (SyntaxErrorException ex)
				{
					var errorEnd = ex.End.Index == ex.Start.Index
						? new Mark(ex.End.Index + 1, ex.End.Line, ex.End.Column)
						: ex.End;

					_bufferedTokens.Add(new SyntaxErrorToken(ex, ex.Start, errorEnd));
				}
			}
		}
	}
}
