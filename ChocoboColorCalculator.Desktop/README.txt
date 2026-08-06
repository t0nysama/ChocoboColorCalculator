CHOCOBO COLOR CALCULATOR - DESKTOP EDITION
==========================================

GETTING STARTED
1. Extract the entire downloaded ZIP file.
2. Run ChocoboColorCalculator.Desktop.exe.
3. Search for and select the current and desired plumage colors.
4. Calculate the route and follow each fruit from top to bottom.

The application is self-contained. It does not require XIVLauncher, Dalamud,
or a separate .NET installation.

FEATURES
- The same globally audited closest-safe route engine used by the Dalamud plugin.
- Searchable current and desired color selectors.
- Shopping list, next-feed guidance, and a complete RGB route table.
- Manual checkboxes, Confirm Next, Undo, Reset, and saved progress.
- A lightweight update check on launch, a dedicated Updates tab, and one-click
  download, integrity verification, installation, and automatic relaunch.
- PDF, plain-text, and responsive HTML exports.
- A built-in feeding and accuracy guide.

ACCURACY
Every named color pair is checked against the accepted +/-5 RGB model. The
engine chooses the closest unclamped endpoint that clears the established
safety threshold, orders every fruit without channel clamping, and reports the
true distance to the nearest named-color boundary. Square Enix does not expose
the live hidden RGB value or publish the exact formula, so a Han Lemon reset to
Desert Yellow remains the most reliable baseline.

IMPORTANT DIFFERENCE
Automatic feed detection requires Dalamud's in-game event services and is not
available in the standalone desktop edition. Desktop progress is tracked
manually and saved automatically.

FILES AND PRIVACY
Saved state:
  %APPDATA%\Chocobo Color Calculator\desktop-state.json

Route exports:
  %USERPROFILE%\Documents\Chocobo Color Calculator\Exports

The application does not require an account and does not send route or progress
data anywhere. It checks this project's public GitHub releases once at launch,
when Check for Updates is selected, and when the user chooses to download an
available update.

UPDATES
The running version is shown in the application header and Updates tab. Use
Check for Updates at any time. When a newer desktop release is available,
Download, Install & Relaunch retrieves the ZIP, checks its GitHub SHA-256
digest, safely replaces the application files after shutdown, and reopens the
new version. Windows may request administrator permission only if the app is in
a protected folder.

PROJECT
https://github.com/t0nysama/ChocoboColorCalculator
