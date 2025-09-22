# Unity Project Analyzer

A powerful .NET console tool for analyzing Unity game projects. Extracts scene hierarchies and identifies unused MonoBehaviour scripts using advanced parsing techniques.

## Features

- **Scene Hierarchy Analysis**: Parses Unity scene files (.unity) and outputs the GameObject hierarchy in a readable format
- **Unused Script Detection**: Identifies C# MonoBehaviour scripts that are not referenced in any scene
- **Interactive HTML Reports**: Generates beautiful, searchable HTML reports with project statistics
- **Parallel Processing**: Uses async/parallel APIs for improved performance with large projects
- **Roslyn Integration**: Uses Microsoft CodeAnalysis (Roslyn) for advanced C# script parsing
- **YamlDotNet Integration**: Robust parsing of Unity's complex YAML scene format
- **Cross-Platform**: Runs on Windows, Linux, and macOS with .NET 8.0

## Requirements

- .NET 8.0 or later
- Windows, macOS, or Linux

## Installation

1. Clone this repository
2. Build the project:
   ```bash
   dotnet build --configuration Release
   ```

## Usage

```bash
dotnet run -- <unity_project_path> <output_folder_path>
```

Or after building:
```bash
./bin/Release/net8.0/UnityProjectAnalyzer.exe <unity_project_path> <output_folder_path>
```

### Example

```bash
dotnet run -- "C:\MyUnityProject" "C:\Output"
```

## Output Files

The tool generates the following files in the output directory:

1. **`[SceneName].unity.dump`** - Scene hierarchy for each scene file
   - Lists GameObjects in hierarchical order
   - Uses `--` prefix for child objects (depth increases with more dashes)
   - Formats names with spaces before numbers (e.g., "Child1" becomes "Child 1")

2. **`UnusedScripts.csv`** - List of unused MonoBehaviour scripts
   - Contains relative path and GUID of each unused script
   - CSV format with headers: "Relative Path,GUID"

## Example Output

### Scene Hierarchy (SampleScene.unity.dump)
```
Main Camera
Directional Light
Parent
--Child 1
----ChildNested
--Child 2
```

### Unused Scripts (UnusedScripts.csv)
```
Relative Path,GUID
Assets/Scripts/UnusedScript.cs,0111ada5c04694881b4ea1c5adfed99f
Assets/Scripts/Nested/UnusedScript2.cs,4851f847002ac48c487adaab15c4350c
```

## Technical Implementation

### Architecture

- **SceneParser**: Handles Unity scene file parsing and hierarchy extraction
- **ScriptAnalyzer**: Uses Roslyn to analyze C# MonoBehaviour scripts
- **UnityProjectAnalyzer**: Main orchestrator that coordinates the analysis

### Key Technologies

- **YamlDotNet**: For parsing Unity's YAML-based scene files
- **Microsoft.CodeAnalysis.CSharp (Roslyn)**: For C# script analysis
- **Async/Parallel Processing**: For improved performance on large projects

### Unity Scene File Format Understanding

Unity scene files are YAML documents containing:
- **GameObjects**: Main scene entities with unique fileIDs
- **Transform/RectTransform**: Position, rotation, scale, and parent-child relationships
- **MonoBehaviour Components**: Script references with GUIDs
- **SceneRoots**: Defines the order of root GameObjects

The tool correctly handles:
- Parent-child relationships through Transform components
- Scene object ordering via SceneRoots section
- Script references through MonoBehaviour components
- GUID-based asset referencing

## Limitations

- Only analyzes `.unity` scene files and `.cs` script files
- Assumes all C# classes inherit from MonoBehaviour
- Does not analyze prefab systems
- Works without Unity Editor installation

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the MIT License.