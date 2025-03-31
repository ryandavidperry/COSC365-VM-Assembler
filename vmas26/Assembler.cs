using System;
using System.IO;
using System.Collections.Generic;

class CodeLine {
    public string RawLine { get; set; }
    public string[] Words { get; set; }

    public CodeLine(string line) {
        RawLine = line;
        Words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}

interface ICommand {
    int Compile();
}

// CGW: Prototype.
class Nop : ICommand {
    public int Compile() => 0x00000000;
}

// CGW: Prototype.
class Exit : ICommand {
    private int? param;
    public Exit(int? param) => this.param = param;
    public int Compile() => 0x11110000 | (param ?? 0);
}

// CGW: Prototype.
class Swap : ICommand {
    private int? param1, param2;
    public Swap(int? param1, int? param2) { this.param1 = param1; this.param2 = param2; }
    public int Compile() => 0x22220000 | ((param1 ?? 0) << 8) | (param2 ?? 0);
}

// CGW: Prototype.
static class Helper {

    // CGW: Attempts to convert a string to an integer. Returns null if conversion fails.
    public static int? ConvertToInt(string? input) {
        return int.TryParse(input, out int result) ? result : (int?)null;
    }
}

// CGW: Prototype.
namespace ProgramSpace {
    interface ICommand {
        int Compile();
    }
}

// CGW: Compiler class handles reading, parsing, and compiling assembly code.
class Compiler {
    public static void Main(string[] inputs) {

        // CGW: Ensures correct number of arguments are provided.
        if (inputs.Length != 2) {
            Console.WriteLine($"Usage: {AppDomain.CurrentDomain.FriendlyName} <source.asm> <output.v>");
            Environment.Exit(1);
        }

        string sourceFile = inputs[0];
        string resultFile = inputs[1];

        // CGW: Checks if the source file exists.
        if (!File.Exists(sourceFile)) {
            Console.WriteLine($"{sourceFile}: File not found");
            return;
        }

        Dictionary<string, int> labelDictionary = new Dictionary<string, int>();
        List<CodeLine> instructions = new List<CodeLine>();
        int programCounter = 0;

        // CGW: Reads and processes the source file line by line.
        using (StreamReader reader = new StreamReader(sourceFile)) {
            string? currentLine;

            while ((currentLine = reader.ReadLine()) != null) {

                // CGW: Remove comments and trim whitespace.
                currentLine = currentLine.Split('#')[0].Trim();
                if (string.IsNullOrWhiteSpace(currentLine)) continue;

                // CGW: Handle labels by storing them with their memory location.
                if (currentLine.EndsWith(':')) {
                    if (labelDictionary.ContainsKey(currentLine.TrimEnd(':'))) {
                        Console.WriteLine($"Error: Duplicate label detected at position {programCounter}");
                        Environment.Exit(1);
                    }
                    labelDictionary[currentLine.TrimEnd(':')] = programCounter;
                } else {

                    // CGW: Stores valid instruction lines and increments the program counter.
                    CodeLine instructionLine = new CodeLine(currentLine);
                    instructions.Add(instructionLine);
                    programCounter += 4; 
                }
            }
        }

        // CGW: Compiles instructions into machine code using the defined classes.
        List<ICommand> compiledInstructions = new List<ICommand>();
        for (int i = 0; i < instructions.Count; i++) {
            var words = instructions[i].Words;
            int? paramA = Helper.ConvertToInt(words.ElementAtOrDefault(1));
            int? paramB = Helper.ConvertToInt(words.ElementAtOrDefault(2));

            // CGW: Creates appropriate ICommand objects based on the instruction name.
            ICommand command = words[0].ToLower() switch {
                "exit" => new Exit(paramA),
                "swap" => new Swap(paramA, paramB),
                "nop" => new Nop(),
                _ => throw new Exception($"Unimplemented command {words[0]}")
            };

            compiledInstructions.Add(command);
        }

        // CGW: Writes compiled machine code to the output file.
        using (var outputStream = File.Open(resultFile, FileMode.Create)) {
            using (BinaryWriter writer = new BinaryWriter(outputStream)) {
                writer.Write(0xEFBE_ADDE); // CGW: Writes a magic header for identification.

                // CGW: Write each compiled instruction to the file.
                foreach (var command in compiledInstructions) {
                    writer.Write(command.Compile());
                }

                // CGW: Pads the file to ensure alignment to 4-byte boundary.
                int paddingAmount = (4 - (compiledInstructions.Count % 4)) % 4;
                for (int i = 0; i < paddingAmount; i++) {
                    writer.Write(new Nop().Compile());
                }
            }
        }
    }
}

