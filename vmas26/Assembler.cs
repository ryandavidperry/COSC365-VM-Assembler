﻿// Assembler!

using System;
using System.IO;
using System.Collections.Generic;

public interface IInstruction {
    int Encode();
}

public class Nop : IInstruction {
    public int Encode () {
        return 0x02000000;
    }
}

public class Dup : IInstruction {
    private readonly int _offset;
    public Dup(int offset) {
        _offset = offset & ~3;
    }
    public int Encode() {
        return (0b1100 << 28) | _offset;
    }
}

public class Exit : IInstruction {
    private readonly int _exitCode;
    public Exit(int exitCode = 0) {
        _exitCode = exitCode & 0xFF;
    }
    public int Encode() {
        return (0b0000 << 28) | (_exitCode);
    }
}

public class Debug : IInstruction {
    private readonly int _value;
    public Debug(int value = 0) {
        _value = value & 0xFFFFFF;
    }
    public int Encode() {
        return (0b0000 << 28) | (0b1111 << 24) | _value;
    }
}

class Assembler {
    private int _lineNumber;
    private int _pass;
    private List<IInstruction> _instructionList = new List<IInstruction>();

    // Address for labels
    private ushort _address;

    // Parse Tokens
    private string _label = "";
    private string _op = "";
    private List<string> _argList = new List<string>();

    /*
     * Symbol table entry.
     * Stores lable and corresponding address.
     */
    struct SymbolTable {
        public string Lab;
        public ushort Val;

        public SymbolTable(string lab, ushort value) {
            Lab = lab;
            Val = value;
        }
    }

    private List<SymbolTable> _symbolTable = new List<SymbolTable>();

    /*
     * Assembly function.
     * Pass 1 encodes the labels to the program counter.
     * Pass 2 writes object code.
     */
    private void assemble (string[] lines, string outputFile) {
        _pass = 1;
        for (_lineNumber = 0; _lineNumber < lines.Length; _lineNumber++) {
            tokenize(lines[_lineNumber]);
            process();
        }

        _pass = 2;
        for (_lineNumber = 0; _lineNumber < lines.Length; _lineNumber++) {
            tokenize(lines[_lineNumber]);
            process();
        }

        writeOutput(outputFile);
    }

    /*
     * Parse each line into tokens:
     * [label] [op] [args]
     */
    private void tokenize(string line) {
        _label = "";
        _op = "";
        _argList.Clear();

        // Remove outlying whitespace and inline 
        // comments (e.g., 'push 0   # comment')
        string pre = line.Split('#')[0].Trim();

        // Skip empty lines or lines that 
        // only contain a comment
        if (string.IsNullOrEmpty(pre)) {
            return;
        }

        // Get label
        if (pre.EndsWith(":")) {
            // Line contains a label
            _label = pre.TrimEnd(':').Trim();
            return;
        }

        // Handle stpush
        if (pre.StartsWith("stpush")) {
            _op = "stpush";
            int start = pre.IndexOf('"');
            int end = pre.LastIndexOf('"');

            string ?words = "";
            if (start != -1 && end > start) {
                words = pre.Substring(start + 1, end - start - 1);
            }

            _argList.Add(words);
            return;
        }

        // Split preprocessed line into op and args sections
        string[] parts = pre.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        // Set opcode
        _op = parts[0].Trim();

        // Set args
        if (parts.Length > 1) {
            _argList = new List<string>(parts[1].Trim().Split(' ',
                        StringSplitOptions.RemoveEmptyEntries));
        }
    }

    /*
     * If pass 1, and there is a label on this line, 
     * record the label's address.
     *
     * If pass 2, call instruction.
     */
    private void process() {
        if (string.IsNullOrEmpty(_op) && _argList.Count == 0) {
            // When there is a label or a blank line, the size of the
            // instruction is 0 and does not call an instruction
            passAction(0, null);
            return;
        }

        var instructionMap = new Dictionary<string, Action> {
            {"exit", exit!}, {"swap", swap!}, {"nop", nop!}, {"stinput", stinput!}, 
            {"debug", debug!}, {"pop", pop!}, {"add", add!}, {"sub", sub!},
            {"mul", mul!}, {"div", div!}, {"rem", rem!}, {"and", and!},
            {"or", or!}, {"xor", xor!}, {"lsl", lsl!}, {"lsr", lsr!},
            {"asr", asr!}, {"neg", neg!}, {"not", not!}, {"stprint", stprint!},
            {"call", call!}, {"return", fReturn!}, {"goto", fGoto!}, {"eq", eq!},
            {"ne", ne!}, {"lt", lt!}, {"gt", gt!}, {"le", le!},
            {"ge", ge!}, {"ez", ez!}, {"nz", nz!}, {"mi", mi!},
            {"pl", pl!}, {"dup", dup!}, {"print", print!}, {"dump", dump!},
            {"push", push!}, {"stpush", stpush!}
        };

        if (instructionMap.TryGetValue(_op, out Action ?action)) {
            action!();
        } else {
            error($"Unknown instruction: {_op}");
        }
    }

    /*
     * Check which pass we are in and perform 
     * the correct action for that pass.
     *
     * Size is size of instruction.
     */
    private void passAction (ushort size, IInstruction ?instruction) {
        if (_pass == 1) {
            // Add new symbol if there is a label
            if (!string.IsNullOrEmpty(_label)) {
                addSymbol();
            }

            // Increment address by size of instruction
            _address += size;
        } else {
            // Add instruction to instruction list
            if (instruction != null) {
                _instructionList.Add(instruction);
            }
        }
    }

    /*
     * Add a symbol to the symbol table.
     */
    private void addSymbol() {
        foreach (var entry in _symbolTable) {
            if (entry.Lab == _label) {
                error($"duplicate label: {_label}");
            }
        }
        _symbolTable.Add(new SymbolTable(_label, _address));
    }

    private void exit() {
        int exitCode = 0;
        if (_argList.Count == 1 && int.TryParse(_argList[0], out int parsedCode)) {
            exitCode = parsedCode;
        }
        checkArguments(_argList.Count <= 1);
        passAction(4, new Exit(exitCode));
    }

    private void swap() {

    }

    private void nop() {
        checkArguments(_argList.Count == 0);
        passAction(4, new Nop());
    }

    private void stinput() {

    }

    private void debug() {
        int debugValue = 0;

        if (_argList.Count == 1) {
            string arg = _argList[0];

            if (arg.StartsWith("0x")) {
                // Hex value
                if (!int.TryParse(arg.Substring(2),
                            System.Globalization.NumberStyles.HexNumber, null, out
                            debugValue)) {
                    error($"Invalid hexadecimal value: {arg}");
                    return;
                }
            } else if (!int.TryParse(arg, out debugValue)) {
                    // Decimal value
                    error($"Invalid decimal value: {arg}");
                    return;
            }
        }
        checkArguments(_argList.Count <= 1);
        passAction(4, new Debug(debugValue));
    }

    private void pop() {

    }

    private void add() {

    }

    private void sub() {

    }

    private void mul() {

    }

    private void div() {

    }

    private void rem() {

    }

    private void and() {

    }

    private void or() {

    }

    private void xor() {

    }

    private void lsl() {

    }

    private void lsr() {

    }

    private void asr() {

    }

    private void neg() {

    }

    private void not() {

    }

    private void stprint() {

    }

    private void call() {

    }

    private void fReturn() {

    }

    private void fGoto() {

    }

    private void eq() {

    }

    private void ne() {

    }

    private void lt() {

    }

    private void gt() {

    }

    private void le() {

    }

    private void ge() {

    }

    private void ez() {

    }

    private void nz() {

    }

    private void mi() {

    }

    private void pl() {

    }

    private void dup() {
        checkArguments(_argList.Count == 1 && int.TryParse(_argList[0], out _));
        int offset = int.Parse(_argList[0]);
        if (offset % 4 != 0) {
            error("dup offset must be divisible by 4");
        }
        passAction(4, new Dup(offset));
    }

    private void print() {

    }

    private void dump() {

    }

    private void push() {

    }

    private void stpush() {

    }

    /*
     * Check Arguments
     */
    private void checkArguments(bool passed) {
        if (!passed) {
            error($"arguments not correct for mnemonic: {_op}");
        }
    }

    /*
     * Write to output file
     */
    private void writeOutput(string outputFile) {
        using (BinaryWriter binFileOut = new BinaryWriter(File.Open(outputFile,
                        FileMode.Create))) {
            // Write magic number
            binFileOut.Write(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

            // Write instructions in little-endian format
            foreach (var inst in _instructionList) {
                binFileOut.Write(inst.Encode());
            }

            // Pad out a multiple of 4 instructions with nops
            int padding = (4 - _instructionList.Count % 4) % 4;
            for (int i = 0; i < padding; i++) {
                /*
                 * nop() adds Nop instructions to the instruction 
                 * list which interferes with the address. Therefore,
                 * add nop instructions directly 
                 */
                binFileOut.Write(0x02000000);
            }
        }
    }

    /*
     * Call on error
     */
    private void error(string message) {
        Console.WriteLine($"{_lineNumber+1}: {message}");
        Environment.Exit(1);
    }

    public static void Main(string[] args) {
        // Check arguments
        if (args.Length < 2) {
            Console.WriteLine($"Usage: {AppDomain.CurrentDomain.FriendlyName} " +
                    "<file.asm> <file.v>");
            return;
        }

        string inputFile = args[0];
        string outputFile = args[1];

        // Check if input file exists
        if (!File.Exists(inputFile)) {
            Console.WriteLine($"{args[0]}: File not found");
            return;
        }

        string[] lines = File.ReadAllLines(inputFile);

        Assembler assembler = new Assembler();
        assembler.assemble(lines, outputFile);
    }

}
