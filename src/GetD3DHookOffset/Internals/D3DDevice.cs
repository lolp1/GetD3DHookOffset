using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// ReSharper disable InconsistentNaming

namespace GetD3DHookOffset.NewFolder1
{
    public abstract class D3DDevice : IDisposable
    {
        readonly string _d3DDllName;

        readonly List<IntPtr> _loadedLibraries = new List<IntPtr>();
        protected readonly IntPtr D3DDevicePtr;
        protected readonly Process TargetProcess;

        bool _disposed;

        IntPtr _myD3DDll;
        IntPtr _theirD3DDll;

        protected D3DDevice(Process targetProcess, string d3DDllName)
        {
            TargetProcess = targetProcess;
            _d3DDllName = d3DDllName;
            Form = new Form();
            LoadDll();
            // ReSharper disable once VirtualMemberCallInConstructor
            InitD3D(out D3DDevicePtr);
        }

        protected Form Form { get; }

        public abstract int EndSceneVtableIndex { get; }
        public abstract int PresentVtableIndex { get; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected abstract void InitD3D(out IntPtr d3DDevicePtr);

        protected abstract void CleanD3D();

        void LoadDll()
        {
            _myD3DDll = LoadLibrary(_d3DDllName);
            if (_myD3DDll == IntPtr.Zero)
            {
                throw new Exception($"Could not load {_d3DDllName}");
            }

            _theirD3DDll =
                TargetProcess.Modules.Cast<ProcessModule>().First(m => m.ModuleName == _d3DDllName).BaseAddress;
        }

        protected IntPtr LoadLibrary(string library)
        {
            // Attempt to grab the module handle if its loaded already.
            var ret = NativeMethods.GetModuleHandle(library);
            if (ret == IntPtr.Zero)
            {
                // Load the lib manually if its not, storing it in a list so we can free it later.
                ret = NativeMethods.LoadLibrary(library);
                _loadedLibraries.Add(ret);
            }
            return ret;
        }

        protected unsafe IntPtr GetVTableFuncAddress(IntPtr obj, int funcIndex)
        {
            var pointer = *(IntPtr*)(void*)obj;
            return *(IntPtr*)(void*)(pointer + funcIndex * IntPtr.Size);
        }

        public unsafe IntPtr GetDeviceVTableFuncAbsoluteAddress(int funcIndex)
        {
            //Expect the unexpected
            var pointer = *(IntPtr*)(void*)D3DDevicePtr;
            pointer = *(IntPtr*)(void*)(pointer + funcIndex * IntPtr.Size);
            var offset = new IntPtr(pointer.ToInt64() - _myD3DDll.ToInt64());
            return new IntPtr(_theirD3DDll.ToInt64() + offset.ToInt64());
        }

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    CleanD3D();
                    Form?.Dispose();

                    foreach (var loadedLibrary in _loadedLibraries)
                    {
                        NativeMethods.FreeLibrary(loadedLibrary);
                    }
                }

                _disposed = true;
            }
        }

        ~D3DDevice()
        {
            Dispose(false);
        }

        internal static class NativeMethods
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr LoadLibrary(string lpFileName);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool FreeLibrary(IntPtr hModule);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate void VTableFuncDelegate(IntPtr instance);
    }
}