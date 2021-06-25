//  This file is part of YamlDotNet - A .NET library for YAML.
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Tokens;

namespace YamlDotNetEditor.Tests
{
	public static class Yaml
	{
		public static TextReader StreamFrom(string name)
		{
			var fromType = typeof(Yaml);
			var assembly = Assembly.GetAssembly(fromType);
			var stream = assembly.GetManifestResourceStream(name) ??
						 assembly.GetManifestResourceStream(fromType.Namespace + ".files." + name);
			return new StreamReader(stream);
		}

		public static string TemplatedOn<T>(this TextReader reader)
		{
			var text = reader.ReadToEnd();
			return text.TemplatedOn<T>();
		}

		public static string TemplatedOn<T>(this string text)
		{
			return Regex.Replace(text, @"{type}", match =>
				Uri.EscapeDataString(String.Format("{0}, {1}", typeof(T).FullName, typeof(T).Assembly.FullName)));
		}

		public static IParser ParserForEmptyContent()
		{
			return new Parser(new StringReader(string.Empty));
		}

		public static IParser ParserForResource(string name)
		{
			return new Parser(Yaml.StreamFrom(name));
		}

		public static IParser ParserForText(string yamlText)
		{
			return new Parser(ReaderForText(yamlText));
		}

		public static Scanner ScannerForResource(string name)
		{
			return new Scanner(Yaml.StreamFrom(name), skipComments: false);
		}

		public static Scanner ScannerForText(string yamlText)
		{
			return new Scanner(ReaderForText(yamlText), skipComments: false);
		}

		public static StringReader ReaderForText(string yamlText)
		{
			var lines = yamlText
				.Split('\n')
				.Select(l => l.TrimEnd('\r', '\n'))
				.SkipWhile(l => l.Trim(' ', '\t').Length == 0)
				.ToList();

			while (lines.Count > 0 && lines[lines.Count - 1].Trim(' ', '\t').Length == 0)
			{
				lines.RemoveAt(lines.Count - 1);
			}

			if (lines.Count > 0)
			{
				var indent = Regex.Match(lines[0], @"^(\t+)");
				if (!indent.Success)
				{
					throw new ArgumentException("Invalid indentation");
				}

				lines = lines
					.Select(l => l.Substring(indent.Groups[1].Length))
					.ToList();
			}

			var reader = new StringReader(string.Join("\n", lines.ToArray()));
			return reader;
		}

		public static IEnumerable<Token> AsEnumerable(this IScanner scanner)
		{
			while (scanner.MoveNext())
			{
				yield return scanner.Current;
			}
		}
	}
}