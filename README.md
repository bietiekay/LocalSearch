# LocalSearch

LocalSearch is a Windows desktop application for fast and efficient file and content search within local directories. The application provides a user-friendly interface to search for files and text, configure search parameters, and manage search results.

## Features

- **File Search**: Quickly search for files by name within a selected directory and its subdirectories.
- **Content Search**: Search for specific text or patterns inside files.
- **Configurable Search Parameters**:
  - Include subdirectories that are indexed.
  - sort by file extensions (e.g., .txt, .cs, .md).
  - partial matching
- **Result Management**:
  - Display search results in a sortable and filterable list.
  - Double-click results to open files directly.
  - Copy file paths to clipboard.
- **User Interface**:
  - Modern, intuitive Windows Forms interface.
  - Responsive design for different screen sizes.
  - About dialog with program information.
  - Settings dialog for customizing search behavior.
- **Performance**:
  - Optimized for fast searching, even in large directories.
  - Asynchronous search to keep the UI responsive.

## How to Use

1. **Select Directory**: Choose the root folder where you want to search.
2. **Set Search Parameters**: Adjust file filters, case sensitivity, and whether to include subfolders.
3. **Enter Search Query**: Type the filename or text you are looking for.
4. **Start Search**: Click the search button to begin. Results will appear in the list below.
5. **Interact with Results**: Double-click to open a file, or right-click for more options.

## Dialogs
- **About Dialog**: Shows information about the application, version, and copyright.
- **Settings Dialog**: Allows customization of search parameters and application behavior.

## Installation
1. Download the latest release from the repository or build from source using Visual Studio.
2. Run `LocalSearch.exe` from the `bin/Debug` or `bin/Release` folder.

## Requirements
- Windows OS
- .NET Framework 4.7.2 or higher

## License
This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

---

Copyright © 2025 Daniel Kirstenpfad 
