# Smart File Organizer

A lightweight, background Windows Service written in C# (.NET) that automatically monitors your Downloads folder and sorts incoming files into categorized subfolders (Images, Documents, Executables, etc.) in real-time.

## Features
* **Event-Driven:** Uses "FileSystemWatcher" to react instantly to new files without consuming CPU cycles with continuous polling.
* **Smart Debouncing:** Automatically detects if a browser is still downloading/writing to a file and waits for the lock to release before moving it.
* **Collision Prevention:** If a file with the same name already exists in the destination (e.g., "invoice.pdf"), it safely auto-renames the new file (e.g., "invoice (1).pdf").
* **Dynamic Paths:** Automatically detects the current logged-in Windows user's profile, making it plug-and-play on any machine.

## Built With
* C# 
* .NET Worker Service architecture
* Asynchronous Programming ("async" / "await")

## How to Run Locally

**1. Get the code**  
Clone this repository to your local machine by running this command in your terminal:       
"git clone https://github.com/YOUR_USERNAME/SmartFileOrganizer.git"

**2. Open the Project**  
Locate the cloned folder and double-click the "SmartFileOrganizer.sln" file to open it in Visual Studio 2022.

**3. Start the Service**  
Press "F5" on your keyboard (or click the green "Play" button). A console window will appear confirming it is watching your Downloads folder.

**4. Test the Automation**  
Download any file from your web browser and watch the console update as it detects, waits for the download to finish, and moves the file to the correct subfolder!

---

## How to Customize / Use It for Yourself

You don't just have to use my default categories. You can easily modify the code to fit your exact workflow.

**1. Add New File Extensions**
Open "Worker.cs" and scroll down to the "GetCategoryForExtension" method. It acts as a dictionary. You can easily add new extensions to existing categories:

"// Example: Adding .gif and .svg to the Images category"
"'.jpg' or '.png' or '.gif' or '.svg' => 'Images',"

**2. Create Custom Folders**
Want a specific folder just for videos or 3D models? Just add a new line to the switch statement:

"'.mp4' or '.mkv' or '.avi' => 'Videos',"
"'.obj' or '.stl' or '.fbx' => '3D Models',"

The program will automatically create these folders in your Downloads directory the next time you download a matching file!

**3. Change the Target Directory**
By default, this sorts files inside your Downloads folder. If you want it to move files directly to your Windows Documents or Desktop folder, change this line inside "ExecuteAsync":

"// Change 'Downloads' to 'Desktop' or 'Documents'"
"_downloadsPath = Path.Combine(userProfile, 'Downloads');"

---

## Architecture Notes
This project was built using the .NET "BackgroundService" class. It demonstrates handling OS-level file system events, asynchronous file stream locks, and defensive programming against I/O exceptions.
