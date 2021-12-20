﻿using System.Reflection;
using System.Text;
using Cesium.CodeGen;
using Cesium.Compiler;
using Cesium.Parser;
using CommandLine;
using Mono.Cecil;
using Yoakke.C.Syntax;
using Yoakke.Streams;

Console.WriteLine($"Cesium v{Assembly.GetExecutingAssembly().GetName().Version}");

return Parser.Default.ParseArguments<Arguments>(args).MapResult(args =>
    {
        using var input = new FileStream(args.InputFilePath, FileMode.Open);
        using var reader = new StreamReader(input, Encoding.UTF8);

        Console.WriteLine($"Processing input file {args.InputFilePath}.");
        var lexer = new CLexer(reader);
        var parser = new CParser(lexer);
        var translationUnit = parser.ParseTranslationUnit().Ok.Value;

        if (parser.TokenStream.Peek().Kind != CTokenType.End)
            throw new Exception($"Excessive output after the end of a translation unit at {lexer.Position}.");

        var assemblyName = Path.GetFileNameWithoutExtension(args.OutputFilePath);
        var moduleKind = Path.GetExtension(args.OutputFilePath).ToLowerInvariant() switch
        {
            ".exe" => ModuleKind.Console,
            ".dll" => ModuleKind.Dll,
            var o => throw new Exception($"Unknown file extension: {o}.")
        };

        Console.WriteLine($"Generating assembly {args.OutputFilePath}.");
        var assembly = Generator.GenerateAssembly(
            translationUnit,
            new AssemblyNameDefinition(assemblyName, new Version()),
            moduleKind);
        assembly.Write(args.OutputFilePath);

        return 0;
    },
    _ => -1);
