/* Names:       Ryan Anderson, Thomas Latawiec, 
 *              Ryan Perry, Chase Woodfill
 *
 * Date:        04/04/2025
 *
 * Synopsis:    Contains classes that implement IIinstruction
 */

using System;

namespace Instruction {

    // CGW: Represents a NOP (No Operation) instruction.
    public class Nop : IInstruction {
        public int Encode() => 0x02000000;
    }

    // CGW: Represents an Exit instruction, optionally using a value parameter.
    public class Exit : IInstruction {
        private Nullable<int> value;
        public Exit(Nullable<int> value) => this.value = value;
        public int Encode() {
            return unchecked((int)((uint)(value ?? 0) & 0xFF));
        }
    }

    // CGW: Represents a Swap instruction with two optional parameters.
    public class Swap : IInstruction {
        private int from, to;

        public Swap(Nullable<int> first, Nullable<int> second) {
            this.from = (first ?? 4) >> 2;
            this.to = (second ?? 0) >> 2;
        }
        public int Encode() {
            int encodedFrom = from & 0xFFF;
            int encodedTo = to & 0xFFF;
            return (0x01000000 | (encodedFrom << 12) | encodedTo);
        }
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
        private Nullable<int> _offset;
        public Dup(Nullable<int> offset) {
            _offset = (offset ?? 0) & ~3;
        }
        public int Encode() => unchecked((int)((0b1100 << 28) | (_offset ?? 0) & 0x0FFFFFFF));
    }

    // RDP: Encodes stprint instruction with optional parameter.
    // CGW: Idea for handling negative numbers, prototype
    public class StPrint : IInstruction {
        private Nullable<int> value;
    
        public StPrint(Nullable<int> value) => this.value = value;
    
        public int Encode() {
            int val = value ?? 0;
        
            // Handle sign extension if value is negative
            if (val < 0) {
                // To ensure the negative values are encoded correctly, sign extend if necessary.
                // This ensures the value fits within the lower 28 bits properly.
                return unchecked((int)(0x40000000 | (val & 0x0FFFFFFF)));
            }
            // For non-negative values, we can directly apply the value
            return unchecked((int)(0x40000000 | (val & 0x0FFFFFFF)));
        }
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
            int offset = (int)target - pc;

            int signExtendedOffset = offset & 0x0FFFFFFF;

            if ((offset & (1 << 27)) != 0) {
                signExtendedOffset |= unchecked((int)0xFE000000);
            }

            int opcode = 0b0101;

            int result = (opcode << 28) | (signExtendedOffset & 0x0FFFFFFF);
  
            return result;
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
            int offset = (int)target - pc;

            int signExtendedOffset = offset & 0x0FFFFFFF;

            if ((offset & (1 << 27)) != 0) {
                signExtendedOffset |= unchecked((int)0xFE000000);
            }

            int opcode = 0b0111;

            int result = (opcode << 28) | (signExtendedOffset & 0x0FFFFFFF);
  
            return result;
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
            int offset = (int)target - pc;

            int signExtendedOffset = offset & 0x01FFFFFF;

            if ((offset & (1 << 24)) != 0) {
                signExtendedOffset |= unchecked((int)0xFE000000);
            }

            int opcode = 0b1000000;

            int result = (opcode << 25) | (signExtendedOffset & 0x01FFFFFF);

            return result;
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
            int offset = (int)target - pc;

            int signExtendedOffset = offset & 0x01FFFFFF;

            if ((offset & (1 << 24)) != 0) {
                signExtendedOffset |= unchecked((int)0xFE000000);
            }

            int opcode = 0b1000001;

            int result = (opcode << 25) | (signExtendedOffset & 0x01FFFFFF);

            return result;
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
            int offset = (int)target - pc;

            int signExtendedOffset = offset & 0x01FFFFFF;

            if ((offset & (1 << 24)) != 0) {
                signExtendedOffset |= unchecked((int)0xFE000000);
            }

            int opcode = 0b1000010;

            int result = (opcode << 25) | (signExtendedOffset & 0x01FFFFFF);
  
            return result;
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
            int offset = (int)target - pc;

            int signExtendedOffset = offset & 0x01FFFFFF;

            if ((offset & (1 << 24)) != 0) {
                signExtendedOffset |= unchecked((int)0xFE000000);
            }

            int opcode = 0b1000011;

            int result = (opcode << 25) | (signExtendedOffset & 0x01FFFFFF);
  
            return result;
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
            int offset = (int)target - pc;

            int signExtendedOffset = offset & 0x01FFFFFF;

            if ((offset & (1 << 24)) != 0) {
                signExtendedOffset |= unchecked((int)0xFE000000);
            }

            int opcode = 0b1000100;

            int result = (opcode << 25) | (signExtendedOffset & 0x01FFFFFF);
  
            return result;
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
            int offset = (int)target - pc;

            int signExtendedOffset = offset & 0x01FFFFFF;

            if ((offset & (1 << 24)) != 0) {
                signExtendedOffset |= unchecked((int)0xFE000000);
            }

            int opcode = 0b1000101;

            int result = (opcode << 25) | (signExtendedOffset & 0x01FFFFFF);

            return result;
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
            int offset = (int)target - pc;

            int signExtendedOffset = offset & 0x01FFFFFF;

            if ((offset & (1 << 24)) != 0) {
                signExtendedOffset |= unchecked((int)0xFE000000);
            }

            int opcode = 0b1001000;

            int result = (opcode << 25) | (signExtendedOffset & 0x01FFFFFF);

            return result;
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
            int offset = (int)target - pc;

            int signExtendedOffset = offset & 0x01FFFFFF;

            if ((offset & (1 << 24)) != 0) {
                signExtendedOffset |= unchecked((int)0xFE000000);
            }

            int opcode = 0b1001001;

            int result = (opcode << 25) | (signExtendedOffset & 0x01FFFFFF);

            return result;
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
            int offset = (int)target - pc;

            int signExtendedOffset = offset & 0x01FFFFFF;

            if ((offset & (1 << 24)) != 0) {
                signExtendedOffset |= unchecked((int)0xFE000000);
            }

            int opcode = 0b1001010;

            int result = (opcode << 25) | (signExtendedOffset & 0x01FFFFFF);

            return result;
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
            int offset = (int)target - pc;

            int signExtendedOffset = offset & 0x01FFFFFF;

            if ((offset & (1 << 24)) != 0) {
                signExtendedOffset |= unchecked((int)0xFE000000);
            }

            int opcode = 0b1001011;

            int result = (opcode << 25) | (signExtendedOffset & 0x01FFFFFF);

            return result;
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
            int encodedOffset = (process >> 2) & 0x03FFFFFF;
            encodedOffset <<= 2;

            if (type == 'h') {
                return unchecked((int)(0xD0000000 | (uint)encodedOffset | 0x00000001));
            } else if (type == 'b') {
                return unchecked((int)(0xD0000000 | (uint)encodedOffset | 0x00000002));
            } else if (type == 'o') {
                return unchecked((int)(0xD0000000 | (uint)encodedOffset | 0x00000003));
            }

            return unchecked((int)(0xD0000000 | (uint)encodedOffset));
        }
    }
}

