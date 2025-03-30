# Compile with C# 7.2
# mcs -langversion:7.2 -out:Assembler.exe Assembler.cs
#
# Copy test directory to current directory
# cp /home/smarz1/courses/cosc365/project/tests .
#
# Give script executable permissions
# chmod +x lab_machines_test_script.sh

# Check args
if [ "$#" -ne 3 ]; then
    echo "Usage: $0 <executable> <input_directory> <output_directory>"
    exit 1
fi

EXECUTABLE=$1
INPUT_DIR=$2
OUTPUT_DIR=$3

# Ensure the output directory exists
mkdir -p "$OUTPUT_DIR"

# Process each .asm file in the input directory
for asm_file in "$INPUT_DIR"/*.asm; do
    # Extract the base filename without extension
    base_name=$(basename "$asm_file" .asm)
    
    # Define the output file path
    output_file="$OUTPUT_DIR/$base_name.v"
    
    # Run the executable with mono
    mono "$EXECUTABLE" "$asm_file" "$output_file"
done

# Run diff on each generated output file against the corresponding solution file
for output_file in "$OUTPUT_DIR"/*.v; do
    base_name=$(basename "$output_file")
    solution_file="$INPUT_DIR/$base_name"
    
    if [ -f "$solution_file" ]; then
        echo "Comparing $output_file with $solution_file"
        diff "$output_file" "$solution_file"
        echo ""
    else
        echo "No solution file found for $output_file"
    fi
done
