// --- IMPORTS (Namespaces) ---
using System;
using System.IO;             // Provides classes for working with files and folders (File, Directory, Path)
using System.Threading;      // Allows us to use CancellationToken (to stop the app gracefully)
using System.Threading.Tasks;// Allows us to use asynchronous programming (Task, async, await)
using Microsoft.Extensions.Hosting; // Provides the BackgroundService base class
using Microsoft.Extensions.Logging; // Provides the logging system to print messages

namespace SmartFileOrganizer
{
    // BackgroundService is a built-in .NET class specifically designed for long-running background tasks.
    public class Worker : BackgroundService
    {
        // We use a logger to print messages to the console instead of Console.WriteLine.
        // It is more professional and includes timestamps automatically.
        private readonly ILogger<Worker> _logger;

        // A class-level variable to store the path so all methods can see it.
        private string _downloadsPath = string.Empty;

        // --- CONSTRUCTOR ---
        // This is called exactly once when the program starts.
        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        // --- MAIN ENTRY POINT ---
        // ExecuteAsync is the "engine" of the BackgroundService. It runs continuously.
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 1. DYNAMIC PATH GENERATION
            // Instead of hardcoding "C:\Users\Name\Downloads", we ask Windows who is currently logged in.
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // Path.Combine safely joins folders together with the correct slashes (e.g., "\")
            _downloadsPath = Path.Combine(userProfile, "Downloads");

            // 2. SET UP THE WATCHER
            // 'using' means the computer will clean up the watcher from memory when the app closes.
            using var watcher = new FileSystemWatcher(_downloadsPath);

            // We only care about two things: when a file's name appears, and when it is created.
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime;

            // 3. WIRE UP THE EVENT
            // When the watcher triggers the 'Created' event, we redirect it to our custom 'OnFileCreatedAsync' method.
            // We use 'async (sender, e) => await ...' because our method takes time (waits for downloads).
            watcher.Created += async (sender, e) => await OnFileCreatedAsync(e);

            // Turn on the watcher so it starts listening!
            watcher.EnableRaisingEvents = true;

            _logger.LogInformation($"Watching folder: {_downloadsPath}");

            // 4. THE HEARTBEAT LOOP
            // This loop keeps the program running. If we remove this, the program instantly exits.
            // 'stoppingToken' becomes true when you close the console window.
            while (!stoppingToken.IsCancellationRequested)
            {
                // Pause for 1 second, then loop again. This prevents the CPU from spiking to 100%.
                await Task.Delay(1000, stoppingToken);
            }
        }

        // --- OUR CUSTOM EVENT METHOD ---
        // This runs every time a new file lands in the Downloads folder.
        private async Task OnFileCreatedAsync(FileSystemEventArgs e)
        {
            string filePath = e.FullPath; // The full path: C:\Users\...\Downloads\image.jpg
            string fileName = e.Name;     // Just the name: image.jpg

            // STEP 1: Ignore temporary browser files
            // Browsers create weird files while downloading. We don't want to touch them yet.
            if (fileName.EndsWith(".crdownload") || fileName.EndsWith(".part") || fileName.EndsWith(".tmp"))
            {
                return; // 'return' stops the method immediately. We ignore the file.
            }

            _logger.LogInformation($"New file detected: {fileName}. Waiting for download to finish...");

            // STEP 2: Wait for the download to actually finish
            // This prevents "Access Denied" or "File in Use" errors.
            bool isReady = await WaitForFileToUnlockAsync(filePath);

            // If 60 seconds passed and it's still locked, give up and leave it alone.
            if (!isReady)
            {
                _logger.LogWarning($"Could not access {fileName}. It might be stuck or still downloading.");
                return;
            }

            // STEP 3: Categorize the file
            // Extract the extension (e.g., ".jpg") and make it lowercase so ".JPG" and ".jpg" are treated the same.
            string extension = Path.GetExtension(filePath).ToLower();

            // Ask our helper method which folder this extension belongs to.
            string categoryFolder = GetCategoryForExtension(extension);

            // If it's an unrecognized file type, we just leave it in the Downloads folder.
            if (categoryFolder == "Others")
            {
                _logger.LogInformation($"Ignoring {fileName} (No category assigned).");
                return;
            }

            // STEP 4: Prepare the destination
            // Combine the base path with the category (e.g., C:\Users\...\Downloads\Images)
            string targetDirectory = Path.Combine(_downloadsPath, categoryFolder);

            // CreateDirectory is safe. If the "Images" folder already exists, it does nothing.
            Directory.CreateDirectory(targetDirectory);

            // STEP 5: Prevent overwriting existing files
            // Get a safe file name (e.g., if image.jpg exists, this returns image (1).jpg)
            string finalDestPath = GetUniqueFilePath(targetDirectory, fileName);

            // STEP 6: Move the file!
            try
            {
                // Physically move the file from the root Downloads folder into the subfolder
                File.Move(filePath, finalDestPath);
                _logger.LogInformation($"Success: Moved {fileName} to {categoryFolder}");
            }
            catch (Exception ex)
            {
                // If anything goes completely wrong (like hard drive full), log the error so we can fix it.
                _logger.LogError($"Error moving {fileName}: {ex.Message}");
            }
        }

        // --- HELPER METHODS ---

        // Checks if a file is still being downloaded by the browser.
        private async Task<bool> WaitForFileToUnlockAsync(string filePath)
        {
            int maxAttempts = 60; // We will try 60 times.
            int delayMilliseconds = 1000; // We will wait 1 second between tries.

            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    // We try to open the file with 'FileShare.None' (Exclusive access).
                    // If the browser is still downloading it, Windows will throw an error.
                    using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        // If we got here, no error was thrown! The file is fully ours.
                        return true;
                    }
                }
                catch (IOException)
                {
                    // An error was thrown! The browser is still using it. 
                    // Pause the method for 1 second, then the loop will try again.
                    await Task.Delay(delayMilliseconds);
                }
            }
            // If we looped 60 times and still couldn't get it, return false.
            return false;
        }

        // Acts as a dictionary to route file extensions to folder names.
        private string GetCategoryForExtension(string extension)
        {
            // This is a modern C# "switch expression". It is cleaner than standard if/else statements.
            return extension switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => "Images",
                ".pdf" or ".docx" or ".txt" or ".xlsx" or ".pptx" => "Documents",
                ".exe" or ".msi" => "Executables",
                ".zip" or ".rar" or ".7z" => "Archives",
                // The underscore (_) means "default". If it doesn't match above, return "Others".
                _ => "Others"
            };
        }

        // Ensures we don't accidentally delete an old file if a new file has the exact same name.
        private string GetUniqueFilePath(string folderPath, string fileName)
        {
            string finalPath = Path.Combine(folderPath, fileName);
            int count = 1;

            // Split the file into its name and its extension (e.g., "invoice" and ".pdf")
            string nameOnly = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);

            // While a file with this name ALREADY exists in the destination folder...
            while (File.Exists(finalPath))
            {
                // Create a new name like "invoice (1).pdf"
                string newName = $"{nameOnly} ({count}){extension}";

                // Build the new full path to check on the next loop iteration
                finalPath = Path.Combine(folderPath, newName);
                count++; // Increase the number for the next try (invoice (2).pdf)
            }

            // Once the while loop finishes, we are guaranteed to have a path that is free to use!
            return finalPath;
        }
    }
}