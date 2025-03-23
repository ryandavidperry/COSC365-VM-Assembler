// Assembler!

using System;
using System.IO;

class Assembler {

    /* 
     * Prepares output file for the second pass.
     *
     * Returns a dictionary of labels and their correpsonding memory location
     * and line number. Dictionary is of the form:
     *      Key - label, Value - (memory location, line number)
     */
    public static Dictionary<string, (int address, int lineNumber)> 
        FirstPass(string InputFile, string OutputFile) {
        var labels = new Dictionary<string, (int, int)>(); 
        int pc = 0;
        int lineNumber = 0;

        using (StreamWriter sw = new StreamWriter(OutputFile)) {
            foreach (string dirtyLine in File.ReadLines(InputFile)) {
                lineNumber++;

                // Remove inline comments (e.g., 'push 0   # comment')
                string line = dirtyLine.Split('#')[0].Trim();

                // Remove empty lines and lines that contain only a comment
                if (string.IsNullOrEmpty(line)) {
                    continue;
                }

                // Handles extra white space (e.g., 'push       10')
                line = string.Join(" ", line.Split(' ',
                                        StringSplitOptions.RemoveEmptyEntries));

                if (line.EndsWith(':')) {
                    // Add label to dictionary
                    string label = line.TrimEnd(':');

                    // Check for duplicate labels
                    if (labels.ContainsKey(label)) {
                        Console.WriteLine($"{lineNumber}: " + 
                                          $"Label already exists: {label}");
                        sw.Close();
                        File.Delete(OutputFile);
                        Environment.Exit(1);
                    }

                    labels[label] = (pc, lineNumber);
                } else {
                    if (line.StartsWith("stpush ")) {
                        int start = line.IndexOf('"');
                        int end = line.LastIndexOf('"');

                        if (start != -1 && end > start) {
                            string words = line.Substring(start + 1,
                                                          end - start - 1);
                            /* 
                             * Words are rounded up (+3) to ensure alignment, and 
                             * the null terminator takes up one additional byte (+1)
                             */
                            int numWords = (words.Length + 3 + 1) / 4; 
                            pc += numWords * 4;
                        } else {
                            // Invalid stpush syntax
                            Console.WriteLine($"{lineNumber}: " + 
                                            $"Malformed string (unterminated \"?)");
                            sw.Close();
                            File.Delete(OutputFile);
                            Environment.Exit(1);
                        }
                    } else {
                        // Non-labels increment program counter
                        pc += 4;
                    }
                    sw.WriteLine(line);
                }
            }
        }
        return labels;
    }

    public static void Main(string[] args) {
        // Check arguments
        if (args.Length < 2) {
            Console.WriteLine($"Usage: {AppDomain.CurrentDomain.FriendlyName} " +
                               "<file.asm> <file.v>");
            return;
        }

        string InputFile = args[0];
        string OutputFile = args[1];

        // Check if input file exists
        if (!File.Exists(InputFile)) {
            Console.WriteLine($"{args[0]}: File not found");
            return;
        }

        var labels = FirstPass(InputFile, OutputFile);

        // Output contents of dictionary for visualization
        foreach (var entry in labels) {
            Console.WriteLine($"label:   {entry.Key}\n" +
                              $"mem loc: {entry.Value.address}\n" +
                              $"line:    {entry.Value.lineNumber}\n");
        }
    }
}
