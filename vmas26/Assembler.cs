using System;
using System.Linq;
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
    public static (SourceLine[], bool) AnalyzeLine(string line, int
            lineNumber, int programCounter) {
        // RDP: Handle empty lines and comments

        // Trim whitespace
        line = line.Trim();

        // Ignore full-line comments
        if (line.StartsWith('#')) return (new SourceLine[0], false);

        // Remove inline comments
        int commentIndex = line.IndexOf('#');
        if (commentIndex != -1) {
            line = line.Substring(0, commentIndex).Trim();
        }

        // Ignore empty lines
        if (string.IsNullOrEmpty(line)) return (new SourceLine[0], false);

        string[] elements = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // CGW: If a line ends with a colon, it's considered a label.
        if (line.EndsWith(":")) {
            return (new[] { new SourceLine(line, lineNumber, programCounter) }, true);
        }

        // RDP: Handle stpush instruction
        if (elements[0] == "stpush") {

            // Check if string literal exists
            if (elements.Length <= 1) {
                Console.WriteLine($"{lineNumber}: stpush must have corresponding string literal");
                Environment.Exit(1);
            }
            string content = line.Substring(line.IndexOf('"'));

            // Check quotation marks
            if (content == "\"" || !content.EndsWith("\"")) {
                Console.WriteLine($"{lineNumber}: Malformed string (unterminated \"?)");
                Environment.Exit(1);
            }

            // Check stpush string length
            if (content.Length <= 2) {
                Console.WriteLine($"{lineNumber}: No string to push.");
                Environment.Exit(1);
            }

            // Replace 'escape character' substrings 
            // with actual escape characters
            content = content.Substring(1, content.Length - 2)
                .Replace("\\n", "\n")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");

            List<SourceLine> expandedInstructions = new List<SourceLine>();
            List<byte> pushBytes = new List<byte>();

            // Convert content string to ASCII bytes
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(content);
            List<byte> byteList = new List<byte>(bytes);

            // Start at the end of the byte list
            for (int i = byteList.Count - 1; i >= 0; i--) {
                pushBytes.Add(byteList[i]);

                // Form one instruction every 3 characters 
                // or when the content string ends
                if (pushBytes.Count == 3 || i == byteList.Count - byteList.Count % 3) {
                    byte contByte = (byte)0x01;

                    // Add null terminator when content string ends
                    if (i == byteList.Count - byteList.Count % 3) {

                        // Pad push byte lists that are too short
                        for (int j = 0; pushBytes.Count < 3; j++) {
                            pushBytes.Insert(0, (byte)0x01);
                        }

                        contByte = (byte)0x00;
                    }

                    // For content strings of a lengths divisible by three, the
                    // content string ends after the first two iterations
                    if (byteList.Count % 3 == 0 && i == byteList.Count - 3) {
                        contByte = (byte)0x00;
                    }

                    // Continue if the end of the content 
                    // string has not been reached
                    pushBytes.Insert(0, contByte);

                    // Reverse for proper ordering
                    byte[] pushArray = pushBytes.ToArray();
                    Array.Reverse(pushArray);

                    // Expand push array to push instruction
                    string pushArg = $"0x{BitConverter.ToUInt32(pushArray, 0):X8}";
                    expandedInstructions.Add(new SourceLine($"push {pushArg}",
                                lineNumber, programCounter));
                    programCounter += 4;

                    // Reset push byte list
                    pushBytes.Clear();
                }
            }
            // Add push instructions expanded from stpush
            return (expandedInstructions.ToArray(), false);
        }
        return (new[] { new SourceLine(line, lineNumber, programCounter) }, false);
    }
}

public class Encoder {

    // RDP: PC-Relative Offset Helper
    public static int PCRelative(Nullable<int> target, int pc, int mask, uint opcode) {

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
        private Nullable<int> value;
        public Exit(Nullable<int> value) => this.value = value;
        public int Encode() => unchecked((int)(0x00000000 | (value ?? 0)));
    }

    // CGW: Represents a Swap instruction with two optional parameters.
    public class Swap : IInstruction {
        private Nullable<int> first, second;
        public Swap(Nullable<int> first, Nullable<int> second) {
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
        private Nullable<int> value;
        public StInput(Nullable<int> value) => this.value = value;
        public int Encode() => unchecked((int)(0x05000000 | (value ?? 0xFFFFFF)));
    }

    // CGW: Debug help.
    public class Debug : IInstruction {
        private Nullable<int> value;
        public Debug(Nullable<int> value) => this.value = value;
        public int Encode() => unchecked((int)(0x0F000000 | (value ?? 0)));
    }

    // CGW: Represents a Pop instruction, removing an item from the stack.
    public class Pop : IInstruction {
        private Nullable<int> value;
        public Pop(Nullable<int> value) => this.value = value;
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
        private readonly Nullable<int> _offset;
        public Dup(Nullable<int> offset) {
            _offset = offset & ~3;
        }
        public int Encode() => unchecked((int)((0b1100 << 28) | (_offset ?? 0)));
    }

    // RDP: Encodes stprint instruction with optional parameter.
    public class StPrint : IInstruction {
        private Nullable<int> value;
        public StPrint(Nullable<int> value) => this.value = value;
        public int Encode() => unchecked((int)(0x40000000 | (value ?? 0)));
    }

    // RDP: Encodes call instruction with pc-relative offset.
    // The argument is checked before passing it to the instruction,
    // therefore the value is not acutally optional as it appears here.
    public class Call : IInstruction {
        private Nullable<int> target;
        private int pc;

        public Call(Nullable<int> target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0xFFFFFFC, 0x50000000);
        }
    }

    // RDP: Encodes return instruction with optional parameter.
    public class Return : IInstruction {
        private Nullable<int> value;
        public Return(Nullable<int> value) => this.value = value;
        public int Encode() => unchecked((int)(0x60000000 | (value ?? 0)));
    }

    // RDP: Encodes goto instruction
    public class Goto : IInstruction {
        private Nullable<int> target;
        private int pc;

        public Goto(Nullable<int> target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x0FFFFFFF, 0x70000000);
        }
    }

    // CGW: Encode push instruction prototype
    public class Push : IInstruction {
        private readonly int _operand;
        public Push(Nullable<int> inputValue = 0) {
            _operand = inputValue ?? 0;
        }
        public int Encode() {
            return (0b1111 << 28) | (_operand & 0x0FFFFFFF);
        }
    }

    // RDP: Binary If instructions
    public class Equals : IInstruction {
        private Nullable<int> target;
        private int pc;

        public Equals(Nullable<int> target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x80000000);
        }
    }

    public class NotEquals : IInstruction {
        private Nullable<int> target;
        private int pc;

        public NotEquals(Nullable<int> target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x82000000);
        }
    }

    public class LessThan : IInstruction {
        private Nullable<int> target;
        private int pc;

        public LessThan(Nullable<int> target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x84000000);
        }
    }

    public class GreaterThan : IInstruction {
        private Nullable<int> target;
        private int pc;

        public GreaterThan(Nullable<int> target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x86000000);
        }
    }

    public class LessEquals : IInstruction {
        private Nullable<int> target;
        private int pc;

        public LessEquals(Nullable<int> target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x88000000);
        }
    }

    public class GreaterEquals : IInstruction {
        private Nullable<int> target;
        private int pc;

        public GreaterEquals(Nullable<int> target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x8A000000);
        }
    }

    // RDP: Unary If instructions
    public class EqualsZero : IInstruction {
        private Nullable<int> target;
        private int pc;

        public EqualsZero(Nullable<int> target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x90000000);
        }
    }

    public class NotEqualsZero : IInstruction {
        private Nullable<int> target;
        private int pc;

        public NotEqualsZero(Nullable<int> target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x92000000);
        }
    }

    public class LessThanZero : IInstruction {
        private Nullable<int> target;
        private int pc;

        public LessThanZero(Nullable<int> target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x94000000);
        }
    }

    public class GreaterThanEqualZero : IInstruction {
        private Nullable<int> target;
        private int pc;

        public GreaterThanEqualZero(Nullable<int> target, int pc) {
            this.target = target;
            this.pc = pc;
        }
        public int Encode() {
            return Encoder.PCRelative(target, pc, 0x00FFFFFF, 0x96000000);
        }
    }

    // RDP: Encodes dump instruction
    public class Dump : IInstruction { public int Encode() => unchecked((int)0xE0000000); }

    // RDP: Encodes print instructions
    public class Print : IInstruction {
        private Nullable<int> offset;
        private char? type;

        public Print(Nullable<int> offset, char? type) {
            this.offset = offset;
            this.type = type;
        }
        public int Encode() {
            int process = (offset ?? 0) & ~3; // Ensure its a multiple of 4

            if (type == 'h') {
                return unchecked((int)(0xD0000000 | process | 0x00000001));
            } else if (type == 'b') {
                return unchecked((int)(0xD0000000 | process | 0x00000002));
            } else if (type == 'o') {
                return unchecked((int)(0xD0000000 | process | 0x00000003));
            }

            return unchecked((int)(0xD0000000 | process));
        }
    }
}

// CGW: Main processor class for the assembler.
class Processor {

    // CGW: Utility class for safe conversion of strings to integers.
    // RDP: Supports hexadecimal values
    private static Nullable<int> ToInteger(string input, string op, Dictionary<string,
            int> labelPositions, int lineNumber, string[] elements) {

        // Check if input is null
        if (string.IsNullOrEmpty(input)) return (Nullable<int>)null;

        // Try to parse hexadecimal value to integer
        if (input.StartsWith("0x")) {

            // Exit cannot accept a hexadecimal argument.
            if (string.Compare(op, "exit") == 0) {
                err($"Invalid exit code: Input string was not in the correct format.", 
                        lineNumber);
            }

            if (int.TryParse(input.Substring(2),
                        System.Globalization.NumberStyles.HexNumber, null,
                        out int res)) {
                return res;
            }
        }

        // Try to parse decimal value to integer
        if (!input.StartsWith("0x") && int.TryParse(input, out int result)) {
            return result;
        }

        // Without this code block, this function returns null for invalid
        // offsets. During encoding, null values default to a valid offset.
        // Therefore, invalid offsets must be caught now.
        HashSet<string> TakesOffset = new HashSet<string>() {
            "pop", "swap", "stinput", "dup", "stprint", "return", "print", "exit"
        };
        if (TakesOffset.Contains(op)) {
            if (string.Compare(op, "exit") == 0) {
                err($"Invalid exit code: Input string was not in the correct format.", 
                        lineNumber);
            }
            if (string.Compare(op, "swap") == 0) {
                err($"invalid offset given to {op} " + 
                        $"{(string.Compare(input, elements[1]) == 0 ? "from" : "to")} '{input}'",
                        lineNumber);
            } else {
                err($"invalid offset given to {op} '{input}'", lineNumber);
            }
        }

        HashSet<string> TakesLabel = new HashSet<string>() { "push", "debug" };
        if (!labelPositions.ContainsKey(input) && TakesLabel.Contains(op)) {
            err($"Invalid value for {op}: {input}.", lineNumber);
        }

        return (Nullable<int>)null;
    }

    // RDP: Check first argument of PC-Relative Instruction
    private static Nullable<int> validatePC(Dictionary<string, int> labelPositions,
            string[] elements, int lineNumber) {

        // Check if argument exists
        if (elements.Length <= 1) {
            err($"no label given for {elements[0]} statement.", lineNumber);
        }
        // Check if label is in labelPositions
        if (!labelPositions.ContainsKey(elements[1])) {
            err($"Invalid label: The given key '{elements[1]}' " +
                    "was not present in the dictionary.", lineNumber);
        }
        return labelPositions[elements[1]];
    }

    // RDP: Check that first argument is a multiple of 4 to ensure alignment
    private static Nullable<int> validateAlign(Nullable<int> arg, string[] elements, int
            lineNumber) {

        // Check that argument exists and is divisible by 4
        if (arg.HasValue && arg % 4 != 0) {
            err($"offsets to {elements[0]} must be multiples of 4.", lineNumber);
        }
        return arg;
    }

    // RDP: Prints an error message and stops program
    private static void err(string msg, int lineNumber) {
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

        // Check if input file exists
        if (!File.Exists(inputPath)) {
            Console.WriteLine($"{args[0]}: File not found");
            Environment.Exit(1);
        }

        Dictionary<string, int> labelPositions = new Dictionary<string, int>();
        List<SourceLine> lines = new List<SourceLine>();

        // CGW: First Pass - Identify labels and calculate memory addresses.
        int programCounter = 0;
        using (StreamReader reader = new StreamReader(inputPath)) {

            string line;
            int lineNumber = 0;
            while ((line = reader.ReadLine()) != null) {
                lineNumber++;

                SourceLine[] operations;
                bool isLabel = false;
                (operations, isLabel) =
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
            Nullable<int> argOne = ToInteger(elements.ElementAtOrDefault(1),
                    elements[0], labelPositions, lineNumber, elements);
            Nullable<int> argTwo = ToInteger(elements.ElementAtOrDefault(2),
                    elements[0], labelPositions, lineNumber, elements);

            // CGW: Resolve labels to memory addresses if applicable.
            if (elements.Length > 1 && labelPositions.ContainsKey(elements[1])) {
                argOne = labelPositions[elements[1]];
            }
            if (elements.Length > 2 && labelPositions.ContainsKey(elements[2])) {
                argTwo = labelPositions[elements[2]];
            }

            // CGW: Match instructions using a switch expression.
            // RDP: Handle argument checking.
            Instruction.IInstruction op;
            switch (elements[0].ToLower()) {
                case "exit":
                    op = new Instruction.Exit(argOne);
                    break;
                case "swap":
                    op = new Instruction.Swap(argOne, argTwo);
                    break;
                case "nop":
                    op = new Instruction.Nop();
                    break;
                case "debug":
                    op = new Instruction.Debug(argOne);
                    break;
                case "pop":
                    op = new Instruction.Pop(validateAlign(argOne, elements, lineNumber));
                    break;
                case "input":
                    op = new Instruction.Input();
                    break;
                case "stinput":
                    op = new Instruction.StInput(argOne);
                    break;
                case "add":
                    op = new Instruction.Add();
                    break;
                case "sub":
                    op = new Instruction.Sub();
                    break;
                case "mul":
                    op = new Instruction.Mul();
                    break;
                case "div":
                    op = new Instruction.Div();
                    break;
                case "rem":
                    op = new Instruction.Rem();
                    break;
                case "and":
                    op = new Instruction.And();
                    break;
                case "or":
                    op = new Instruction.Or();
                    break;
                case "xor":
                    op = new Instruction.Xor();
                    break;
                case "lsl":
                    op = new Instruction.Lsl();
                    break;
                case "lsr":
                    op = new Instruction.Lsr();
                    break;
                case "asr":
                    op = new Instruction.Asr();
                    break;
                case "neg":
                    op = new Instruction.Neg();
                    break;
                case "not":
                    op = new Instruction.Not();
                    break;
                case "push":
                    op = new Instruction.Push(argOne);
                    break;
                case "dup":
                    op = new Instruction.Dup(validateAlign(argOne, elements, lineNumber));
                    break;
                case "stprint":
                    op = new Instruction.StPrint(argOne);
                    break;
                case "call":
                    op = new Instruction.Call(validatePC(labelPositions, elements, lineNumber), pc);
                    break;
                case "return":
                    op = new Instruction.Return(validateAlign(argOne, elements, lineNumber));
                    break;
                case "goto":
                    op = new Instruction.Goto(validatePC(labelPositions, elements, lineNumber), pc);
                    break;
                case "ifeq":
                    op = new Instruction.Equals(validatePC(labelPositions, elements, lineNumber), pc);
                    break;
                case "ifne":
                    op = new Instruction.NotEquals(validatePC(labelPositions, elements, lineNumber), pc);
                    break;
                case "iflt":
                    op = new Instruction.LessThan(validatePC(labelPositions, elements, lineNumber), pc);
                    break;
                case "ifgt":
                    op = new Instruction.GreaterThan(validatePC(labelPositions, elements, lineNumber), pc);
                    break;
                case "ifle":
                    op = new Instruction.LessEquals(validatePC(labelPositions, elements, lineNumber), pc);
                    break;
                case "ifge":
                    op = new Instruction.GreaterEquals(validatePC(labelPositions, elements, lineNumber), pc);
                    break;
                case "ifez":
                    op = new Instruction.EqualsZero(validatePC(labelPositions, elements, lineNumber), pc);
                    break;
                case "ifnz":
                    op = new Instruction.NotEqualsZero(validatePC(labelPositions, elements, lineNumber), pc);
                    break;
                case "ifmi":
                    op = new Instruction.LessThanZero(validatePC(labelPositions, elements, lineNumber), pc);
                    break;
                case "ifpl":
                    op = new Instruction.GreaterThanEqualZero(validatePC(labelPositions, elements, lineNumber), pc);
                    break;
                case "dump":
                    op = new Instruction.Dump();
                    break;
                case "print":
                    op = new Instruction.Print(argOne, 'd');
                    break;
                case "printh":
                    op = new Instruction.Print(argOne, 'h');
                    break;
                case "printo":
                    op = new Instruction.Print(argOne, 'o');
                    break;
                case "printb":
                    op = new Instruction.Print(argOne, 'b');
                    break;
                default:
                    err($"Unknown instruction {elements[0]}", lineNumber);
                    return;
            }
            operationList.Add(op);
        }

        if (operationList.Count < 1) {
            Console.WriteLine($"ERROR: {AppDomain.CurrentDomain.FriendlyName}: " +
                               "no instructions to assemble.");
            Environment.Exit(1);
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
    }
}
