// PEPatcher: injects a startup MessageBox into a PE32 executable.
// This is a minimal, best-effort patcher that:
//  - Assumes 32-bit PE (PE32, not PE32+)
//  - Looks for KERNEL32 imports for LoadLibraryA and GetProcAddress
//  - Appends a new executable section containing shellcode to load user32.dll,
//    resolve MessageBoxA, show "Hello", then jump back to the original entry point.
//  - Leaves resources (including icons) untouched.
//
// If the target binary does not meet these assumptions, injectMessageBox
// will return false.
#ifndef PEPATCHER_H
#define PEPATCHER_H

#include <QString>

class PEPatcher
{
public:
    // Inject a MessageBox("Hello") at process start.
    // inputPath: path to source executable
    // outputPath: path to write modified executable (not in-place)
    // Returns true on success.
    static bool injectMessageBox(const QString &inputPath, const QString &outputPath);
};

#endif // PEPATCHER_H
