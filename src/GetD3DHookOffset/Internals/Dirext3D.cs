using System;
using System.Diagnostics;
using System.Linq;

namespace GetD3DHookOffset.NewFolder1
{
    internal class Dirext3D
    {
        internal Dirext3D(System.Diagnostics.Process targetProc)
        {
            TargetProcess = targetProc;

            UsingDirectX11 = TargetProcess.Modules.Cast<ProcessModule>().Any(m => m.ModuleName == "d3d11.dll");

            Device = UsingDirectX11
                ? (D3DDevice) new D3D11Device(targetProc)
                : new D3D9Device(targetProc);

            HookAddress = UsingDirectX11
                ? ((D3D11Device) Device).GetSwapVTableFuncAbsoluteAddress(Device.PresentVtableIndex)
                : Device.GetDeviceVTableFuncAbsoluteAddress(Device.EndSceneVtableIndex);
        }

        internal System.Diagnostics.Process TargetProcess { get; }

        internal bool UsingDirectX11 { get; }

        internal IntPtr HookAddress { get; private set; }

        internal D3DDevice Device { get; }
    }
}