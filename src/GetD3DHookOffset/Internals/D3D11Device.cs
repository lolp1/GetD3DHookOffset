using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace GetD3DHookOffset.NewFolder1
{
    public sealed class D3D11Device : D3DDevice
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        public delegate int DxgiSwapChainPresentDelegate(IntPtr swapChainPtr, int syncInterval, int flags);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        public delegate int DxgiSwapChainResizeTargetDelegate(
            IntPtr swapChainPtr, ref ModeDescription newTargetParameters);

        const int DXGI_FORMAT_R8G8B8A8_UNORM = 0x1C;
        const int DXGI_USAGE_RENDER_TARGET_OUTPUT = 0x20;
        const int D3D11_SDK_VERSION = 7;
        const int D3D_DRIVER_TYPE_HARDWARE = 1;
        IntPtr _device;
        VTableFuncDelegate _deviceContextRelease;

        VTableFuncDelegate _deviceRelease;

        IntPtr _myDxgiDll;

        IntPtr _swapChain;
        VTableFuncDelegate _swapchainRelease;
        IntPtr _theirDxgiDll;

        public D3D11Device(Process targetProc) : base(targetProc, "d3d11.dll")
        {
        }

        public override int EndSceneVtableIndex => VTableIndexes.D3D11DeviceContextEnd;

        public override int PresentVtableIndex => VTableIndexes.DXGISwapChainPresent;

        protected override void InitD3D(out IntPtr d3DDevicePtr)
        {
            LoadDxgiDll();
            var scd = new SwapChainDescription
            {
                BufferCount = 1,
                ModeDescription = new ModeDescription { Format = DXGI_FORMAT_R8G8B8A8_UNORM },
                Usage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
                OutputHandle = Form.Handle,
                SampleDescription = new SampleDescription { Count = 1 },
                IsWindowed = true
            };

            unsafe
            {
                var pSwapChain = IntPtr.Zero;
                var pDevice = IntPtr.Zero;
                var pImmediateContext = IntPtr.Zero;

                var ret = D3D11CreateDeviceAndSwapChain((void*)IntPtr.Zero, D3D_DRIVER_TYPE_HARDWARE,
                    (void*)IntPtr.Zero, 0, (void*)IntPtr.Zero, 0, D3D11_SDK_VERSION, &scd, &pSwapChain, &pDevice,
                    (void*)IntPtr.Zero, &pImmediateContext);

                _swapChain = pSwapChain;
                _device = pDevice;
                d3DDevicePtr = pImmediateContext;

                if (ret >= 0)
                {
                    var vTableFuncAddress = GetVTableFuncAddress(_swapChain, VTableIndexes.DXGISwapChainRelease);
                    _swapchainRelease = Marshal.GetDelegateForFunctionPointer<VTableFuncDelegate>(vTableFuncAddress);

                    var deviceptr = GetVTableFuncAddress(_device, VTableIndexes.D3D11DeviceRelease);
                    _deviceRelease = Marshal.GetDelegateForFunctionPointer<VTableFuncDelegate>(deviceptr);

                    var contex = GetVTableFuncAddress(d3DDevicePtr, VTableIndexes.D3D11DeviceContextRelease);
                    _deviceContextRelease = Marshal.GetDelegateForFunctionPointer<VTableFuncDelegate>(contex);
                }
            }
        }

        void LoadDxgiDll()
        {
            _myDxgiDll = LoadLibrary("dxgi.dll");
            if (_myDxgiDll == IntPtr.Zero)
            {
                throw new Exception("Could not load dxgi.dll");
            }

            _theirDxgiDll =
                TargetProcess.Modules.Cast<ProcessModule>().First(m => m.ModuleName == "dxgi.dll").BaseAddress;
        }

        public unsafe IntPtr GetSwapVTableFuncAbsoluteAddress(int funcIndex)
        {
            var pointer = *(IntPtr*)(void*)_swapChain;
            pointer = *(IntPtr*)(void*)(pointer + funcIndex * IntPtr.Size);

            var offset = new IntPtr(pointer.ToInt64() - _myDxgiDll.ToInt64());
            return new IntPtr(_theirDxgiDll.ToInt64() + offset.ToInt64());
        }

        protected override void CleanD3D()
        {
            if (_swapChain != IntPtr.Zero)
            {
                _swapchainRelease(_swapChain);
            }

            if (_device != IntPtr.Zero)
            {
                _deviceRelease(_device);
            }

            if (D3DDevicePtr != IntPtr.Zero)
            {
                _deviceContextRelease(D3DDevicePtr);
            }
        }

        [DllImport("d3d11.dll")]
        public static extern unsafe int D3D11CreateDeviceAndSwapChain(void* pAdapter, int driverType, void* Software,
            int flags, void* pFeatureLevels,
            int FeatureLevels, int SDKVersion,
            void* pSwapChainDesc, void* ppSwapChain,
            void* ppDevice, void* pFeatureLevel,
            void* ppImmediateContext);

        [StructLayout(LayoutKind.Sequential)]
        public struct Rational
        {
            readonly int Numerator;
            readonly int Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ModeDescription
        {
            readonly int Width;
            readonly int Height;
            readonly Rational RefreshRate;
            public int Format;
            readonly int ScanlineOrdering;
            readonly int Scaling;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SampleDescription
        {
            public int Count;
            readonly int Quality;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SwapChainDescription
        {
            public ModeDescription ModeDescription;
            public SampleDescription SampleDescription;
            public int Usage;
            public int BufferCount;
            public IntPtr OutputHandle;
            [MarshalAs(UnmanagedType.Bool)] public bool IsWindowed;

            readonly int SwapEffect;
            readonly int Flags;
        }

        public struct VTableIndexes
        {
            public const int DXGISwapChainRelease = 2;
            public const int D3D11DeviceRelease = 2;
            public const int D3D11DeviceContextRelease = 2;
            public const int DXGISwapChainPresent = 8;
            public const int D3D11DeviceContextBegin = 0x1B;
            public const int D3D11DeviceContextEnd = 0x1C;
        }
    }
}

// ReSharper restore InconsistentNaming