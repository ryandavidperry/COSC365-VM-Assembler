using System;
using System.IO;
using System.Collections.Generic;

// CGW: Prototyping
class SourceLine {
    public string OriginalText { get; set; }
    public string[] Elements { get; set; }
    public int LineNumber { get; set; }
    public int ProgramCounter { get; set; }

    public SourceLine(string line, int lineNumber, int programCounter) {
        OriginalText = line;
        Elements = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        LineNumber = lineNumber;
        ProgramCounter = programCounter;
    }
}

// CGW: Prototyping
static class InitialPass {
    public static (SourceLine[]?, bool) AnalyzeLine(string line, int lineNumber, int programCounter) {
        // RDP: Handle empty lines and comments

        // Trim whitespace
        line = line.Trim();

        // Ignore full-line comments
        if (line.StartsWith('#')) return (null, false);

        // Remove inline comments
        int commentIndex = line.IndexOf('#');
        if (commentIndex != -1) {
            line = line.Substring(0, commentIndex).Trim();
        }

        // Ignore empty lines
        if (string.IsNullOrEmpty(line)) return (null, false);

        // CGW: If a line ends with a colon, it's considered a label.
        if (line.EndsWith(":")) {
            return (new[] { new SourceLine(line, lineNumber, programCounter) }, true);
        }
        return (new[] { new SourceLine(line, lineNumber, programCounter) }, false);
    }
}

public class Encoder {

    // RDP: PC-Relative Offset Helper
    public static int PCRelative(int? target, int pc, int mask, uint opcode) {

        // Calculate the PC-Relative Offset
        int pcRelativeOffset = target.GetValueOrDefault() - pc;

        // Apply the mask to align the offset
        int offset = (int)(pcRelativeOffset & mask);

        // Combine the base opcode with the offset
        return unchecked((int)((int)opcode | offset));
    }
}

// CGW: Prototyping 
namespace Instruction {

    // CGW: Interface representing a generic instruction with a Encode method.
    public interface IInstruction {
        int Encode();
    }

    // CGW: Represents a NOP (No Operation) instruction.
    public class Nop : IInstruction {
        public int Encode() => 0x02000000;
    }

    // CGW: Represents an Exit instruction, optionally using a value parameter.
    public class Exit : IInstruction {
        private int? value;
        public Exit(int? value) => this.value = value;
        public int Encode() => unchecked((int)(0x00000000 | (value ?? 0)));
    }

    // CGW: Represents a Swap instruction with two optional parameters.
    public class Swap : IInstruction {
        private int? first, second;
        public Swap(int? first, int? second) {
            this.first = first;
            this.second = second;
        }
        public int Encode() => unchecked((int)(0x01000000 | ((first ?? 4) << 12) | (second ?? 0)));
    }

    // CGW: Represents a simple Input instruction.
    public class Input : IInstruction {
        public int Encode() => unchecked((int)0x04000000);
    }

    // CGW: Represents a StInput instruction with a parameter.
    public class StInput : IInstruction {
        private int? value;
        public StInput(int? value) => this.value = value;
        public int Encode() => unchecked((int)(0x05000000 | (value ?? 0xFFFFFF)));
    }

    // CGW: Debug help.
    public class Debug : IInstruction {
        private int? value;
        public Debug(int? value) => this.value = value;
        public int Encode() => unchecked((int)(0x0F000000 | (value ?? 0)));
    }

    // CGW: Represents a Pop instruction, removing an item from the stack.
    public class Pop : IInstruction {
        private int? value;
        public Pop(int? value) => this.value = value;
        public int Encode() => unchecked((int)(0x10000000 | (value ?? 4))); 
    }

    // CGW: Arithmetic operation instructions.
    public class Add : IInstruction { public int Encode() => unchecked((int)0x20000000); }
    public class Sub : IInstruction { public int Encode() => unchecked((int)0x21000000); }
    public class Mul : IInstruction { public int Encode() => unchecked((int)0x22000000); }
    public class Div : IInstruction { public int Encode() => unchecked((int)0x23000000); }
    public class Rem : IInstruction { public int Encode() => unchecked((int)0x24000000); }
    public class And : IInstruction { public int Encode() => unchecked((int)0x25000000); }

    // RDP: Arithmetic operations instructions
    public class Or : IInstruction { public int Encode() => unchecked((int)0x26000000); }
    public class Xor : IInstruction { public int Encode() => unchecked((int)0x27000000); }
    public class Lsl : IInstruction { public int Encode() => unchecked((int)0x28000000); }
    public class Lsr : IInstruction { public int Encode() => unchecked((int)0x29000000); }
    public class Asr : IInstruction { public int Encode() => unchecked((int)0x2B000000); }

    // RDP: Unary arithmetic instructions
    public class Neg : IInstruction { public int Encode() => unchecked((int)0x30000000); }
    public class Not : IInstruction { public int Encode() => unchecked((int)0x31000000); }

    // RDP: Encodes dup instruction with optional parameter
    public class Dup : IInstruction {
        private readonly int? _offset;
        public Dup(int? offset) {
            _offset = offset & ~3;
        }
        public int Encode() => unchecked((int)((0b1100 << 28) | (_offset ?? 0)));
    }

    // RDP: Encodes stprint instruction with optional parameter.
    public class StPrint : IInstruction {
        private int? value;
        public StPrint(int? value) => this.value = value;
        public int Encode() => unchecked((int)(0x40000000 | (value ?? 0)));
    }

    // RDP: Encodes call instruction with pc-relative offset.
    // The argument is checked before passing it to the instruction,
    // therefore the value is not acutally optional as it appears here.
    public class Call : IInstruction {
        private int? target;
        private int pc;

        public Call(int? target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0xFFFFFFC, 0x50000000);
        }
    }

    // RDP: Encodes return instruction with optional parameter.
    public class Return : IInstruction {
        private int? value;
        public Return(int? value) => this.value = value;
        public int Encode() => unchecked((int)(0x60000000 | (value ?? 0)));
    }

    // RDP: Encodes goto instruction
    public class Goto : IInstruction {
        private int? target;
        private int pc;

        public Goto(int? target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x0FFFFFFF, 0x70000000);
        }
    }

    // CGW: Push instruction prototype, needs testing.
    public class Push : IInstruction {
        private readonly int _operand;
        public Push(int? inputValue = 0) {
            _operand = inputValue ?? 0;
        }
        public int Encode() {
            return (0b1111 << 28) | (_operand & 0x0FFFFFFF);
        }
    }

    // RDP: Binary If instructions
    public class Equals : IInstruction {
        private int? target;
        private int pc;

        public Equals(int? target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x80000000);
        }
    }

    public class NotEquals : IInstruction {
        private int? target;
        private int pc;

        public NotEquals(int? target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x82000000);
        }
    }

    public class LessThan : IInstruction {
        private int? target;
        private int pc;

        public LessThan(int? target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x84000000);
        }
    }

    public class GreaterThan : IInstruction {
        private int? target;
        private int pc;

        public GreaterThan(int? target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x86000000);
        }
    }

    public class LessEquals : IInstruction {
        private int? target;
        private int pc;

        public LessEquals(int? target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x88000000);
        }
    }

    public class GreaterEquals : IInstruction {
        private int? target;
        private int pc;

        public GreaterEquals(int? target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x8A000000);
        }
    }

    // RDP: Unary If instructions
    public class EqualsZero : IInstruction {
        private int? target;
        private int pc;

        public EqualsZero(int? target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x90000000);
        }
    }
    
    public class NotEqualsZero : IInstruction {
        private int? target;
        private int pc;

        public NotEqualsZero(int? target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x92000000);
        }
    }

    public class LessThanZero : IInstruction {
        private int? target;
        private int pc;

        public LessThanZero(int? target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x94000000);
        }
    }

    public class GreaterThanEqualZero : IInstruction {
        private int? target;
        private int pc;

        public GreaterThanEqualZero(int? target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x96000000);
        }
    }

}

// CGW: Utility class for safe conversion of strings to integers.
// RDP: Supports hexadecimal values
static class Converter {
    public static int? ToInteger(string? input) {
        if (string.IsNullOrEmpty(input)) return (int?)null;

        if (input.StartsWith("0x")) {
            if (int.TryParse(input.Substring(2),
                        System.Globalization.NumberStyles.HexNumber, null, out
                        int result)) {
                return result;
            }
        } else if (int.TryParse(input, out int result)) {
            return result;
        }
        return (int?)null;
    }
}

// CGW: Main processor class for the assembler.
class Processor {

    // RDP: Validates condition
    private static bool checkArgs(bool cond, string ?msg, int lineNumber) {
        if (!cond) {
            err(msg, lineNumber);
        }
        return true;
    }

    // RDP: Prints an error message and stops program
    private static void err(string ?msg, int lineNumber) {
        if (!string.IsNullOrEmpty(msg)) {
            Console.WriteLine($"{lineNumber}: {msg}");
        }
        Environment.Exit(1);
    }

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
            int lineNumber = 0;
            while ((line = reader.ReadLine()) != null) {
                lineNumber++;

                (SourceLine[]? operations, bool isLabel) =
                    InitialPass.AnalyzeLine(line.Trim(), lineNumber, programCounter);

                if (operations == null || operations.Length == 0) continue;

                if (isLabel) {
                    string labelName = operations[0].OriginalText.TrimEnd(':');
                    if (labelPositions.ContainsKey(labelName)) {
                        Console.WriteLine($"Error: Duplicate label '{labelName}'" +
                                          $" detected at address {programCounter}");
                        Environment.Exit(1);
                    }
                    labelPositions[labelName] = programCounter;
                } else {
                    programCounter += operations.Length * 4;
                    lines.AddRange(operations);
                }
            }
        }

        // CGW: Second Pass - Encode machine code from instructions.
        List<Instruction.IInstruction> operationList = new List<Instruction.IInstruction>();
        for (int i = 0; i < lines.Count; i++) {
            int lineNumber = lines[i].LineNumber;
            int pc = lines[i].ProgramCounter;
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
            // RDP: Handle argument checking.
            Instruction.IInstruction op = elements[0].ToLower() switch {
                "exit" => new Instruction.Exit(argOne),
                "swap" => new Instruction.Swap(argOne, argTwo),
                "nop" => new Instruction.Nop(),
                "debug" => new Instruction.Debug(argOne),
                "pop" => new Instruction.Pop(checkArgs(argOne % 4 == 0 ||
                        !argOne.HasValue,  "offset to pop() is not a multiple of 4.",
                        lineNumber) ? argOne : null),
                "input" => new Instruction.Input(),
                "stinput" => new Instruction.StInput(argOne),
                "add" => new Instruction.Add(),
                "sub" => new Instruction.Sub(),
                "mul" => new Instruction.Mul(),
                "div" => new Instruction.Div(),
                "rem" => new Instruction.Rem(),
                "and" => new Instruction.And(),
                "or" => new Instruction.Or(),
                "xor" => new Instruction.Xor(),
                "lsl" => new Instruction.Lsl(),
                "lsr" => new Instruction.Lsr(),
                "asr" => new Instruction.Asr(),
                "neg" => new Instruction.Neg(),
                "not" => new Instruction.Not(),
                "push" => new Instruction.Push(argOne),
                "dup" => new Instruction.Dup(checkArgs(argOne % 4 == 0 ||
                        !argOne.HasValue, "offsets to dup must be multiples of 4.",
                        lineNumber) ? argOne : null),
                "stprint" => new Instruction.StPrint(argOne),
                "call" => new Instruction.Call(
                        checkArgs(elements.Length > 1, 
                            "no label given for call statement.", lineNumber) && 
                        checkArgs(labelPositions.ContainsKey(elements[1]), 
                            "Invalid label: The given key '" + elements[1] + 
                            "' was not present in the dictionary.", lineNumber) 
                        ? argOne : null, pc),
                "return" => new Instruction.Return(checkArgs(argOne % 4 == 0 || 
                        !argOne.HasValue, "offset to return is not a multiple of 4.", 
                        lineNumber) ? argOne : null),
                "goto" => new Instruction.Goto(
                        checkArgs(elements.Length > 1, 
                            "no label given for goto statement.", lineNumber) && 
                        checkArgs(labelPositions.ContainsKey(elements[1]), 
                            "Invalid label: The given key '" + elements[1] + 
                            "' was not present in the dictionary.", lineNumber) 
                        ? argOne : null, pc),
                "ifeq" => new Instruction.Equals(
                        checkArgs(elements.Length > 1, 
                            "no label given for ifeq statement.", lineNumber) && 
                        checkArgs(labelPositions.ContainsKey(elements[1]), 
                            "Invalid label: The given key '" + elements[1] + 
                            "' was not present in the dictionary.", lineNumber) 
                        ? argOne : null, pc),
                "ifne" => new Instruction.NotEquals(
                        checkArgs(elements.Length > 1, 
                            "no label given for ifne statement.", lineNumber) && 
                        checkArgs(labelPositions.ContainsKey(elements[1]), 
                            "Invalid label: The given key '" + elements[1] + 
                            "' was not present in the dictionary.", lineNumber) 
                        ? argOne : null, pc),
                "iflt" => new Instruction.LessThan(
                        checkArgs(elements.Length > 1, 
                            "no label given for iflt statement.", lineNumber) && 
                        checkArgs(labelPositions.ContainsKey(elements[1]), 
                            "Invalid label: The given key '" + elements[1] + 
                            "' was not present in the dictionary.", lineNumber) 
                        ? argOne : null, pc),
                "ifgt" => new Instruction.GreaterThan(
                        checkArgs(elements.Length > 1, 
                            "no label given for ifgt statement.", lineNumber) && 
                        checkArgs(labelPositions.ContainsKey(elements[1]), 
                            "Invalid label: The given key '" + elements[1] + 
                            "' was not present in the dictionary.", lineNumber) 
                        ? argOne : null, pc),
                "ifle" => new Instruction.LessEquals(
                        checkArgs(elements.Length > 1, 
                            "no label given for ifle statement.", lineNumber) && 
                        checkArgs(labelPositions.ContainsKey(elements[1]), 
                            "Invalid label: The given key '" + elements[1] + 
                            "' was not present in the dictionary.", lineNumber) 
                        ? argOne : null, pc),
                "ifge" => new Instruction.GreaterEquals(
                        checkArgs(elements.Length > 1, 
                            "no label given for ifge statement.", lineNumber) && 
                        checkArgs(labelPositions.ContainsKey(elements[1]), 
                            "Invalid label: The given key '" + elements[1] + 
                            "' was not present in the dictionary.", lineNumber) 
                        ? argOne : null, pc),
                "ifez" => new Instruction.EqualsZero(
                        checkArgs(elements.Length > 1, 
                            "no label given for ifez statement.", lineNumber) && 
                        checkArgs(labelPositions.ContainsKey(elements[1]), 
                            "Invalid label: The given key '" + elements[1] + 
                            "' was not present in the dictionary.", lineNumber) 
                        ? argOne : null, pc),
                "ifnz" => new Instruction.NotEqualsZero(
                        checkArgs(elements.Length > 1, 
                            "no label given for ifnz statement.", lineNumber) && 
                        checkArgs(labelPositions.ContainsKey(elements[1]), 
                            "Invalid label: The given key '" + elements[1] + 
                            "' was not present in the dictionary.", lineNumber) 
                        ? argOne : null, pc),
                "ifmi" => new Instruction.LessThanZero(
                        checkArgs(elements.Length > 1, 
                            "no label given for ifmi statement.", lineNumber) && 
                        checkArgs(labelPositions.ContainsKey(elements[1]), 
                            "Invalid label: The given key '" + elements[1] + 
                            "' was not present in the dictionary.", lineNumber) 
                        ? argOne : null, pc),
                "ifpl" => new Instruction.GreaterThanEqualZero(
                        checkArgs(elements.Length > 1, 
                            "no label given for ifpl statement.", lineNumber) && 
                        checkArgs(labelPositions.ContainsKey(elements[1]), 
                            "Invalid label: The given key '" + elements[1] + 
                            "' was not present in the dictionary.", lineNumber) 
                        ? argOne : null, pc),
                _ => throw new Exception($"Unimplemented operation {elements[0]}")
            };

            operationList.Add(op);
        }

        // RDP: Encode each operation in operation list to output path
        using (BinaryWriter bw = new BinaryWriter(File.Open(outputPath,
                        FileMode.Create))) {
            // Encode magic number
            bw.Write(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

            foreach (var op in operationList) {
                int objectCode = op.Encode();
                bw.Write(objectCode);
            }

            // Padd out a multiple of 4 instructions with nops
            int padding = (4 - operationList.Count % 4) % 4;
            for (int i = 0; i < padding; i++) {
                bw.Write(0x02000000);
            }
        }

        Console.WriteLine("Assembly completed successfully.");
    }
}

