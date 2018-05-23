using GetD3DHookOffset.NewFolder1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GetD3DHookOffset
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Please enter the process name with out extension (for example, if process name is game.exe enter just game) and press Enter.");
            var processName = Console.ReadLine();
            var processCollection = System.Diagnostics.Process.GetProcessesByName(processName);
            if(processCollection == null || !processCollection.Any())
            {
                Console.WriteLine($"No process found by the name: {processName} exit the console (hit enter or the X) and try again");
                Console.ReadLine();
                return;
            }
            var process = processCollection.FirstOrDefault();
            if(process == null)
            {
                Console.WriteLine($"No process found by the name: {processName} or is now invalid, exit the console (hit enter or the X) and try again");
                Console.ReadLine();
                return;
            }
            var dxDevice = new Dirext3D(process);
            var dxVersion = dxDevice.UsingDirectX11 ? "Directx11" : "Directx9";

            var functionToHook = dxDevice.UsingDirectX11 ? "public delegate int DxgiSwapChainPresentDelegate(IntPtr swapChainPtr, int syncInterval, int flags)" : "public delegate int Direct3D9EndScene(IntPtr device)";
            var processSize = IntPtr.Size == 4 ? "x86" : "x64";
            Console.WriteLine($"DirectX version: {dxVersion}");
            Console.WriteLine($"Process architecture: {processSize}");
            Console.WriteLine($"Function to hook: {functionToHook}");
            var rebased = new IntPtr(dxDevice.HookAddress.ToInt64() - process.MainModule.BaseAddress.ToInt64());
            Console.WriteLine($"ImageBase + offset hook address= {dxDevice.HookAddress.ToString("X")}");
            Console.WriteLine($"Offset hook address: = {rebased.ToString("X")}");
            Console.WriteLine("Hit enter to exit or close the console");
            Console.ReadLine();
        }
    }
}
