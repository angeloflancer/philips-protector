// Minimal PE32 patcher to prepend a Hello MessageBox at process start.
// Limitations:
//  - Supports PE32 (32-bit) only.
//  - Requires KERNEL32 imports for LoadLibraryA and GetProcAddress.
//  - Uses preferred image base (ASLR relocations not handled).
//  - Appends a new executable section with shellcode; resources/icons are untouched.
#include "pepatcher.h"

#include <QByteArray>
#include <QFile>
#include <QString>
#include <QDebug>
#include <algorithm>
#include <cstring>
#include <windows.h>

namespace {

constexpr DWORD alignUp(DWORD value, DWORD alignment)
{
    return (value + alignment - 1) & ~(alignment - 1);
}

// Convert RVA to file offset using section headers.
DWORD rvaToOffset(DWORD rva, const IMAGE_NT_HEADERS32 *nt, const IMAGE_SECTION_HEADER *sections)
{
    const WORD secCount = nt->FileHeader.NumberOfSections;
    for (WORD i = 0; i < secCount; ++i) {
        DWORD va = sections[i].VirtualAddress;
        DWORD size = std::max(sections[i].Misc.VirtualSize, sections[i].SizeOfRawData);
        if (rva >= va && rva < va + size) {
            return sections[i].PointerToRawData + (rva - va);
        }
    }
    return rva; // Fallback: may work for headers
}

} // namespace

bool PEPatcher::injectMessageBox(const QString &inputPath, const QString &outputPath)
{
    QFile f(inputPath);
    if (!f.open(QIODevice::ReadOnly)) {
        return false;
    }
    QByteArray data = f.readAll();
    f.close();

    if (data.size() < static_cast<int>(sizeof(IMAGE_DOS_HEADER))) {
        return false;
    }

    auto *dos = reinterpret_cast<IMAGE_DOS_HEADER *>(data.data());
    if (dos->e_magic != IMAGE_DOS_SIGNATURE) {
        return false;
    }
    if (dos->e_lfanew <= 0 || dos->e_lfanew + static_cast<long>(sizeof(IMAGE_NT_HEADERS32)) > data.size()) {
        return false;
    }

    auto *nt = reinterpret_cast<IMAGE_NT_HEADERS32 *>(data.data() + dos->e_lfanew);
    if (nt->Signature != IMAGE_NT_SIGNATURE) {
        qDebug() << "Invalid PE signature";
        return false;
    }
    if (nt->OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR32_MAGIC) {
        qDebug() << "Not a 32-bit PE file (Magic:" << QString::number(nt->OptionalHeader.Magic, 16) << ")";
        return false; // Only PE32 supported
    }

    auto *sections = reinterpret_cast<IMAGE_SECTION_HEADER *>(
        reinterpret_cast<char *>(&nt->OptionalHeader) + nt->FileHeader.SizeOfOptionalHeader);

    const WORD sectionCount = nt->FileHeader.NumberOfSections;
    if (sectionCount == 0) {
        qDebug() << "PE file has no sections";
        return false;
    }

    // Check if there is room for one more section header before first section raw data.
    const auto headerEnd = dos->e_lfanew + sizeof(IMAGE_NT_HEADERS32) +
                           sectionCount * sizeof(IMAGE_SECTION_HEADER);
    const bool canAddSection = (headerEnd + static_cast<long>(sizeof(IMAGE_SECTION_HEADER)) <=
                                sections[0].PointerToRawData);

    // Locate kernel32 imports for LoadLibraryA and GetProcAddress
    DWORD importRva = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT].VirtualAddress;
    if (importRva == 0) {
        qDebug() << "PE file has no import directory";
        return false;
    }
    DWORD importOffset = rvaToOffset(importRva, nt, sections);
    if (importOffset >= static_cast<DWORD>(data.size())) {
        return false;
    }

    DWORD loadLibraryIatRva = 0;
    DWORD getProcAddressIatRva = 0;

    auto *importDesc = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR *>(data.data() + importOffset);
    for (; importDesc->Name != 0; ++importDesc) {
        DWORD nameOffset = rvaToOffset(importDesc->Name, nt, sections);
        if (nameOffset >= static_cast<DWORD>(data.size())) {
            continue;
        }
        const char *dllName = data.constData() + nameOffset;
        QByteArray dllUpper = QByteArray(dllName).toUpper();
        if (!dllUpper.contains("KERNEL32")) {
            continue;
        }

        DWORD origThunkRva = importDesc->OriginalFirstThunk ? importDesc->OriginalFirstThunk
                                                            : importDesc->FirstThunk;
        DWORD thunkOffset = rvaToOffset(origThunkRva, nt, sections);
        DWORD iatOffset = rvaToOffset(importDesc->FirstThunk, nt, sections);
        if (thunkOffset >= static_cast<DWORD>(data.size()) ||
            iatOffset >= static_cast<DWORD>(data.size())) {
            continue;
        }

        auto *thunkData = reinterpret_cast<IMAGE_THUNK_DATA32 *>(data.data() + thunkOffset);
        auto *iat = reinterpret_cast<IMAGE_THUNK_DATA32 *>(data.data() + iatOffset);

        for (int idx = 0; thunkData[idx].u1.AddressOfData != 0; ++idx) {
            DWORD hintNameRva = thunkData[idx].u1.AddressOfData;
            if ((hintNameRva & IMAGE_ORDINAL_FLAG32) != 0) {
                continue; // Skip ordinals
            }
            DWORD hintNameOffset = rvaToOffset(hintNameRva, nt, sections);
            if (hintNameOffset >= static_cast<DWORD>(data.size())) {
                continue;
            }
            auto *hintName = reinterpret_cast<IMAGE_IMPORT_BY_NAME *>(data.data() + hintNameOffset);
            const char *funcName = reinterpret_cast<const char *>(hintName->Name);
            if (std::strcmp(funcName, "LoadLibraryA") == 0) {
                loadLibraryIatRva = importDesc->FirstThunk + idx * sizeof(DWORD);
            } else if (std::strcmp(funcName, "GetProcAddress") == 0) {
                getProcAddressIatRva = importDesc->FirstThunk + idx * sizeof(DWORD);
            }
        }
    }

    if (loadLibraryIatRva == 0 || getProcAddressIatRva == 0) {
        qDebug() << "Required kernel32 imports not found (LoadLibraryA or GetProcAddress)";
        return false; // required imports not found
    }

    const DWORD imageBase = nt->OptionalHeader.ImageBase;
    const DWORD origEntryRva = nt->OptionalHeader.AddressOfEntryPoint;

    // Prepare shellcode and data
    const QByteArray user32Str = "user32.dll";
    const QByteArray msgBoxStr = "MessageBoxA";
    const QByteArray helloStr = "Hello";

    // Offsets within the new section
    // Layout: [code][user32][msgBox][hello]
    std::vector<quint8> shellcode;
    auto append8 = [&](quint8 v) { shellcode.push_back(v); };
    auto append32 = [&](quint32 v) {
        for (int i = 0; i < 4; ++i) shellcode.push_back(static_cast<quint8>((v >> (8 * i)) & 0xFF));
    };

    const DWORD codeRva = 0; // start of section

    // Strings placed after code; we'll compute offsets after code assembled.
    // Build code:
    // pushad
    append8(0x60);
    // pushfd
    append8(0x9C);

    // Call LoadLibraryA("user32.dll")
    append8(0x68); // push imm32
    // placeholder for user32 string addr
    DWORD user32AddrPlaceholderOffset = shellcode.size();
    append32(0);
    append8(0xFF); append8(0x15); // call [abs]
    DWORD loadLibAddrPlaceholderOffset = shellcode.size();
    append32(0);

    // eax now holds module handle
    // Call GetProcAddress(hModule, "MessageBoxA")
    append8(0x68); // push imm32 (ptr to "MessageBoxA")
    DWORD msgBoxNamePlaceholderOffset = shellcode.size();
    append32(0);
    append8(0x50); // push eax (hModule)
    append8(0xFF); append8(0x15); // call [abs] GetProcAddress
    DWORD getProcAddrPlaceholderOffset = shellcode.size();
    append32(0);

    // eax now = MessageBoxA
    // push args: MB_OK(0), title, text, hWnd NULL
    append8(0x6A); append8(0x00); // push 0 (uType)
    append8(0x68); // push title
    DWORD titlePlaceholderOffset = shellcode.size();
    append32(0);
    append8(0x68); // push text
    DWORD textPlaceholderOffset = shellcode.size();
    append32(0);
    append8(0x6A); append8(0x00); // push 0 (hWnd)
    append8(0xFF); append8(0xD0); // call eax

    // restore flags/registers
    append8(0x9D); // popfd
    append8(0x61); // popad

    // jump to original entry point (absolute)
    append8(0xB8); // mov eax, imm32
    DWORD oepPlaceholderOffset = shellcode.size();
    append32(0);
    append8(0xFF); append8(0xE0); // jmp eax

    const DWORD codeSize = static_cast<DWORD>(shellcode.size());
    const DWORD user32Offset = codeSize;
    const DWORD msgBoxOffset = user32Offset + user32Str.size() + 1;
    const DWORD helloOffset = msgBoxOffset + msgBoxStr.size() + 1;

    const DWORD finalCodeSize = helloOffset + helloStr.size() + 1;

    // Compute new section placement
    IMAGE_SECTION_HEADER &lastSec = sections[sectionCount - 1];
    const DWORD fileAlign = nt->OptionalHeader.FileAlignment;
    const DWORD sectionAlign = nt->OptionalHeader.SectionAlignment;
    const DWORD newRawPtr = alignUp(lastSec.PointerToRawData + lastSec.SizeOfRawData, fileAlign);
    const DWORD newVA = alignUp(lastSec.VirtualAddress +
                                    std::max(lastSec.Misc.VirtualSize, lastSec.SizeOfRawData),
                                sectionAlign);
    const DWORD newSectionSizeRaw = alignUp(finalCodeSize, fileAlign);
    const DWORD newSectionSizeVA = alignUp(finalCodeSize, sectionAlign);

    // Determine if we can add a new section or need to extend the last one
    bool addedNewSection = false;
    IMAGE_SECTION_HEADER *targetSectionHeader = nullptr;

    if (canAddSection) {
        // Add a new section header
        targetSectionHeader = sections + sectionCount;
        std::memset(targetSectionHeader, 0, sizeof(IMAGE_SECTION_HEADER));
        std::memcpy(targetSectionHeader->Name, ".hmsg", 5);
        targetSectionHeader->VirtualAddress = newVA;
        targetSectionHeader->Misc.VirtualSize = finalCodeSize;
        targetSectionHeader->SizeOfRawData = newSectionSizeRaw;
        targetSectionHeader->PointerToRawData = newRawPtr;
        targetSectionHeader->Characteristics =
            IMAGE_SCN_MEM_READ | IMAGE_SCN_MEM_EXECUTE | IMAGE_SCN_CNT_CODE;
        addedNewSection = true;
        qDebug() << "Adding new section .hmsg";
    } else {
        // Fallback: extend the last section (make it executable if needed)
        targetSectionHeader = &lastSec;
        qDebug() << "No room for new section header; extending last section:" << 
                    (const char*)lastSec.Name;
        
        // Ensure the section is executable and readable
        targetSectionHeader->Characteristics |= 
            (IMAGE_SCN_MEM_EXECUTE | IMAGE_SCN_MEM_READ | IMAGE_SCN_CNT_CODE);
        
        // Update section sizes to accommodate new code
        const DWORD neededRaw = (newRawPtr - lastSec.PointerToRawData) + newSectionSizeRaw;
        const DWORD neededVA = (newVA - lastSec.VirtualAddress) + newSectionSizeVA;
        targetSectionHeader->SizeOfRawData = alignUp(neededRaw, fileAlign);
        targetSectionHeader->Misc.VirtualSize = alignUp(neededVA, sectionAlign);
    }

    // Absolute addresses for placeholders
    const DWORD baseNewSectionVA = nt->OptionalHeader.ImageBase + newVA;
    const DWORD user32Abs = baseNewSectionVA + user32Offset;
    const DWORD msgBoxAbs = baseNewSectionVA + msgBoxOffset;
    const DWORD helloAbs = baseNewSectionVA + helloOffset;
    const DWORD loadLibAbs = imageBase + loadLibraryIatRva;
    const DWORD getProcAbs = imageBase + getProcAddressIatRva;
    const DWORD oepAbs = imageBase + origEntryRva;

    auto patch32 = [&](DWORD offset, DWORD value) {
        shellcode[offset + 0] = static_cast<quint8>(value & 0xFF);
        shellcode[offset + 1] = static_cast<quint8>((value >> 8) & 0xFF);
        shellcode[offset + 2] = static_cast<quint8>((value >> 16) & 0xFF);
        shellcode[offset + 3] = static_cast<quint8>((value >> 24) & 0xFF);
    };

    patch32(user32AddrPlaceholderOffset, user32Abs);
    patch32(loadLibAddrPlaceholderOffset, loadLibAbs);
    patch32(msgBoxNamePlaceholderOffset, msgBoxAbs);
    patch32(getProcAddrPlaceholderOffset, getProcAbs);
    patch32(titlePlaceholderOffset, helloAbs); // title = "Hello"
    patch32(textPlaceholderOffset, helloAbs);  // text = "Hello"
    patch32(oepPlaceholderOffset, oepAbs);

    // Build new section data
    QByteArray sectionData;
    sectionData.resize(newSectionSizeRaw);
    std::fill(sectionData.begin(), sectionData.end(), 0);
    std::memcpy(sectionData.data(), shellcode.data(), shellcode.size());
    std::memcpy(sectionData.data() + user32Offset, user32Str.constData(), user32Str.size());
    std::memcpy(sectionData.data() + msgBoxOffset, msgBoxStr.constData(), msgBoxStr.size());
    std::memcpy(sectionData.data() + helloOffset, helloStr.constData(), helloStr.size());

    // Append new section raw data to file image
    const int requiredSize = static_cast<int>(newRawPtr + newSectionSizeRaw);
    if (data.size() < requiredSize) {
        data.resize(requiredSize);
    }
    std::memcpy(data.data() + newRawPtr, sectionData.constData(), newSectionSizeRaw);

    // Update headers
    if (addedNewSection) {
        nt->FileHeader.NumberOfSections += 1;
    }
    nt->OptionalHeader.AddressOfEntryPoint = newVA;
    nt->OptionalHeader.SizeOfImage = alignUp(newVA + newSectionSizeVA, sectionAlign);

    // Save modified file
    QFile out(outputPath);
    if (!out.open(QIODevice::WriteOnly | QIODevice::Truncate)) {
        return false;
    }
    const auto written = out.write(data);
    out.close();
    return written == data.size();
}
