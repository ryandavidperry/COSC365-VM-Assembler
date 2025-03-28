using System;
using System.IO;
using System.Collections.Generic;

// CGW: Prototyping
class SourceLine {
    public string OriginalText { get; set; }
    public string[] Elements { get; set; }
    public SourceLine(string line) {
        OriginalText = line;
        Elements = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}

// CGW: Prototyping
static class InitialPass {
    public static (SourceLine[]?, bool) AnalyzeLine(string line) {

        // CGW: If a line ends with a colon, it's considered a label.
        if (line.EndsWith(":")) {
            return (new[] { new SourceLine(line) }, true);
        }
        return (new[] { new SourceLine(line) }, false);
    }
}

// CGW: Prototyping 
namespace Instruction {

    // CGW: Interface representing a generic instruction with a Generate method.
    public interface IInstruction {
        int Generate();
    }

    // CGW: Represents a NOP (No Operation) instruction.
    public class Nop : IInstruction {
        public int Generate() => 0x02000000;
    }

    // CGW: Represents an Exit instruction, optionally using a value parameter.
    public class Exit : IInstruction {
        private int? value;
        public Exit(int? value) => this.value = value;
        public int Generate() => unchecked((int)(0x00000000 | (value ?? 0)));
    }

    // CGW: Represents a Swap instruction with two optional parameters.
    public class Swap : IInstruction {
        private int? first, second;
        public Swap(int? first, int? second) {
            this.first = first;
            this.second = second;
        }
        public int Generate() => unchecked((int)(0x22220000 | ((first ?? 0) << 8) | (second ?? 0)));
    }

    // CGW: Represents a simple Input instruction.
    public class Input : IInstruction {
        public int Generate() => unchecked((int)0x33330000);
    }

    // CGW: Represents a StInput instruction with a parameter.
    public class StInput : IInstruction {
        private int? value;
        public StInput(int? value) => this.value = value;
        public int Generate() => unchecked((int)(0x44440000 | (value ?? 0)));
    }

    // CGW: Debug help.
    public class Debug : IInstruction {
        private int? value;
        public Debug(int? value) => this.value = value;
        public int Generate() => unchecked((int)(0x0F000000 | (value ?? 0)));
    }

    // CGW: Represents a Pop instruction, removing an item from the stack.
    public class Pop : IInstruction {
        private int? value;
        public Pop(int? value) => this.value = value;
        public int Generate() => unchecked((int)(0x66660000 | (value ?? 0)));
    }

    // CGW: Arithmetic operation instructions.
    public class Add : IInstruction { public int Generate() => unchecked((int)0x77770000); }
    public class Sub : IInstruction { public int Generate() => unchecked((int)0x88880000); }
    public class Mul : IInstruction { public int Generate() => unchecked((int)0x99990000); }
    public class Div : IInstruction { public int Generate() => unchecked((int)0xAAAA0000); }
    public class Rem : IInstruction { public int Generate() => unchecked((int)0xBBBB0000); }
    public class And : IInstruction { public int Generate() => unchecked((int)0xCCCC0000); }
}

// CGW: Utility class for safe conversion of strings to integers.
static class Converter {
    public static int? ToInteger(string? input) {
        return int.TryParse(input, out int result) ? result : (int?)null;
    }
}

// CGW: Main processor class for the assembler.
class Processor {
    public static void Main(string[] args) {
        
        // CGW: Validate command-line arguments.
        if (args.Length != 2) {
            Console.WriteLine($"Usage: {AppDomain.CurrentDomain.FriendlyName} <file.asm> <file.v>");
            Environment.Exit(1);
        }

        string inputPath = args[0];
        string outputPath = args[1];

        Dictionary<string, int> labelPositions = new Dictionary<string, int>();
        List<SourceLine> lines = new List<SourceLine>();

        // CGW: First Pass - Identify labels and calculate memory addresses.
        int programCounter = 0;
        using (StreamReader reader = new StreamReader(inputPath)) {
            string? line;
            while ((line = reader.ReadLine()) != null) {
                (SourceLine[]? operations, bool isLabel) = InitialPass.AnalyzeLine(line.Trim());
                if (operations == null || operations.Length == 0) continue;

                if (isLabel) {
                    string labelName = operations[0].OriginalText.TrimEnd(':');
                    if (labelPositions.ContainsKey(labelName)) {
                        Console.WriteLine($"Error: Duplicate label '{labelName}' detected at address {programCounter}");
                        Environment.Exit(1);
                    }
                    labelPositions[labelName] = programCounter;
                } else {
                    programCounter += operations.Length * 4;
                    lines.AddRange(operations);
                }
            }
        }

        // CGW: Second Pass - Generate machine code from instructions.
        List<Instruction.IInstruction> operationList = new List<Instruction.IInstruction>();
        for (int i = 0; i < lines.Count; i++) {
            var elements = lines[i].Elements;
            int? argOne = Converter.ToInteger(elements.ElementAtOrDefault(1));
            int? argTwo = Converter.ToInteger(elements.ElementAtOrDefault(2));

            // CGW: Resolve labels to memory addresses if applicable.
            if (elements.Length > 1 && labelPositions.ContainsKey(elements[1])) {
                argOne = labelPositions[elements[1]];
            }
            if (elements.Length > 2 && labelPositions.ContainsKey(elements[2])) {
                argTwo = labelPositions[elements[2]];
            }

            // CGW: Match instructions using a switch expression.
            Instruction.IInstruction op = elements[0].ToLower() switch {
                "exit" => new Instruction.Exit(argOne),
                "swap" => new Instruction.Swap(argOne, argTwo),
                "nop" => new Instruction.Nop(),
                "debug" => new Instruction.Debug(argOne),
                _ => throw new Exception($"Unimplemented operation {elements[0]}")
            };

            operationList.Add(op);
        }

        // RDP: Encode each operation in operation list to output path
        using (FileStream fs = new FileStream(outputPath, FileMode.Create,
                    FileAccess.Write)) {
            using (BinaryWriter bw = new BinaryWriter(fs)) {
                // Encode magic number
                bw.Write(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

                foreach (var op in operationList) {
                    int objectCode = op.Generate();
                    bw.Write(objectCode);
                }

                // Padd out a multiple of 4 instructions with nops
                int padding = (4 - operationList.Count % 4) % 4;
                for (int i = 0; i < padding; i++) {
                    bw.Write(0x02000000);
                }
            }
        }

        Console.WriteLine("Assembly completed successfully.");
    }
}

