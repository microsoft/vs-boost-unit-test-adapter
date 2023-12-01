// (C) Copyright ETAS 2015.
// Distributed under the Boost Software License, Version 1.0.
// (See accompanying file LICENSE_1_0.txt or copy at
// http://www.boost.org/LICENSE_1_0.txt)

using Microsoft.Win32.SafeHandles;
using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BoostTestAdapter.Utility
{
    /// <summary>
    /// Wrapper class around the DbgHelp library.
    /// It exposes only the functionalities relevant to BUTA.
    /// </summary>
    public sealed class DebugHelper : IDisposable
    {
        private static class NativeMethods
        {
            public const ushort IMAGE_DOS_SIGNATURE = 0x5A4D;
            public const ushort IMAGE_NT_SIGNATURE = 0x00004550;

            public const uint GENERIC_READ = unchecked(0x80000000);
            public const uint FILE_MAP_READ = 0x0004;
            public const uint FILE_SHARE_READ = 0x00000001;
            public const uint OPEN_EXISTING = 3;
            public const uint PAGE_READONLY = 0x02;

            internal struct LoadedImage
            {
                public IntPtr MappedAddress;
                public IntPtr FileHeader;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal unsafe struct IMAGE_DOS_HEADER
            {
                public ushort e_magic;
                public ushort e_cblp;
                public ushort e_cp;
                public ushort e_crlc;
                public ushort e_cparhdr;
                public ushort e_minalloc;
                public ushort e_maxalloc;
                public ushort e_ss;
                public ushort e_sp;
                public ushort e_csum;
                public ushort e_ip;
                public ushort e_cs;
                public ushort e_lfarlc;
                public ushort e_ovno;
                public fixed ushort e_res1[4];
                public ushort e_oemid;
                public ushort e_oeminfo;
                public fixed ushort e_res2[10];
                public int e_lfanew;
            }

            [StructLayout(LayoutKind.Explicit)]
            internal struct IMAGE_IMPORT_DESCRIPTOR
            {
                [FieldOffset(0)]
                public uint Characteristics;
                [FieldOffset(0)]
                public uint OriginalFirstThunk;

                [FieldOffset(4)]
                public uint TimeDateStamp;
                [FieldOffset(8)]
                public uint ForwarderChain;
                [FieldOffset(12)]
                public uint Name;
                [FieldOffset(16)]
                public uint FirstThunk;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal unsafe struct IMAGE_NT_HEADERS
            {
                public int Signature;
                public fixed byte FileHeader[20];
                public fixed byte OptionalHeader[224];
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            internal struct SYMBOL_INFO
            {
                public uint SizeOfStruct;
                public uint TypeIndex;            // Type Index of symbol
                ulong Reserved0;
                ulong Reserved1;
                public uint Index;
                public uint Size;
                public ulong ModBase;             // Base Address of module containing this symbol
                public uint Flags;
                public ulong Value;               // Value of symbol, ValuePresent should be 1
                public ulong Address;             // Address of symbol including base address of module
                public uint Register;             // Register holding value or pointer to value
                public uint Scope;                // Scope of the symbol
                public uint Tag;                  // PDB classification
                public uint NameLen;              // Length of name stored in buffer
                public uint MaxNameLen;           // Buffer size
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
                public string Name;               // Buffer
            }

            [Flags]
            internal enum SymLoadModuleFlags : uint
            {
                SLMFLAG_NONE = 0,
                SLMFLAG_NO_SYMBOLS = 4,
                SLMFLAG_VIRTUAL = 1
            }

            [Flags]
            internal enum Options : uint
            {
                SYMOPT_ALLOW_ABSOLUTE_SYMBOLS = 0x00000800, // Enables the use of symbols that are stored with absolute addresses. Most symbols are stored as RVAs from the base of the module. DbgHelp translates them to absolute addresses. There are symbols that are stored as an absolute address. These have very specialized purposes and are typically not used.
                SYMOPT_ALLOW_ZERO_ADDRESS = 0x01000000, // Enables the use of symbols that do not have an address. By default, DbgHelp filters out symbols that do not have an address.
                SYMOPT_AUTO_PUBLICS = 0x00010000, // Do not search the public symbols when searching for symbols by address, or when enumerating symbols, unless they were not found in the global symbols or within the current scope. This option has no effect with SYMOPT_PUBLICS_ONLY.
                SYMOPT_CASE_INSENSITIVE = 0x00000001, // All symbol searches are insensitive to case.
                SYMOPT_DEBUG = 0x80000000, // Pass debug output through OutputDebugString or the SymRegisterCallbackProc64 callback function.
                SYMOPT_DEFERRED_LOADS = 0x00000004, // Symbols are not loaded until a reference is made requiring the symbols be loaded. This is the fastest, most efficient way to use the symbol handler.
                SYMOPT_DISABLE_SYMSRV_AUTODETECT = 0x02000000, // Disables the auto-detection of symbol server stores in the symbol path, even without the "SRV*" designation, maintaining compatibility with previous behavior. DbgHelp 6.6 and earlier:  This value is not supported.
                SYMOPT_EXACT_SYMBOLS = 0x00000400, // Do not load an unmatched .pdb file. Do not load export symbols if all else fails.
                SYMOPT_FAIL_CRITICAL_ERRORS = 0x00000200, // Do not display system dialog boxes when there is a media failure such as no media in a drive. Instead, the failure happens silently.
                SYMOPT_FAVOR_COMPRESSED = 0x00800000, // If there is both an uncompressed and a compressed file available, favor the compressed file. This option is good for slow connections.
                SYMOPT_FLAT_DIRECTORY = 0x00400000, // Symbols are stored in the root directory of the default downstream store. DbgHelp 6.1 and earlier:  This value is not supported.
                SYMOPT_IGNORE_CVREC = 0x00000080, // Ignore path information in the CodeView record of the image header when loading a .pdb file.
                SYMOPT_IGNORE_IMAGEDIR = 0x00200000, // Ignore the image directory. DbgHelp 6.1 and earlier:  This value is not supported.
                SYMOPT_IGNORE_NT_SYMPATH = 0x00001000, // Do not use the path specified by _NT_SYMBOL_PATH if the user calls SymSetSearchPath without a valid path. DbgHelp 5.1:  This value is not supported.
                SYMOPT_INCLUDE_32BIT_MODULES = 0x00002000, // When debugging on 64-bit Windows, include any 32-bit modules.
                SYMOPT_LOAD_ANYTHING = 0x00000040, // Disable checks to ensure a file (.exe, .dbg., or .pdb) is the correct file. Instead, load the first file located.
                SYMOPT_LOAD_LINES = 0x00000010, // Loads line number information.
                SYMOPT_NO_CPP = 0x00000008, // All C++ decorated symbols containing the symbol separator "::" are replaced by "__". This option exists for debuggers that cannot handle parsing real C++ symbol names.
                SYMOPT_NO_IMAGE_SEARCH = 0x00020000, // Do not search the image for the symbol path when loading the symbols for a module if the module header cannot be read. DbgHelp 5.1:  This value is not supported.
                SYMOPT_NO_PROMPTS = 0x00080000, // Prevents prompting for validation from the symbol server.
                SYMOPT_NO_PUBLICS = 0x00008000, // Do not search the publics table for symbols. This option should have little effect because there are copies of the public symbols in the globals table. DbgHelp 5.1:  This value is not supported.
                SYMOPT_NO_UNQUALIFIED_LOADS = 0x00000100, // Prevents symbols from being loaded when the caller examines symbols across multiple modules. Examine only the module whose symbols have already been loaded.
                SYMOPT_OVERWRITE = 0x00100000, // Overwrite the downlevel store from the symbol store. DbgHelp 6.1 and earlier:  This value is not supported.
                SYMOPT_PUBLICS_ONLY = 0x00004000, // Do not use private symbols. The version of DbgHelp that shipped with earlier Windows release supported only public symbols; this option provides compatibility with this limitation. DbgHelp 5.1:  This value is not supported.
                SYMOPT_SECURE = 0x00040000, // DbgHelp will not load any symbol server other than SymSrv. SymSrv will not use the downstream store specified in _NT_SYMBOL_PATH. After this flag has been set, it cannot be cleared. DbgHelp 6.0 and 6.1:  This flag can be cleared. DbgHelp 5.1:  This value is not supported.
                SYMOPT_UNDNAME = 0x00000002, // All symbols are presented in undecorated form. This option has no effect on global or local symbols because they are stored undecorated. This option applies only to public symbols.
            }

            [Flags]
            public enum SetErrorFlags : uint
            {
                /// <summary>
                /// Use the system default, which is to display all error dialog boxes.
                /// </summary>
                SEM_DEFAULT = 0,

                /// <summary>
                /// The system does not display the critical-error-handler message box. Instead, the system sends the error to the calling process.
                /// </summary>
                /// <remarks>
                /// Best practice is that all applications call the process-wide SetErrorMode function with a parameter of SEM_FAILCRITICALERRORS at startup. This is to prevent error mode dialogs from hanging the application.
                /// </remarks>
                SEM_FAILCRITICALERRORS = 0x0001,

                /// <summary>
                /// The system automatically fixes memory alignment faults and makes them invisible to the application. It does this for the calling process and any descendant processes. This feature is only supported by certain processor architectures. For more information, see the Remarks section.
                /// </summary>
                /// <remarks>
                /// After this value is set for a process, subsequent attempts to clear the value are ignored.
                /// </remarks>
                SEM_NOALIGNMENTFAULTEXCEPT = 0x0004,

                /// <summary>
                /// The system does not display the Windows Error Reporting dialog.
                /// </summary>
                SEM_NOGPFAULTERRORBOX = 0x0002,

                /// <summary>
                /// The system does not display a message box when it fails to find a file. Instead, the error is returned to the calling process.
                /// </summary>
                SEM_NOOPENFILEERRORBOX = 0x8000
            }

            #region Imports

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern SafeFileHandle CreateFile(
                string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
                uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern SafeFileHandle CreateFileMapping(
                SafeFileHandle hFile, IntPtr lpFileMappingAttributes, uint flProtect,
                uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool GetFileSizeEx(SafeFileHandle hFile, out long lpFileSize);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr MapViewOfFile(
                SafeFileHandle hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh,
                uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);

            [DllImport("Kernel32.dll")]
            internal static extern SetErrorFlags SetErrorMode(SetErrorFlags flags);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

            [DllImport("DbgHelp.dll", SetLastError = true)]
            internal static extern Options SymGetOptions();

            [DllImport("DbgHelp.dll", SetLastError = true)]
            internal static extern uint SymSetOptions(Options options);

            [DllImport("DbgHelp.dll", CharSet = CharSet.Unicode, SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SymInitialize(IntPtr handle, [MarshalAs(UnmanagedType.AnsiBStr)] string userSearchPath, [MarshalAs(UnmanagedType.Bool)] bool invadeProcess);

            [DllImport("DbgHelp.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SymCleanup(IntPtr handle);

            [DllImport("DbgHelp.dll", CharSet = CharSet.Unicode, SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            internal static extern ulong SymLoadModuleEx(IntPtr hProcess, IntPtr hFile, string imageName, string moduleName, ulong baseOfDll, uint dllSize, IntPtr dataZero, SymLoadModuleFlags flags);

            [DllImport("DbgHelp.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public extern static bool SymUnloadModule64(IntPtr hProcess, ulong baseOfDll);
            
            public delegate bool SymEnumSymbolsProc(ref SYMBOL_INFO symInfo, uint symbolSize, IntPtr contextZero);

            [DllImport("DbgHelp.dll", CharSet = CharSet.Unicode, SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public extern static bool SymEnumSymbols(IntPtr hProcess, ulong baseOfDll, string mask, SymEnumSymbolsProc callback, IntPtr contextZero);

            [DllImport("DbgHelp.dll")]
            public static extern unsafe void* ImageDirectoryEntryToData(IntPtr pBase, byte mappedAsImage, ushort directoryEntry, uint* size);

            [DllImport("dbghelp.dll")]
            public static extern IntPtr ImageRvaToVa(IntPtr pNtHeaders, IntPtr pBase, uint rva, IntPtr pLastRvaSection);

            #endregion

        }

        private IntPtr _libHandle;
        private ulong _dllBase;

        public string LastErrorMessage { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="exeName">The executable name</param>
        /// <exception cref="Win32Exception"></exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "BoostTestAdapter.Utility.DebugHelper+NativeMethods.SymSetOptions(BoostTestAdapter.Utility.DebugHelper+NativeMethods+Options)")]
        public DebugHelper(string exeName)
        {
            IntPtr handle = Process.GetCurrentProcess().Handle;
            NativeMethods.SetErrorMode(NativeMethods.SetErrorFlags.SEM_FAILCRITICALERRORS | NativeMethods.SetErrorFlags.SEM_NOOPENFILEERRORBOX);

            NativeMethods.Options options = NativeMethods.SymGetOptions();
            options |= NativeMethods.Options.SYMOPT_DEFERRED_LOADS;
            options |= NativeMethods.Options.SYMOPT_DEBUG;
            NativeMethods.SymSetOptions(options);

            if (!NativeMethods.SymInitialize(handle, null, false))
            {
                var ex = new Win32Exception(Marshal.GetLastWin32Error());
                LastErrorMessage = ex.Message;
            }

            _dllBase = NativeMethods.SymLoadModuleEx(handle,
                IntPtr.Zero,
                exeName,
                null,
                0,
                0,
                IntPtr.Zero,
                NativeMethods.SymLoadModuleFlags.SLMFLAG_NONE);

            if (_dllBase == 0)
            {
                int error = Marshal.GetLastWin32Error();
                Dispose(false);

                throw new Win32Exception(error);
            }

            _libHandle = handle;
        }

        private static bool MapAndLoad(string imageName, out NativeMethods.LoadedImage loadedImage)
        {
            loadedImage = new NativeMethods.LoadedImage();

            long fileSize;
            IntPtr mapAddr;
            using (var hFile = NativeMethods.CreateFile(imageName, NativeMethods.GENERIC_READ,
                NativeMethods.FILE_SHARE_READ, IntPtr.Zero, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero))
            {
                if (hFile.IsInvalid)
                    return false;

                if (!NativeMethods.GetFileSizeEx(hFile, out fileSize))
                    return false;

                using (var hMapping = NativeMethods.CreateFileMapping(hFile, IntPtr.Zero, NativeMethods.PAGE_READONLY, 0, 0, null))
                {
                    if (hMapping.IsInvalid)
                        return false;

                    mapAddr = NativeMethods.MapViewOfFile(hMapping, NativeMethods.FILE_MAP_READ, 0, 0, UIntPtr.Zero);
                    if (mapAddr == IntPtr.Zero)
                        return false;
                }
            }

            unsafe
            {
                if (fileSize < Marshal.SizeOf<NativeMethods.IMAGE_DOS_HEADER>())
                    return false;

                var dosHeader = (NativeMethods.IMAGE_DOS_HEADER*)mapAddr;
                NativeMethods.IMAGE_NT_HEADERS* rawFileHeader;
                if (dosHeader->e_magic == NativeMethods.IMAGE_DOS_SIGNATURE)
                {
                    if (dosHeader->e_lfanew <= 0
                        || fileSize < dosHeader->e_lfanew + Marshal.SizeOf<NativeMethods.IMAGE_NT_HEADERS>())
                    {
                        return false;
                    }

                    rawFileHeader = (NativeMethods.IMAGE_NT_HEADERS*)((byte*)mapAddr + dosHeader->e_lfanew);
                }
                else if (dosHeader->e_magic == NativeMethods.IMAGE_NT_SIGNATURE)
                {
                    if (fileSize < Marshal.SizeOf<NativeMethods.IMAGE_NT_HEADERS>())
                        return false;

                    rawFileHeader = (NativeMethods.IMAGE_NT_HEADERS*)mapAddr;
                }
                else
                {
                    return false;
                }

                if (rawFileHeader->Signature != NativeMethods.IMAGE_NT_SIGNATURE)
                    return false;

                loadedImage.MappedAddress = mapAddr;
                loadedImage.FileHeader = (IntPtr)rawFileHeader;
                return true;
            }
        }

        private static bool UnMapAndLoad(ref NativeMethods.LoadedImage loadedImage)
        {
            if (NativeMethods.UnmapViewOfFile(loadedImage.MappedAddress))
            {
                loadedImage = new NativeMethods.LoadedImage();
                return true;
            }
            return false;
        }

        private static void ParsePeFile(string executable, Action<NativeMethods.LoadedImage> action)
        {
            NativeMethods.LoadedImage image = new NativeMethods.LoadedImage();
            bool loaded = false;
            try
            {
                loaded = MapAndLoad(executable, out image);
                if (loaded)
                    action(image);
            }
            finally
            {
                if (loaded && !UnMapAndLoad(ref image))
                    Logger.Error(Resources.UnMapLoad);
            }
        }

        private static unsafe void ProcessImports(string executable, Func<string, bool> predicate)
        {
            ParsePeFile(executable, (image) =>
            {
                bool shouldContinue = true;
                uint size = 0u;
                var directoryEntry = (NativeMethods.IMAGE_IMPORT_DESCRIPTOR*)NativeMethods.ImageDirectoryEntryToData(image.MappedAddress, 0, 1, &size);
                if (directoryEntry == null)
                {
                    Logger.Error(Resources.ImageDirectoryEntryToData);
                    return;
                }
                while (shouldContinue && directoryEntry->OriginalFirstThunk != 0u)
                {
                    shouldContinue = predicate(GetString(image, directoryEntry->Name));
                    directoryEntry++;
                }
            });
        }

        public static bool FindImport(string executable, string import, StringComparison comparisonType)
        {
            var found = false;
            ProcessImports(executable, (currentImport) =>
            {
                found = currentImport.StartsWith(import, comparisonType);
                return !found; // Continue only if not found yet.
            });
            return found;
        }

        private static string PtrToStringUtf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return null;

            int size = 0;
            while (Marshal.ReadByte(ptr, size) != 0)
                ++size;

            byte[] buffer = new byte[size];
            Marshal.Copy(ptr, buffer, 0, buffer.Length);

            return Encoding.UTF8.GetString(buffer);
        }

        private static string GetString(NativeMethods.LoadedImage image, uint offset)
        {
            IntPtr stringPtr = NativeMethods.ImageRvaToVa(image.FileHeader, image.MappedAddress, offset, IntPtr.Zero);
            return PtrToStringUtf8(stringPtr);
        }

        #region IDisposable

        ~DebugHelper()
        {
            Dispose(false);
        }

        private bool _disposed = false;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "disposing")]
        private void Dispose(bool disposing)
        {
            if (_disposed)
            { 
                return;
            }

            if (_libHandle == IntPtr.Zero)
            { 
                return;
            }

            if (_dllBase != 0)
            { 
                NativeMethods.SymUnloadModule64(_libHandle, _dllBase);
            }

            NativeMethods.SymCleanup(_libHandle);

            _libHandle = IntPtr.Zero;

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion IDisposable

        /// <summary>
        /// Performs a symbol lookup via a wildcard pattern and enumerates the fully-qualified name of
        /// the symbols in question
        /// </summary>
        /// <param name="mask">The wildcard symbol name pattern</param>
        /// <returns>A collection of symbols which match the provided wildcard pattern</returns>
        public IEnumerable<string> LookupSymbolNamesByPattern(string mask)
        {
            var symbols = new List<string>();

            NativeMethods.SymEnumSymbolsProc callback = (ref NativeMethods.SYMBOL_INFO symInfo, uint symbolSize, IntPtr context) =>
            {
                symbols.Add(symInfo.Name);
                return true;
            };
            
            var result = NativeMethods.SymEnumSymbols(_libHandle, _dllBase, mask, callback, IntPtr.Zero);

            return (result) ? symbols : Enumerable.Empty<string>();
        }
    }
}
