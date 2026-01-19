ExecutableEmbedder Module - Usage Instructions
===============================================

This module allows you to create a single executable file that:
1. Contains an embedded executable (encrypted)
2. Checks hardware key before running
3. Only runs if hardware key matches
4. Decrypts and executes the embedded executable if authorized

HOW IT WORKS:
-------------
1. The original executable is encrypted with a hardware key
2. The encrypted data is appended to a wrapper executable
3. When the wrapper runs, it:
   - Extracts the embedded data from itself
   - Verifies the current hardware key matches
   - If valid: decrypts and runs the embedded executable
   - If invalid: shows error and exits

USAGE:
------

Step 1: Create a Wrapper Executable
------------------------------------
You need to compile wrapper_main.cpp to create a wrapper executable template.

Option A: Compile wrapper_main.cpp separately:
  - Create a new Qt project with wrapper_main.cpp
  - Include executableembedder.h and link to the required libraries
  - Compile to create wrapper_template.exe
  - Place wrapper_template.exe in your project directory

Option B: Use an existing executable as wrapper:
  - Any executable can be used as a wrapper
  - The wrapper just needs to call ExecutableEmbedder::runEmbeddedExecutable()
  - You can modify any Qt application to include this call

Step 2: Create Embedded Executable
-----------------------------------
Use ExecutableEmbedder::createEmbeddedExecutable():

    QString originalExe = "C:/path/to/original.exe";
    QString outputExe = "C:/path/to/protected.exe";
    QString hardwareKey = HardwareFingerprint::generateHardwareKey();
    
    bool success = ExecutableEmbedder::createEmbeddedExecutable(
        originalExe,
        outputExe,
        hardwareKey
    );

This will:
- Encrypt the original executable
- Copy the wrapper template (or use existing executable)
- Append the encrypted data to the wrapper
- Create a single standalone executable

Step 3: Run the Protected Executable
-------------------------------------
Simply run the output executable. It will:
- Check hardware key automatically
- Run the embedded executable if authorized
- Show error if hardware key doesn't match

FILE STRUCTURE:
---------------
The embedded executable file structure:
[Wrapper Executable Code]
[PHILIPS_EMBEDDED_V1]  <- Marker (17 bytes)
[Hardware Key Hash]    <- SHA-256 hex (64 bytes)
[Encrypted Data Size]  <- 8 bytes (qint64)
[Encrypted Executable] <- Variable size

NOTES:
------
- The wrapper executable must be compiled with ExecutableEmbedder support
- The embedded executable is encrypted with AES-256
- Hardware key verification happens at runtime
- The decrypted executable is temporarily written to disk before execution
- Original executable remains unchanged

EXAMPLE CODE:
-------------
See the UI integration in mod.cpp for a complete example of how to use this module.
