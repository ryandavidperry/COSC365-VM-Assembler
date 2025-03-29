PROJECT_PATH="./vmas26"
INPUT_FILES="../input_files"
OUTPUT_FILES="../output_files"

# If you get a permission denied error, run 'sudo chmod +x run_test.sh' 
# to give the script execute privileges

# Move to project directory
cd "$PROJECT_PATH" || exit

# Check if input directory exists
if [ ! -d "$INPUT_FILES"]; then
    echo "The directory '$INPUT_FILES' does not exist"
    echo ""
    exit 1
fi

# Make output directory
mkdir -p "$OUTPUT_FILES"

# Process each .asm file in the input directory
for input_file in $INPUT_FILES/*.asm; do

    # Extract filename
    base_name=$(basename "$input_file" .asm)

    # Define output file path (relative to project directory)
    output_file="$OUTPUT_FILES/$base_name.v"

    # Run .NET program
    echo "Running: dotnet run -- \"$input_file\" \"$output_file\""
    dotnet run -- "$input_file" "$output_file"

    # Check if output file was created
    if [ -s "$output_file" ]; then
        echo "Processed: $input_file -> $output_file"
        echo ""
    else
        echo "Failed to create output for $input_file"
        echo ""
    fi
done

echo "All files processed. Output is in $OUTPUT_FILES"
